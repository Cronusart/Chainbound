using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Queues another sequence to play after the current one completes.
/// Use this to hand off post-intro rails without nesting via Call Sequence.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_QueueSequence",
    menuName = "Chainbound/Narrative/Steps/Control Flow/Queue Sequence")]
public class Step_QueueSequence : SequenceStep
{
    [Header("Sequence")]
    [SerializeField] private SequenceAsset sequence;

    [Tooltip("Idle/wait rails should leave this false so the player can roam.")]
    [SerializeField] private bool acquireInputLock;

    public SequenceAsset Sequence => sequence;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (sequence == null)
        {
            Debug.LogError("[Step_QueueSequence] Sequence is not set!");
            onComplete?.Invoke(StepOutcome.Failed("Sequence is not set"));
            yield break;
        }

        SequenceDirector director = SequenceDirector.Instance;
        if (director == null)
        {
            Debug.LogError("[Step_QueueSequence] SequenceDirector not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceDirector not found"));
            yield break;
        }

        director.Queue(sequence, new SequenceParameters { acquireInputLock = acquireInputLock });
        if (enableDebugLogs)
            Debug.Log($"[Step_QueueSequence] Queued '{sequence.name}' (acquireInputLock={acquireInputLock})");

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public override bool Validate()
    {
        if (sequence == null)
        {
            Debug.LogWarning("[Step_QueueSequence] Sequence is not assigned!");
            return false;
        }

        return true;
    }
}
