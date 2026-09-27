using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneLoadingConfig_Default", menuName = "Signal Jam/Loading/Scene Loading Config")]
public sealed class SceneLoadingConfigSO : ScriptableObject
{
    [Serializable]
    public struct DestinationRule
    {
        [SerializeField] private string sceneId;
        [SerializeField] private TransitionStyle style;

        public string SceneId => sceneId?.Trim() ?? string.Empty;
        public TransitionStyle Style => style;

        public DestinationRule(string sceneId, TransitionStyle style)
        {
            this.sceneId = sceneId;
            this.style = style;
        }
    }

    [Header("Startup")]
    [SerializeField] private string startupSceneId = "main_menu";
    [SerializeField] private TransitionStyle startupStyle = TransitionStyle.FullLoading;
    [SerializeField] private string recoverySceneId = "main_menu";
    [SerializeField] private TransitionStyle defaultStyle = TransitionStyle.QuickFade;

    [Header("Destination Rules")]
    [SerializeField] private List<DestinationRule> destinationRules = new List<DestinationRule>
    {
        new DestinationRule("main_menu", TransitionStyle.FullLoading),
        new DestinationRule("Loomville", TransitionStyle.FullLoading),
    };

    [Header("Overworld Streaming")]
    [Tooltip("One Overworld Streaming Set asset per hub (e.g. OverworldStreamingSet_Loomville). Scene members remain covered destinations only when explicitly listed as destination rules.")]
    [SerializeField] private OverworldStreamingConfigSO overworldStreamingConfig;

    [Header("Presentation Timing")]
    [SerializeField, Min(0f)] private float fullMinimumDisplaySeconds = 2.75f;
    [SerializeField, Min(0f)] private float quickMinimumDisplaySeconds = 0.8f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 0.2f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.4f;
    [SerializeField, Min(0f)] private float postLoadFadeOutDelaySeconds = 0.3f;
    [SerializeField, Min(1f)] private float stillLoadingMessageSeconds = 8f;

    [Header("Loading Work")]
    [SerializeField] private ThreadPriority fullLoadingPriority = ThreadPriority.Normal;
    [SerializeField] private ThreadPriority quickLoadingPriority = ThreadPriority.BelowNormal;
    [SerializeField, Min(0.1f)] private float warmupTaskTimeoutSeconds = 10f;
    [SerializeField, Min(1)] private int warmupBatchSize = 32;

    public string StartupSceneId => string.IsNullOrWhiteSpace(startupSceneId) ? "main_menu" : startupSceneId.Trim();
    public TransitionStyle StartupStyle => startupStyle;
    public string RecoverySceneId => string.IsNullOrWhiteSpace(recoverySceneId) ? "main_menu" : recoverySceneId.Trim();
    public TransitionStyle DefaultStyle => defaultStyle;
    public IReadOnlyList<DestinationRule> DestinationRules => destinationRules;
    public OverworldStreamingConfigSO OverworldStreamingConfig => overworldStreamingConfig;
    public float FadeInSeconds => fadeInSeconds;
    public float FadeOutSeconds => fadeOutSeconds;
    public float PostLoadFadeOutDelaySeconds => postLoadFadeOutDelaySeconds;
    public float StillLoadingMessageSeconds => stillLoadingMessageSeconds;
    public float WarmupTaskTimeoutSeconds => warmupTaskTimeoutSeconds;
    public int WarmupBatchSize => warmupBatchSize;

    public float GetMinimumDisplaySeconds(TransitionStyle style)
    {
        return style == TransitionStyle.FullLoading
            ? fullMinimumDisplaySeconds
            : quickMinimumDisplaySeconds;
    }

    public ThreadPriority GetBackgroundPriority(TransitionStyle style)
    {
        return style == TransitionStyle.FullLoading
            ? fullLoadingPriority
            : quickLoadingPriority;
    }

    public TransitionStyle ResolveStyle(string sceneId, TransitionStyle? styleOverride = null)
    {
        if (styleOverride.HasValue)
            return styleOverride.Value;

        string normalized = sceneId?.Trim() ?? string.Empty;
        for (int i = 0; i < destinationRules.Count; i++)
        {
            if (string.Equals(destinationRules[i].SceneId, normalized, StringComparison.OrdinalIgnoreCase))
                return destinationRules[i].Style;
        }

        return defaultStyle;
    }

    public static SceneLoadingConfigSO CreateRuntimeDefault()
    {
        SceneLoadingConfigSO config = CreateInstance<SceneLoadingConfigSO>();
        config.name = "SceneLoadingConfig_RuntimeDefault";
        config.hideFlags = HideFlags.HideAndDontSave;
        config.overworldStreamingConfig = OverworldStreamingConfigSO.CreateRuntimeDefault();
        config.ClampValues();
        return config;
    }

    private void OnValidate()
    {
        ClampValues();
    }

    private void ClampValues()
    {
        startupSceneId = startupSceneId?.Trim() ?? string.Empty;
        recoverySceneId = recoverySceneId?.Trim() ?? string.Empty;
        destinationRules ??= new List<DestinationRule>();
        fullMinimumDisplaySeconds = Mathf.Max(0f, fullMinimumDisplaySeconds);
        quickMinimumDisplaySeconds = Mathf.Max(0f, quickMinimumDisplaySeconds);
        fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
        fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
        postLoadFadeOutDelaySeconds = Mathf.Max(0f, postLoadFadeOutDelaySeconds);
        stillLoadingMessageSeconds = Mathf.Max(1f, stillLoadingMessageSeconds);
        warmupTaskTimeoutSeconds = Mathf.Max(0.1f, warmupTaskTimeoutSeconds);
        warmupBatchSize = Mathf.Max(1, warmupBatchSize);
    }
}
