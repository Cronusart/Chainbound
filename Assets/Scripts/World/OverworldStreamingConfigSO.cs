using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative data for an additive overworld set. Scene-loader rules refer
/// to this asset rather than duplicating chunk membership in transition code.
/// </summary>
[CreateAssetMenu(fileName = "OverworldStreamingSet", menuName = "Signal Jam/World/Overworld Streaming Set")]
public sealed class OverworldStreamingConfigSO : ScriptableObject
{
    [Serializable]
    public sealed class Chunk
    {
        [SerializeField] private string sceneId;
        [SerializeField] private bool preloadOnSetEntry;
        [SerializeField] private string previousChunkId;
        [SerializeField] private string nextChunkId;
        [SerializeField, Min(0f)] private float prefetchDistance = 20f;
        [SerializeField] private string prefetchTriggerId;
        [SerializeField] private string releasePreviousTriggerId;

        public string SceneId => sceneId?.Trim() ?? string.Empty;
        public bool PreloadOnSetEntry => preloadOnSetEntry;
        public string PreviousChunkId => previousChunkId?.Trim() ?? string.Empty;
        public string NextChunkId => nextChunkId?.Trim() ?? string.Empty;
        public float PrefetchDistance => Mathf.Max(0f, prefetchDistance);
        public string PrefetchTriggerId => prefetchTriggerId?.Trim() ?? string.Empty;
        public string ReleasePreviousTriggerId => releasePreviousTriggerId?.Trim() ?? string.Empty;

        public Chunk(string sceneId, bool preloadOnSetEntry)
        {
            this.sceneId = sceneId;
            this.preloadOnSetEntry = preloadOnSetEntry;
            previousChunkId = string.Empty;
            nextChunkId = string.Empty;
            prefetchDistance = 20f;
            prefetchTriggerId = string.Empty;
            releasePreviousTriggerId = string.Empty;
        }

        internal void Normalize()
        {
            sceneId = SceneId;
            previousChunkId = PreviousChunkId;
            nextChunkId = NextChunkId;
            prefetchTriggerId = PrefetchTriggerId;
            releasePreviousTriggerId = ReleasePreviousTriggerId;
            prefetchDistance = Mathf.Max(0f, prefetchDistance);
        }
    }

    [Header("Set")]
    [Tooltip("Authoring label for this overworld set. Use one asset per hub, e.g. Loomville.")]
    [SerializeField] private string setId = "Loomville";
    [SerializeField] private string hubSceneId = "Loomville";
    [SerializeField] private List<Chunk> orderedChunks = new List<Chunk>
    {
        new Chunk("Loomville", true),
        new Chunk("Link1", true),
    };

    [Header("Silent IO")]
    [SerializeField] private ThreadPriority silentLoadingPriority = ThreadPriority.BelowNormal;
    [SerializeField, Min(0.1f)] private float silentWarmupTimeoutSeconds = 10f;
    [SerializeField] private string cameraRigRootName = "CameraRigRoot";

    public string SetId => string.IsNullOrWhiteSpace(setId) ? HubSceneId : setId.Trim();
    public string HubSceneId => hubSceneId?.Trim() ?? string.Empty;
    public IReadOnlyList<Chunk> OrderedChunks => orderedChunks;
    public ThreadPriority SilentLoadingPriority => silentLoadingPriority;
    public float SilentWarmupTimeoutSeconds => Mathf.Max(0.1f, silentWarmupTimeoutSeconds);
    public string CameraRigRootName => string.IsNullOrWhiteSpace(cameraRigRootName)
        ? "CameraRigRoot"
        : cameraRigRootName.Trim();

