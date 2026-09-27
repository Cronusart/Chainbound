#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent content authoring for Loomville's grass encounters.
/// Runtime behavior remains data-driven through EncounterAreaSO, EncounterZoneSO,
/// NarrativeQuestGuard, and the existing EncounterTriggerZone interpreter.
/// </summary>
public static class LoomvilleEncounterZoneAuthoring
{
    private const string ScenePath = "Assets/Scenes/Loomville.unity";
    private const string ContentFolder = "Assets/Data/Combat/Encounters/Loomville";
    private const string AreaPath = ContentFolder + "/EncounterArea_Loomville.asset";
    private const string ZonePath = ContentFolder + "/EncounterZone_Loomville_Grass.asset";
    private const string MeleePoolPath = ContentFolder + "/WildMovePool_Loomville_MeleeBaseline.asset";
    private const string RangedPoolPath = ContentFolder + "/WildMovePool_Loomville_RangedBaseline.asset";
    private const string DefaultVariantPoolPath = "Assets/Data/Combat/Encounters/WildVariantPool_Default.asset";
    private const string IntroQuestPath = "Assets/Scripts/Narrative/Quests/chainbound_intro/Chainbound_Intro.asset";
    private const string TriggerObjectName = "GrassEncounterTrigger";
    private const string BattleSceneName = "Combat2_TestHarness";

    private static readonly string[] GrassPatchNames =
    {
        "GrassPatch",
        "GrassPatch (1)",
        "GrassPatch (2)",
        "GrassPatch (3)",
        "GrassPatch (4)",
    };

