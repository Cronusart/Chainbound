using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class SceneLoadingPipelineTests
{
    private sealed class RejectingBackend : ISceneOperationBackend
    {
        public bool BeginCalled { get; private set; }

        public bool CanLoad(string sceneId) => false;

        public bool TryBeginLoad(string sceneId, out ISceneLoadOperation operation, out string error)
        {
            BeginCalled = true;
            operation = null;
            error = "unexpected";
            return false;
        }

        public bool TryBeginLoadAdditive(string sceneId, out ISceneLoadOperation operation, out string error)
        {
            operation = null;
            error = "unexpected";
            return false;
        }

        public bool TryBeginUnload(UnityEngine.SceneManagement.Scene scene, out ISceneOperation operation, out string error)
        {
            operation = null;
            error = "unexpected";
            return false;
        }

        public bool SetActiveScene(UnityEngine.SceneManagement.Scene scene, out string error)
        {
            error = "unexpected";
            return false;
        }
    }

    [Test]
    public void RuntimeDefault_UsesProductionLeverValues()
    {
        SceneLoadingConfigSO config = SceneLoadingConfigSO.CreateRuntimeDefault();
        try
        {
            Assert.That(config.StartupSceneId, Is.EqualTo("main_menu"));
            Assert.That(config.StartupStyle, Is.EqualTo(TransitionStyle.FullLoading));
            Assert.That(config.RecoverySceneId, Is.EqualTo("main_menu"));
            Assert.That(config.ResolveStyle("Loomville"), Is.EqualTo(TransitionStyle.FullLoading));
            Assert.That(config.ResolveStyle("Link1"), Is.EqualTo(TransitionStyle.QuickFade));
            Assert.That(config.OverworldStreamingConfig, Is.Not.Null);
            Assert.That(config.OverworldStreamingConfig.ContainsScene("Loomville"), Is.True);
            Assert.That(config.OverworldStreamingConfig.ContainsScene("Link1"), Is.True);
            Assert.That(config.ResolveStyle("player_room"), Is.EqualTo(TransitionStyle.QuickFade));
            Assert.That(config.GetMinimumDisplaySeconds(TransitionStyle.FullLoading), Is.EqualTo(2.75f));
            Assert.That(config.GetMinimumDisplaySeconds(TransitionStyle.QuickFade), Is.EqualTo(0.8f));
            Assert.That(config.WarmupTaskTimeoutSeconds, Is.EqualTo(10f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ExplicitStyleOverride_WinsOverDestinationRule()
    {
        SceneLoadingConfigSO config = SceneLoadingConfigSO.CreateRuntimeDefault();
        try
        {
            Assert.That(
                config.ResolveStyle("Loomville", TransitionStyle.QuickFade),
                Is.EqualTo(TransitionStyle.QuickFade));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Validator_ReportsEmptyAndDuplicateDestinationRules()
    {
        SceneLoadingConfigSO config = SceneLoadingConfigSO.CreateRuntimeDefault();
        try
        {
            SerializedObject serialized = new SerializedObject(config);
            SerializedProperty rules = serialized.FindProperty("destinationRules");
            rules.arraySize = 4;
            SetRule(rules.GetArrayElementAtIndex(0), "main_menu", TransitionStyle.FullLoading);
            SetRule(rules.GetArrayElementAtIndex(1), "Loomville", TransitionStyle.FullLoading);
            SetRule(rules.GetArrayElementAtIndex(2), "Loomville", TransitionStyle.QuickFade);
            SetRule(rules.GetArrayElementAtIndex(3), string.Empty, TransitionStyle.QuickFade);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            HashSet<string> buildScenes = new HashSet<string>
            {
                "main_menu",
                "Loomville",
            };
            List<string> errors = new List<string>();
            SceneLoadingConfigValidator.ValidateConfig(config, "test", buildScenes, errors);

            Assert.That(errors.Exists(message => message.Contains("duplicated")), Is.True);
            Assert.That(errors.Exists(message => message.Contains("empty scene id")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Validator_RequiresSetMembersInBuildSettings_ButNotAsFullLoadingDestinations()
    {
        SceneLoadingConfigSO config = SceneLoadingConfigSO.CreateRuntimeDefault();
        try
        {
            HashSet<string> buildScenes = new HashSet<string>
            {
                "main_menu",
                "Loomville",
                "Link1",
            };
            List<string> errors = new List<string>();
            SceneLoadingConfigValidator.ValidateConfig(config, "test", buildScenes, errors);

            Assert.That(errors.Exists(message => message.Contains("Link1") && message.Contains("FullLoading")), Is.False);
            Assert.That(errors.Exists(message => message.Contains("overworld resident scene") && message.Contains("Link1")), Is.False);
            Assert.That(errors.Exists(message => message.Contains("Jungle")), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(config.OverworldStreamingConfig);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void OverworldStreamingConfig_RejectsDuplicateChunkIds()
    {
        OverworldStreamingConfigSO config = OverworldStreamingConfigSO.CreateRuntimeDefault();
        try
        {
            SerializedObject serialized = new SerializedObject(config);
            SerializedProperty chunks = serialized.FindProperty("orderedChunks");
            chunks.arraySize = 3;
            chunks.GetArrayElementAtIndex(0).FindPropertyRelative("sceneId").stringValue = "Loomville";
            chunks.GetArrayElementAtIndex(1).FindPropertyRelative("sceneId").stringValue = "Link1";
            chunks.GetArrayElementAtIndex(2).FindPropertyRelative("sceneId").stringValue = "Link1";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            List<string> errors = config.Validate();
            Assert.That(errors.Exists(message => message.Contains("duplicated")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void OverworldStreamingConfig_RejectsUnknownNeighborIds()
    {
        OverworldStreamingConfigSO config = OverworldStreamingConfigSO.CreateRuntimeDefault();
        try
        {
            SerializedObject serialized = new SerializedObject(config);
            SerializedProperty chunks = serialized.FindProperty("orderedChunks");
            chunks.GetArrayElementAtIndex(1).FindPropertyRelative("nextChunkId").stringValue = "Jungle";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            List<string> errors = config.Validate();
            Assert.That(errors.Exists(message => message.Contains("nextChunkId") && message.Contains("Jungle")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Validator_AllowsFutureChunksMissingFromBuildSettings()
    {
        SceneLoadingConfigSO config = SceneLoadingConfigSO.CreateRuntimeDefault();
        try
        {
            SerializedObject serialized = new SerializedObject(config.OverworldStreamingConfig);
            SerializedProperty chunks = serialized.FindProperty("orderedChunks");
            chunks.arraySize = 3;
            chunks.GetArrayElementAtIndex(0).FindPropertyRelative("sceneId").stringValue = "Loomville";
            chunks.GetArrayElementAtIndex(0).FindPropertyRelative("preloadOnSetEntry").boolValue = true;
            chunks.GetArrayElementAtIndex(1).FindPropertyRelative("sceneId").stringValue = "Link1";
            chunks.GetArrayElementAtIndex(1).FindPropertyRelative("preloadOnSetEntry").boolValue = true;
            chunks.GetArrayElementAtIndex(1).FindPropertyRelative("nextChunkId").stringValue = "Jungle";
            chunks.GetArrayElementAtIndex(2).FindPropertyRelative("sceneId").stringValue = "Jungle";
            chunks.GetArrayElementAtIndex(2).FindPropertyRelative("preloadOnSetEntry").boolValue = false;
            chunks.GetArrayElementAtIndex(2).FindPropertyRelative("previousChunkId").stringValue = "Link1";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            HashSet<string> buildScenes = new HashSet<string>
            {
                "main_menu",
                "Loomville",
                "Link1",
            };
            List<string> errors = new List<string>();
            SceneLoadingConfigValidator.ValidateConfig(config, "test", buildScenes, errors);

            Assert.That(errors.Exists(message => message.Contains("Jungle")), Is.False);
            Assert.That(config.OverworldStreamingConfig.ContainsScene("Jungle"), Is.True);
            Assert.That(config.OverworldStreamingConfig.GetEntryResidentSceneIds().Contains("Jungle"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(config.OverworldStreamingConfig);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void PreferredChunk_LoadsOnlyAuthoredChunk_NotFullPreloadSet()
    {
        OverworldStreamingConfigSO config = OverworldStreamingConfigSO.CreateRuntimeDefault();
        try
        {
            SerializedObject serialized = new SerializedObject(config);
            SerializedProperty chunks = serialized.FindProperty("orderedChunks");
            chunks.arraySize = 3;
            chunks.GetArrayElementAtIndex(0).FindPropertyRelative("sceneId").stringValue = "Loomville";
            chunks.GetArrayElementAtIndex(0).FindPropertyRelative("preloadOnSetEntry").boolValue = true;
            chunks.GetArrayElementAtIndex(1).FindPropertyRelative("sceneId").stringValue = "Link1";
            chunks.GetArrayElementAtIndex(1).FindPropertyRelative("preloadOnSetEntry").boolValue = true;
            chunks.GetArrayElementAtIndex(2).FindPropertyRelative("sceneId").stringValue = "Jungle";
            chunks.GetArrayElementAtIndex(2).FindPropertyRelative("preloadOnSetEntry").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            List<string> preload = config.GetEntryResidentSceneIds();
            Assert.That(preload, Does.Contain("Loomville"));
            Assert.That(preload, Does.Contain("Link1"));
            Assert.That(preload, Does.Not.Contain("Jungle"));

            List<string> jungleOnly = config.GetResidentSceneIdsForPreferredChunks(new[] { "Jungle" });
            Assert.That(jungleOnly, Is.EquivalentTo(new[] { "Jungle" }));

            List<string> jungleTrailhead = config.GetResidentSceneIdsForPreferredChunks(new[] { "Jungle", "Link1" });
            Assert.That(jungleTrailhead, Is.EquivalentTo(new[] { "Jungle", "Link1" }));
            Assert.That(jungleTrailhead, Does.Not.Contain("Loomville"));

            List<string> link = config.GetResidentSceneIdsForPreferredChunks(new[] { "Link1" });
            Assert.That(link, Is.EquivalentTo(new[] { "Link1" }));

            List<string> unknown = config.GetResidentSceneIdsForPreferredChunks(new string[0]);
            Assert.That(unknown, Is.EquivalentTo(preload));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void PreferredChunk_LivePortalOverridesStaleSavedSnapshot()
    {
        GameObject host = new GameObject("SanctuaryPortal_JungleTrailhead_Test");
        OverworldStreamingConfigSO config = OverworldStreamingConfigSO.CreateRuntimeDefault();
        try
        {
            host.AddComponent<BoxCollider>().isTrigger = true;
            SanctuaryLatchZone zone = host.AddComponent<SanctuaryLatchZone>();
            SerializedObject serialized = new SerializedObject(zone);
            serialized.FindProperty("kind").intValue = (int)SanctuaryLatchKind.Portal;
            serialized.FindProperty("sceneId").stringValue = "Jungle";
            serialized.FindProperty("spawnId").stringValue = "sanctuary_jungle_trailhead";
            SerializedProperty chunks = serialized.FindProperty("residentChunkSceneIds");
            chunks.arraySize = 2;
            chunks.GetArrayElementAtIndex(0).stringValue = "Jungle";
            chunks.GetArrayElementAtIndex(1).stringValue = "Link1";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string[] resolved = SanctuaryLatchZone.ResolvePreferredResidentChunkSceneIds(
                "Jungle",
                "sanctuary_jungle_trailhead",
                new[] { "Jungle" });
            Assert.That(resolved, Is.EquivalentTo(new[] { "Jungle", "Link1" }));

            SerializedObject streaming = new SerializedObject(config);
            SerializedProperty ordered = streaming.FindProperty("orderedChunks");
            ordered.arraySize = 3;
            ordered.GetArrayElementAtIndex(0).FindPropertyRelative("sceneId").stringValue = "Loomville";
            ordered.GetArrayElementAtIndex(0).FindPropertyRelative("preloadOnSetEntry").boolValue = true;
            ordered.GetArrayElementAtIndex(1).FindPropertyRelative("sceneId").stringValue = "Link1";
            ordered.GetArrayElementAtIndex(1).FindPropertyRelative("preloadOnSetEntry").boolValue = true;
            ordered.GetArrayElementAtIndex(2).FindPropertyRelative("sceneId").stringValue = "Jungle";
            ordered.GetArrayElementAtIndex(2).FindPropertyRelative("preloadOnSetEntry").boolValue = false;
            streaming.ApplyModifiedPropertiesWithoutUndo();

            List<string> resident = config.GetResidentSceneIdsForPreferredChunks(resolved);
            Assert.That(resident, Is.EquivalentTo(new[] { "Jungle", "Link1" }));
            Assert.That(resident, Does.Not.Contain("Loomville"));

            string[] savedWhenPortalMissing = SanctuaryLatchZone.ResolvePreferredResidentChunkSceneIds(
                "Jungle",
                "missing_spawn",
                new[] { "Jungle", "Link1" });
            Assert.That(savedWhenPortalMissing, Is.EquivalentTo(new[] { "Jungle", "Link1" }));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void SanctuarySnapshot_JsonUtility_RoundTripsResidentChunkArray()
    {
        var snapshot = new SanctuaryStateSnapshot
        {
            lastPortal = new SanctuaryPortalRecord
            {
                sceneId = "Jungle",
                spawnId = "sanctuary_jungle_trailhead",
                overworldChunkSceneId = "Jungle",
                residentChunkSceneIds = new[] { "Jungle", "Link1" }
            },
            lastLatch = new SanctuaryLatchRecord
            {
                kind = SanctuaryLatchKind.Portal,
                sceneId = "Jungle",
                spawnId = "sanctuary_jungle_trailhead",
                overworldChunkSceneId = "Jungle",
                residentChunkSceneIds = new[] { "Jungle", "Link1" }
            }
        };

        SanctuaryStateSnapshot loaded = JsonUtility.FromJson<SanctuaryStateSnapshot>(JsonUtility.ToJson(snapshot));
        Assert.That(loaded.lastPortal.residentChunkSceneIds, Is.EquivalentTo(new[] { "Jungle", "Link1" }));
        Assert.That(loaded.lastLatch.residentChunkSceneIds, Is.EquivalentTo(new[] { "Jungle", "Link1" }));
    }

    [Test]
    public void EmptyTransitionRequest_IsRejectedWithStructuredResult()
    {
        GameObject host = new GameObject("SceneLoader Test Host");
        try
        {
            SceneLoader loader = host.AddComponent<SceneLoader>();
            SceneTransitionResult result = default;
            bool callbackCalled = false;

            bool accepted = loader.RequestTransition(new SceneTransitionRequest(
                string.Empty,
                completed: completed =>
                {
                    result = completed;
                    callbackCalled = true;
                }));

            Assert.That(accepted, Is.False);
            Assert.That(callbackCalled, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(SceneTransitionOutcome.Invalid));
            Assert.That(loader.IsTransitioning, Is.False);
            Assert.That(loader.CurrentStatus.Phase, Is.EqualTo(SceneTransitionPhase.Idle));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BackendValidation_RejectsUnavailableSceneBeforeStartingIo()
    {
        GameObject host = new GameObject("SceneLoader Backend Test Host");
        try
        {
            SceneLoader loader = host.AddComponent<SceneLoader>();
            RejectingBackend backend = new RejectingBackend();
            loader.SetOperationBackend(backend);
            SceneTransitionResult result = default;

            bool accepted = loader.RequestTransition(new SceneTransitionRequest(
                "UnavailableScene",
                completed: completed => result = completed));

            Assert.That(accepted, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(SceneTransitionOutcome.Invalid));
            Assert.That(backend.BeginCalled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void ProjectWiring_PassesLoadingValidation()
    {
        List<string> errors = SceneLoadingConfigValidator.ValidateProject();
        Assert.That(errors, Is.Empty, string.Join("\n", errors));
    }

    [Test]
    public void LoadingPrefab_StartsCoveredAtUnitScale()
    {
        const string path = "Assets/Prefabs/UI/LoadingScreenUI/LoadingScreenUI.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            LoadingScreenUI ui = root.GetComponentInChildren<LoadingScreenUI>(true);
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.transform.localScale, Is.EqualTo(Vector3.one));
            SerializedObject serialized = new SerializedObject(ui);
            Assert.That(serialized.FindProperty("startCoveredOnAwake").boolValue, Is.True);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetRule(
        SerializedProperty property,
        string sceneId,
        TransitionStyle style)
    {
        property.FindPropertyRelative("sceneId").stringValue = sceneId;
        property.FindPropertyRelative("style").enumValueIndex = (int)style;
    }
}
