using System;
using System.Collections;
using SignalJam.UI.MobileControls;
using UnityEngine;

/// <summary>
/// Sequence step that pushes a <see cref="MobileHudContext"/> on
/// <see cref="MobileHudService"/> and stores the returned <see cref="MobileHudToken"/>
/// on the <see cref="SequenceContext"/> under a designer-chosen <c>tokenKey</c>.
///
/// Pair with <c>Step_ReleaseMobileHudContext</c> using the same key to restore the
/// HUD. If the sequence is stopped early, <see cref="SequenceContext.ReleaseAllMobileHudTokens"/>
/// runs on <c>Clear()</c> so tokens never leak across sequences.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_SetMobileHudContext",
    menuName = "Chainbound/Narrative/Steps/Mobile/Set Mobile HUD Context")]
public class Step_SetMobileHudContext : SequenceStep
{
    [Header("Context")]
    [Tooltip("Context to push onto the HUD service. Hidden > Menu > Cutscene > Dialogue > Battle > Overworld.")]
    [SerializeField] private MobileHudContext hudContext = MobileHudContext.Cutscene;

    [Tooltip("Human-readable reason (debug only). Defaults to sequence id + step name when blank.")]
    [SerializeField] private string reason = "";

    [Header("Token")]
    [Tooltip("Key used to store this token on the SequenceContext. Paired Step_ReleaseMobileHudContext uses the same key.")]
    [SerializeField] private string tokenKey = "default";

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        MobileHudService svc = context.MobileHudService;
        if (svc == null)
        {
            Debug.LogError("[Step_SetMobileHudContext] MobileHudService not found!");
            onComplete?.Invoke(StepOutcome.Failed("MobileHudService not found"));
            yield break;
        }

        if (string.IsNullOrEmpty(tokenKey))
        {
            Debug.LogError("[Step_SetMobileHudContext] tokenKey is empty. Pick a key so a matching Release step can find it.");
            onComplete?.Invoke(StepOutcome.Failed("tokenKey is empty"));
            yield break;
        }

        string effectiveReason = string.IsNullOrEmpty(reason)
            ? $"Sequence:{context.CurrentSequenceId}:{GetStepName()}"
            : reason;

        MobileHudToken token = svc.AcquireContext(hudContext, effectiveReason);
        context.StoreMobileHudToken(tokenKey, token);

        if (enableDebugLogs)
        {
            Debug.Log($"[Step_SetMobileHudContext] Acquired {hudContext} ('{effectiveReason}') key='{tokenKey}'");
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}
