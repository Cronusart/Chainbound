using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step that enables or disables one or more GameObjects by name.
///
/// Primary target: the single GameObject defined by gameObjectName/enable (backward compatible
/// with all existing step assets).
///
/// Additional targets: use the additionalTargets list to batch related objects into one step,
/// each with its own enable/disable flag. Objects are processed in order after the primary target.
///
/// includeInactive applies to all targets, active and additional.
///
/// Optional persistence records desired active state in <see cref="NarrativeSceneObjectStateService"/>
/// so targets can be reapplied after scene reload (see <see cref="NarrativeSceneObjectBinding"/>).
/// </summary>
[CreateAssetMenu(fileName = "Step_EnableGameObject", menuName = "Chainbound/Narrative/Steps/Enable GameObject")]
public class Step_EnableGameObject : SequenceStep
{
    [Serializable]
    public class GameObjectEntry
    {
        [Tooltip("Name of the GameObject to find in the scene.")]
        public string gameObjectName = "";

        [Tooltip("Enable (true) or disable (false) this GameObject.")]
        public bool enable = true;

        [Tooltip("Optional persistence key; when empty, gameObjectName is used as the key.")]
        public string stateIdOverride = "";
    }

    [Header("Primary Target")]
    [Tooltip("Name of the primary GameObject to enable/disable.")]
    [SerializeField] private string gameObjectName = "";

    [Tooltip("Enable or disable the primary GameObject.")]
    [SerializeField] private bool enable = true;

    [Header("Additional Targets")]
    [Tooltip("Optional list of extra GameObjects to enable/disable in the same step. " +
             "Each entry has its own enable/disable flag. Processed after the primary target.")]
    [SerializeField] private List<GameObjectEntry> additionalTargets = new List<GameObjectEntry>();

    [Header("Search")]
    [Tooltip("Search inactive (disabled) GameObjects. Use when the target starts disabled.")]
    [SerializeField] private bool includeInactive = true;

    [Header("Persistence")]
    [Tooltip("Record desired active state for scene reload. Requires NarrativeSceneObjectStateService under GameServices.")]
    [SerializeField] private bool persistAcrossSceneLoads;

    [Tooltip("When true, missing objects still record persistence (for bindings or objects not loaded yet).")]
    [SerializeField] private bool allowMissingWhenPersisting;

    [Tooltip("Optional key for persistence; when empty, primary gameObjectName is used.")]
    [SerializeField] private string stateIdOverride = "";

    [Tooltip("Also write a per-object profile world flag so continue-game keeps this Enable/Disable. Unset flags leave scene defaults.")]
    [SerializeField] private bool persistAsProfileWorldFlag;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        bool hasPrimary = !string.IsNullOrEmpty(gameObjectName);
        bool hasAdditional = additionalTargets != null && additionalTargets.Count > 0;

        if (!hasPrimary && !hasAdditional)
        {
            Debug.LogError("[Step_EnableGameObject] No GameObjects configured!");
            onComplete?.Invoke(StepOutcome.Failed("No GameObjects configured"));
            yield break;
        }

        NarrativeSceneObjectStateService stateService = null;
        if (persistAcrossSceneLoads)
        {
            stateService = NarrativeSceneObjectStateService.Instance
                           ?? FindObjectOfType<NarrativeSceneObjectStateService>(true);
            if (stateService == null)
                Debug.LogWarning("[Step_EnableGameObject] NarrativeSceneObjectStateService not found — persistence will be skipped.");
        }

        GameObject[] allObjects = null;

        if (hasPrimary)
        {
            string persistKey = ResolvePersistKey(gameObjectName, stateIdOverride);
            GameObject obj = FindObject(gameObjectName, ref allObjects);

            if (obj == null)
            {
                if (persistAcrossSceneLoads && stateService != null && allowMissingWhenPersisting && !string.IsNullOrEmpty(persistKey))
                {
                    stateService.RecordDesiredState(persistKey, enable);
                    RecordProfileWorldFlag(gameObjectName, stateIdOverride, enable);
                    if (enableDebugLogs)
                        Debug.Log($"[Step_EnableGameObject] Persisted (missing) primary '{persistKey}' -> active={enable}");
                }
                else
                {
                    Debug.LogWarning($"[Step_EnableGameObject] Primary target '{gameObjectName}' not found!");
                    onComplete?.Invoke(StepOutcome.Failed($"GameObject '{gameObjectName}' not found"));
                    yield break;
                }
            }
            else
            {
                obj.SetActive(enable);
                if (persistAcrossSceneLoads && stateService != null && !string.IsNullOrEmpty(persistKey))
                    stateService.RecordDesiredState(persistKey, enable);
                RecordProfileWorldFlag(gameObjectName, stateIdOverride, enable);

                if (enableDebugLogs)
                    Debug.Log($"[Step_EnableGameObject] {(enable ? "Enabled" : "Disabled")} '{gameObjectName}'");
            }
        }

