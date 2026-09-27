using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that acquires an input lock.
/// Stores handle in context for later release.
/// </summary>
[CreateAssetMenu(fileName = "Step_LockInput", menuName = "Chainbound/Narrative/Steps/Lock Input")]
public class Step_LockInput : SequenceStep
{
    [Header("Lock Settings")]
    [Tooltip("Channel to lock. All also suppresses the gameplay player menu; PlayerMenu suppresses only that menu.")]
    [SerializeField] private InputLockChannel channel = InputLockChannel.All;
    
    [Tooltip("Reason for lock (for debugging)")]
    [SerializeField] private string reason = "Sequence";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        InputLockService inputLock = context.InputLockService;
        if (inputLock == null)
        {
            Debug.LogError("[Step_LockInput] InputLockService not found!");
            onComplete?.Invoke(StepOutcome.Failed("InputLockService not found"));
            yield break;
        }
        
        InputLockHandle handle = inputLock.AcquireLock(reason, channel, SequenceRunner.Instance);
        context.StoreInputLock(handle);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_LockInput] Acquired lock: {reason} ({channel})");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}

