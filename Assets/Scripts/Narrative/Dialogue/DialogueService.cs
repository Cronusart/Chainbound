using System;
using System.Collections;
using UnityEngine;

public readonly struct DialoguePlaybackContext
{
    public static readonly DialoguePlaybackContext Invalid = new DialoguePlaybackContext(0, null, false);

    public DialoguePlaybackContext(int playbackId, DialogueSequenceSO sequence, bool locksInput)
    {
        PlaybackId = playbackId;
        Sequence = sequence;
        LocksInput = locksInput;
    }

    public int PlaybackId { get; }
    public DialogueSequenceSO Sequence { get; }
    public bool LocksInput { get; }
    public bool IsValid => PlaybackId > 0 && Sequence != null;
}

/// <summary>
/// Service for playing dialogue sequences.
/// Manages dialogue state and coordinates with DialogueUIController.
/// </summary>
public class DialogueService : MonoBehaviour
{
    private static DialogueService instance;
    
    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static DialogueService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<DialogueService>();
                if (instance == null)
                {
                    GameObject go = new GameObject("DialogueService");
                    instance = go.AddComponent<DialogueService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("References")]
    [Tooltip("Dialogue UI controller. Auto-finds if null.")]
    [SerializeField] private DialogueUIController uiController;
    
    [Header("Settings")]
    [Tooltip("Auto-acquire input lock while dialogue is active")]
    [SerializeField] private bool autoLockInput = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // State
    private DialogueSequenceSO currentSequence;
    private int currentLineIndex = -1;
    private bool isPlaying = false;
    private Coroutine activePlaybackCoroutine;
    private InputLockHandle inputLockHandle;
    private DialoguePlaybackContext currentPlayback = DialoguePlaybackContext.Invalid;
    private int nextPlaybackId = 1;
    
    // Events
    public event Action<DialogueSequenceSO> OnDialogueStarted;
    public event Action<DialoguePlaybackContext> OnDialoguePlaybackStarted;
    public event Action<DialogueLine, int> OnLineStarted;
    public event Action<DialogueLine, int> OnLineShown;
    public event Func<DialogueLine, int, bool> OnLineAdvanceReady;
    public event Action<DialogueSequenceSO> OnDialogueComplete;
    public event Action<DialoguePlaybackContext> OnDialoguePlaybackComplete;
    
    /// <summary>Is dialogue currently playing?</summary>
    public bool IsPlaying => isPlaying;
    
    /// <summary>Current dialogue sequence.</summary>
    public DialogueSequenceSO CurrentSequence => currentSequence;
    
    /// <summary>Current line index (-1 if not playing).</summary>
    public int CurrentLineIndex => currentLineIndex;
    
    /// <summary>Read-only playback context for the currently active dialogue.</summary>
    public DialoguePlaybackContext CurrentPlayback => currentPlayback;

    /// <summary>True when the current playback locks player input.</summary>
    public bool CurrentDialogueLocksInput => currentPlayback.IsValid && currentPlayback.LocksInput;

    /// <summary>Value associated with the last choice selected during the current playback.</summary>
    public string LastChoiceValue { get; private set; } = string.Empty;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[DialogueService] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        instance = this;
        
