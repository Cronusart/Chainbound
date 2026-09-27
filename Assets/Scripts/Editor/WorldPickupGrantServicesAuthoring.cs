#if UNITY_EDITOR
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Idempotent GameServices wiring for world-pickup bag grant + obtain overlay.
/// Menu: <b>Tools / Chainbound / World / Wire World Pickup Grant in GameServices Prefab</b>
/// </summary>
public static class WorldPickupGrantServicesAuthoring
{
    private const string PrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string ObtainPrefabPath = "Assets/Prefabs/UI/ItemObtainPanel.prefab";
    private const string GrantChildName = "WorldPickupGrantServices";
    private const string ObtainChildName = "ItemObtainPresenter";

    [MenuItem("Tools/Chainbound/World/Wire World Pickup Grant in GameServices Prefab")]
    public static void Wire()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[WorldPickupGrantAuthoring] Could not load {PrefabPath}");
            return;
        }

        var report = new List<string>();
        try
        {
            Transform host = ResolveGrantHost(root.transform, report);
            CoreBagWorldPickupGrantService grant = EnsureComponent<CoreBagWorldPickupGrantService>(host, report);
            WireGrantConfig(grant, report);
            EnsureObtainPresenter(root.transform, report);

            EditorUtility.SetDirty(host.gameObject);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            report.Add($"Saved {PrefabPath}");
            Debug.Log("[WorldPickupGrantAuthoring]\n - " + string.Join("\n - ", report));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform ResolveGrantHost(Transform rootT, List<string> report)
    {
        CoreBagWorldPickupGrantService existing = rootT.GetComponentInChildren<CoreBagWorldPickupGrantService>(true);
        if (existing != null)
        {
            report.Add($"Host = '{existing.gameObject.name}' (existing grant service)");
            return existing.transform;
        }

        ZoneEncounterProvider zoneProvider = rootT.GetComponentInChildren<ZoneEncounterProvider>(true);
        if (zoneProvider != null)
        {
            report.Add($"Host = '{zoneProvider.gameObject.name}' (existing ZoneEncounterProvider)");
            return zoneProvider.transform;
        }

        Transform named = rootT.Find(GrantChildName);
        if (named != null)
        {
            report.Add($"Host = '{GrantChildName}' (existing child)");
            return named;
        }

        var created = new GameObject(GrantChildName);
        created.transform.SetParent(rootT, false);
        report.Add($"Host = '{GrantChildName}' (created)");
        return created.transform;
    }

    private static void EnsureObtainPresenter(Transform rootT, List<string> report)
    {
        ItemObtainPresenter existing = rootT.GetComponentInChildren<ItemObtainPresenter>(true);
        if (existing != null)
        {
            report.Add($"ItemObtainPresenter: already present on '{existing.gameObject.name}'");
            return;
        }

        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObtainPrefabPath);
        if (panelPrefab != null)
        {
            GameObject host = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, rootT);
            if (host != null)
            {
                host.name = ObtainChildName;
                report.Add($"ItemObtainPresenter: instantiated {ObtainPrefabPath}");
                return;
            }
        }

        var created = new GameObject(ObtainChildName);
        created.transform.SetParent(rootT, false);
        created.AddComponent<ItemObtainPresenter>();
        report.Add("ItemObtainPresenter: ADDED empty host (prefab missing; runtime UI will build)");
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

    private static void WireGrantConfig(CoreBagWorldPickupGrantService grant, List<string> report)
    {
        if (grant == null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:SupabaseConfigSO");
        SupabaseConfigSO config = null;
        if (guids != null && guids.Length > 0)
            config = AssetDatabase.LoadAssetAtPath<SupabaseConfigSO>(AssetDatabase.GUIDToAssetPath(guids[0]));

        var so = new SerializedObject(grant);
        SerializedProperty prop = so.FindProperty("supabaseConfig");
        if (prop == null)
        {
            report.Add("CoreBagWorldPickupGrantService.supabaseConfig: field not found");
            return;
        }

        if (prop.objectReferenceValue != null)
        {
            report.Add($"CoreBagWorldPickupGrantService.supabaseConfig: already assigned ({prop.objectReferenceValue.name})");
            return;
        }

        if (config == null)
        {
            report.Add("CoreBagWorldPickupGrantService.supabaseConfig: no SupabaseConfigSO found");
            return;
        }

        prop.objectReferenceValue = config;
        so.ApplyModifiedPropertiesWithoutUndo();
        report.Add($"CoreBagWorldPickupGrantService.supabaseConfig -> {config.name}");
    }
}
#endif
