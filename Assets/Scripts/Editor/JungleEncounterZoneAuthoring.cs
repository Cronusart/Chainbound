#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Chainbound.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent Jungle wild encounters. Adds a trigger child on each BushPatch
/// using a clone of that patch's box collider. Does not change bush meshes,
/// materials, or the source collider.
/// Dawn and day: Rattlestake, Forkrook, Hopoch.
/// Dusk and night: those three, plus Deghoul and Blockwyrm.
/// Humpacked is reserved for a later stream trigger.
/// Weights are the tier curve times 10: Common 450, Uncommon 220, Rare 100, Legendary 6.
/// </summary>
public static class JungleEncounterZoneAuthoring
{
    private const string ScenePath = "Assets/Scenes/Jungle.unity";
    private const string ContentFolder = "Assets/Data/Combat/Encounters/Jungle";
    private const string AreaPath = ContentFolder + "/EncounterArea_Jungle.asset";
    private const string ZonePath = ContentFolder + "/EncounterZone_Jungle_Bush.asset";
    private const string MeleePoolPath = "Assets/Data/Combat/Encounters/Loomville/WildMovePool_Loomville_MeleeBaseline.asset";
    private const string RangedPoolPath = "Assets/Data/Combat/Encounters/Loomville/WildMovePool_Loomville_RangedBaseline.asset";
    private const string DefaultVariantPoolPath = "Assets/Data/Combat/Encounters/WildVariantPool_Default.asset";
    private const string IntroQuestPath = "Assets/Scripts/Narrative/Quests/chainbound_intro/Chainbound_Intro.asset";
    private const string TriggerObjectName = "GrassEncounterTrigger";
    private const string BattleSceneName = "Combat2_TestHarness";
    private const string BushPatchNamePrefix = "BushPatch";

