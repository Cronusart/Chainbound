#if UNITY_EDITOR
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Idempotent prefab authoring for the encounter modifier / server-event stack under
/// <c>GameServices.prefab</c>. Adds <see cref="EncounterModifierService"/>,
/// <see cref="SupabaseEncounterEventsProvider"/>, and <see cref="FavorService"/> beside the
/// existing <see cref="SolanaServiceBridge"/> / <see cref="ZoneEncounterProvider"/>.
///
/// Safe to re-run: existing components are reused and only unset fields are filled.
///
/// Menu: <b>Tools / Chainbound / Combat / Wire Encounter Services in GameServices Prefab</b>
/// </summary>
public static class EncounterServicesGameServicesAuthoring
{
    private const string PrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string SolanaServicesChildName = "SolanaServices";
    private const string FavorConfigAssetPath = "Assets/Data/Combat/FavorConfig.asset";

    [MenuItem("Tools/Chainbound/Combat/Wire Encounter Services in GameServices Prefab")]
    public static void Wire()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[EncounterServicesAuthoring] Could not load {PrefabPath}");
            return;
        }

        var report = new List<string>();
        try
        {
            Transform host = ResolveHost(root.transform, report);

            EncounterModifierService modifierService = EnsureComponent<EncounterModifierService>(host, report);
            SupabaseEncounterEventsProvider eventsProvider = EnsureComponent<SupabaseEncounterEventsProvider>(host, report);
            FavorService favorService = EnsureComponent<FavorService>(host, report);

            WireEventsProvider(eventsProvider, report);
            WireModifierService(modifierService, eventsProvider, report);
            WireFavorService(favorService, report);

            EditorUtility.SetDirty(host.gameObject);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            report.Add($"Saved {PrefabPath}");
            Debug.Log("[EncounterServicesAuthoring]\n - " + string.Join("\n - ", report));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Prefer the child that already owns the zone encounter stack so encounter services stay co-located.
    /// </summary>
    private static Transform ResolveHost(Transform rootT, List<string> report)
    {
        ZoneEncounterProvider zoneProvider = rootT.GetComponentInChildren<ZoneEncounterProvider>(true);
        if (zoneProvider != null)
        {
            report.Add($"Host = '{zoneProvider.gameObject.name}' (existing ZoneEncounterProvider)");
            return zoneProvider.transform;
        }

        SolanaServiceBridge bridge = rootT.GetComponentInChildren<SolanaServiceBridge>(true);
        if (bridge != null)
        {
            report.Add($"Host = '{bridge.gameObject.name}' (existing SolanaServiceBridge)");
            return bridge.transform;
        }

        Transform existing = rootT.Find(SolanaServicesChildName);
        if (existing != null)
        {
            report.Add($"Host = '{SolanaServicesChildName}' (existing child)");
            return existing;
        }

        var created = new GameObject(SolanaServicesChildName);
        created.transform.SetParent(rootT, false);
        report.Add($"Host = '{SolanaServicesChildName}' (created)");
        return created.transform;
    }

    private static T EnsureComponent<T>(Transform host, List<string> report) where T : Component
    {
        T existing = host.root.GetComponentInChildren<T>(true);
        if (existing != null)
        {
            report.Add($"{typeof(T).Name}: already present on '{existing.gameObject.name}'");
            return existing;
        }

        T added = host.gameObject.AddComponent<T>();
        report.Add($"{typeof(T).Name}: ADDED to '{host.gameObject.name}'");
        return added;
    }

    private static void WireEventsProvider(SupabaseEncounterEventsProvider provider, List<string> report)
    {
        if (provider == null)
            return;

        var so = new SerializedObject(provider);
        SupabaseConfigSO config = FindSupabaseConfig();

        if (!TrySetObjectIfEmpty(so, "supabaseConfig", config, out string configNote))
            report.Add($"SupabaseEncounterEventsProvider.supabaseConfig: {configNote}");
        else
            report.Add($"SupabaseEncounterEventsProvider.supabaseConfig -> {config.name}");

        SetBool(so, "autoRegisterWithModifierService", true);
        SetBool(so, "fetchOnStart", true);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireModifierService(
        EncounterModifierService service,
        SupabaseEncounterEventsProvider provider,
        List<string> report)
    {
        if (service == null)
            return;

        var so = new SerializedObject(service);

        // Explicitly list the server provider so the aggregation is inspectable rather than
        // relying only on runtime singleton discovery.
        SerializedProperty sources = so.FindProperty("additionalSources");
        if (sources != null && provider != null)
        {
            bool alreadyListed = false;
            for (int i = 0; i < sources.arraySize; i++)
            {
                if (sources.GetArrayElementAtIndex(i).objectReferenceValue == provider)
                {
                    alreadyListed = true;
                    break;
                }
            }

            if (!alreadyListed)
            {
                sources.InsertArrayElementAtIndex(sources.arraySize);
                sources.GetArrayElementAtIndex(sources.arraySize - 1).objectReferenceValue = provider;
                report.Add("EncounterModifierService.additionalSources += SupabaseEncounterEventsProvider");
            }
            else
            {
                report.Add("EncounterModifierService.additionalSources: provider already listed");
            }
        }

        // localModifiers is intentionally left as authored: example/event assets must be opted into
        // by a designer so running this command never changes live encounter odds.
        SerializedProperty local = so.FindProperty("localModifiers");
        if (local != null)
            report.Add($"EncounterModifierService.localModifiers: left as-is ({local.arraySize} entry/entries)");

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireFavorService(FavorService favorService, List<string> report)
    {
        if (favorService == null)
            return;

        FavorConfigSO config = AssetDatabase.LoadAssetAtPath<FavorConfigSO>(FavorConfigAssetPath);
        if (config == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:FavorConfigSO");
            if (guids != null && guids.Length > 0)
                config = AssetDatabase.LoadAssetAtPath<FavorConfigSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<FavorConfigSO>();
            AssetDatabase.CreateAsset(config, FavorConfigAssetPath);
            report.Add($"FavorConfig: CREATED {FavorConfigAssetPath} (tunable favor curves)");
        }

        var so = new SerializedObject(favorService);
        if (!TrySetObjectIfEmpty(so, "favorConfig", config, out string note))
            report.Add($"FavorService.favorConfig: {note}");
        else
            report.Add($"FavorService.favorConfig -> {config.name}");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SupabaseConfigSO FindSupabaseConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:SupabaseConfigSO");
        if (guids == null || guids.Length == 0)
            return null;

        return AssetDatabase.LoadAssetAtPath<SupabaseConfigSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static bool TrySetObjectIfEmpty(
        SerializedObject so,
        string propertyName,
        UnityEngine.Object value,
        out string note)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            note = $"field '{propertyName}' not found";
            return false;
        }

        if (prop.objectReferenceValue != null)
        {
            note = $"already assigned ({prop.objectReferenceValue.name}); left untouched";
            return false;
        }

        if (value == null)
        {
            note = "no candidate asset found in project; assign manually";
            return false;
        }

        prop.objectReferenceValue = value;
        note = "assigned";
        return true;
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.boolValue = value;
    }
}
#endif
