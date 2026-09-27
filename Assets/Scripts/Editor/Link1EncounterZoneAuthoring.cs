#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent Link1 wild encounters: existing grass triggers plus BushPatch
/// volumes cloned from each GrassPatchManager box collider.
/// Day: caw, snek. Night: deghoul, ghosty.
/// </summary>
public static class Link1EncounterZoneAuthoring
{
    private const string ScenePath = "Assets/Scenes/Link1.unity";
    private const string ContentFolder = "Assets/Data/Combat/Encounters/Link1";
    private const string AreaPath = ContentFolder + "/EncounterArea_Link1.asset";
    private const string ZonePath = ContentFolder + "/EncounterZone_Link1_Grass.asset";
    private const string MeleePoolPath = "Assets/Data/Combat/Encounters/Loomville/WildMovePool_Loomville_MeleeBaseline.asset";
    private const string RangedPoolPath = "Assets/Data/Combat/Encounters/Loomville/WildMovePool_Loomville_RangedBaseline.asset";
    private const string DefaultVariantPoolPath = "Assets/Data/Combat/Encounters/WildVariantPool_Default.asset";
    private const string IntroQuestPath = "Assets/Scripts/Narrative/Quests/chainbound_intro/Chainbound_Intro.asset";
    private const string TriggerObjectName = "GrassEncounterTrigger";
    private const string BattleSceneName = "Combat2_TestHarness";
    private const string BushPatchNamePrefix = "BushPatch";

    [MenuItem("Tools/Chainbound/Combat/Author Link1 Grass Encounters")]
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