    private static readonly HashSet<string> StarterSpeciesIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "validape",
        "gilarime",
        "kitsauto",
    };

    [MenuItem("Tools/Chainbound/Combat/Author Loomville Grass Encounters")]
    public static void ApplyFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static void Apply()
    {
        EnsureFolder(ContentFolder);

        WildMovePoolSO meleePool = EnsureMovePool(MeleePoolPath, "baseline_striker");
        WildMovePoolSO rangedPool = EnsureMovePool(RangedPoolPath, "baseline_shot");
        WildVariantPoolSO variantPool = AssetDatabase.LoadAssetAtPath<WildVariantPoolSO>(DefaultVariantPoolPath);
        if (variantPool == null)
            throw new InvalidOperationException($"Missing default variant pool at '{DefaultVariantPoolPath}'.");

        EncounterAreaSO area = EnsureAsset<EncounterAreaSO>(AreaPath);
        AuthorArea(area, variantPool);

        EncounterZoneSO zone = EnsureAsset<EncounterZoneSO>(ZonePath);
        AuthorZone(zone, area, meleePool, rangedPool);
        ValidateEncounterContent(zone);

        QuestDefinitionSO introQuest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(IntroQuestPath);
        if (introQuest == null || !string.Equals(introQuest.QuestId, "chainbound_intro", StringComparison.Ordinal))
            throw new InvalidOperationException($"Missing or invalid chainbound_intro quest at '{IntroQuestPath}'.");

        AssetDatabase.SaveAssets();
        WireScene(zone, introQuest);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[LoomvilleEncounterAuthoring] Authored five quest-gated grass triggers. " +
            "Day/Dawn: cinislug, caw, snek. Dusk/Night: kreechank, ghosty, deghoul. " +
            "Starter species are excluded.");
    }

    private static void AuthorArea(EncounterAreaSO area, WildVariantPoolSO variantPool)
    {
        area.areaId = "loomville";
        area.encounterChance = 0.12f;
        area.difficulty = EncounterDifficulty.Standard;
        area.minEncounterPartySize = 1;
        area.maxEncounterPartySize = 1;
        area.wildTraits = new WildRollConfig();
        area.entries = new List<ZoneEncounterEntry>();
        area.defaultMovePool = null;
        area.defaultVariantPool = variantPool;
        EditorUtility.SetDirty(area);
    }

    private static void AuthorZone(
        EncounterZoneSO zone,
        EncounterAreaSO area,
        WildMovePoolSO meleePool,
        WildMovePoolSO rangedPool)
    {
        zone.zoneId = "loomville_grass";
        zone.area = area;
        zone.encounterChance = -1f;
        zone.cadence = EncounterCadence.PerSecond;
        zone.cadenceIntervalSeconds = 3f;
        zone.inheritDifficulty = true;
        zone.difficulty = EncounterDifficulty.Standard;
        zone.bossIntroDialogueAsset = null;
        zone.minEncounterPartySize = -1;
        zone.maxEncounterPartySize = -1;
        zone.entryMergeMode = EncounterEntryMergeMode.Replace;
        zone.entries = new List<ZoneEncounterEntry>
        {
            CreateEntry("cinislug", 50, TimeSegmentMask.Dawn | TimeSegmentMask.Day, rangedPool, 60,
                SegmentWeight(TimeSegment.Dawn, 0.8f), SegmentWeight(TimeSegment.Day, 1.2f)),
            CreateEntry("caw", 30, TimeSegmentMask.Dawn | TimeSegmentMask.Day, rangedPool, 55,
                SegmentWeight(TimeSegment.Dawn, 1.2f), SegmentWeight(TimeSegment.Day, 0.8f)),
            CreateEntry("snek", 20, TimeSegmentMask.Dawn | TimeSegmentMask.Day, rangedPool, 55),
            CreateEntry("kreechank", 50, TimeSegmentMask.Dusk | TimeSegmentMask.Night, meleePool, 55,
                SegmentWeight(TimeSegment.Dusk, 1.2f), SegmentWeight(TimeSegment.Night, 0.8f)),
            CreateEntry("ghosty", 35, TimeSegmentMask.Dusk | TimeSegmentMask.Night, rangedPool, 50,
                SegmentWeight(TimeSegment.Dusk, 0.8f), SegmentWeight(TimeSegment.Night, 1.2f)),
            CreateEntry("deghoul", 15, TimeSegmentMask.Night, meleePool, 45),
        };
        zone.mutationChanceOverride = -1f;
        zone.entangledChanceOverride = -1f;
        zone.ivMinOverride = int.MinValue;
        zone.ivMaxOverride = int.MinValue;
        zone.defaultMovePool = null;
        zone.defaultVariantPool = null;
        EditorUtility.SetDirty(zone);
    }

    private static ZoneEncounterEntry CreateEntry(
        string speciesId,
        int weight,
        TimeSegmentMask allowedSegments,
        WildMovePoolSO movePool,
        int captureRate,
        params EncounterSegmentWeight[] segmentWeights)
    {
        return new ZoneEncounterEntry
        {
            speciesId = speciesId,
            weight = weight,
            allowedSegments = allowedSegments,
            segmentWeights = new List<EncounterSegmentWeight>(segmentWeights ?? Array.Empty<EncounterSegmentWeight>()),
            baseCaptureRate = captureRate,
            baseStars = 0,
            starVariance = 0,
            movePool = movePool,
            inlineMovePool = new List<WeightedMoveEntry>(),
            battleKitSizeMin = 1,
            battleKitSizeMax = 1,
            variantRarityTable = null,
            variantPool = null,
            inlineVariantPool = new List<WeightedVariantEntry>(),
        };
    }

    private static EncounterSegmentWeight SegmentWeight(TimeSegment segment, float weight)
    {
        return new EncounterSegmentWeight
        {
            segment = segment,
            weight = weight,
        };
    }

    private static WildMovePoolSO EnsureMovePool(string path, string moveId)
    {
        WildMovePoolSO pool = EnsureAsset<WildMovePoolSO>(path);
        pool.entries = new List<WeightedMoveEntry>
        {
            new WeightedMoveEntry { moveId = moveId, weight = 100 },
        };
        EditorUtility.SetDirty(pool);
        return pool;
    }

    private static void WireScene(EncounterZoneSO zone, QuestDefinitionSO introQuest)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Dictionary<string, Transform> patches = FindNamedTransforms(scene, GrassPatchNames);

            for (int i = 0; i < GrassPatchNames.Length; i++)
            {
                string patchName = GrassPatchNames[i];
                if (!patches.TryGetValue(patchName, out Transform patch))
                    throw new InvalidOperationException($"Loomville scene is missing expected grass patch '{patchName}'.");

                AuthorTrigger(patch, zone, introQuest);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Failed to save '{ScenePath}'.");
        }
        finally
        {
            if (!Application.isBatchMode && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static void AuthorTrigger(Transform grassPatch, EncounterZoneSO zone, QuestDefinitionSO introQuest)
    {
        Transform triggerTransform = grassPatch.Find(TriggerObjectName);
        GameObject triggerObject;
        if (triggerTransform == null)
        {
            triggerObject = new GameObject(TriggerObjectName);
            triggerTransform = triggerObject.transform;
            triggerTransform.SetParent(grassPatch, false);
        }
        else
        {
            triggerObject = triggerTransform.gameObject;
        }

        BoxCollider grassCollider = null;
        Transform grassColliderTransform = grassPatch.Find("GrassCollider");
        if (grassColliderTransform != null)
            grassCollider = grassColliderTransform.GetComponent<BoxCollider>();

        float sizeX = grassCollider != null ? grassCollider.size.x : 15f;
        float sizeZ = grassCollider != null ? grassCollider.size.z : 15.5f;
        float localX = grassColliderTransform != null ? grassColliderTransform.localPosition.x : 0f;
        float localZ = grassColliderTransform != null ? grassColliderTransform.localPosition.z : 0f;

        triggerObject.layer = 0;
        triggerTransform.localPosition = new Vector3(localX, 5.25f, localZ);
        triggerTransform.localRotation = Quaternion.identity;
        triggerTransform.localScale = Vector3.one;
        triggerObject.SetActive(true);

        BoxCollider triggerCollider = triggerObject.GetComponent<BoxCollider>();
        if (triggerCollider == null)
            triggerCollider = triggerObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.center = Vector3.zero;
        triggerCollider.size = new Vector3(sizeX, 8f, sizeZ);

        EncounterTriggerZone trigger = triggerObject.GetComponent<EncounterTriggerZone>();
        if (trigger == null)
            trigger = triggerObject.AddComponent<EncounterTriggerZone>();

        SerializedObject serialized = new SerializedObject(trigger);
        serialized.FindProperty("zone").objectReferenceValue = zone;
        serialized.FindProperty("questService").objectReferenceValue = null;
        serialized.FindProperty("battleSceneName").stringValue = BattleSceneName;
        serialized.FindProperty("playerLayer").intValue = ~0;
        serialized.FindProperty("cooldownSeconds").floatValue = 10f;
        serialized.FindProperty("minDistanceBetweenEncounters").floatValue = 5f;
        serialized.FindProperty("debugForceEncounter").boolValue = false;
        serialized.FindProperty("debugIgnoreCadence").boolValue = false;
        serialized.FindProperty("enableDebugLogs").boolValue = false;
        AuthorCompletedQuestGuard(serialized.FindProperty("questGuard"), introQuest);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(triggerObject);
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(triggerCollider);
    }

    private static void AuthorCompletedQuestGuard(SerializedProperty guard, QuestDefinitionSO quest)
    {
        if (guard == null)
            throw new InvalidOperationException("EncounterTriggerZone.questGuard serialized property was not found.");

        guard.FindPropertyRelative("requiredMatchMode").enumValueIndex = (int)NarrativeQuestGuardMatchMode.All;

        SerializedProperty required = guard.FindPropertyRelative("requiredClauses");
        required.arraySize = 1;
        SerializedProperty clause = required.GetArrayElementAtIndex(0);
        clause.FindPropertyRelative("questDefinition").objectReferenceValue = quest;
        clause.FindPropertyRelative("questId").stringValue = string.Empty;
        clause.FindPropertyRelative("allowedLifecycleMask").intValue = (int)NarrativeQuestLifecycleMask.Completed;

        guard.FindPropertyRelative("excludedClauses").arraySize = 0;
    }

    private static Dictionary<string, Transform> FindNamedTransforms(Scene scene, IReadOnlyList<string> names)
    {
        var requested = new HashSet<string>(names, StringComparer.Ordinal);
        var matches = new Dictionary<string, Transform>(StringComparer.Ordinal);
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Transform candidate = transforms[transformIndex];
                if (!requested.Contains(candidate.name))
                    continue;

                if (matches.ContainsKey(candidate.name))
                    throw new InvalidOperationException($"Loomville has more than one object named '{candidate.name}'.");

                matches.Add(candidate.name, candidate);
            }
        }

        return matches;
    }

    private static void ValidateEncounterContent(EncounterZoneSO zone)
    {
        if (zone.entries == null || zone.entries.Count == 0)
            throw new InvalidOperationException("Loomville grass encounter table is empty.");

        bool hasDay = false;
        bool hasEvening = false;
        for (int i = 0; i < zone.entries.Count; i++)
        {
            ZoneEncounterEntry entry = zone.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.speciesId))
                throw new InvalidOperationException($"Loomville encounter entry {i} has no species ID.");
            if (StarterSpeciesIds.Contains(entry.speciesId))
                throw new InvalidOperationException($"Starter species '{entry.speciesId}' cannot appear in Loomville wild encounters.");
            if (entry.movePool == null || entry.movePool.entries == null || entry.movePool.entries.Count != 1)
                throw new InvalidOperationException($"Species '{entry.speciesId}' must have exactly one baseline wild move.");

            hasDay |= (entry.allowedSegments & (TimeSegmentMask.Dawn | TimeSegmentMask.Day)) != 0;
            hasEvening |= (entry.allowedSegments & (TimeSegmentMask.Dusk | TimeSegmentMask.Night)) != 0;
        }

        if (!hasDay || !hasEvening)
            throw new InvalidOperationException("Loomville encounters must define both daytime and evening species tables.");
    }

    private static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