    public bool ContainsScene(string sceneId)
    {
        string normalized = sceneId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (string.Equals(HubSceneId, normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        for (int i = 0; i < orderedChunks.Count; i++)
        {
            if (string.Equals(orderedChunks[i]?.SceneId, normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public List<string> GetAllSceneIds()
    {
        List<string> sceneIds = new List<string>();
        AddUnique(sceneIds, HubSceneId);
        for (int i = 0; i < orderedChunks.Count; i++)
            AddUnique(sceneIds, orderedChunks[i]?.SceneId);
        return sceneIds;
    }

    public List<string> GetEntryResidentSceneIds()
    {
        List<string> sceneIds = new List<string>();
        AddUnique(sceneIds, HubSceneId);
        for (int i = 0; i < orderedChunks.Count; i++)
        {
            Chunk chunk = orderedChunks[i];
            if (chunk != null && chunk.PreloadOnSetEntry)
                AddUnique(sceneIds, chunk.SceneId);
        }
        return sceneIds;
    }

    /// <summary>
    /// Sanctuary portal continue/wipe residency. Loads the authored chunks only;
    /// other members stream in through existing volumes. Falls back to
    /// <see cref="GetEntryResidentSceneIds"/> when no authored id is in this set.
    /// </summary>
    public List<string> GetResidentSceneIdsForPreferredChunks(IEnumerable<string> preferredChunkSceneIds)
    {
        List<string> sceneIds = new List<string>();
        if (preferredChunkSceneIds != null)
        {
            foreach (string preferredChunkSceneId in preferredChunkSceneIds)
            {
                string preferred = preferredChunkSceneId != null ? preferredChunkSceneId.Trim() : string.Empty;
                if (string.IsNullOrEmpty(preferred) || !ContainsScene(preferred))
                    continue;
                AddUnique(sceneIds, preferred);
            }
        }

        if (sceneIds.Count == 0)
            return GetEntryResidentSceneIds();

        return sceneIds;
    }

    public List<string> Validate()
    {
        List<string> errors = new List<string>();
        if (string.IsNullOrWhiteSpace(HubSceneId))
            errors.Add("The overworld hub scene id is empty.");

        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < orderedChunks.Count; i++)
        {
            Chunk chunk = orderedChunks[i];
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.SceneId))
            {
                errors.Add($"Chunk {i} has an empty scene id.");
                continue;
            }

            if (!ids.Add(chunk.SceneId))
                errors.Add($"Chunk scene '{chunk.SceneId}' is duplicated.");
        }

        if (!string.IsNullOrWhiteSpace(HubSceneId) && !ids.Contains(HubSceneId))
            errors.Add($"Hub scene '{HubSceneId}' must also appear in ordered chunks.");

        for (int i = 0; i < orderedChunks.Count; i++)
        {
            Chunk chunk = orderedChunks[i];
            if (chunk == null)
                continue;

            ValidateNeighbor(errors, ids, chunk.SceneId, chunk.PreviousChunkId, "previousChunkId");
            ValidateNeighbor(errors, ids, chunk.SceneId, chunk.NextChunkId, "nextChunkId");
        }

        return errors;
    }

    public static OverworldStreamingConfigSO CreateRuntimeDefault()
    {
        OverworldStreamingConfigSO config = CreateInstance<OverworldStreamingConfigSO>();
        config.name = "OverworldStreamingSet_RuntimeDefault";
        config.setId = "Loomville";
        config.hideFlags = HideFlags.HideAndDontSave;
        config.Normalize();
        return config;
    }

    private void OnValidate()
    {
        Normalize();
    }

    private void Normalize()
    {
        setId = SetId;
        hubSceneId = HubSceneId;
        cameraRigRootName = CameraRigRootName;
        silentWarmupTimeoutSeconds = Mathf.Max(0.1f, silentWarmupTimeoutSeconds);
        orderedChunks ??= new List<Chunk>();
        for (int i = 0; i < orderedChunks.Count; i++)
            orderedChunks[i]?.Normalize();
    }

    private static void ValidateNeighbor(
        ICollection<string> errors,
        HashSet<string> ids,
        string sceneId,
        string neighborId,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(neighborId))
            return;

        if (!ids.Contains(neighborId))
            errors.Add($"Chunk '{sceneId}' {fieldName} '{neighborId}' is not in this set.");
    }

    private static void AddUnique(ICollection<string> destination, string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
            return;

        foreach (string existing in destination)
        {
            if (string.Equals(existing, sceneId, StringComparison.OrdinalIgnoreCase))
                return;
        }

        destination.Add(sceneId.Trim());
    }
}
