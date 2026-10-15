using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Retains or releases the active NPC interaction session.
/// Use Retain before a gap with no dialogue, such as an item-obtain panel,
/// so the NPC keeps the route pause and player facing until Release.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_HoldNpcInteraction",
    menuName = "Chainbound/Narrative/Steps/Hold NPC Interaction")]
public class Step_HoldNpcInteraction : SequenceStep
{
    public enum HoldMode
    {
        Retain,
        Release
    }

    [SerializeField] private HoldMode mode = HoldMode.Retain;

    public HoldMode Mode => mode;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (context == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("Sequence context is missing"));
            yield break;
        }

        if (mode == HoldMode.Retain)
        {
            if (!context.TryRetainNpcInteraction())
            {
                onComplete?.Invoke(StepOutcome.Failed("No NPC interaction session is active to retain."));
                yield break;
            }
        }
        else
        {
            context.ReleaseNpcInteractionRetention();
        }

        if (enableDebugLogs)
            Debug.Log($"[Step_HoldNpcInteraction] {mode}.", this);

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}
