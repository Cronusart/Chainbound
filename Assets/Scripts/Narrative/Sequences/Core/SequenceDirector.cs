using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Parameters for starting a sequence.
/// </summary>
[System.Serializable]
public class SequenceParameters
{
    [Tooltip("Initial variables to set")]
    public Dictionary<string, string> initialVariables = new Dictionary<string, string>();
    
    [Tooltip("Input lock channel for this sequence")]
    public InputLockChannel lockChannel = InputLockChannel.All;

    [Tooltip("When false, SequenceDirector does not acquire an input lock for this play. Use when the first step expects free movement/interaction (e.g. wait-for-event at a mid-sequence label).")]
    public bool acquireInputLock = true;
}

/// <summary>
/// Game-wide entry point for playing sequences.
/// Manages sequence lifecycle, gating, and integration with other systems.
/// </summary>
public class SequenceDirector : MonoBehaviour
{
    private static SequenceDirector instance;
    
    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static SequenceDirector Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SequenceDirector>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SequenceDirector");
                    instance = go.AddComponent<SequenceDirector>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // References
    private SequenceRunner runner;
    private InputLockHandle currentLockHandle;
    
    private readonly struct QueuedSequence
    {
        public QueuedSequence(SequenceAsset sequence, SequenceParameters parameters)
        {
            Sequence = sequence;
            Parameters = parameters;
        }

        public SequenceAsset Sequence { get; }
        public SequenceParameters Parameters { get; }
    }

    private readonly Queue<QueuedSequence> sequenceQueue = new Queue<QueuedSequence>();
    
    // Events
    public event Action<SequenceAsset> OnSequenceStarted;
    public event Action<SequenceAsset> OnSequenceCompleted;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[SequenceDirector] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Get or create runner
        runner = SequenceRunner.Instance;
        if (runner == null)
        {
            GameObject runnerObj = new GameObject("SequenceRunner");
            runner = runnerObj.AddComponent<SequenceRunner>();
            DontDestroyOnLoad(runnerObj);
        }
        
        // Subscribe to runner events
        runner.OnSequenceComplete += OnRunnerSequenceComplete;
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
        
