using System;
using System.Collections;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Step_SetGameplayMenuAvailability",
    menuName = "Chainbound/Narrative/Steps/Set Gameplay Menu Availability")]
public class Step_SetGameplayMenuAvailability : SequenceStep
{
    public enum AvailabilityAction
    {
        Suppress,
        Release,
        ClearAll,
    }

    [SerializeField] private AvailabilityAction action = AvailabilityAction.Suppress;
    [SerializeField] private string tokenKey = "gameplay_menu_sequence";
    [SerializeField] private string reason = "";
    [SerializeField] private bool requireMatchingToken = false;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        GameplayMenuAvailabilityService service = GameplayMenuAvailabilityService.Instance;
        if (service == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("GameplayMenuAvailabilityService not found"));
            yield break;
        }

        switch (action)
        {
            case AvailabilityAction.Suppress:
            {
                string effectiveReason = string.IsNullOrWhiteSpace(reason)
                    ? $"Sequence:{context.CurrentSequenceId}:{GetStepName()}"
                    : reason.Trim();
                GameplayMenuAvailabilityToken token = service.AcquireSuppression(effectiveReason);
                context.StoreGameplayMenuAvailabilityToken(tokenKey, token);
                onComplete?.Invoke(StepOutcome.Success("Gameplay menu suppressed."));
                break;
            }
            case AvailabilityAction.Release:
            {
                bool released = context.TryReleaseGameplayMenuAvailabilityToken(tokenKey);
                if (!released && requireMatchingToken)
                    onComplete?.Invoke(StepOutcome.Failed($"No gameplay menu token stored under '{tokenKey}'."));
                else
                    onComplete?.Invoke(StepOutcome.Success(released ? "Gameplay menu suppression released." : "No matching token to release."));
                break;
            }
            case AvailabilityAction.ClearAll:
                context.ReleaseAllGameplayMenuAvailabilityTokens();
                service.ClearAllSuppressions();
                onComplete?.Invoke(StepOutcome.Success("All gameplay menu suppressions cleared."));
                break;
            default:
                onComplete?.Invoke(StepOutcome.Failed($"Unsupported action {action}."));
                break;
        }

        yield return null;
    }
}
