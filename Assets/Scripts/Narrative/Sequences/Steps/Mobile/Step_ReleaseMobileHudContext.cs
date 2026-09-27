using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Sequence step that releases the <see cref="SignalJam.UI.MobileControls.MobileHudToken"/>
/// previously stored on the <see cref="SequenceContext"/> by a paired
/// <c>Step_SetMobileHudContext</c> with the same <c>tokenKey</c>.
///
/// If the token is missing (already released, key mismatch) the step logs a warning
/// and reports success by default: cutscenes should not halt just because cleanup
/// already ran. Flip <c>requireMatchingToken</c> when you need strict authoring.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_ReleaseMobileHudContext",
    menuName = "Chainbound/Narrative/Steps/Mobile/Release Mobile HUD Context")]
public class Step_ReleaseMobileHudContext : SequenceStep
{
    [Header("Token")]
    [Tooltip("Key of the token to release. Must match the key used by the paired Step_SetMobileHudContext.")]
    [SerializeField] private string tokenKey = "default";

    [Tooltip("When true, failing to find a token reports the step as Failed instead of Success.")]
    [SerializeField] private bool requireMatchingToken = false;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(tokenKey))
        {
            Debug.LogError("[Step_ReleaseMobileHudContext] tokenKey is empty.");
            onComplete?.Invoke(StepOutcome.Failed("tokenKey is empty"));
            yield break;
        }

        bool released = context.TryReleaseMobileHudToken(tokenKey);

        if (enableDebugLogs)
        {
            Debug.Log($"[Step_ReleaseMobileHudContext] key='{tokenKey}' released={released}");
        }

        if (!released)
        {
            string msg = $"No MobileHudToken stored under key '{tokenKey}'";
            if (requireMatchingToken)
            {
                onComplete?.Invoke(StepOutcome.Failed(msg));
                yield break;
            }
            Debug.LogWarning($"[Step_ReleaseMobileHudContext] {msg} (continuing).");
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}
