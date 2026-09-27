#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent authoring for Mart leftover crates: unique world pickups
/// (patch + orb) gated by leftover permission, with no spent visual or crate SFX.
/// Menu: <b>Tools / Chainbound / World / Author Loomville Mart Leftover Crates</b>
/// </summary>
public static class MartLeftoverCrateAuthoring
{
    public const string PatchSpawnId = "world.pickup.loomville_mart.open_crate_patch";
    public const string OrbSpawnId = "world.pickup.loomville_mart.open_crate_orb";
    public const string PatchItemId = "chainling_patch";
    public const string OrbItemId = "signal_orb";
    public const string PatchCrateName = "Open_Crate";
    public const string OrbCrateName = "Open_Crate (1)";
    public const string PickupChildName = "WorldPickupInteractable";
    public const string LeftoverGateName = "LeftoverPickupGate";

    private const string ScenePath = "Assets/Loomville_Mart.unity";
    private const string PrefabPath = "Assets/Prefabs/Interactables/WorldPickupInteractable.prefab";

    [MenuItem("Tools/Chainbound/World/Author Loomville Mart Leftover Crates")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void Apply()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Missing world pickup prefab at '{PrefabPath}'.");

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject patchCrate = FindSceneObject(scene, PatchCrateName);
        GameObject orbCrate = FindSceneObject(scene, OrbCrateName);
        if (patchCrate == null || orbCrate == null)
            throw new InvalidOperationException("Open_Crate and Open_Crate (1) must exist in Loomville_Mart.");

        AuthorCrate(patchCrate, prefab, PatchSpawnId, PatchItemId);
        AuthorCrate(orbCrate, prefab, OrbSpawnId, OrbItemId);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save '{ScenePath}'.");

        Debug.Log(
            "[MartLeftoverCrateAuthoring] Authored leftover crates: " +
            $"{PatchSpawnId} → {PatchItemId}, {OrbSpawnId} → {OrbItemId}.");
    }

    public static WorldPickupGrant FindGrant(GameObject crate)
    {
        if (crate == null)
            return null;
        return crate.GetComponentInChildren<WorldPickupGrant>(true);
    }

    public static WorldPresenceBinding FindLeftoverGate(GameObject crate)
    {
        if (crate == null)
            return null;

        Transform gate = crate.transform.Find(LeftoverGateName);
        return gate != null ? gate.GetComponent<WorldPresenceBinding>() : null;
    }

    private static void AuthorCrate(
        GameObject crate,
        GameObject pickupPrefab,
        string spawnId,
        string itemId)
    {
        GameObject pickup = EnsurePickupInstance(crate, pickupPrefab);
        pickup.SetActive(false);

        WorldPickupGrant grant = pickup.GetComponent<WorldPickupGrant>();
        WorldPresenceBinding claimBinding = pickup.GetComponent<WorldPresenceBinding>();
        if (grant == null || claimBinding == null)
            throw new InvalidOperationException($"'{pickup.name}' is missing WorldPickupGrant or WorldPresenceBinding.");

        Transform interactVolume = pickup.transform.Find("Interactable");
        var claimSerialized = new SerializedObject(claimBinding);
        claimSerialized.FindProperty("presenceId").stringValue = spawnId;
        if (interactVolume != null)
            claimSerialized.FindProperty("target").objectReferenceValue = interactVolume.gameObject;
        claimSerialized.FindProperty("activeWhenTrue").boolValue = false;
        claimSerialized.FindProperty("lifetimeOverride").enumValueIndex = (int)WorldPresenceLifetime.Unspecified;
        claimSerialized.ApplyModifiedPropertiesWithoutUndo();

        var grantSerialized = new SerializedObject(grant);
        grantSerialized.FindProperty("presenceId").stringValue = spawnId;
        grantSerialized.FindProperty("presenceBinding").objectReferenceValue = claimBinding;
        grantSerialized.FindProperty("itemId").stringValue = itemId;
        grantSerialized.FindProperty("quantity").intValue = 1;
        grantSerialized.FindProperty("requireInventoryGrant").boolValue = true;
        grantSerialized.FindProperty("spentPresentation").enumValueIndex =
            (int)WorldPickupSpentPresentation.HideInteractableOnly;
        grantSerialized.FindProperty("door").objectReferenceValue = null;
        grantSerialized.FindProperty("spentVisual").objectReferenceValue = null;
        grantSerialized.FindProperty("obtainPanelDelaySeconds").floatValue = 0f;
        grantSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject leftoverGate = EnsureChild(crate.transform, LeftoverGateName);
        leftoverGate.SetActive(true);
        WorldPresenceBinding leftoverBinding = leftoverGate.GetComponent<WorldPresenceBinding>();
        if (leftoverBinding == null)
            leftoverBinding = leftoverGate.AddComponent<WorldPresenceBinding>();

        var leftoverSerialized = new SerializedObject(leftoverBinding);
        leftoverSerialized.FindProperty("presenceId").stringValue = CoffeeMartGateAuthoring.OpenFlag;
        leftoverSerialized.FindProperty("target").objectReferenceValue = pickup;
        leftoverSerialized.FindProperty("activeWhenTrue").boolValue = true;
        leftoverSerialized.FindProperty("lifetimeOverride").enumValueIndex = (int)WorldPresenceLifetime.Unspecified;
        leftoverSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(crate);
        EditorUtility.SetDirty(pickup);
        EditorUtility.SetDirty(leftoverGate);
    }

    private static GameObject EnsurePickupInstance(GameObject crate, GameObject pickupPrefab)
    {
        Transform existing = crate.transform.Find(PickupChildName);
        if (existing != null)
            return existing.gameObject;

        WorldPickupGrant existingGrant = crate.GetComponentInChildren<WorldPickupGrant>(true);
        if (existingGrant != null)
        {
            existingGrant.gameObject.name = PickupChildName;
            return existingGrant.gameObject;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, crate.transform);
        instance.name = PickupChildName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        var created = new GameObject(name);
        created.transform.SetParent(parent, false);
        created.transform.localPosition = Vector3.zero;
        created.transform.localRotation = Quaternion.identity;
        created.transform.localScale = Vector3.one;
        return created;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < transforms.Length; t++)
            {
                if (transforms[t].name == objectName)
                    return transforms[t].gameObject;
            }
        }

        return null;
    }
}
#endif
