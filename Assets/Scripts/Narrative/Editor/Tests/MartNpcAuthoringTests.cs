#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MartNpcAuthoringTests
{
    private const string PrefabPath = "Assets/Prefabs/NPC/Mart.prefab";
    private const string ProfilePath =
        "Assets/Scripts/NPC/Interaction/Loomville/Mart/mart_chainbound_intro_profile.asset";
    private const string RulesPath =
        "Assets/Resources/Narrative/ProfileWorldFlagSceneRules.asset";
    private const string ScenePath = "Assets/Scenes/Loomville.unity";

    [Test]
    public void MartPrefab_UsesBertDirectionalPatternAndBlinkSprites()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.name, Is.EqualTo("Mart"));

        NPCController controller = prefab.GetComponent<NPCController>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.NPCId, Is.EqualTo("mart"));
        Assert.That(controller.InteractionBehavior, Is.Not.Null);
        Assert.That(controller.InteractionBehavior.FacePlayerOnStart, Is.True);
        Assert.That(controller.RouteSchedule, Is.Null);

        MeshRendererNPCVisual visual = prefab.GetComponentInChildren<MeshRendererNPCVisual>(true);
        Assert.That(visual, Is.Not.Null);
        var serialized = new SerializedObject(visual);
        AssertDirectionalCount(serialized, "idleSequences", 8);
        AssertDirectionalCount(serialized, "walkSequences", 4);
        Assert.That(serialized.FindProperty("blinkDown").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("blinkLeft").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("blinkRight").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("preserveAspectRatio").boolValue, Is.True);
        Assert.That(serialized.FindProperty("referenceHeight").floatValue, Is.EqualTo(5f));

        QuestProgressInteractionBridge bridge = prefab.GetComponent<QuestProgressInteractionBridge>();
        Assert.That(bridge, Is.Not.Null);
        Assert.That(bridge.DefaultProfile, Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<InteractableObject>(true), Is.Not.Null);
        InteractableObject interactable = prefab.GetComponentInChildren<InteractableObject>(true);
        Assert.That(interactable, Is.Not.Null);
        InteractableIndicator indicator = prefab.GetComponentInChildren<InteractableIndicator>(true);
        Assert.That(indicator, Is.Not.Null);
        var interactableSerialized = new SerializedObject(interactable);
        Assert.That(
            interactableSerialized.FindProperty("indicator").objectReferenceValue,
            Is.SameAs(indicator));
    }

    [Test]
    public void MartProfile_GatesShortAndCompletedDialogueByIntroLifecycle()
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(ProfilePath);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.QuestId, Is.EqualTo("chainbound_intro"));
        Assert.That(profile.Rules.Count, Is.EqualTo(1));

        QuestInteractionRule active = profile.Rules[0];
        Assert.That(active.lifecycleScope, Is.EqualTo(QuestInteractionRuleLifecycleScope.UseLifecycleMask));
        Assert.That(active.lifecycleMask, Is.EqualTo(NarrativeQuestLifecycleMask.Active));
        Assert.That(active.dialogue, Is.Not.Null);
        Assert.That(active.dialogue.LineCount, Is.EqualTo(2));
        Assert.That(active.useDialogueCamera, Is.True);
        Assert.That(active.npcBehavior.mode, Is.EqualTo(QuestInteractionNpcBehaviorMode.UseNpcDefault));
    }

    [Test]
    public void MartDeparture_IsAuthoredAsProfileSceneEntryFlagAndSceneGate()
    {
        ProfileWorldFlagSceneRulesSO rules =
            AssetDatabase.LoadAssetAtPath<ProfileWorldFlagSceneRulesSO>(RulesPath);
        Assert.That(rules, Is.Not.Null);
        Assert.That(rules.Rules.Any(rule =>
            string.Equals(rule.SceneId, "loomville_mart", StringComparison.OrdinalIgnoreCase) &&
            rule.FlagKey == "mart_departed_loomville" &&
            rule.FlagValue), Is.True);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject mart = Find(scene, "Mart");
            GameObject gateHost = Find(scene, "Mart_ProfileGate");
            Assert.That(mart, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(mart), Is.EqualTo(PrefabPath));
            Assert.That(gateHost, Is.Not.Null);

            ProfileWorldFlagObjectGate gate = gateHost.GetComponent<ProfileWorldFlagObjectGate>();
            Assert.That(gate, Is.Not.Null);
            var serialized = new SerializedObject(gate);
            Assert.That(serialized.FindProperty("target").objectReferenceValue, Is.EqualTo(mart));
            Assert.That(serialized.FindProperty("flagKey").stringValue,
                Is.EqualTo("mart_departed_loomville"));
            Assert.That(serialized.FindProperty("activeWhenMatched").boolValue, Is.False);
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public async Task ProfileWorldFlags_RoundTripThroughProfileLocalSnapshot()
    {
        string profileId = "mart_world_flag_test_" + Guid.NewGuid().ToString("N");
        var identity = new NarrativeProfileIdentity { playerProfileId = profileId };
        string hash = Hash128.Compute(identity.CacheIdentity).ToString();
        string snapshotKey = "SignalJam_ProfileWorldFlags_v1_" + hash;
        string outboxKey = "SignalJam_ProfileWorldFlagOutbox_v1_" + hash;

        try
        {
            ProfileWorldFlagStateService service = ProfileWorldFlagStateService.Instance;
            await service.HydrateProfileAsync(identity, forceOffline: true);
            service.SetFlag("mart_departed_loomville", true);
            Assert.That(service.GetFlag("mart_departed_loomville"), Is.True);
            Assert.That(WorldState.Instance.GetGlobalFlag("mart_departed_loomville"), Is.True);
            UnityEngine.Object.DestroyImmediate(service.gameObject);

            ProfileWorldFlagStateService restored = ProfileWorldFlagStateService.Instance;
            await restored.HydrateProfileAsync(identity, forceOffline: true);
            Assert.That(restored.GetFlag("mart_departed_loomville"), Is.True);
            UnityEngine.Object.DestroyImmediate(restored.gameObject);
        }
        finally
        {
            ProfileWorldFlagStateService lingeringService =
                UnityEngine.Object.FindObjectOfType<ProfileWorldFlagStateService>();
            if (lingeringService != null)
                UnityEngine.Object.DestroyImmediate(lingeringService.gameObject);

            WorldState lingeringWorldState = UnityEngine.Object.FindObjectOfType<WorldState>();
            if (lingeringWorldState != null)
                UnityEngine.Object.DestroyImmediate(lingeringWorldState.gameObject);

            PlayerPrefs.DeleteKey(snapshotKey);
            PlayerPrefs.DeleteKey(outboxKey);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void CoffeeQuest_IncludesTalkToMartObjective()
    {
        QuestDefinitionSO quest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
            "Assets/Scripts/Narrative/Quests/Quest_CoffeeAndBeer.asset");
        Assert.That(quest, Is.Not.Null);
        Assert.That(quest.Objectives.Any(item => item.objectiveId == CoffeeMartGateAuthoring.TalkToMartObjective), Is.True);
    }

    [Test]
    public void CoffeeProfile_LeftoverRuleFiresShopPermissionTrigger()
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(
            "Assets/Scripts/NPC/Interaction/Loomville/Mart/mart_coffee_interaction_profile.asset");
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.QuestId, Is.EqualTo(CoffeeMartGateAuthoring.CoffeeQuestId));
        Assert.That(profile.Rules.Count, Is.GreaterThanOrEqualTo(2));

        QuestInteractionRule beforeIntercept = profile.Rules.First(rule =>
            rule.objectiveId == "talk_to_reynard");
        Assert.That(beforeIntercept.objectiveComparison, Is.EqualTo(QuestInteractionObjectiveComparison.Complete));
        Assert.That(beforeIntercept.triggerId, Is.Null.Or.Empty);
        Assert.That(beforeIntercept.dialogue, Is.Not.Null);
        Assert.That(beforeIntercept.dialogue.name, Is.EqualTo("Dial_Mart_Packing"));

        QuestInteractionRule afterIntercept = profile.Rules.First(rule =>
            rule.objectiveId == CoffeeMartGateAuthoring.TalkToMartObjective);
        Assert.That(afterIntercept.dialogue, Is.Not.Null);
        Assert.That(afterIntercept.dialogue.Lines.Any(line =>
            line.text != null && line.text.IndexOf("truck", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);

        DialogueSequenceSO intercept = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(
            "Assets/Scripts/NPC/Interaction/Loomville/Mart/Dial_Mart_OrbCreator.asset");
        Assert.That(intercept, Is.Not.Null);
        Assert.That(intercept.LineCount, Is.EqualTo(CoffeeMartGateAuthoring.LeftoverDialogueLines.Length));
        Assert.That(intercept.Lines.Any(line =>
            line.text != null && line.text.IndexOf("user acquisition", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
        Assert.That(intercept.Lines.Any(line =>
            line.text != null && line.text.IndexOf("Chainling PC", StringComparison.Ordinal) >= 0), Is.True);
        Assert.That(intercept.Lines.Any(line =>
            line.text != null && line.text.IndexOf("player menu", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        Assert.That(intercept.Lines.Any(line =>
            line.text != null && line.text.IndexOf("handheld", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        Assert.That(intercept.Lines.Any(line =>
            line.text != null && line.text.IndexOf("send a tech", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        Assert.That(beforeIntercept.useDialogueCamera, Is.True);
    }

    [Test]
    public void CoffeeLeftoverTracks_UnlockChainlingPcFlag()
    {
        Assert.That(
            TrackSetsFlag(
                "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence/coffee_loomville_gate_standing_track.asset",
                ChainlingPcTerminalIds.UnlockFlagKey),
            Is.True);
        Assert.That(
            TrackSetsFlag(
                "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence/coffee_loomville_gate_exit_track.asset",
                ChainlingPcTerminalIds.UnlockFlagKey),
            Is.True);
    }

    [Test]
    public void MartRouter_PrefersCoffeeThenIntro()
    {
        QuestInteractionRouterSO router = AssetDatabase.LoadAssetAtPath<QuestInteractionRouterSO>(
            "Assets/Scripts/NPC/Interaction/Loomville/Mart/Mart_InteractionRouter.asset");
        Assert.That(router, Is.Not.Null);
        Assert.That(router.Slots.Count, Is.EqualTo(2));
        Assert.That(router.Slots[0].Profile, Is.Not.Null);
        Assert.That(router.Slots[0].Profile.QuestId, Is.EqualTo(CoffeeMartGateAuthoring.CoffeeQuestId));
        Assert.That(router.Slots[1].Profile, Is.Not.Null);
        Assert.That(router.Slots[1].Profile.QuestId, Is.EqualTo("chainbound_intro"));
    }

    [Test]
    public void CoffeeGateSequence_IsCatalogedWithAutoStart()
    {
        SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(
            "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence/coffee_loomville_gate_sequence.asset");
        SequenceCatalogSO catalog = AssetDatabase.LoadAssetAtPath<SequenceCatalogSO>(
            "Assets/Resources/Narrative/SequenceCatalog.asset");
        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.SequenceId, Is.EqualTo(CoffeeMartGateAuthoring.GateSequenceId));
        Assert.That(sequence.AutoStartWhenNoSequenceRunning, Is.True);
        Assert.That(sequence.AutoStartQuestId, Is.EqualTo(CoffeeMartGateAuthoring.CoffeeQuestId));
        Assert.That(sequence.AutoStartSkipWorldFlagKey, Is.EqualTo(CoffeeMartGateAuthoring.OpenFlag));
        Assert.That(catalog.Sequences.Contains(sequence), Is.True);
    }

    [Test]
    public void IntroSequence_QueuesCoffeeGateAfterCoffeeAvailable()
    {
        SequenceAsset intro = AssetDatabase.LoadAssetAtPath<SequenceAsset>(
            "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_sequence.asset");
        Assert.That(intro, Is.Not.Null);

        int coffeeIndex = -1;
        bool queuedAfter = false;
        for (int i = 0; i < intro.StepCount; i++)
        {
            SequenceStep step = intro.GetStep(i);
            if (step != null && step.name == "intro_setquestavailable_coffee")
                coffeeIndex = i;
            if (coffeeIndex >= 0 && i > coffeeIndex && step is Step_QueueSequence queue &&
                queue.Sequence != null &&
                queue.Sequence.SequenceId == CoffeeMartGateAuthoring.GateSequenceId)
            {
                queuedAfter = true;
                break;
            }
        }

        Assert.That(coffeeIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(queuedAfter, Is.True);
    }

    [Test]
    public void MartPrefab_UsesCoffeeRouter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        QuestProgressInteractionBridge bridge = prefab.GetComponent<QuestProgressInteractionBridge>();
        Assert.That(bridge, Is.Not.Null);
        Assert.That(bridge.InteractionRouter, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(bridge.InteractionRouter),
            Is.EqualTo("Assets/Scripts/NPC/Interaction/Loomville/Mart/Mart_InteractionRouter.asset"));
    }

    [Test]
    public void LoomvilleScene_AuthorsShopAndExitGates()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject doorTrigger = Find(scene, CoffeeMartGateAuthoring.DoorTriggerObjectName);
            GameObject transition = Find(scene, CoffeeMartGateAuthoring.TransitionObjectName);
            GameObject doorClosed = Find(scene, CoffeeMartGateAuthoring.DoorClosedTriggerName);
            GameObject exitTrigger = Find(scene, CoffeeMartGateAuthoring.ExitTriggerName);
            GameObject redirect = Find(scene, CoffeeMartGateAuthoring.ReynardRedirectName);
            Assert.That(doorTrigger, Is.Not.Null);
            Assert.That(transition, Is.Not.Null);
            Assert.That(doorClosed, Is.Not.Null);
            Assert.That(exitTrigger, Is.Not.Null);
            Assert.That(redirect, Is.Not.Null);
            Assert.That(doorTrigger.activeSelf, Is.False);
            Assert.That(transition.activeSelf, Is.False);
            Assert.That(doorClosed.GetComponent<SequenceTrigger>(), Is.Not.Null);
            Assert.That(exitTrigger.GetComponent<SequenceTrigger>(), Is.Not.Null);
            Assert.That(redirect.GetComponent<NarrativeRedirectZone>(), Is.Not.Null);
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GameplaySceneFilter_SkipsMenuAndBootstrap()
    {
        Assert.That(SequenceCatalogAutoStart.IsGameplayScene("Loomville"), Is.True);
        Assert.That(SequenceCatalogAutoStart.IsGameplayScene("main_menu"), Is.False);
        Assert.That(SequenceCatalogAutoStart.IsGameplayScene(BootstrapStartupController.BootstrapSceneId), Is.False);
    }

    [Test]
    public void LoomvilleMart_LeftoverCratesUseWorldPickupGrant()
    {
        const string martScenePath = "Assets/Loomville_Mart.unity";
        Scene scene = SceneManager.GetSceneByPath(martScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(martScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject patchCrate = Find(scene, MartLeftoverCrateAuthoring.PatchCrateName);
            GameObject orbCrate = Find(scene, MartLeftoverCrateAuthoring.OrbCrateName);
            Assert.That(patchCrate, Is.Not.Null);
            Assert.That(orbCrate, Is.Not.Null);

            AssertLeftoverCrate(
                patchCrate,
                MartLeftoverCrateAuthoring.PatchSpawnId,
                MartLeftoverCrateAuthoring.PatchItemId);
            AssertLeftoverCrate(
                orbCrate,
                MartLeftoverCrateAuthoring.OrbSpawnId,
                MartLeftoverCrateAuthoring.OrbItemId);
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssertLeftoverCrate(GameObject crate, string spawnId, string itemId)
    {
        WorldPickupGrant grant = crate.GetComponentInChildren<WorldPickupGrant>(true);
        Assert.That(grant, Is.Not.Null, crate.name);
        Assert.That(grant.PresenceId, Is.EqualTo(spawnId));
        Assert.That(grant.ItemId, Is.EqualTo(itemId));
        Assert.That(grant.Quantity, Is.EqualTo(1));
        Assert.That(grant.GetComponent<SoundEventPlayer>(), Is.Null);

        var grantSerialized = new SerializedObject(grant);
        Assert.That(
            grantSerialized.FindProperty("spentPresentation").enumValueIndex,
            Is.EqualTo((int)WorldPickupSpentPresentation.HideInteractableOnly));
        Assert.That(grantSerialized.FindProperty("obtainPanelDelaySeconds").floatValue, Is.EqualTo(0f));
        Assert.That(grantSerialized.FindProperty("requireInventoryGrant").boolValue, Is.True);
        Assert.That(grant.gameObject.activeSelf, Is.False);

        WorldPresenceBinding claimBinding = grant.GetComponent<WorldPresenceBinding>();
        Assert.That(claimBinding, Is.Not.Null);
        Assert.That(claimBinding.PresenceId, Is.EqualTo(spawnId));
        Assert.That(claimBinding.ActiveWhenTrue, Is.False);

        Transform interactable = grant.transform.Find("Interactable");
        Assert.That(interactable, Is.Not.Null);
        Assert.That(interactable.GetComponent<InteractableObject>(), Is.Not.Null);
        Assert.That(claimBinding.Target, Is.EqualTo(interactable.gameObject));

        Transform leftoverGate = crate.transform.Find(MartLeftoverCrateAuthoring.LeftoverGateName);
        Assert.That(leftoverGate, Is.Not.Null);
        WorldPresenceBinding leftoverBinding = leftoverGate.GetComponent<WorldPresenceBinding>();
        Assert.That(leftoverBinding, Is.Not.Null);
        Assert.That(leftoverBinding.PresenceId, Is.EqualTo(CoffeeMartGateAuthoring.OpenFlag));
        Assert.That(leftoverBinding.ActiveWhenTrue, Is.True);
        Assert.That(leftoverBinding.Target, Is.EqualTo(grant.gameObject));
    }

    private static void AssertDirectionalCount(
        SerializedObject visual,
        string sequenceProperty,
        int expectedCount)
    {
        string[] directions = { "down", "left", "right", "up" };
        foreach (string direction in directions)
        {
            int count = visual.FindProperty(sequenceProperty)
                .FindPropertyRelative(direction)
                .FindPropertyRelative("sprites")
                .arraySize;
            Assert.That(count, Is.EqualTo(expectedCount), $"{sequenceProperty}.{direction}");
        }
    }

    [Test]
    public void PlayerRoom_AuthorsGatedChainlingPcTerminal()
    {
        const string scenePath = "Assets/Scenes/player_room.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            GameObject monitor = Find(scene, ChainlingPcTerminalIds.PlayerRoomMonitorName);
            GameObject pc = Find(scene, ChainlingPcTerminalIds.PlayerRoomPcObjectName);
            GameObject introGate = Find(scene, ChainlingPcTerminalIds.IntroGateName);
            GameObject pcGate = Find(scene, ChainlingPcTerminalIds.PcGateName);
            Assert.That(monitor, Is.Not.Null);
            Assert.That(pc, Is.Not.Null);
            Assert.That(introGate, Is.Not.Null);
            Assert.That(pcGate, Is.Not.Null);
            Assert.That(monitor.GetComponent<QuestProgressInteractionBridge>(), Is.Not.Null);

            ChainlingPcTerminalInteractable binding = pc.GetComponent<ChainlingPcTerminalInteractable>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.TerminalId, Is.EqualTo(ChainlingPcTerminalIds.PlayerRoomTerminalId));
            Assert.That(binding.ScreenPrefab, Is.Not.Null);
            Assert.That(pc.GetComponent<InteractableObject>(), Is.Not.Null);
            Assert.That(pc.activeSelf, Is.False);
            Assert.That(monitor.GetComponent<ChainlingPcTerminalInteractable>(), Is.Null);

            WorldPresenceBinding introBinding = introGate.GetComponent<WorldPresenceBinding>();
            WorldPresenceBinding pcBinding = pcGate.GetComponent<WorldPresenceBinding>();
            Assert.That(introBinding, Is.Not.Null);
            Assert.That(pcBinding, Is.Not.Null);
            Assert.That(introBinding.PresenceId, Is.EqualTo(ChainlingPcTerminalIds.UnlockFlagKey));
            Assert.That(pcBinding.PresenceId, Is.EqualTo(ChainlingPcTerminalIds.UnlockFlagKey));
            Assert.That(introBinding.ActiveWhenTrue, Is.False);
            Assert.That(pcBinding.ActiveWhenTrue, Is.True);
            Assert.That(introBinding.Target.GetInstanceID(), Is.EqualTo(monitor.GetInstanceID()));
            Assert.That(pcBinding.Target.GetInstanceID(), Is.EqualTo(pc.GetInstanceID()));
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ChainlingPcPrefab_IsAuthoredView()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChainlingPcTerminalAuthoring.ScreenPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        ChainlingPcTerminalScreen screen = prefab.GetComponent<ChainlingPcTerminalScreen>();
        Assert.That(screen, Is.Not.Null);
        Canvas canvas = prefab.GetComponent<Canvas>();
        Assert.That(canvas, Is.Not.Null);
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
        Assert.That(scaler, Is.Not.Null);
        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(prefab.transform.Find("SafeArea"), Is.Not.Null);
        Assert.That(prefab.transform.Find("SafeArea/TerminalBoot/MenuPanel/TopLeft/StorageScroll"), Is.Not.Null);
        Transform partySlots = prefab.transform.Find("SafeArea/TerminalBoot/MenuPanel/TopCenter/PartySlots");
        Assert.That(partySlots, Is.Not.Null);
        Assert.That(partySlots.childCount, Is.EqualTo(4));
        Transform template = partySlots.Find("PartySlotTemplate");
        Assert.That(template, Is.Not.Null);
        Assert.That(template.gameObject.activeSelf, Is.False);
        Assert.That(template.GetComponent<InventoryItemUseTargetSlot>(), Is.Not.Null);
        int liveSlots = 0;
        for (int i = 0; i < partySlots.childCount; i++)
        {
            Transform child = partySlots.GetChild(i);
            Assert.That(child.GetComponent<InventoryItemUseTargetSlot>(), Is.Not.Null);
            if (child.gameObject.activeSelf)
                liveSlots++;
        }
        Assert.That(liveSlots, Is.EqualTo(3));
        Assert.That(prefab.transform.Find("SafeArea/ActionSheet"), Is.Not.Null);
        Assert.That(prefab.transform.Find("SafeArea/ActionSheet/Panel/Actions/MoveToParty"), Is.Not.Null);
        Assert.That(prefab.transform.Find("SafeArea/ActionSheet/Panel/Actions/SendToStorage"), Is.Not.Null);
        Assert.That(prefab.transform.Find("SafeArea/ActionSheet/Panel/Actions/Manage"), Is.Not.Null);
        Assert.That(prefab.transform.Find("SafeArea/ActionSheet/Panel/Actions/Release"), Is.Not.Null);
        Assert.That(prefab.transform.Find("SafeArea/ActionSheet/Panel/Confirm"), Is.Not.Null);
        SerializedObject serialized = new SerializedObject(screen);
        Assert.That(serialized.FindProperty("detailPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("partyRowPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.transform.Find("SafeArea/TerminalBoot/MenuPanel/TopRight/Close"), Is.Not.Null);
        Assert.That(prefab.transform.Find("SafeArea/TerminalBoot/MenuPanel/DialogueLowerCenter/Status"), Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<PlayerMenuBackgroundController>(true), Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<PlayerMenuBootSequenceController>(true), Is.Not.Null);
    }

    [Test]
    public void ChainlingPcRoster_LastPartyMemberCannotBeStored()
    {
        Assert.That(ChainlingPcTerminalScreen.CanSendPartyMemberToStorage(1), Is.False);
        Assert.That(ChainlingPcTerminalScreen.CanSendPartyMemberToStorage(2), Is.True);
        Assert.That(ChainlingPcTerminalScreen.CanSendPartyMemberToStorage(3), Is.True);
    }

    [Test]
    public void ChainlingPcRoster_PartyMemberCannotBeReleased()
    {
        Assert.That(ChainlingPcTerminalScreen.CanReleaseChainling(true), Is.False);
        Assert.That(ChainlingPcTerminalScreen.CanReleaseChainling(false), Is.True);
    }

    private static bool TrackSetsFlag(string sequencePath, string flagKey)
    {
        SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(sequencePath);
        Assert.That(sequence, Is.Not.Null, sequencePath);
        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) is Step_SetProfileWorldFlag flag &&
                string.Equals(flag.FlagKey, flagKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject Find(Scene scene, string objectName)
    {
        GameObject namedWithBridge = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (!string.Equals(current.name, objectName, StringComparison.Ordinal))
                    continue;
                if (current.GetComponent<QuestProgressInteractionBridge>() != null ||
                    current.GetComponent<ChainlingPcTerminalInteractable>() != null ||
                    current.GetComponent<WorldPresenceBinding>() != null)
                {
                    return current.gameObject;
                }

                if (namedWithBridge == null)
                    namedWithBridge = current.gameObject;
            }
        }

        return namedWithBridge;
    }
}
#endif