        // Auto-find UI controller
        if (uiController == null)
        {
            uiController = FindObjectOfType<DialogueUIController>();
        }
    }
    
    private void OnDestroy()
    {
        // The service can persist while its presentation is torn down during a
        // scene/app lifecycle change. Never leave its owned dialogue lock behind.
        StopDialogue();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnDisable()
    {
        // Disabling the service is a terminal presentation state, not a pause.
        // StopDialogue releases the token and notifies any sequence waiting on it.
        StopDialogue();
    }
    
    /// <summary>
    /// Play a dialogue sequence. Returns coroutine for async completion.
    /// </summary>
    /// <param name="sequence">Dialogue sequence to play</param>
    /// <param name="lockInputOverride">Override for input locking. True = lock, false = no lock. Null = use autoLockInput setting.</param>
    /// <returns>Coroutine that completes when dialogue finishes</returns>
    public Coroutine PlayDialogue(DialogueSequenceSO sequence, bool? lockInputOverride = null)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[DialogueService] Cannot play null dialogue sequence!");
            return null;
        }
        
        if (isPlaying)
        {
            if (currentSequence == sequence && activePlaybackCoroutine != null)
                return activePlaybackCoroutine;

            Debug.LogWarning("[DialogueService] Dialogue already playing! Stopping current dialogue.");
            StopDialogue();
        }

        int playbackId = nextPlaybackId++;
        Coroutine playbackCoroutine = StartCoroutine(PlayDialogueCoroutine(sequence, lockInputOverride, playbackId));
        if (currentPlayback.PlaybackId == playbackId)
            activePlaybackCoroutine = playbackCoroutine;
        return playbackCoroutine;
    }
    
    /// <summary>
    /// Stop current dialogue immediately.
    /// </summary>
    public void StopDialogue()
    {
        if (!isPlaying || !currentPlayback.IsValid)
            return;

        DialogueSequenceSO stoppedSequence = currentSequence;
        DialoguePlaybackContext stoppedPlayback = currentPlayback;
        Coroutine stoppedCoroutine = activePlaybackCoroutine;
        activePlaybackCoroutine = null;
        if (stoppedCoroutine != null)
            StopCoroutine(stoppedCoroutine);

        FinishPlayback(stoppedSequence, stoppedPlayback, clearLastChoice: true);

        if (enableDebugLogs)
        {
            Debug.Log($"[DialogueService] Dialogue stopped: {stoppedSequence?.name ?? "<unknown>"}");
        }
    }
    
    /// <summary>
    /// Skip current line or entire sequence (based on sequence settings).
    /// </summary>
    public void Skip()
    {
        if (!isPlaying || uiController == null) return;
        
        if (currentSequence.AllowSkip)
        {
            if (currentSequence.SkipToEndOfLine)
            {
                uiController.SkipToEndOfLine();
            }
            else
            {
                StopDialogue();
            }
        }
    }
    
    private IEnumerator PlayDialogueCoroutine(
        DialogueSequenceSO sequence,
        bool? lockInputOverride,
        int playbackId)
    {
        isPlaying = true;
        currentSequence = sequence;
        currentLineIndex = -1;
        LastChoiceValue = string.Empty;
        
        // Acquire input lock (override takes precedence over autoLockInput)
        bool shouldLockInput = lockInputOverride ?? autoLockInput;
        DialoguePlaybackContext playback = new DialoguePlaybackContext(playbackId, sequence, shouldLockInput);
        currentPlayback = playback;
        if (shouldLockInput)
        {
            InputLockService inputLock = InputLockService.Instance;
            if (inputLock != null)
            {
                inputLockHandle = inputLock.AcquireLock("Dialogue", InputLockChannel.All, this);
            }
        }
        
        // Show UI
        if (uiController == null)
        {
            Debug.LogError("[DialogueService] No DialogueUIController found! Cannot play dialogue.");
            FinishPlayback(sequence, playback, clearLastChoice: true);
            yield break;
        }
        
        uiController.ShowDialogue();
        
        // Fire start event
        OnDialogueStarted?.Invoke(sequence);
        OnDialoguePlaybackStarted?.Invoke(currentPlayback);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DialogueService] Started dialogue: {sequence.name} ({sequence.LineCount} lines)");
        }
        
        // Play each line
        for (int i = 0; i < sequence.LineCount; i++)
        {
            currentLineIndex = i;
            DialogueLine line = sequence.GetLine(i);
            
            if (line == null) continue;

            OnLineStarted?.Invoke(line, i);
            if (!IsCurrentPlayback(playback))
                yield break;
            
            // Show line
            yield return uiController.ShowLine(line, sequence);
            if (!IsCurrentPlayback(playback))
                yield break;
            
            // Fire line shown event
            OnLineShown?.Invoke(line, i);
            if (!IsCurrentPlayback(playback))
                yield break;

            if (line.HasChoices)
            {
                bool choicePicked = false;
                DialogueChoice selectedChoice = null;
                uiController.ShowChoices(line.choices, value =>
                {
                    LastChoiceValue = value ?? string.Empty;
                    selectedChoice = FindChoiceByValue(line.choices, LastChoiceValue);
                    choicePicked = true;
                });

                yield return new WaitUntil(() => choicePicked || !IsCurrentPlayback(playback));
                if (!IsCurrentPlayback(playback))
                {
                    uiController.HideChoices();
                    yield break;
                }

                yield return uiController.WaitForChoiceMotion();
                if (!IsCurrentPlayback(playback))
                {
                    uiController.HideChoices();
                    yield break;
                }

                if (selectedChoice != null && selectedChoice.endDialogue)
                    break;

                continue;
            }
            
            // Wait for advance (auto or manual)
            if (sequence.AutoAdvance)
            {
                if (sequence.AutoAdvanceDelay > 0f)
                {
                    yield return new WaitForSeconds(sequence.AutoAdvanceDelay);
                }

                yield return new WaitUntil(() => AreLineAdvanceConditionsMet(line, i));
            }
            else
            {
                // Wait for input (UI gates advance on continue-blink minimum; confirm skips reveal then advances when allowed)
                yield return uiController.WaitForAdvance(() => AreLineAdvanceConditionsMet(line, i));
                if (!IsCurrentPlayback(playback))
                    yield break;
            }
        }

        FinishPlayback(sequence, playback);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DialogueService] Completed dialogue: {sequence.name}");
        }
    }

    private static DialogueChoice FindChoiceByValue(DialogueChoice[] choices, string value)
    {
        if (choices == null)
            return null;

        for (int i = 0; i < choices.Length; i++)
        {
            DialogueChoice choice = choices[i];
            if (choice != null && string.Equals(choice.value, value, StringComparison.Ordinal))
                return choice;
        }

        return null;
    }
    
    /// <summary>
    /// Set the UI controller reference.
    /// </summary>
    public void SetUIController(DialogueUIController controller)
    {
        uiController = controller;
    }

    private bool IsCurrentPlayback(DialoguePlaybackContext playback)
    {
        return isPlaying &&
               playback.IsValid &&
               currentPlayback.PlaybackId == playback.PlaybackId;
    }

    /// <summary>
    /// Ends the active playback exactly once. Completion events are lifecycle-terminal:
    /// they also fire for an explicit stop so camera, NPC, and sequence owners can unwind.
    /// </summary>
    private void FinishPlayback(
        DialogueSequenceSO sequence,
        DialoguePlaybackContext playback,
        bool clearLastChoice = false)
    {
        if (!IsCurrentPlayback(playback))
            return;

        activePlaybackCoroutine = null;
        isPlaying = false;
        currentSequence = null;
        currentLineIndex = -1;
        currentPlayback = DialoguePlaybackContext.Invalid;
        if (clearLastChoice)
            LastChoiceValue = string.Empty;

        InputLockHandle completedLock = inputLockHandle;
        inputLockHandle = default;
        if (completedLock.IsValid && !completedLock.IsReleased)
            completedLock.Release();

        if (uiController != null)
            uiController.HideDialogue();

        OnDialogueComplete?.Invoke(sequence);
        OnDialoguePlaybackComplete?.Invoke(playback);
    }

    private bool AreLineAdvanceConditionsMet(DialogueLine line, int index)
    {
        if (OnLineAdvanceReady == null)
        {
            return true;
        }

        Delegate[] delegates = OnLineAdvanceReady.GetInvocationList();
        foreach (Func<DialogueLine, int, bool> handler in delegates)
        {
            try
            {
                if (!handler.Invoke(line, index))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        return true;
    }
}

