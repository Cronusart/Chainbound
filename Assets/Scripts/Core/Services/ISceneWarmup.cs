using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A deterministic, scene-scoped readiness task executed after activation and
/// before the persistent loading cover reveals the destination.
/// </summary>
public interface ISceneWarmupTask
{
    string WarmupId { get; }
    int Order { get; }
    float Weight { get; }
    bool IsCritical { get; }
    float TimeoutSeconds { get; }
    IEnumerator Warmup(SceneWarmupContext context);
}

public sealed class SceneWarmupContext
{
    private readonly Action<float, string> reportProgress;

    public SceneTransitionContext Transition { get; }
    public SceneLoadingConfigSO Config { get; }
    public bool Failed { get; private set; }
    public string FailureMessage { get; private set; }
    public IReadOnlyList<string> Warnings => warnings;
    public bool HasWarnings => warnings.Count > 0;

    private readonly List<string> warnings = new List<string>();

    public SceneWarmupContext(
        SceneTransitionContext transition,
        SceneLoadingConfigSO config,
        Action<float, string> reportProgress)
    {
        Transition = transition;
        Config = config;
        this.reportProgress = reportProgress;
    }

    public void ReportProgress(float progress, string message = "")
    {
        reportProgress?.Invoke(Mathf.Clamp01(progress), message ?? string.Empty);
    }

    public void Fail(string message)
    {
        Failed = true;
        FailureMessage = string.IsNullOrWhiteSpace(message) ? "Scene warmup failed." : message;
    }

    public void Warn(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            warnings.Add(message.Trim());
    }
}