        if (hasAdditional)
        {
            foreach (var entry in additionalTargets)
            {
                if (string.IsNullOrEmpty(entry.gameObjectName))
                {
                    Debug.LogWarning("[Step_EnableGameObject] Additional target has empty name — skipping.");
                    continue;
                }

                string persistKey = ResolvePersistKey(entry.gameObjectName, entry.stateIdOverride);
                GameObject obj = FindObject(entry.gameObjectName, ref allObjects);

                if (obj == null)
                {
                    if (persistAcrossSceneLoads && stateService != null && allowMissingWhenPersisting && !string.IsNullOrEmpty(persistKey))
                    {
                        stateService.RecordDesiredState(persistKey, entry.enable);
                        RecordProfileWorldFlag(entry.gameObjectName, entry.stateIdOverride, entry.enable);
                        if (enableDebugLogs)
                            Debug.Log($"[Step_EnableGameObject] Persisted (missing) additional '{persistKey}' -> active={entry.enable}");
                    }
                    else
                        Debug.LogWarning($"[Step_EnableGameObject] Additional target '{entry.gameObjectName}' not found — skipping.");
                    continue;
                }

                obj.SetActive(entry.enable);
                if (persistAcrossSceneLoads && stateService != null && !string.IsNullOrEmpty(persistKey))
                    stateService.RecordDesiredState(persistKey, entry.enable);
                RecordProfileWorldFlag(entry.gameObjectName, entry.stateIdOverride, entry.enable);

                if (enableDebugLogs)
                    Debug.Log($"[Step_EnableGameObject] {(entry.enable ? "Enabled" : "Disabled")} '{entry.gameObjectName}'");
            }
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    private static string ResolvePersistKey(string gameObjectName, string overrideKey)
    {
        if (!string.IsNullOrEmpty(overrideKey))
            return overrideKey.Trim();
        return gameObjectName ?? "";
    }

    private void RecordProfileWorldFlag(string objectName, string overrideKey, bool enabled)
    {
        if (!persistAsProfileWorldFlag || string.IsNullOrWhiteSpace(objectName))
            return;

        ProfileWorldFlagStateService flags = ProfileWorldFlagStateService.Instance
            ?? FindFirstObjectByType<ProfileWorldFlagStateService>(FindObjectsInactive.Include);
        if (flags == null)
            return;

        string flagKey = ResolveProfileFlagKey(objectName, overrideKey);
        flags.SetFlag(flagKey, enabled);
        if (enableDebugLogs)
            Debug.Log($"[Step_EnableGameObject] Profile flag {flagKey} = {enabled}");
    }

    public static string ResolveProfileFlagKey(string objectName, string overrideKey)
    {
        if (!string.IsNullOrWhiteSpace(overrideKey))
            return overrideKey.Trim();

        string name = objectName != null ? objectName.Trim() : "";
        return string.IsNullOrEmpty(name) ? "" : $"intro.guardrail.{name}";
    }

    /// <summary>
    /// Finds a GameObject by name. Caches the full inactive search results to avoid
    /// repeated scene scans when processing multiple entries in the same step.
    /// </summary>
    private GameObject FindObject(string name, ref GameObject[] cachedAll)
    {
        GameObject obj = GameObject.Find(name);

        if (obj == null && includeInactive)
        {
            if (cachedAll == null)
                cachedAll = FindObjectsOfType<GameObject>(true);

            foreach (var go in cachedAll)
            {
                if (go.name == name)
                    return go;
            }
        }

        return obj;
    }

    public override bool Validate()
    {
        bool hasPrimary = !string.IsNullOrEmpty(gameObjectName);
        bool hasAdditional = additionalTargets != null && additionalTargets.Count > 0;

        if (!hasPrimary && !hasAdditional)
        {
            Debug.LogWarning("[Step_EnableGameObject] No GameObjects configured!");
            return false;
        }

        return true;
    }
}
