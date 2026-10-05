using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Host for sequence execution: one authoritative Main track and optional child tracks.
/// Preserves legacy <see cref="SequenceRunner"/> API (delegates to main execution).
/// </summary>
public class SequenceRunner : MonoBehaviour
{
    private static SequenceRunner instance;

    public static SequenceRunner Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<SequenceRunner>();
            return instance;
        }
    }

    [Header("Starting Sequence")]
    [SerializeField] private SequenceAsset startingSequence;
    [SerializeField] private bool autoStart = false;

    [Header("State Persistence")]
    [SerializeField] private bool persistState = true;

    [Header("Failure Policy")]
    [SerializeField] private StepFailurePolicy defaultFailurePolicy = StepFailurePolicy.AbortSequence;
    [SerializeField] private SequenceAsset fallbackSequence;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [Tooltip("When true, NarrativeDebugOverlay (if present) can mirror this runner.")]
    [SerializeField] private bool showDebugUI = false;

    private readonly SequenceExecution mainExecution = new SequenceExecution();
    private readonly Dictionary<string, SequenceExecution> childExecutions = new Dictionary<string, SequenceExecution>(StringComparer.Ordinal);
    private SequenceExecution activeControlExecution;

    public event Action<SequenceAsset> OnSequenceStarted;
    public event Action<SequenceAsset> OnSequenceComplete;
    public event Action<SequenceAsset, StepOutcome> OnSequenceAborted;
    public event Action<SequenceStep, int> OnStepStarted;
    public event Action<SequenceStep, int> OnStepComplete;
    public event Action<SequenceStep, StepOutcome> OnStepFailed;
    public event Action<string, SequenceExecution> OnChildTrackComplete;
    public event Action<SequenceCheckpointRecord> OnPersistentStateChanged;

    public bool ShowDebugUI => showDebugUI;

    public bool IsRunning => mainExecution.IsRunning;

    public SequenceAsset CurrentSequence => mainExecution.Sequence;

    public int CurrentStepIndex => mainExecution.CurrentStepIndex;

    public SequenceContext Context => mainExecution.Context;

    public int CallStackDepth => mainExecution.CallStack.Count;

    public StepOutcome LastStepOutcome => mainExecution.LastStepOutcome;

    public SequenceExecution GetMainExecution() => mainExecution;

    public IReadOnlyDictionary<string, SequenceExecution> GetChildExecutions() => childExecutions;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[SequenceRunner] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (persistState && transform.parent == null)
            DontDestroyOnLoad(gameObject);

        mainExecution.TrackId = "main";
        mainExecution.TrackType = SequenceTrackType.Main;
        mainExecution.Context = new SequenceContext();
        mainExecution.PersistentState = new SequenceState();
        mainExecution.ExecutionId = Guid.NewGuid().ToString("N");
        mainExecution.CancelPolicy = TrackCancelPolicy.ManualOnly;

        LoadPersistentState();
    }

    private void Start()
    {
        if (autoStart && startingSequence != null)
            StartSequence(startingSequence);
        else if (persistState && mainExecution.PersistentState.IsValid)
            ResumeFromCheckpoint();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void StartSequence(SequenceAsset sequence)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[SequenceRunner] Cannot start null sequence!");
            return;
        }

        if (mainExecution.IsRunning)
        {
            Debug.LogWarning("[SequenceRunner] Sequence already running! Stopping current sequence.");
            StopSequence();
        }

        StopAllChildrenForNewMain();

        mainExecution.Sequence = sequence;
        mainExecution.CurrentStepIndex = -1;
        mainExecution.CallStack.Clear();
        mainExecution.Context.Clear();
        mainExecution.Context.CurrentSequenceId = sequence.SequenceId;
        mainExecution.PersistentState.currentSequenceId = sequence.SequenceId;
        mainExecution.PersistentState.sequenceVersion = sequence.ContentVersion;
        BuildLabelIndex(mainExecution, sequence);
        LoadStateToContext();

        if (enableDebugLogs)
            Debug.Log($"[SEQ] StartSequence sequenceId={sequence.SequenceId} stepCount={sequence.StepCount}");

        mainExecution.Coroutine = StartCoroutine(ExecuteSequenceCoroutine(mainExecution));
    }

    /// <summary>
    /// Starts the sequence at a <see cref="Step_Label"/> step without executing earlier steps.
    /// Use for dev harnesses and mid-sequence testing. Mirrors <see cref="StartSequence"/> setup
    /// but sets execution position from the label index.
    /// </summary>
    public void StartSequenceAtLabel(SequenceAsset sequence, string label)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[SequenceRunner] StartSequenceAtLabel: sequence is null!");
            return;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            Debug.LogWarning("[SequenceRunner] StartSequenceAtLabel: label is empty!");
            return;
        }

        if (mainExecution.IsRunning)
        {
            if (enableDebugLogs)
                Debug.Log("[SequenceRunner] StartSequenceAtLabel: stopping current sequence.");
            StopSequence();
        }

        StopAllChildrenForNewMain();

        mainExecution.Sequence = sequence;
        BuildLabelIndex(mainExecution, sequence);

        if (!mainExecution.LabelIndex.TryGetValue(label, out int stepIndex))
        {
            Debug.LogWarning($"[SequenceRunner] StartSequenceAtLabel: label '{label}' not found in '{sequence.name}'.");
            mainExecution.Sequence = null;
            return;
        }

        mainExecution.CurrentStepIndex = stepIndex - 1;
        mainExecution.CallStack.Clear();
        mainExecution.Context.Clear();
        mainExecution.PersistentState.currentSequenceId = sequence.SequenceId;
        mainExecution.PersistentState.currentStepIndex = stepIndex;
        LoadStateToContext();

        if (enableDebugLogs)
            Debug.Log($"[SEQ] StartSequenceAtLabel sequenceId={sequence.SequenceId} label={label} stepIndex={stepIndex}");

        mainExecution.Coroutine = StartCoroutine(ExecuteSequenceCoroutine(mainExecution));
    }

    private void StopAllChildrenForNewMain()
    {
        var keys = childExecutions.Keys.ToList();
        foreach (string k in keys)
            StopChildTrackInternal(k, true);
    }

    public void ResumeFromBeginning()
    {
        if (mainExecution.Sequence == null)
        {
            Debug.LogWarning("[SequenceRunner] Cannot resume - no current sequence!");
            return;
        }
        StartSequence(mainExecution.Sequence);
    }

    public void ResumeFromCheckpoint()
    {
        SequenceState ps = mainExecution.PersistentState;
        if (!ps.IsValid)
        {
            Debug.LogWarning("[SequenceRunner] Cannot resume - no valid checkpoint!");
            return;
        }

        if (mainExecution.Sequence == null || mainExecution.Sequence.SequenceId != ps.currentSequenceId)
        {
            Debug.LogWarning($"[SequenceRunner] Cannot resume sequence '{ps.currentSequenceId}' - not loaded!");
            return;
        }

        BuildLabelIndex(mainExecution, mainExecution.Sequence);

        int resumeIndex = ResolveResumeIndex(mainExecution.Sequence, ps, out string recoveryReason);
        if (!string.IsNullOrWhiteSpace(recoveryReason))
            Debug.LogWarning($"[SequenceRunner] {recoveryReason}");

        if (IntroResumePolicy.ShouldDiscardNamelessStepProgress(
                mainExecution.Sequence.SequenceId,
                CurrentDisplayName(),
                resumeIndex))
            DiscardNamelessIntroProgressInMemory();

        mainExecution.CurrentStepIndex = resumeIndex - 1;
        mainExecution.Context.CurrentSequenceId = ps.currentSequenceId;
        mainExecution.Context.CurrentStepIndex = resumeIndex;
        LoadStateToContext();

        mainExecution.Coroutine = StartCoroutine(ExecuteSequenceCoroutine(mainExecution));
    }

    public void ResumeFromLabel(string label)
    {
        if (mainExecution.Sequence == null)
        {
            Debug.LogWarning("[SequenceRunner] Cannot resume - no current sequence!");
            return;
        }

        BuildLabelIndex(mainExecution, mainExecution.Sequence);

        if (!mainExecution.LabelIndex.TryGetValue(label, out int stepIndex))
        {
            Debug.LogWarning($"[SequenceRunner] Cannot resume - label '{label}' not found!");
            return;
        }

        if (ShouldHoldNamelessResume(mainExecution.Sequence, stepIndex))
        {
            Debug.LogWarning(
                $"[SequenceRunner] Display name is unset. Ignoring resume label '{label}' and starting at step 0.");
            stepIndex = 0;
        }

        if (IntroResumePolicy.ShouldDiscardNamelessStepProgress(
                mainExecution.Sequence.SequenceId,
                CurrentDisplayName(),
                stepIndex))
            DiscardNamelessIntroProgressInMemory();

        mainExecution.CurrentStepIndex = stepIndex - 1;
        mainExecution.Context.CurrentStepIndex = stepIndex;
        LoadStateToContext();

        mainExecution.Coroutine = StartCoroutine(ExecuteSequenceCoroutine(mainExecution));
    }

    public void JumpToStep(int stepIndex)
    {
        JumpToStep(mainExecution, stepIndex);
    }

    private void JumpToStep(SequenceExecution exec, int stepIndex)
    {
        if (exec.Sequence == null || stepIndex < 0 || stepIndex >= exec.Sequence.StepCount)
        {
            Debug.LogWarning($"[SequenceRunner] Cannot jump to step {stepIndex} - invalid index!");
            return;
        }

        exec.CurrentStepIndex = stepIndex - 1;
        exec.Context.CurrentStepIndex = stepIndex;
    }

    public void JumpToLabel(string label)
    {
        SequenceExecution exec = activeControlExecution != null ? activeControlExecution : mainExecution;
        JumpToLabel(exec, label);
    }

    private void JumpToLabel(SequenceExecution exec, string label)
    {
        if (exec.LabelIndex.TryGetValue(label, out int stepIndex))
            JumpToStep(exec, stepIndex);
        else
            Debug.LogWarning($"[SequenceRunner] Cannot jump to label '{label}' - not found!");
    }

    private void BuildLabelIndex(SequenceExecution exec, SequenceAsset sequence)
    {
        exec.LabelIndex.Clear();
        if (sequence == null) return;

        for (int i = 0; i < sequence.StepCount; i++)
        {
            SequenceStep step = sequence.GetStep(i);
            if (step is Step_Label labelStep)
            {
                string labelName = labelStep.GetLabelName();
                if (!string.IsNullOrEmpty(labelName))
                {
                    if (exec.LabelIndex.ContainsKey(labelName))
                        Debug.LogWarning($"[SequenceRunner] Duplicate label '{labelName}' at step {i}!");
                    else
                        exec.LabelIndex[labelName] = i;
                }
            }
        }

        if (enableDebugLogs)
            Debug.Log($"[SequenceRunner] Built label index with {exec.LabelIndex.Count} labels");
    }

    public void StopSequence()
    {
        if (!mainExecution.IsRunning)
            return;

        mainExecution.IsRunning = false;
        if (mainExecution.Coroutine != null)
        {
            StopCoroutine(mainExecution.Coroutine);
            mainExecution.Coroutine = null;
        }

        mainExecution.Context.ReleaseAllNpcClaims();
        mainExecution.Context.ReleaseAllInputLocks();
        mainExecution.CallStack.Clear();
        SavePersistentStateFor(mainExecution);
        StopChildrenOnParentEnd();

        if (enableDebugLogs)
        {
            string seqId = mainExecution.Sequence != null ? mainExecution.Sequence.SequenceId : "null";
            Debug.Log($"[SEQ] StopSequence sequenceId={seqId}");
        }
    }

    private void StopChildrenOnParentEnd()
    {
        List<string> keys = childExecutions.Keys.ToList();
        foreach (string k in keys)
        {
            if (childExecutions.TryGetValue(k, out SequenceExecution ch) &&
                ch.CancelPolicy == TrackCancelPolicy.StopOnParentEnd)
                StopChildTrackInternal(k, true);
        }
    }

    /// <summary>
    /// Start a non-authoritative child track. Returns false if <paramref name="trackId"/> is already running.
    /// </summary>
    public bool TryStartChildTrack(
        string trackId,
        SequenceAsset sequence,
        SequenceTrackType trackType,
        TrackContextMode contextMode,
        TrackCancelPolicy cancelPolicy,
        out string errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrEmpty(trackId))
        {
            errorMessage = "trackId is empty";
            return false;
        }
        if (sequence == null)
        {
            errorMessage = "sequence is null";
            return false;
        }
        if (childExecutions.TryGetValue(trackId, out SequenceExecution existing) && existing.IsRunning)
        {
            errorMessage = $"track '{trackId}' is already running";
            return false;
        }

        if (existing != null && !existing.IsRunning)
            childExecutions.Remove(trackId);

        var exec = new SequenceExecution
        {
            ExecutionId = Guid.NewGuid().ToString("N"),
            TrackId = trackId,
            TrackType = trackType,
            Sequence = sequence,
            PersistentState = null,
            CancelPolicy = cancelPolicy,
            LastStepOutcome = StepOutcome.Success()
        };

        if (contextMode == TrackContextMode.Shared)
            exec.Context = mainExecution.Context;
        else
            exec.Context = SequenceContext.CreateChildFrom(mainExecution.Context);

        exec.CurrentStepIndex = -1;
        exec.Context.CurrentSequenceId = sequence.SequenceId;
        BuildLabelIndex(exec, sequence);

        childExecutions[trackId] = exec;
        exec.Coroutine = StartCoroutine(ExecuteSequenceCoroutine(exec));
        return true;
    }

    public bool IsChildTrackRunning(string trackId)
    {
        return !string.IsNullOrEmpty(trackId) &&
               childExecutions.TryGetValue(trackId, out SequenceExecution e) &&
               e.IsRunning;
    }

    public void StopChildTrack(string trackId, TrackStopMode mode)
    {
        if (string.IsNullOrEmpty(trackId))
            return;
        if (!childExecutions.TryGetValue(trackId, out SequenceExecution exec))
            return;

        switch (mode)
        {
            case TrackStopMode.Immediate:
                StopChildTrackInternal(trackId, true);
                break;
            case TrackStopMode.Graceful:
                exec.GracefulStopRequested = true;
                break;
            case TrackStopMode.SignalCancel:
                NarrativeSignalBus.Instance?.Emit($"cancel_{trackId}", trackId, exec.Sequence != null ? exec.Sequence.SequenceId : "", null);
                exec.GracefulStopRequested = true;
                break;
        }
    }

    private void StopChildTrackInternal(string trackId, bool releaseLocks)
    {
        if (!childExecutions.TryGetValue(trackId, out SequenceExecution exec))
            return;

        exec.IsRunning = false;
        if (exec.Coroutine != null)
        {
            StopCoroutine(exec.Coroutine);
            exec.Coroutine = null;
        }

        if (releaseLocks && exec.Context != null)
        {
            exec.Context.ReleaseAllNpcClaims();
            exec.Context.ReleaseAllInputLocks();
            exec.Context.Variables.ClearLocal();
        }

        childExecutions.Remove(trackId);
    }

    private IEnumerator ExecuteSequenceCoroutine(SequenceExecution exec)
    {
        exec.IsRunning = true;
        activeControlExecution = exec;
        bool releaseInputLocksOnExit = false;
        bool aborted = false;

        if (enableDebugLogs)
            Debug.Log($"[SequenceRunner] Starting track '{exec.TrackId}': {exec.Sequence.name} ({exec.Sequence.StepCount} steps)");

        if (exec == mainExecution)
            OnSequenceStarted?.Invoke(exec.Sequence);

        while (exec.CurrentStepIndex < exec.Sequence.StepCount - 1)
        {
            if (exec.TrackType != SequenceTrackType.Main && exec.GracefulStopRequested)
            {
                releaseInputLocksOnExit = true;
                break;
            }

            activeControlExecution = exec;
            exec.CurrentStepIndex++;
            exec.Context.CurrentStepIndex = exec.CurrentStepIndex;

            SequenceStep step = exec.Sequence.GetStep(exec.CurrentStepIndex);
            if (step == null)
            {
                Debug.LogWarning($"[SequenceRunner] Step {exec.CurrentStepIndex} is null! Skipping.");
                continue;
            }

            if (step.ShouldSkip(exec.PersistentState))
            {
                if (enableDebugLogs)
                    Debug.Log($"[SequenceRunner] Skipping execute-once step {exec.CurrentStepIndex}: {step.GetStepName()}");
                continue;
            }

            if (enableDebugLogs)
                Debug.Log($"[SEQ] StepStart track={exec.TrackId} stepIndex={exec.CurrentStepIndex} stepName={step.GetStepName()}");

            if (exec == mainExecution)
                OnStepStarted?.Invoke(step, exec.CurrentStepIndex);

            SavePersistentStateFor(exec);

            StepOutcome outcome = StepOutcome.Success();
            bool stepCompleted = false;

            IEnumerator stepCoroutine = step.Execute(exec.Context, result =>
            {
                outcome = result;
                stepCompleted = true;
            });

            while (stepCoroutine.MoveNext())
                yield return stepCoroutine.Current;

            while (!stepCompleted)
                yield return null;

            exec.LastStepOutcome = outcome;

            if (outcome.Result == StepResult.Success)
            {
                if (enableDebugLogs)
                    Debug.Log($"[SEQ] StepComplete track={exec.TrackId} stepIndex={exec.CurrentStepIndex} stepName={step.GetStepName()}");
                step.MarkCompleted(exec.PersistentState);
                if (exec == mainExecution && step is Step_Label completedLabel)
                {
                    exec.PersistentState.lastCompletedLabel = completedLabel.GetLabelName();
                    // A label before the first authored checkpoint must not become a
                    // resume record. That record used to fall through to the safe
                    // label and skip name entry.
                    if (IntroResumePolicy.ShouldPersistLabelProgress(exec.PersistentState.checkpointId))
                        NotifyPersistentStateChanged(SequenceCheckpointStatus.Active);
                }
                if (exec == mainExecution)
                    OnStepComplete?.Invoke(step, exec.CurrentStepIndex);
            }
            else if (outcome.Result == StepResult.Failed)
            {
                if (!HandleStepFailure(exec, step, outcome))
                {
                    releaseInputLocksOnExit = true;
                    aborted = true;
                    break;
                }
            }
            else if (outcome.Result == StepResult.Cancelled)
            {
                if (enableDebugLogs)
                    Debug.Log($"[SequenceRunner] Step cancelled: {step.GetStepName()}");
                releaseInputLocksOnExit = true;
                aborted = true;
                break;
            }
        }

        exec.IsRunning = false;
        exec.CurrentStepIndex = -1;
        exec.Context.ReleaseAllNpcClaims();
        if (releaseInputLocksOnExit)
            exec.Context.ReleaseAllInputLocks();

        if (exec != mainExecution)
            exec.Context.Variables.ClearLocal();

        if (exec == mainExecution)
        {
            exec.Context.Variables.ClearLocal();
            if (!aborted && exec.LastStepOutcome.Result == StepResult.Success)
            {
                SavePersistentStateFor(exec);
                NotifyPersistentStateChanged(SequenceCheckpointStatus.Completed);
                exec.PersistentState.Reset();
            }
            else if (aborted)
            {
                OnSequenceAborted?.Invoke(exec.Sequence, exec.LastStepOutcome);
            }
            StopChildrenOnParentEnd();
            OnSequenceComplete?.Invoke(exec.Sequence);
            if (enableDebugLogs)
                Debug.Log($"[SEQ] SequenceComplete sequenceId={exec.Sequence.SequenceId}");
        }
        else
        {
            string tid = exec.TrackId;
            childExecutions.Remove(tid);
            OnChildTrackComplete?.Invoke(tid, exec);
            if (enableDebugLogs)
                Debug.Log($"[SEQ] ChildTrackComplete trackId={tid}");
        }

        if (activeControlExecution == exec)
            activeControlExecution = null;

        exec.Coroutine = null;
    }

    private bool HandleStepFailure(SequenceExecution exec, SequenceStep step, StepOutcome outcome)
    {
        StepFailurePolicy policy = step.GetFailurePolicy();
        if (policy == StepFailurePolicy.UseRunner)
            policy = defaultFailurePolicy;

        if (exec == mainExecution)
            OnStepFailed?.Invoke(step, outcome);

        if (enableDebugLogs)
            Debug.Log($"[SEQ] StepFailed track={exec.TrackId} stepName={step.GetStepName()} result={outcome.Result} message={outcome.Message}");

        switch (policy)
        {
            case StepFailurePolicy.AbortSequence:
                if (enableDebugLogs)
                    Debug.LogError($"[SequenceRunner] Step failed, aborting sequence: {step.GetStepName()} - {outcome.Message}");
                return false;

            case StepFailurePolicy.SkipAndContinue:
                if (enableDebugLogs)
                    Debug.LogWarning($"[SequenceRunner] Step failed, skipping: {step.GetStepName()} - {outcome.Message}");
                return true;

            case StepFailurePolicy.GotoLabel:
                string label = outcome.FailureLabel ?? step.GetOnFailLabel();
                if (!string.IsNullOrEmpty(label))
                {
                    JumpToLabel(exec, label);
                    return true;
                }
                Debug.LogError($"[SequenceRunner] Step has GotoLabel policy but no label set!");
                return false;

            case StepFailurePolicy.RetryStep:
                Debug.LogWarning($"[SequenceRunner] Retry policy not yet implemented for step: {step.GetStepName()}");
                return true;

            case StepFailurePolicy.RunFallback:
                if (exec != mainExecution)
                {
                    Debug.LogWarning($"[SequenceRunner] RunFallback on child track not supported; aborting track.");
                    return false;
                }
                if (fallbackSequence != null)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[SequenceRunner] Step failed, running fallback sequence: {step.GetStepName()}");
                    StopSequence();
                    StartSequence(fallbackSequence);
                    return false;
                }
                Debug.LogError($"[SequenceRunner] Step has RunFallback policy but no fallback sequence set!");
                return false;

            default:
                return false;
        }
    }

    public void PushCallStack(CallStackFrame frame)
    {
        (activeControlExecution != null ? activeControlExecution : mainExecution).CallStack.Push(frame);
    }

    public CallStackFrame PopCallStack()
    {
        Stack<CallStackFrame> stack = (activeControlExecution != null ? activeControlExecution : mainExecution).CallStack;
        return stack.Count > 0 ? stack.Pop() : null;
    }

    public void SetCheckpoint(string checkpointId, string checkpointLabel, string sceneId = "", string spawnId = "")
    {
        mainExecution.PersistentState.SetCheckpoint(checkpointId, checkpointLabel, mainExecution.CurrentStepIndex, sceneId, spawnId);
        CaptureRailFlagSnapshot(mainExecution.PersistentState);
        SavePersistentStateFor(mainExecution);
        NotifyPersistentStateChanged(SequenceCheckpointStatus.Active);
    }

    public SequenceState GetPersistentState() => mainExecution.PersistentState;

    public void MarkGateKeyPlayed(string gateKey)
    {
        if (!string.IsNullOrEmpty(gateKey))
        {
            mainExecution.PersistentState.playedGateKeys.Add(gateKey);
            SavePersistentStateFor(mainExecution);
        }
    }

    /// <summary>
    /// Drops in-memory intro progress so a nameless restart re-runs the name
    /// prompt. Keeps <see cref="SequenceState.currentSequenceId"/> so the state
    /// stays valid, and does not notify persistence: the server checkpoint is
    /// restored on the next launch and this guard runs again until a name commits.
    /// </summary>
    private void DiscardNamelessIntroProgressInMemory()
    {
        SequenceState ps = mainExecution.PersistentState;
        if (ps == null)
            return;

        string sequenceId = ps.currentSequenceId;
        ps.currentStepIndex = -1;
        ps.checkpointId = "";
        ps.checkpointLabel = "";
        ps.checkpointStepIndex = -1;
        ps.lastCompletedLabel = "";
        ps.sceneId = "";
        ps.spawnId = "";
        ps.playerName = "";
        ps.starterSelected = "";
        if (ps.persistentVariables == null)
            ps.persistentVariables = new Dictionary<string, string>();
        else
            ps.persistentVariables.Clear();
        if (ps.railFlagSnapshot == null)
            ps.railFlagSnapshot = new Dictionary<string, bool>(StringComparer.Ordinal);
        else
            ps.railFlagSnapshot.Clear();
        if (ps.completedStepGuids == null)
            ps.completedStepGuids = new HashSet<string>();
        else
            ps.completedStepGuids.Clear();
        ps.currentSequenceId = sequenceId;
    }

    private void LoadStateToContext()
    {
        SequenceState ps = mainExecution.PersistentState;
        mainExecution.Context.PlayerName = ps.playerName;
        mainExecution.Context.StarterSelected = ps.starterSelected;
        mainExecution.Context.CurrentSequenceId = ps.currentSequenceId;
        mainExecution.Context.CurrentStepIndex = ps.currentStepIndex;
        mainExecution.Context.Variables.RestorePersistentVariables(ps.persistentVariables);
    }

    private void SavePersistentStateFor(SequenceExecution exec)
    {
        if (!persistState || exec?.PersistentState == null)
            return;

        SequenceState ps = exec.PersistentState;
        ps.currentSequenceId = exec.Context.CurrentSequenceId;
        ps.currentStepIndex = exec.Context.CurrentStepIndex;
        ps.playerName = exec.Context.PlayerName;
        ps.starterSelected = exec.Context.StarterSelected;
        ps.persistentVariables = exec.Context.Variables.GetPersistentVariables();
    }

    private void LoadPersistentState()
    {
        // Profile-scoped loading is coordinated after authentication by
        // NarrativeProfileStateCoordinator. Keep runner construction synchronous.
    }

    public bool RestoreCheckpoint(SequenceCheckpointRecord record, SequenceCatalogSO catalog)
    {
        if (record == null || record.status != SequenceCheckpointStatus.Active ||
            string.IsNullOrWhiteSpace(record.sequenceId) || catalog == null ||
            !catalog.TryGet(record.sequenceId, out SequenceAsset sequence) || sequence == null)
        {
            return false;
        }

        if (mainExecution.IsRunning)
            StopSequence();

        mainExecution.Sequence = sequence;
        BuildLabelIndex(mainExecution, sequence);

        SequenceState state = mainExecution.PersistentState;
        state.Reset();
        state.currentSequenceId = record.sequenceId;
        state.checkpointId = record.checkpointId ?? "";
        state.checkpointLabel = record.checkpointLabel ?? "";
        state.lastCompletedLabel = record.lastCompletedLabel ?? "";
        state.checkpointStepIndex = record.checkpointStepIndex;
        state.sceneId = record.sceneId ?? "";
        state.spawnId = record.spawnId ?? "";
        state.revision = Math.Max(0L, record.revision);
        state.sequenceVersion = Math.Max(1, record.sequenceVersion);

        if (record.persistentVariables != null)
        {
            for (int i = 0; i < record.persistentVariables.Count; i++)
            {
                NarrativeStringValue entry = record.persistentVariables[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
                    state.persistentVariables[entry.key] = entry.value ?? "";
            }
        }

        if (record.completedStepGuids != null)
            state.completedStepGuids.UnionWith(record.completedStepGuids.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (record.playedGateKeys != null)
            state.playedGateKeys.UnionWith(record.playedGateKeys.Where(value => !string.IsNullOrWhiteSpace(value)));

        state.ReplaceRailFlagSnapshot(record.railFlagSnapshot);

        mainExecution.Context.Clear();
        LoadStateToContext();
        return true;
    }

    public bool HasRestoredCheckpoint(string sequenceId = null)
    {
        SequenceState state = mainExecution.PersistentState;
        if (state == null || !state.IsValid || mainExecution.Sequence == null)
            return false;

        return string.IsNullOrWhiteSpace(sequenceId) ||
            string.Equals(state.currentSequenceId, sequenceId, StringComparison.Ordinal);
    }

    public bool ResumeRestoredCheckpoint(string sequenceId = null)
    {
        if (!HasRestoredCheckpoint(sequenceId) || mainExecution.IsRunning)
            return false;

        ResumeAtRestoredGate();
        return mainExecution.IsRunning;
    }

    /// <summary>
    /// Scene/spawn/label used to place the player before continuing a hydrated sequence.
    /// </summary>
    /// <summary>
    /// Drops a hydrated intro checkpoint so the next play starts at step 0
    /// on the same in-memory runner. Used when the profile still has no name.
    /// </summary>
    public void DiscardRestoredCheckpoint(string sequenceId)
    {
        if (mainExecution.IsRunning)
            StopSequence();

        SequenceState state = mainExecution.PersistentState;
        bool matches = string.IsNullOrWhiteSpace(sequenceId) ||
            (state != null && string.Equals(state.currentSequenceId, sequenceId, StringComparison.Ordinal)) ||
            (mainExecution.Sequence != null && string.Equals(mainExecution.Sequence.SequenceId, sequenceId, StringComparison.Ordinal));
        if (!matches)
            return;

        if (state != null)
            state.Reset();
        mainExecution.Sequence = null;
        mainExecution.CurrentStepIndex = -1;
        mainExecution.Context.Clear();
    }

    public bool TryGetResumePlacement(out string sceneId, out string spawnId, out string resumeLabel)
    {
        sceneId = "";
        spawnId = "";
        resumeLabel = "";
        SequenceState state = mainExecution.PersistentState;
        SequenceAsset sequence = mainExecution.Sequence;
        if (state == null || !state.IsValid || sequence == null)
            return false;

        BuildLabelIndex(mainExecution, sequence);
        IntroCheckpointResume choice = BuildResumeChoice(sequence, state);
        if (choice.RestartAtBeginning)
        {
            if (!string.IsNullOrWhiteSpace(choice.RecoveryReason) && enableDebugLogs)
                Debug.LogWarning($"[SequenceRunner] {choice.RecoveryReason}");
            return true;
        }

        sceneId = state.sceneId ?? "";
        spawnId = state.spawnId ?? "";
        resumeLabel = choice.ResumeLabel ?? "";

        Step_SetCheckpoint authored = FindCheckpointStep(sequence, state.checkpointId);
        if ((authored == null || string.IsNullOrWhiteSpace(authored.ResumeSceneId)) &&
            !string.IsNullOrWhiteSpace(resumeLabel))
        {
            Step_SetCheckpoint labeled = FindCheckpointByLabel(sequence, resumeLabel);
            if (labeled != null)
                authored = labeled;
        }

        if (authored != null)
        {
            IntroResumePolicy.PreferAuthoredResumePlacement(
                sceneId,
                spawnId,
                authored.ResumeSceneId,
                authored.ResumeSpawnId,
                out sceneId,
                out spawnId);
            if (string.IsNullOrWhiteSpace(resumeLabel))
                resumeLabel = authored.CheckpointLabel;
        }

        if (!string.IsNullOrWhiteSpace(resumeLabel) &&
            !mainExecution.LabelIndex.ContainsKey(resumeLabel))
        {
            resumeLabel = "";
        }

        if (!string.IsNullOrWhiteSpace(choice.RecoveryReason) && enableDebugLogs)
            Debug.LogWarning($"[SequenceRunner] {choice.RecoveryReason}");

        return true;
    }

    public void ApplyResumeProfileWorldFlags()
    {
        SequenceState state = mainExecution.PersistentState;
        SequenceAsset sequence = mainExecution.Sequence;
        if (state == null || sequence == null)
            return;

        ProfileWorldFlagStateService flags = ProfileWorldFlagStateService.Instance
            ?? FindFirstObjectByType<ProfileWorldFlagStateService>(FindObjectsInactive.Include);
        if (flags == null)
            return;

        Step_SetCheckpoint authored = FindCheckpointStep(sequence, state.checkpointId);
        List<(string key, bool value)> resolved = WorldPresenceResumePolicy.Resolve(
            state.CopyRailFlagSnapshot(),
            authored != null ? authored.ResumeProfileWorldFlags : null);
        flags.RestoreRailSnapshot(resolved);
    }

    public void ResumeAtRestoredGate()
    {
        if (!TryGetResumePlacement(out _, out _, out string resumeLabel))
            return;

        if (!string.IsNullOrWhiteSpace(resumeLabel))
            ResumeFromLabel(resumeLabel);
        else
            ResumeFromCheckpoint();
    }

    private static Step_SetCheckpoint FindCheckpointStep(SequenceAsset sequence, string checkpointId)
    {
        if (sequence == null || string.IsNullOrWhiteSpace(checkpointId))
            return null;

        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) is Step_SetCheckpoint checkpoint &&
                string.Equals(checkpoint.CheckpointId, checkpointId, StringComparison.Ordinal))
            {
                return checkpoint;
            }
        }

        return null;
    }

    private static Step_SetCheckpoint FindCheckpointByLabel(SequenceAsset sequence, string label)
    {
        if (sequence == null || string.IsNullOrWhiteSpace(label))
            return null;

        Step_SetCheckpoint fallback = null;
        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) is Step_SetCheckpoint checkpoint &&
                string.Equals(checkpoint.CheckpointLabel, label, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(checkpoint.ResumeSceneId))
                    return checkpoint;
                if (fallback == null)
                    fallback = checkpoint;
            }
        }

        return fallback;
    }

    public void ClearPersistentState(string sequenceId)
    {
        SequenceState state = mainExecution.PersistentState;
        if (state == null || (!string.IsNullOrWhiteSpace(sequenceId) &&
            !string.Equals(state.currentSequenceId, sequenceId, StringComparison.Ordinal)))
            return;

        if (mainExecution.IsRunning)
            StopSequence();
        state.Reset();
        mainExecution.Sequence = null;
        mainExecution.CurrentStepIndex = -1;
        mainExecution.Context.Clear();
    }

    public SequenceCheckpointRecord CreateCheckpointRecord(SequenceCheckpointStatus status)
    {
        SequenceState state = mainExecution.PersistentState;
        SequenceAsset sequence = mainExecution.Sequence;
        if (state == null || sequence == null || string.IsNullOrWhiteSpace(sequence.SequenceId))
            return null;

        SavePersistentStateFor(mainExecution);
        var record = new SequenceCheckpointRecord
        {
            sequenceId = sequence.SequenceId,
            status = status,
            checkpointId = state.checkpointId ?? "",
            checkpointLabel = state.checkpointLabel ?? "",
            lastCompletedLabel = state.lastCompletedLabel ?? "",
            checkpointStepIndex = state.checkpointStepIndex,
            sceneId = state.sceneId ?? "",
            spawnId = state.spawnId ?? "",
            revision = state.revision,
            sequenceVersion = sequence.ContentVersion,
            updatedAtUtc = DateTime.UtcNow.ToString("O")
        };

        foreach (KeyValuePair<string, string> pair in state.persistentVariables)
            record.persistentVariables.Add(new NarrativeStringValue(pair.Key, pair.Value));
        record.persistentVariables.Sort((a, b) => string.CompareOrdinal(a?.key ?? "", b?.key ?? ""));
        record.railFlagSnapshot = state.CopyRailFlagSnapshot();
        record.completedStepGuids.AddRange(state.completedStepGuids.OrderBy(value => value, StringComparer.Ordinal));
        record.playedGateKeys.AddRange(state.playedGateKeys.OrderBy(value => value, StringComparer.Ordinal));
        return record;
    }

    private void CaptureRailFlagSnapshot(SequenceState state)
    {
        if (state == null)
            return;

        ProfileWorldFlagStateService flags = ProfileWorldFlagStateService.Instance
            ?? FindFirstObjectByType<ProfileWorldFlagStateService>(FindObjectsInactive.Include);
        if (flags == null)
        {
            state.ReplaceRailFlagSnapshot(null);
            return;
        }

        state.ReplaceRailFlagSnapshot(flags.CaptureRailSnapshot());
    }

    private void NotifyPersistentStateChanged(SequenceCheckpointStatus status)
    {
        if (!persistState || mainExecution.Sequence == null)
            return;

        SequenceCheckpointRecord record = CreateCheckpointRecord(status);
        if (record == null)
            return;

        record.revision = ++mainExecution.PersistentState.revision;
        OnPersistentStateChanged?.Invoke(record);
    }

    private int ResolveResumeIndex(SequenceAsset sequence, SequenceState state, out string recoveryReason)
    {
        IntroCheckpointResume choice = BuildResumeChoice(sequence, state);
        recoveryReason = choice.RecoveryReason ?? "";
        return choice.StepIndex;
    }

    private IntroCheckpointResume BuildResumeChoice(SequenceAsset sequence, SequenceState state)
    {
        if (sequence == null || state == null)
        {
            return IntroResumePolicy.Choose("", false, -1, "", "", false, -1, "", false, -1);
        }

        bool checkpointFound = TryResolveCheckpointId(
            sequence,
            state.checkpointId,
            out int checkpointIndex,
            out string authoredLabel);
        int labelIndex = -1;
        bool labelFound = !string.IsNullOrWhiteSpace(state.checkpointLabel) &&
            mainExecution.LabelIndex.TryGetValue(state.checkpointLabel, out labelIndex);
        int safeIndex = -1;
        bool safeFound = !string.IsNullOrWhiteSpace(sequence.SafeResumeLabel) &&
            mainExecution.LabelIndex.TryGetValue(sequence.SafeResumeLabel, out safeIndex);

        IntroCheckpointResume choice = IntroResumePolicy.Choose(
            state.checkpointId,
            checkpointFound,
            checkpointIndex,
            authoredLabel,
            state.checkpointLabel,
            labelFound,
            labelIndex,
            sequence.SafeResumeLabel,
            safeFound,
            safeIndex);

        int resumeLabelIndex = -1;
        if (!string.IsNullOrWhiteSpace(choice.ResumeLabel) &&
            mainExecution.LabelIndex.TryGetValue(choice.ResumeLabel, out int foundLabelIndex))
            resumeLabelIndex = foundLabelIndex;

        return IntroResumePolicy.HoldNamelessPastNameCommit(
            choice,
            sequence.SequenceId,
            CurrentDisplayName(),
            FindNameCommitStepIndex(sequence),
            resumeLabelIndex);
    }

    private static bool ShouldHoldNamelessResume(SequenceAsset sequence, int resumeStepIndex)
    {
        if (sequence == null)
            return false;

        return IntroResumePolicy.ShouldHoldNamelessResume(
            sequence.SequenceId,
            CurrentDisplayName(),
            FindNameCommitStepIndex(sequence),
            resumeStepIndex);
    }

    private static int FindNameCommitStepIndex(SequenceAsset sequence)
    {
        if (sequence == null)
            return -1;

        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) is Step_CommitPlayerName)
                return i;
        }

        return -1;
    }

    private static string CurrentDisplayName()
    {
        GameSessionContext session = GameSessionContext.Instance;
        return session != null ? session.DisplayName ?? "" : "";
    }

    private static bool TryResolveCheckpointId(
        SequenceAsset sequence,
        string checkpointId,
        out int stepIndex,
        out string checkpointLabel)
    {
        stepIndex = -1;
        checkpointLabel = "";
        if (sequence == null || string.IsNullOrWhiteSpace(checkpointId))
            return false;

        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) is Step_SetCheckpoint checkpoint &&
                string.Equals(checkpoint.CheckpointId, checkpointId, StringComparison.Ordinal))
            {
                stepIndex = i;
                checkpointLabel = checkpoint.CheckpointLabel;
                return true;
            }
        }

        return false;
    }
}
