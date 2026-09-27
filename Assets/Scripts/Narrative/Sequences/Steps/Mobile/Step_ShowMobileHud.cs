using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// One-liner convenience wrapper that releases the default token acquired by
/// <c>Step_HideMobileHud</c>. Safe to call even if the Hide step never ran; missing
/// token releases log a warning and report success unless <c>requireMatchingToken</c>
/// is enabled.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_ShowMobileHud",
    menuName = "Chainbound/Narrative/Steps/Mobile/Show Mobile HUD")]
public class Step_ShowMobileHud : SequenceStep
{
    [Header("Strictness")]
    [Tooltip("When true, failing to find the default token reports the step as Failed instead of Success.")]
    [SerializeField] private bool requireMatchingToken = false;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        bool released = context.TryReleaseMobileHudToken(Step_HideMobileHud.DefaultTokenKey);

        if (enableDebugLogs)
        {
            Debug.Log($"[Step_ShowMobileHud] released={released} (key='{Step_HideMobileHud.DefaultTokenKey}')");
        }

        if (!released)
        {
            string msg = $"No MobileHudToken stored under default key '{Step_HideMobileHud.DefaultTokenKey}'";
            if (requireMatchingToken)
            {
                onComplete?.Invoke(StepOutcome.Failed(msg));
                yield break;
            }
            Debug.LogWarning($"[Step_ShowMobileHud] {msg} (continuing).");
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}
