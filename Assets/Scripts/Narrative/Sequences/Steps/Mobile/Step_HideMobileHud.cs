using System;
using System.Collections;
using SignalJam.UI.MobileControls;
using UnityEngine;

/// <summary>
/// One-liner convenience wrapper around <c>Step_SetMobileHudContext</c> for the common
/// "hide the HUD for a cutscene" case. Acquires a <see cref="MobileHudContext.Cutscene"/>
/// token (or <see cref="MobileHudContext.Hidden"/> when the designer flips the toggle)
/// and stores it under a fixed default key so a later <c>Step_ShowMobileHud</c> can
/// release it without any configuration.
///
/// Use <c>Step_SetMobileHudContext</c> directly when you need a custom tokenKey,
/// Dialogue/Battle contexts, or multiple concurrent tokens.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_HideMobileHud",
    menuName = "Chainbound/Narrative/Steps/Mobile/Hide Mobile HUD")]
public class Step_HideMobileHud : SequenceStep
{
    /// <summary>Default token key used by the convenience Hide/Show pair.</summary>
    public const string DefaultTokenKey = "mobile_hud_convenience";

    [Header("Strength")]
    [Tooltip("Use Hidden (PSG1-like full hide) instead of Cutscene. Cutscene is the usual choice so dialogue/skip surfaces still appear.")]
    [SerializeField] private bool fullyHide = false;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        MobileHudService svc = context.MobileHudService;
        if (svc == null)
        {
            Debug.LogError("[Step_HideMobileHud] MobileHudService not found!");
            onComplete?.Invoke(StepOutcome.Failed("MobileHudService not found"));
            yield break;
        }

        MobileHudContext target = fullyHide ? MobileHudContext.Hidden : MobileHudContext.Cutscene;
        string reason = $"Sequence:{context.CurrentSequenceId}:Step_HideMobileHud";

        MobileHudToken token = svc.AcquireContext(target, reason);
        context.StoreMobileHudToken(DefaultTokenKey, token);

        if (enableDebugLogs)
        {
            Debug.Log($"[Step_HideMobileHud] Acquired {target} (key='{DefaultTokenKey}')");
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}
