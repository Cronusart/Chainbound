using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayModePolicyTests
{
    [Test]
    public void GameplayMenuAvailability_AllAndPlayerMenuLocksSuppressMenu()
    {
        GameObject lockObject = new GameObject("InputLockService_MenuPolicyTest");
        GameObject gateObject = new GameObject("GameplayMenuAvailability_MenuPolicyTest");

        try
        {
            InputLockService inputLocks = lockObject.AddComponent<InputLockService>();
            GameplayMenuAvailabilityService gate = gateObject.AddComponent<GameplayMenuAvailabilityService>();
            NarrativeTestUtility.SetPrivateField(gate, "requireIntroComplete", false);

            Assert.That(gate.CanOpen, Is.True);

            InputLockHandle movement = inputLocks.AcquireLock("movement", InputLockChannel.Movement);
            Assert.That(gate.CanOpen, Is.True);
            movement.Release();

            InputLockHandle interaction = inputLocks.AcquireLock("interaction", InputLockChannel.Interaction);
            Assert.That(gate.CanOpen, Is.True);
            interaction.Release();

            InputLockHandle all = inputLocks.AcquireLock("narrative", InputLockChannel.All);
            Assert.That(gate.CanOpen, Is.False);
            all.Release();

            InputLockHandle playerMenu = inputLocks.AcquireLock("menu-only", InputLockChannel.PlayerMenu);
            Assert.That(gate.CanOpen, Is.False);
            playerMenu.Release();

            Assert.That(gate.CanOpen, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(gateObject);
            Object.DestroyImmediate(lockObject);
        }
    }

    [Test]
    public void CombatScenePolicy_BlocksMenuAndDeactivatesOwnedPlayerRoot()
    {
        Scene arenaScene = SceneManager.CreateScene("ArenaPolicyTest");
        Scene overworldScene = SceneManager.CreateScene("OverworldPolicyTest");
        GameObject arenaRoot = new GameObject("ArenaRoot");
        GameObject playerRoot = new GameObject("PlayerRoot_PolicyTest");
        GameObject spawnServiceObject = new GameObject("SpawnService_PolicyTest");

        try
        {
            SceneManager.MoveGameObjectToScene(arenaRoot, arenaScene);
            arenaRoot.AddComponent<CombatBattleController>();

            Assert.That(GameplayMenuScenePolicy.BlocksPlayerMenu(arenaScene), Is.True);

            SpawnService spawnService = spawnServiceObject.AddComponent<SpawnService>();
            NarrativeTestUtility.SetPrivateField(spawnService, "playerObject", playerRoot);
            NarrativeTestUtility.SetPrivateField(spawnService, "applyCombatScenePlayerRestrictions", true);
            NarrativeTestUtility.SetPrivateField(spawnService, "deactivatePlayerRootInCombatScenes", true);

            spawnService.RefreshCombatSceneRestrictions(arenaScene);
            Assert.That(spawnService.IsCombatSceneActive, Is.True);
            Assert.That(playerRoot.activeSelf, Is.False);

            spawnService.RefreshCombatSceneRestrictions(overworldScene);
            Assert.That(spawnService.IsCombatSceneActive, Is.False);
            Assert.That(playerRoot.activeSelf, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(spawnServiceObject);
            Object.DestroyImmediate(playerRoot);
            Object.DestroyImmediate(arenaRoot);

            if (arenaScene.IsValid() && arenaScene.isLoaded)
                EditorSceneManager.CloseScene(arenaScene, true);
            if (overworldScene.IsValid() && overworldScene.isLoaded)
                EditorSceneManager.CloseScene(overworldScene, true);
        }
    }
}
