#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared NPC interact prompt. Copies the working Reynard Indicator onto any
/// NPC Interactable volume so prompt visibility is not one-off per character.
/// </summary>
public static class NpcInteractableAuthoring
{
    public const string NpcPrefabPath = "Assets/Prefabs/NPC/NPC.prefab";
    public const string MomoPrefabPath = "Assets/Prefabs/NPC/Momo.prefab";
    public const string MartPrefabPath = "Assets/Prefabs/NPC/Mart.prefab";
    public const string ProfTobyPrefabPath = "Assets/Prefabs/NPC/Prof_Toby.prefab";
    public const string SailorPrefabPath = "Assets/Prefabs/NPC/Sailor.prefab";
    public const string ReynardPrefabPath = "Assets/Prefabs/NPC/Reynard.prefab";

    private static readonly string[] PrefabPaths =
    {
        NpcPrefabPath,
        MomoPrefabPath,
        MartPrefabPath,
        ProfTobyPrefabPath,
        SailorPrefabPath
    };

    [MenuItem("Tools/Chainbound/NPC/Author NPC Interactable Indicators")]
    public static void ApplyFromMenu()
    {
        ApplyToNpcPrefabs();
    }

    public static void ApplyToNpcPrefabs()
    {
        for (int i = 0; i < PrefabPaths.Length; i++)
            ApplyToPrefab(PrefabPaths[i]);

        AssetDatabase.SaveAssets();
        Debug.Log("[NpcInteractableAuthoring] Authored Interactable Indicator on NPC, Momo, Mart, Prof_Toby, and Sailor.");
    }

    public static void ApplyToPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            GameObject interaction = EnsureInteractableVolume(root);
            EnsureIndicator(interaction);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static GameObject EnsureInteractableVolume(GameObject root)
    {
        Transform found = root.transform.Find("Interactable");
        GameObject interaction = found != null ? found.gameObject : new GameObject("Interactable");
        interaction.transform.SetParent(root.transform, false);
        interaction.transform.localPosition = new Vector3(0f, 1f, 0f);
        interaction.transform.localRotation = Quaternion.identity;
        if (found == null)
            interaction.transform.localScale = new Vector3(4f, 3f, 4f);

        int buildings = LayerMask.NameToLayer("Buildings");
        if (buildings >= 0)
            interaction.layer = buildings;

        SphereCollider collider = interaction.GetComponent<SphereCollider>();
        if (collider == null)
            collider = interaction.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.85f;
        collider.center = Vector3.zero;

        InteractableObject interactable = interaction.GetComponent<InteractableObject>();
        if (interactable == null)
            interactable = interaction.AddComponent<InteractableObject>();

        var serialized = new SerializedObject(interactable);
        serialized.FindProperty("triggerMode").enumValueIndex =
            (int)InteractableObject.TriggerMode.RequireInteract;
        serialized.FindProperty("playerTag").stringValue = "Player";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(interactable);
        return interaction;
    }

    public static InteractableIndicator EnsureIndicator(GameObject interaction)
    {
        if (interaction == null)
            return null;

        Transform existing = interaction.transform.Find("Indicator");
        GameObject sourceIndicatorObject = LoadSourceIndicator();
        if (existing == null)
        {
            if (sourceIndicatorObject == null)
            {
                Debug.LogWarning("[NpcInteractableAuthoring] Reynard Indicator source was missing.");
                return null;
            }

            GameObject clone = Object.Instantiate(sourceIndicatorObject, interaction.transform);
            clone.name = "Indicator";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            existing = clone.transform;
        }

        InteractableIndicator indicator = existing.GetComponent<InteractableIndicator>();
        if (indicator == null)
            return null;

        var indicatorSerialized = new SerializedObject(indicator);
        indicatorSerialized.FindProperty("scale").floatValue = 1f;
        if (sourceIndicatorObject != null)
        {
            InteractableIndicator sourceIndicator = sourceIndicatorObject.GetComponent<InteractableIndicator>();
            if (sourceIndicator != null)
            {
                var sourceSerialized = new SerializedObject(sourceIndicator);
                indicatorSerialized.FindProperty("interactActionRef").objectReferenceValue =
                    sourceSerialized.FindProperty("interactActionRef").objectReferenceValue;
            }
        }

        indicatorSerialized.ApplyModifiedPropertiesWithoutUndo();

        InteractableObject interactable = interaction.GetComponent<InteractableObject>();
        if (interactable != null)
        {
            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("indicator").objectReferenceValue = indicator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(interactable);
        }

        EditorUtility.SetDirty(indicator);
        return indicator;
    }

    private static GameObject LoadSourceIndicator()
    {
        GameObject reynard = AssetDatabase.LoadAssetAtPath<GameObject>(ReynardPrefabPath);
        if (reynard == null)
            return null;

        Transform indicator = reynard.transform.Find("Interactable/Indicator");
        return indicator != null ? indicator.gameObject : null;
    }
}
#endif
