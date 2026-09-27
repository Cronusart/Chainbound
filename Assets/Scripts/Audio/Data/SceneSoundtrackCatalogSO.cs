using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central catalog for resolving soundtrack cues by destination scene.
/// Owned by the persistent music service, not by individual transition callers.
/// </summary>
[CreateAssetMenu(fileName = "SceneSoundtrackCatalog", menuName = "Chainbound/Audio/Music/Scene Soundtrack Catalog")]
public class SceneSoundtrackCatalogSO : ScriptableObject
{
    [Serializable]
    public class SceneSoundtrackEntry
    {
        [Tooltip("Scene name (must match exactly).")]
        public string sceneName;

        [Tooltip("Soundtrack cue to play for this scene. Leave empty to explicitly stop music.")]
        public SoundtrackCueSO cue;

        [Tooltip("When this scene's soundtrack should be triggered.")]
        public SoundtrackTriggerMoment triggerMoment = SoundtrackTriggerMoment.TransitionStart;

        [Tooltip("Fade time for this scene request.")]
        [Range(0f, 5f)]
        public float fadeSeconds = 0.75f;

        [Tooltip("If true, scene entry always restarts its intro even when that cue is already active.")]
        public bool forceRestart = false;
    }

    [Header("Scene Soundtracks")]
    [Tooltip("Per-scene soundtrack policy entries.")]
    [SerializeField] private List<SceneSoundtrackEntry> entries = new List<SceneSoundtrackEntry>();

    [Header("Fallback")]
    [Tooltip("Fade time used when an unmapped scene intentionally stops music.")]
    [Range(0f, 5f)]
    [SerializeField] private float unmappedStopFadeSeconds = 0.5f;

    private Dictionary<string, SceneSoundtrackEntry> lookup;
    private bool initialized;

    public float UnmappedStopFadeSeconds => unmappedStopFadeSeconds;

    public bool TryGetEntry(string sceneName, out SceneSoundtrackEntry entry)
    {
        Initialize();

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            entry = null;
            return false;
        }

        return lookup.TryGetValue(sceneName, out entry);
    }

    private void Initialize()
    {
        if (initialized)
            return;

        lookup = new Dictionary<string, SceneSoundtrackEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (SceneSoundtrackEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.sceneName))
                continue;

            if (!lookup.ContainsKey(entry.sceneName))
                lookup.Add(entry.sceneName, entry);
        }

        initialized = true;
    }

    private void OnValidate()
    {
        initialized = false;
        unmappedStopFadeSeconds = Mathf.Max(0f, unmappedStopFadeSeconds);

        if (entries == null)
            return;

        foreach (SceneSoundtrackEntry entry in entries)
        {
            if (entry == null)
                continue;

            entry.fadeSeconds = Mathf.Max(0f, entry.fadeSeconds);
        }
    }
}

public enum SoundtrackTriggerMoment
{
    TransitionStart,
    BeforeReveal,
    ImmediateOnSceneLoadedFallback
}