    [MenuItem("Tools/Chainbound/Combat/Author Jungle Bush Encounters")]
    public static void ApplyFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

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
            $"[JungleEncounterAuthoring] Authored Jungle bush encounters. " +
            $"Dawn/Day: rattlestake, forkrook, hopoch. Dusk/Night adds deghoul, blockwyrm. BushPatch triggers={bushCount}.");
    }

    private static void AuthorArea(EncounterAreaSO area, WildVariantPoolSO variantPool)
    {
        area.areaId = "jungle";
        area.encounterChance = 0.16f;
        area.difficulty = EncounterDifficulty.Standard;
        area.minEncounterPartySize = 1;
        area.maxEncounterPartySize = 1;
        area.extraWildChanceAtOne = WildPartyExtraRoll.DefaultChanceAtOne;
        area.extraWildChanceAtTwo = WildPartyExtraRoll.DefaultChanceAtTwo;
        area.extraWildChanceAtThree = WildPartyExtraRoll.DefaultChanceAtThree;
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
        zone.zoneId = "jungle_bush";
        zone.combatStageId = CombatStageIds.Jungle;
        zone.area = area;
        zone.encounterChance = -1f;
        zone.cadence = EncounterCadence.PerSecond;
        zone.cadenceIntervalSeconds = 3f;
        zone.inheritDifficulty = true;
        zone.difficulty = EncounterDifficulty.Standard;
        zone.bossIntroDialogueAsset = null;
        zone.minEncounterPartySize = -1;
        zone.maxEncounterPartySize = -1;
        zone.extraWildChanceAtOne = -1f;
        zone.extraWildChanceAtTwo = -1f;
        zone.extraWildChanceAtThree = -1f;
        zone.entryMergeMode = EncounterEntryMergeMode.Replace;
        TimeSegmentMask always = TimeSegmentMask.Dawn | TimeSegmentMask.Day | TimeSegmentMask.Dusk | TimeSegmentMask.Night;
        TimeSegmentMask evening = TimeSegmentMask.Dusk | TimeSegmentMask.Night;
        zone.entries = new List<ZoneEncounterEntry>
        {
            CreateEntry("rattlestake", 450, always, rangedPool, 55),
            CreateEntry("forkrook", 220, always, rangedPool, 55),
            CreateEntry("hopoch", 100, always, meleePool, 40),
            CreateEntry("deghoul", 450, evening, meleePool, 45),
            CreateEntry("blockwyrm", 6, evening, meleePool, 20),
        };
        zone.mutationChanceOverride = -1f;
        zone.entangledChanceOverride = -1f;
        zone.ivMinOverride = int.MinValue;
        zone.ivMaxOverride = int.MinValue;
        zone.defaultMovePool = null;
        zone.defaultVariantPool = null;
        AssignCaptureRollConfig(zone);
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

            int bushCount = AuthorBushPatchTriggers(scene, zone, introQuest);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Failed to save '{ScenePath}'.");

            return bushCount;
        }
        finally
        {
            if (!Application.isBatchMode && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
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
                throw new InvalidOperationException($"'{manager.gameObject.name}' has no box collider to clone for encounters.");

            AuthorTriggerFromCollider(manager.transform, source, zone, introQuest);
            count++;
        }

        if (count == 0)
            throw new InvalidOperationException("Jungle scene has no BushPatch objects to attach encounter triggers to.");

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
        triggerTransform.localPosition = source.center;
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

        ApplyTriggerFields(trigger, zone, introQuest);
        EditorUtility.SetDirty(triggerObject);
        EditorUtility.SetDirty(triggerCollider);
    }

    private static void ApplyTriggerFields(
        EncounterTriggerZone trigger,
        EncounterZoneSO zone,
        QuestDefinitionSO introQuest)
    {
        SerializedObject serialized = new SerializedObject(trigger);
        serialized.Update();
        serialized.FindProperty("zone").objectReferenceValue = zone;
        serialized.FindProperty("questService").objectReferenceValue = null;
        serialized.FindProperty("battleSceneName").stringValue = BattleSceneName;
        serialized.FindProperty("playerLayer").intValue = ~0;
        serialized.FindProperty("cooldownSeconds").floatValue = 5f;
        serialized.FindProperty("minDistanceBetweenEncounters").floatValue = 2f;
        serialized.FindProperty("debugForceEncounter").boolValue = false;
        serialized.FindProperty("debugIgnoreCadence").boolValue = false;
        serialized.FindProperty("enableDebugLogs").boolValue = false;
        AuthorCompletedQuestGuard(serialized.FindProperty("questGuard"), introQuest);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ForceSceneReferences(trigger, zone, introQuest);
        EditorUtility.SetDirty(trigger);
        PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
    }

    private static void ForceSceneReferences(
        EncounterTriggerZone trigger,
        EncounterZoneSO zoneAsset,
        QuestDefinitionSO quest)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(EncounterTriggerZone).GetField("zone", flags)?.SetValue(trigger, zoneAsset);

        object guard = typeof(EncounterTriggerZone).GetField("questGuard", flags)?.GetValue(trigger);
        object clauses = guard?.GetType().GetField("requiredClauses", flags)?.GetValue(guard);
        if (clauses is System.Collections.IList list && list.Count > 0 && list[0] != null)
            list[0].GetType().GetField("questDefinition", flags)?.SetValue(list[0], quest);
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

    private static void AssignCaptureRollConfig(EncounterZoneSO zone)
    {
        zone.speciesDatabase = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>(
            "Assets/Data/Combat/ChainlingSpecies/Database/SpeciesDatabase.asset");
        zone.progressionRewardTables = AssetDatabase.LoadAssetAtPath<ChainlingProgressionRewardTablesSO>(
            "Assets/Data/Combat/ChainlingProgressionRewardTables.asset");
        zone.progressionConfig = AssetDatabase.LoadAssetAtPath<ProgressionConfigSO>(
            "Assets/Data/Combat/Config/ProgressionConfig_Default.asset");
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
