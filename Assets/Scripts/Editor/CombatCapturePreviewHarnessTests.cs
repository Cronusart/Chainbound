#if UNITY_EDITOR
using System.Collections.Generic;
using Chainbound.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CombatCapturePreviewHarnessTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            if (_created[i] != null)
                Object.DestroyImmediate(_created[i]);
        }
        _created.Clear();
    }

    [Test]
    public void HarnessScene_HasEnabledCapturePreview()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/Combat2_TestHarness.unity",
            OpenSceneMode.Additive);
        try
        {
            CombatCapturePreviewHarness harness = null;
            CombatCapturePreviewHarness[] found = Object.FindObjectsByType<CombatCapturePreviewHarness>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].gameObject.scene == scene)
                {
                    harness = found[i];
                    break;
                }
            }

            Assert.That(harness, Is.Not.Null);
            Assert.That(harness.enabled, Is.True);
            Assert.That(harness.GetComponent<CombatBattleController>(), Is.Not.Null);
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void PreviewCaptureTurn_LeavesTheWildUntouched()
    {
        CombatResolver resolver = CreateResolver();
        ChainlingInstance recorder = CreateChainling("recorder", speed: 5);
        ChainlingInstance partner = CreateChainling("partner", speed: 90);
        ChainlingInstance wild = CreateChainling("wild", maxHp: 80, speed: 70);
        CombatState state = resolver.InitializeBattle(CreateTeam(0, recorder, partner), CreateTeam(1, wild));
        int wildHp = wild.currentHP;

        resolver.BeginTurn(state);
        List<CombatAction> player = CombatCapturePreviewActions.BuildPlayerCaptureTurn(state);
        List<CombatAction> opponent = CombatCapturePreviewActions.BuildOpponentPasses(state);
        List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(state, player, opponent);
        for (int i = 0; i < queue.Count; i++)
            resolver.ExecuteActionStep(state, queue[i]);

        Assert.That(player[0].actionType, Is.EqualTo(CombatActionType.Capture));
        Assert.That(player[1].actionType, Is.EqualTo(CombatActionType.Pass));
        Assert.That(opponent.TrueForAll(action => action.actionType == CombatActionType.Pass), Is.True);
        Assert.That(wild.currentHP, Is.EqualTo(wildHp));
        Assert.That(state.pendingCaptureAttempt, Is.Not.Null);
        Assert.That(state.pendingCaptureAttempt.targetInstanceId, Is.EqualTo(wild.instanceId));
    }

    private CombatResolver CreateResolver()
    {
        var matrix = ScriptableObject.CreateInstance<TypeChemistryMatrixSO>();
        var catalog = ScriptableObject.CreateInstance<StatusCatalogSO>();
        _created.Add(matrix);
        _created.Add(catalog);
        catalog.BuildLookup();
        return new CombatResolver(matrix, catalog, 17, true);
    }

    private static ChainlingInstance CreateChainling(string id, int maxHp = 100, int speed = 20)
    {
        var chainling = new ChainlingInstance
        {
            instanceId = id,
            speciesId = id,
            displayName = id,
            stars = 1,
            currentAP = 10,
            baseMaxHP = maxHp,
            currentHP = maxHp,
            baseMeleeAtk = 80,
            baseMeleeDef = 40,
            baseRangedAtk = 80,
            baseRangedDef = 40,
            baseSpeed = speed,
            maxSlots = 4,
            isAlive = true,
        };
        chainling.SyncDisplayVitals();
        return chainling;
    }

    private static TeamState CreateTeam(int teamIndex, params ChainlingInstance[] chainlings)
    {
        var team = new TeamState(teamIndex, "Team " + teamIndex);
        team.chainlings.AddRange(chainlings);
        return team;
    }
}
#endif
