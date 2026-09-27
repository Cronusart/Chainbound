using System;
using System.Collections.Generic;
using UnityEngine;

public enum AppInterruptContext
{
    Overworld,
    PlayerMenu,
    MainMenu,
    CombatPvE,
    DialogueOrSequence,
    WalletSigning,
    Loading
}

public enum AppInterruptOverlayMode
{
    None,
    ContinueOnly,
    ContinueAndFlee,
    WalletWait
}

[Serializable]
public struct AppInterruptRule
{
    public AppInterruptContext context;
    public bool pauseTimeScale;
    public bool muteAudio;
    public AppInterruptOverlayMode overlay;
    public bool lockInput;
    public bool keepBattle;
}

[CreateAssetMenu(menuName = "Signal Jam/App Lifecycle Interrupt Policy", fileName = "AppInterruptPolicy")]
public sealed class AppInterruptPolicySO : ScriptableObject
{
    [SerializeField] private List<AppInterruptRule> rules = new List<AppInterruptRule>();
    [SerializeField, Min(1f)] private float walletTimeoutSeconds = 120f;
    [SerializeField] private bool ignoreOsInterruptsInEditor = true;

    public float WalletTimeoutSeconds => Mathf.Max(1f, walletTimeoutSeconds);
    public bool IgnoreOsInterruptsInEditor => ignoreOsInterruptsInEditor;
    public IReadOnlyList<AppInterruptRule> Rules => rules;

    public AppInterruptRule GetRule(AppInterruptContext context)
    {
        for (int i = 0; i < rules.Count; i++)
            if (rules[i].context == context)
                return rules[i];
        return AppInterruptPolicyResolver.DefaultRule(context);
    }

    public void ResetToDefaults()
    {
        rules.Clear();
        foreach (AppInterruptContext context in Enum.GetValues(typeof(AppInterruptContext)))
            rules.Add(AppInterruptPolicyResolver.DefaultRule(context));
    }
}

public readonly struct AppInterruptPolicy
{
    public readonly bool PauseTimeScale;
    public readonly bool MuteAudio;
    public readonly AppInterruptOverlayMode Overlay;
    public readonly bool LockInput;
    public readonly bool KeepBattle;

    public AppInterruptPolicy(bool pauseTimeScale, bool muteAudio, AppInterruptOverlayMode overlay, bool lockInput, bool keepBattle)
    {
        PauseTimeScale = pauseTimeScale;
        MuteAudio = muteAudio;
        Overlay = overlay;
        LockInput = lockInput;
        KeepBattle = keepBattle;
    }
}

/// <summary>Pure policy lookup kept separate from lifecycle side effects for easy tests.</summary>
public static class AppInterruptPolicyResolver
{
    public static AppInterruptPolicy Resolve(AppInterruptContext context)
    {
        return Resolve(null, context);
    }

    public static AppInterruptPolicy Resolve(AppInterruptPolicySO policy, AppInterruptContext context)
    {
        AppInterruptRule rule = policy != null ? policy.GetRule(context) : DefaultRule(context);
        return new AppInterruptPolicy(rule.pauseTimeScale, rule.muteAudio, rule.overlay, rule.lockInput, rule.keepBattle);
    }

    public static AppInterruptRule DefaultRule(AppInterruptContext context)
    {
        switch (context)
        {
            case AppInterruptContext.Overworld:
            case AppInterruptContext.PlayerMenu:
            case AppInterruptContext.DialogueOrSequence:
                return new AppInterruptRule { context = context, pauseTimeScale = true, muteAudio = true, overlay = AppInterruptOverlayMode.ContinueOnly, lockInput = true, keepBattle = true };
            case AppInterruptContext.CombatPvE:
                return new AppInterruptRule { context = context, pauseTimeScale = true, muteAudio = true, overlay = AppInterruptOverlayMode.ContinueAndFlee, lockInput = true, keepBattle = true };
            case AppInterruptContext.WalletSigning:
                return new AppInterruptRule { context = context, pauseTimeScale = false, muteAudio = false, overlay = AppInterruptOverlayMode.WalletWait, lockInput = true, keepBattle = true };
            case AppInterruptContext.Loading:
            case AppInterruptContext.MainMenu:
            default:
                return new AppInterruptRule { context = context, pauseTimeScale = false, muteAudio = false, overlay = AppInterruptOverlayMode.None, lockInput = false, keepBattle = true };
        }
    }
}