    public static void Apply()
    {
        EnsureFolder(ContentFolder);

        WildMovePoolSO meleePool = AssetDatabase.LoadAssetAtPath<WildMovePoolSO>(MeleePoolPath);
        WildMovePoolSO rangedPool = AssetDatabase.LoadAssetAtPath<WildMovePoolSO>(RangedPoolPath);
        WildVariantPoolSO variantPool = AssetDatabase.LoadAssetAtPath<WildVariantPoolSO>(DefaultVariantPoolPath);
        if (meleePool == null || rangedPool == null)
            throw new InvalidOperationException("Missing Loomville baseline wild move pools.");
        if (variantPool == null)
            throw new InvalidOperationException($"Missing default variant pool at '{DefaultVariantPoolPath}'.");

        EncounterAreaSO area = EnsureAsset<EncounterAreaSO>(AreaPath);
        AuthorArea(area, variantPool);

        EncounterZoneSO zone = EnsureAsset<EncounterZoneSO>(ZonePath);
        AuthorZone(zone, area, meleePool, rangedPool);

        QuestDefinitionSO introQuest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(IntroQuestPath);
        if (introQuest == null || !string.Equals(introQuest.QuestId, "chainbound_intro", StringComparison.Ordinal))
            throw new InvalidOperationException($"Missing or invalid chainbound_intro quest at '{IntroQuestPath}'.");

        AssetDatabase.SaveAssets();
        int bushCount = WireScene(zone, introQuest);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[Link1EncounterAuthoring] Authored Link1 grass/bush encounters. " +
            $"Day: caw, snek. Night: deghoul, ghosty. BushPatch triggers={bushCount}.");
    }

    private static void AuthorArea(EncounterAreaSO area, WildVariantPoolSO variantPool)
    {
        area.areaId = "link1";
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
        zone.zoneId = "link1_grass";
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
            CreateEntry("caw", 50, TimeSegmentMask.Dawn | TimeSegmentMask.Day, rangedPool, 55),
            CreateEntry("snek", 50, TimeSegmentMask.Dawn | TimeSegmentMask.Day, rangedPool, 55),
            CreateEntry("ghosty", 50, TimeSegmentMask.Dusk | TimeSegmentMask.Night, rangedPool, 50),
            CreateEntry("deghoul", 50, TimeSegmentMask.Dusk | TimeSegmentMask.Night, meleePool, 45),
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
        int captureRate)
    {
        return new ZoneEncounterEntry
        {
            speciesId = speciesId,
            weight = weight,
            allowedSegments = allowedSegments,
            segmentWeights = new List<EncounterSegmentWeight>(),
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

    private static int WireScene(EncounterZoneSO zone, QuestDefinitionSO introQuest)
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int retargeted = RetargetExistingTriggers(scene, zone, introQuest);
            int bushCount = AuthorBushPatchTriggers(scene, zone, introQuest);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Failed to save '{ScenePath}'.");

            Debug.Log($"[Link1EncounterAuthoring] Retargeted {retargeted} existing grass triggers.");
            return bushCount;
        }
        finally
        {
            if (!Application.isBatchMode && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static int RetargetExistingTriggers(Scene scene, EncounterZoneSO zone, QuestDefinitionSO introQuest)
    {
        int count = 0;
        EncounterTriggerZone[] triggers = UnityEngine.Object.FindObjectsByType<EncounterTriggerZone>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < triggers.Length; i++)
        {
            EncounterTriggerZone trigger = triggers[i];
            if (trigger == null || trigger.gameObject.scene != scene)
                continue;

            ApplyTriggerFields(trigger, zone, introQuest, cooldownSeconds: 5f, minDistance: 2f);
            count++;
        }

        return count;
    }

    private static int AuthorBushPatchTriggers(Scene scene, EncounterZoneSO zone, QuestDefinitionSO introQuest)
    {
        int count = 0;
        GrassPatchManager[] managers = UnityEngine.Object.FindObjectsByType<GrassPatchManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            GrassPatchManager manager = managers[i];
            if (manager == null || manager.gameObject.scene != scene)
                continue;
            if (!manager.gameObject.name.StartsWith(BushPatchNamePrefix, StringComparison.Ordinal))
                continue;

            BoxCollider source = manager.GetComponent<BoxCollider>();
            if (source == null)
                source = manager.GetComponentInChildren<BoxCollider>(true);
            if (source == null)
                throw new InvalidOperationException($"'{manager.gameObject.name}' has no box collider to clone for encounters.");

            AuthorTriggerFromCollider(manager.transform, source, zone, introQuest);
            count++;
        }

        return count;
    }

    private static void AuthorTriggerFromCollider(
        Transform host,
        BoxCollider source,
        EncounterZoneSO zone,
        QuestDefinitionSO introQuest)
    {
        Transform triggerTransform = host.Find(TriggerObjectName);
        GameObject triggerObject;
        if (triggerTransform == null)
        {
            triggerObject = new GameObject(TriggerObjectName);
            triggerTransform = triggerObject.transform;
            triggerTransform.SetParent(host, false);
        }
        else
        {
            triggerObject = triggerTransform.gameObject;
        }

        triggerObject.layer = 0;
        triggerTransform.localPosition = source.transform == host
            ? source.center
            : host.InverseTransformPoint(source.transform.TransformPoint(source.center));
        triggerTransform.localRotation = Quaternion.identity;
        triggerTransform.localScale = Vector3.one;
        triggerObject.SetActive(true);

        BoxCollider triggerCollider = triggerObject.GetComponent<BoxCollider>();
        if (triggerCollider == null)
            triggerCollider = triggerObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.center = Vector3.zero;
        Vector3 size = source.size;
        size.x = Mathf.Abs(size.x);
        size.y = Mathf.Max(8f, Mathf.Abs(size.y));
        size.z = Mathf.Abs(size.z);
        triggerCollider.size = size;

        EncounterTriggerZone trigger = triggerObject.GetComponent<EncounterTriggerZone>();
        if (trigger == null)
            trigger = triggerObject.AddComponent<EncounterTriggerZone>();

        ApplyTriggerFields(trigger, zone, introQuest, cooldownSeconds: 5f, minDistance: 2f);
        EditorUtility.SetDirty(triggerObject);
        EditorUtility.SetDirty(triggerCollider);
    }

    private static void ApplyTriggerFields(
        EncounterTriggerZone trigger,
        EncounterZoneSO zone,
        QuestDefinitionSO introQuest,
        float cooldownSeconds,
        float minDistance)
    {
        SerializedObject serialized = new SerializedObject(trigger);
        serialized.FindProperty("zone").objectReferenceValue = zone;
        serialized.FindProperty("questService").objectReferenceValue = null;
        serialized.FindProperty("battleSceneName").stringValue = BattleSceneName;
        serialized.FindProperty("playerLayer").intValue = ~0;
        serialized.FindProperty("cooldownSeconds").floatValue = cooldownSeconds;
        serialized.FindProperty("minDistanceBetweenEncounters").floatValue = minDistance;
        serialized.FindProperty("debugForceEncounter").boolValue = false;
        serialized.FindProperty("debugIgnoreCadence").boolValue = false;
        serialized.FindProperty("enableDebugLogs").boolValue = false;
        AuthorCompletedQuestGuard(serialized.FindProperty("questGuard"), introQuest);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);
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
