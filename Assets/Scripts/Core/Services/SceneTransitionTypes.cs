using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneTransitionPhase
{
    Idle,
    Covering,
    Loading,
    Activating,
    Warming,
    Finalizing,
    Revealing,
    Completed,
    Failed,
}

public enum SceneTransitionOutcome
{
    Succeeded,
    SucceededDegraded,
    Busy,
    Invalid,
    Failed,
}

/// <summary>
/// Immutable runtime request for the scene-transition service. Scene ids are
/// Build Settings scene names; scene-owned object references never cross this boundary.
/// </summary>
public sealed class SceneTransitionRequest
{
    public string SceneId { get; }
    public string SpawnId { get; }
    public TransitionStyle? StyleOverride { get; }
    public string SourceId { get; }
    public Action<SceneTransitionContext> BeforeReveal { get; }
    public Action<SceneTransitionResult> Completed { get; }
    public string PreferredOverworldChunkSceneId { get; }
    public string[] PreferredOverworldChunkSceneIds { get; }

    public SceneTransitionRequest(
        string sceneId,
        string spawnId = "",
        TransitionStyle? styleOverride = null,
        string sourceId = "",
        Action<SceneTransitionContext> beforeReveal = null,
        Action<SceneTransitionResult> completed = null,
        string preferredOverworldChunkSceneId = "",
        string[] preferredOverworldChunkSceneIds = null)
    {
        SceneId = sceneId?.Trim() ?? string.Empty;
        SpawnId = spawnId?.Trim() ?? string.Empty;
        StyleOverride = styleOverride;
        SourceId = sourceId?.Trim() ?? string.Empty;
        BeforeReveal = beforeReveal;
        Completed = completed;
        PreferredOverworldChunkSceneIds = NormalizePreferredChunkIds(
            preferredOverworldChunkSceneIds,
            preferredOverworldChunkSceneId);
        PreferredOverworldChunkSceneId = PreferredOverworldChunkSceneIds.Length > 0
            ? PreferredOverworldChunkSceneIds[0]
            : string.Empty;
    }

    private static string[] NormalizePreferredChunkIds(string[] authored, string legacySingleId)
    {
        return SanctuaryLatchZone.NormalizeResidentChunkSceneIds(authored, legacySingleId, "");
    }
}

public readonly struct SceneTransitionContext
{
    public string SceneId { get; }
    public string SpawnId { get; }
    public TransitionStyle Style { get; }
    public Scene Scene { get; }

    public SceneTransitionContext(string sceneId, string spawnId, TransitionStyle style, Scene scene)
    {
        SceneId = sceneId;
        SpawnId = spawnId;
        Style = style;
        Scene = scene;
    }
}

public readonly struct SceneTransitionStatus
{
    public SceneTransitionPhase Phase { get; }
    public float Progress { get; }
    public string SceneId { get; }
    public string Message { get; }
    public bool IsDegraded { get; }

    public SceneTransitionStatus(
        SceneTransitionPhase phase,
        float progress,
        string sceneId,
        string message,
        bool isDegraded)
    {
        Phase = phase;
        Progress = Mathf.Clamp01(progress);
        SceneId = sceneId ?? string.Empty;
        Message = message ?? string.Empty;
        IsDegraded = isDegraded;
    }
}

public readonly struct SceneTransitionResult
{
    public SceneTransitionOutcome Outcome { get; }
    public string SceneId { get; }
    public string SpawnId { get; }
    public string Message { get; }
    public bool SceneActivated { get; }

    public bool Succeeded => Outcome == SceneTransitionOutcome.Succeeded ||
                             Outcome == SceneTransitionOutcome.SucceededDegraded;

    public SceneTransitionResult(
        SceneTransitionOutcome outcome,
        string sceneId,
        string message = "",
        bool sceneActivated = false,
        string spawnId = "")
    {
        Outcome = outcome;
        SceneId = sceneId ?? string.Empty;
        SpawnId = spawnId ?? string.Empty;
        Message = message ?? string.Empty;
        SceneActivated = sceneActivated;
    }
}
