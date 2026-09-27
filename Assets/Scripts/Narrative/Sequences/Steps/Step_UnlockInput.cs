using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that releases every input lock stored on this sequence context.
/// That includes <see cref="Step_LockInput"/> handles and the play-wide lock
/// <see cref="SequenceDirector"/> stores on the same context when a sequence starts.
/// </summary>
[CreateAssetMenu(fileName = "Step_UnlockInput", menuName = "Chainbound/Narrative/Steps/Unlock Input")]
public class Step_UnlockInput : SequenceStep
{
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        context.ReleaseAllInputLocks();

        InputLockService inputLock = context.InputLockService;
        if (inputLock != null && (inputLock.IsInteractionLocked || inputLock.IsPlayerMenuLocked))
        {
            Debug.LogWarning(
                "[Step_UnlockInput] Sequence-owned locks released, but gameplay input is still locked by: " +
                string.Join(", ", inputLock.GetActiveLockReasons()));
        }
        else if (enableDebugLogs)
        {
            Debug.Log("[Step_UnlockInput] Released all sequence-owned input locks");
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}