        if (runner != null)
        {
            runner.OnSequenceComplete -= OnRunnerSequenceComplete;
        }
    }
    
    /// <summary>
    /// Play a sequence by asset.
    /// </summary>
    public void Play(SequenceAsset sequence, SequenceParameters parameters = null)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[SequenceDirector] Cannot play null sequence!");
            return;
        }
        
        PlaySequence(sequence, parameters);
    }

    /// <summary>
    /// Resumes the profile-hydrated checkpoint. Loads the authored resume scene
    /// first when the player is not already there, then continues at the
    /// checkpoint label (idle gate) when one exists.
    /// </summary>
    public bool ResumeHydratedSequence(
        string sequenceId,
        SequenceParameters parameters = null,
        Action<string> onFailed = null)
    {
        if (runner == null || runner.IsRunning || !runner.HasRestoredCheckpoint(sequenceId))
            return false;

        StartCoroutine(ResumeHydratedSequenceRoutine(sequenceId, parameters, onFailed));
        return true;
    }

    private IEnumerator ResumeHydratedSequenceRoutine(
        string sequenceId,
        SequenceParameters parameters,
        Action<string> onFailed)
    {
        if (!runner.TryGetResumePlacement(out string sceneId, out string spawnId, out _))
        {
            FailResume(onFailed, "Restored sequence checkpoint was invalid.");
            yield break;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        bool needsLoad = !string.IsNullOrWhiteSpace(sceneId) &&
            (!activeScene.IsValid() ||
             !string.Equals(activeScene.name, sceneId, StringComparison.OrdinalIgnoreCase));

        if (needsLoad)
        {
            SceneLoader sceneLoader = SceneLoader.Instance;
            if (sceneLoader == null)
            {
                FailResume(onFailed, "SceneLoader not found.");
                yield break;
            }

            if (sceneLoader.IsTransitioning)
            {
                FailResume(onFailed, "SceneLoader is busy.");
                yield break;
            }

            bool requestFinished = false;
            SceneTransitionResult transitionResult = default;
            bool accepted = sceneLoader.RequestTransition(new SceneTransitionRequest(
                sceneId,
                spawnId,
                sourceId: $"SequenceResume.{sequenceId}",
                completed: result =>
                {
                    transitionResult = result;
                    requestFinished = true;
                }));
            if (!accepted && !requestFinished)
            {
                FailResume(onFailed, "SceneLoader rejected resume placement.");
                yield break;
            }

            while (!requestFinished)
                yield return null;

            if (!transitionResult.Succeeded)
            {
                FailResume(onFailed, transitionResult.Message);
                yield break;
            }
        }

        runner.ApplyResumeProfileWorldFlags();

        bool shouldAcquireLock = parameters != null && parameters.acquireInputLock;
        if (shouldAcquireLock)
        {
            InputLockChannel lockChannel = parameters.lockChannel;
            InputLockService lockService = InputLockService.Instance;
            if (lockService != null)
                currentLockHandle = lockService.AcquireLock($"Sequence resume: {sequenceId}", lockChannel, this);
        }

        if (!runner.ResumeRestoredCheckpoint(sequenceId))
        {
            if (currentLockHandle.IsValid && !currentLockHandle.IsReleased)
                currentLockHandle.Release();
            FailResume(onFailed, "Could not continue the restored sequence.");
            yield break;
        }

        BindDirectorLockToContext();
        ApplyInitialVariables(parameters);
        OnSequenceStarted?.Invoke(runner.CurrentSequence);
    }

    private void FailResume(Action<string> onFailed, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Debug.LogError($"[SequenceDirector] Resume failed: {message}");
        onFailed?.Invoke(message);
    }

    /// <summary>
    /// Starts the sequence at a label (dev / harness): no earlier steps run.
    /// Acquires the same input lock as <see cref="Play"/>.
    /// </summary>
    public void PlayFromLabel(SequenceAsset sequence, string label, SequenceParameters parameters = null)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[SequenceDirector] PlayFromLabel: sequence is null!");
            return;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            Debug.LogWarning("[SequenceDirector] PlayFromLabel: label is empty!");
            return;
        }

        if (runner == null)
        {
            Debug.LogError("[SequenceDirector] SequenceRunner not found!");
            return;
        }

        if (runner.IsRunning)
        {
            if (enableDebugLogs)
                Debug.Log("[SequenceDirector] Stopping current sequence for PlayFromLabel");
            StopCurrent();
        }

        bool shouldAcquireLock = parameters == null || parameters.acquireInputLock;
        if (shouldAcquireLock)
        {
            InputLockChannel lockChannel = parameters?.lockChannel ?? InputLockChannel.All;
            InputLockService lockService = InputLockService.Instance;
            if (lockService != null)
                currentLockHandle = lockService.AcquireLock($"Sequence (at label): {sequence.name}", lockChannel, this);
        }

        runner.StartSequenceAtLabel(sequence, label.Trim());
        BindDirectorLockToContext();
        ApplyInitialVariables(parameters);

        OnSequenceStarted?.Invoke(sequence);

        if (enableDebugLogs)
            Debug.Log($"[SequenceDirector] PlayFromLabel: {sequence.name} @ '{label}'");
    }
    
    /// <summary>
    /// Play a sequence by ID (requires sequence registry - TODO).
    /// </summary>
    public void Play(string sequenceId, SequenceParameters parameters = null)
    {
        Debug.LogWarning("[SequenceDirector] Play by ID not yet implemented - use Play(SequenceAsset) instead");
        // TODO: Implement sequence registry lookup
    }
    
    /// <summary>
    /// Try to play a sequence once using a gate key.
    /// Returns true if sequence was played, false if it was already played.
    /// </summary>
    public bool TryPlayOnce(string gateKey, SequenceAsset sequence, SequenceParameters parameters = null)
    {
        if (string.IsNullOrEmpty(gateKey))
        {
            Debug.LogWarning("[SequenceDirector] Gate key is empty!");
            return false;
        }
        
        if (sequence == null)
        {
            Debug.LogWarning("[SequenceDirector] Cannot play null sequence!");
            return false;
        }
        
        // Check if already played
        SequenceState state = GetPersistentState();
        if (state != null && state.playedGateKeys.Contains(gateKey))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SequenceDirector] Sequence already played with gate key: {gateKey}");
            }
            return false;
        }
        
        // Mark as played
        if (runner != null)
        {
            runner.MarkGateKeyPlayed(gateKey);
        }
        
        // Play sequence
        PlaySequence(sequence, parameters);
        return true;
    }
    
    /// <summary>
    /// Check if a gate key has been played.
    /// </summary>
    public bool HasPlayed(string gateKey)
    {
        SequenceState state = GetPersistentState();
        return state != null && state.playedGateKeys.Contains(gateKey);
    }
    
    /// <summary>
    /// Stop the current sequence.
    /// </summary>
    public void StopCurrent()
    {
        if (runner != null && runner.IsRunning)
        {
            runner.StopSequence();
        }
        
        // Release input lock
        if (currentLockHandle.IsValid && !currentLockHandle.IsReleased)
        {
            currentLockHandle.Release();
        }
        
        // Clear queue
        sequenceQueue.Clear();
    }
    
    /// <summary>
    /// Interrupt current sequence and play another.
    /// </summary>
    public void Interrupt(SequenceAsset interruptSequence, SequenceParameters parameters = null)
    {
        if (interruptSequence == null)
        {
            Debug.LogWarning("[SequenceDirector] Cannot interrupt with null sequence!");
            return;
        }
        
        StopCurrent();
        PlaySequence(interruptSequence, parameters);
    }
    
    /// <summary>
    /// Queue a sequence to play after current one completes.
    /// </summary>
    public void Queue(SequenceAsset sequence, SequenceParameters parameters = null)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[SequenceDirector] Cannot queue null sequence!");
            return;
        }
        
        sequenceQueue.Enqueue(new QueuedSequence(sequence, parameters));
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SequenceDirector] Queued sequence: {sequence.name} (queue size: {sequenceQueue.Count})");
        }
    }
    
    /// <summary>
    /// Is a sequence currently playing?
    /// </summary>
    public bool IsPlaying => runner != null && runner.IsRunning;
    
    /// <summary>
    /// Current sequence asset.
    /// </summary>
    public SequenceAsset CurrentSequence => runner != null ? runner.CurrentSequence : null;
    
    /// <summary>
    /// Internal method to play a sequence.
    /// </summary>
    private void PlaySequence(SequenceAsset sequence, SequenceParameters parameters)
    {
        if (runner == null)
        {
            Debug.LogError("[SequenceDirector] SequenceRunner not found!");
            return;
        }
        
        // Stop current if running
        if (runner.IsRunning)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[SequenceDirector] Stopping current sequence to play new one");
            }
            StopCurrent();
        }
        
        bool shouldAcquireLock = parameters == null || parameters.acquireInputLock;
        if (shouldAcquireLock)
        {
            InputLockChannel lockChannel = parameters?.lockChannel ?? InputLockChannel.All;
            InputLockService lockService = InputLockService.Instance;
            if (lockService != null)
                currentLockHandle = lockService.AcquireLock($"Sequence: {sequence.name}", lockChannel, this);
        }

        // StartSequence clears SequenceContext, so store the director lock and
        // initial variables after that reset. Step_UnlockInput can then release
        // this play-wide lock the same way it releases Step_LockInput handles.
        runner.StartSequence(sequence);
        BindDirectorLockToContext();
        ApplyInitialVariables(parameters);

        OnSequenceStarted?.Invoke(sequence);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SequenceDirector] Started sequence: {sequence.name}");
        }
    }
    
    /// <summary>
    /// Handle runner sequence completion.
    /// </summary>
    private void OnRunnerSequenceComplete(SequenceAsset sequence)
    {
        // Release input lock
        if (currentLockHandle.IsValid && !currentLockHandle.IsReleased)
        {
            currentLockHandle.Release();
        }
        
        OnSequenceCompleted?.Invoke(sequence);
        
        // Process queue
        if (sequenceQueue.Count > 0)
        {
            QueuedSequence next = sequenceQueue.Dequeue();
            if (enableDebugLogs)
            {
                Debug.Log($"[SequenceDirector] Playing queued sequence: {next.Sequence.name}");
            }
            PlaySequence(next.Sequence, next.Parameters);
        }
    }
    
    /// <summary>
    /// Get persistent state from runner.
    /// </summary>
    private SequenceState GetPersistentState()
    {
        return runner != null ? runner.GetPersistentState() : null;
    }

    private void BindDirectorLockToContext()
    {
        if (!currentLockHandle.IsValid || currentLockHandle.IsReleased)
            return;

        SequenceContext context = runner != null ? runner.Context : null;
        context?.StoreInputLock(currentLockHandle);
    }

    private void ApplyInitialVariables(SequenceParameters parameters)
    {
        if (parameters?.initialVariables == null)
            return;

        SequenceContext context = runner != null ? runner.Context : null;
        if (context == null)
            return;

        foreach (var kvp in parameters.initialVariables)
            context.Variables.Set<string>(kvp.Key, kvp.Value, VariableScope.Local);
    }
}

