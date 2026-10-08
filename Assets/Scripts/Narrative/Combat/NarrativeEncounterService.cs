using System;
using System.Collections;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Real encounter service used by narrative sequences. Resolves a configured manual encounter,
    /// launches the combat scene, then waits for battle completion before resuming the sequence.
    /// </summary>
    public class NarrativeEncounterService : MonoBehaviour, IEncounterService
    {
        [Serializable]
        public class EncounterRegistryEntry
        {
            public string encounterId;
            public ManualEncounterProviderSO encounterProvider;
            [Tooltip("Optional narrative-only combat launch overrides copied into a transient bridge before scene load.")]
            public NarrativeCombatLaunchProfileSO combatLaunchProfile;
            public DialogueSequenceSO preEncounterDialogue;
            [Tooltip(
                "Return / post-battle dialogue: played after combat ends and the player is back in the overworld " +
                "(or when the narrative flow resumes). Use for 'what happened' lines after the encounter. " +
                "When using SceneLoader for return, playback is deferred until after the load pipeline finishes " +
                "(fade + transition hold), preserving deterministic input-lock ownership.")]
            public DialogueSequenceSO postEncounterDialogue;
            [Tooltip(
                "When enabled (default), acquires InputLockService during postEncounterDialogue so the player " +
                "cannot move or interact until lines finish. Maps to DialogueService.PlayDialogue(..., lockInput). " +
                "Disable only if you need ambient gameplay during return dialogue.")]
            public bool lockInputDuringPostEncounterDialogue = true;

            [Tooltip("Scene load, transition, and optional timeout overrides for this encounter.")]
            public EncounterBattleLoadSettings battleLoad = new EncounterBattleLoadSettings();

            [Tooltip(
                "Optional SpawnPoint id in this overworld scene for post-battle return (authored +Y). " +
                "When set, post-battle prefers SpawnService at this id over raw snapshot teleport.")]
            [SerializeField] private string returnSpawnPointId = "";

            [SerializeField, HideInInspector, FormerlySerializedAs("battleSceneOverride")]
            private string legacyBattleSceneOverride = "";

            /// <summary>Migrated from former <c>battleSceneOverride</c> field; prefer <see cref="battleLoad"/>.</summary>
            public string LegacyBattleSceneOverride => legacyBattleSceneOverride;

            public string ReturnSpawnPointId => returnSpawnPointId;
        }

        [Header("Encounter Registry")]
        [SerializeField] private List<EncounterRegistryEntry> registry = new List<EncounterRegistryEntry>();

        [Header("Battle scene (defaults)")]
        [Tooltip("Used when a registry entry leaves Battle Scene Name empty.")]
        [SerializeField] private string battleSceneName = "Combat2_TestHarness";
        [SerializeField] private bool useSceneLoader = true;
        [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.QuickFade;

        [Header("Timeouts")]
        [SerializeField] private float sceneLoadTimeoutSeconds = 12f;
        [SerializeField] private float battleControllerTimeoutSeconds = 12f;
        [SerializeField] private float returnSceneTimeoutSeconds = 20f;
        [SerializeField] private bool waitForReturnSceneAfterBattle = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("Bootstrap / battle start failure")]
        [Tooltip("Max wait for ICombatSceneBootstrap (e.g. CombatSceneInstaller) after the battle scene loads.")]
        [SerializeField] private float battleBootstrapTimeoutSeconds = 20f;
        [Tooltip("After bootstrap is ready, max wait for CombatBattleController to actually start the battle.")]
        [SerializeField] private float battleStartGraceSeconds = 16f;
        [Tooltip("When return snapshot is missing, load this scene after an aborted encounter.")]
        [SerializeField] private string combatAbortFallbackHomeScene = "player_room";
        [SerializeField] private string combatAbortFallbackHomeSpawnId = "home_bed";

        public EncounterOutcome LastOutcome { get; private set; } = EncounterOutcome.ForcedRun;
        public BattleResult LastBattleResult { get; private set; }

        public IEnumerator StartEncounter(string encounterId, EncounterConfig config)
        {
            LastBattleResult = null;
            LastOutcome = ResolveFallbackOutcome(config);

            EncounterRegistryEntry entry = FindEntry(encounterId);
            if (entry == null || entry.encounterProvider == null)
            {
                Debug.LogError($"[NarrativeEncounterService] Encounter '{encounterId}' is not registered.");
                yield break;
            }

            SolanaServiceBridge solanaBridge = SolanaServiceBridge.Instance != null
                ? SolanaServiceBridge.Instance
                : FindObjectOfType<SolanaServiceBridge>(true);

            if (solanaBridge == null)
            {
                Debug.LogError("[NarrativeEncounterService] SolanaServiceBridge not found. Cannot launch encounter battle.");
                yield break;
            }

            EncounterTransitionBridge transitionBridge = EncounterTransitionBridge.Instance;
            if (transitionBridge == null)
            {
                Debug.LogError("[NarrativeEncounterService] EncounterTransitionBridge not found. Cannot launch encounter battle.");
                yield break;
            }

            NarrativeCombatLaunchBridge launchBridge = null;
            CombatBattleController battleController = null;
            Action<BattleResult> handleBattleEnded = null;
            Action<string> handleBattleStartFailed = null;
            bool launchBridgePublished = false;
            // Scene activation can dispose this enumerator before the battle
            // scene's Awake. Keep the payload until that installer has observed it.
            bool retainPayloadForInstaller = false;

            try
            {
                if (entry.preEncounterDialogue != null)
                    yield return PlayDialogueIfPossible(entry.preEncounterDialogue);

                EncounterBattleLoadSettings load = entry.battleLoad ?? new EncounterBattleLoadSettings();
                string targetScene = load.ResolveBattleSceneName(battleSceneName, entry.LegacyBattleSceneOverride);
                TransitionStyle resolvedTransition = load.ResolveTransitionStyle(transitionStyle);
                bool resolvedUseSceneLoader = load.ResolveUseSceneLoader(useSceneLoader);
                string battleSpawnId = load.battleSpawnId ?? "";
                float resolvedSceneLoadTimeout = load.ResolveSceneLoadTimeout(sceneLoadTimeoutSeconds);
                float resolvedBattleControllerTimeout = load.ResolveBattleControllerTimeout(battleControllerTimeoutSeconds);
                float resolvedReturnTimeout = load.ResolveReturnSceneTimeout(returnSceneTimeoutSeconds);
                bool resolvedWaitForReturn = load.ResolveWaitForReturnScene(waitForReturnSceneAfterBattle);
                string returnSpawnId = ResolveReturnSpawnId(entry, config);
                bool skipSaveReturnContext = config != null && config.skipSaveReturnContext;
                NarrativeCombatLaunchContext launchContext =
                    NarrativeCombatLaunchContext.Create(encounterId, entry.encounterProvider, entry.combatLaunchProfile, config);

                if (skipSaveReturnContext && enableDebugLogs)
                {
                    Debug.Log("[NarrativeEncounterService] EncounterTransitionBridge will preserve the previously saved return snapshot (EncounterConfig.skipSaveReturnContext). ");
                }

                // Narrative encounters now flow through the transient launch bridge instead of the shared manual encounter slot.
                solanaBridge.ClearActiveEncounter();

                // Snapshot scene is saved during the transition (or already saved by a prior wait-for-trigger).
                // Capture before load so a later active-scene flip cannot be mistaken for the return destination.
                string expectedReturnSceneId = ResolveExpectedReturnSceneId(solanaBridge);

                yield return transitionBridge.BeginEncounterTransition(new EncounterTransitionBridge.EncounterTransitionRequest
                {
                    battleSceneName = targetScene,
                    battleSpawnId = battleSpawnId,
                    transitionStyle = resolvedTransition,
                    useSceneLoader = resolvedUseSceneLoader,
                    returnSpawnId = returnSpawnId,
                    skipSaveReturnContext = skipSaveReturnContext,
                    launchSource = EncounterTransitionBridge.EncounterLaunchSource.Narrative,
                    authoritativeNarrativeEncounterId = encounterId,
                    onReadyToLoad = () =>
                    {
                        launchBridge = NarrativeCombatLaunchBridge.GetOrCreate(transform);
                        launchBridge.Publish(launchContext);
                        launchBridgePublished = true;
                        retainPayloadForInstaller = true;

                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"[NarrativeEncounterService] Published launch bridge for encounter '{encounterId}' " +
                                $"using provider '{entry.encounterProvider.name}'.");
                        }
                    },
                });

                if (!transitionBridge.LastTransitionIssuedLoad)
                {
                    retainPayloadForInstaller = false;
                    Debug.LogError("[NarrativeEncounterService] Encounter transition did not start battle scene load.");
                    yield break;
                }

                string savedReturnSceneId = ResolveExpectedReturnSceneId(solanaBridge);
                if (!string.IsNullOrEmpty(savedReturnSceneId) &&
                    !string.Equals(savedReturnSceneId, targetScene, StringComparison.OrdinalIgnoreCase))
                {
                    expectedReturnSceneId = savedReturnSceneId;
                }

                OverworldReturnInfo capturedReturn = CaptureReturnInfo(solanaBridge, targetScene, expectedReturnSceneId);

                float loadDeadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, resolvedSceneLoadTimeout);
                while (Time.realtimeSinceStartup < loadDeadline)
                {
                    if (string.Equals(SceneManager.GetActiveScene().name, targetScene, StringComparison.Ordinal))
                        break;

                    yield return null;
                }

                if (!string.Equals(SceneManager.GetActiveScene().name, targetScene, StringComparison.Ordinal))
                {
                    retainPayloadForInstaller = false;
                    Debug.LogError($"[NarrativeEncounterService] Timed out waiting for battle scene '{targetScene}' to load.");
                    yield break;
                }

                retainPayloadForInstaller = false;

                float controllerDeadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, resolvedBattleControllerTimeout);
                while (Time.realtimeSinceStartup < controllerDeadline)
                {
                    battleController = FindBattleControllerInActiveScene(targetScene);
                    if (battleController != null)
                        break;

                    yield return null;
                }

                if (battleController == null)
                {
                    Debug.LogError("[NarrativeEncounterService] CombatBattleController not found after battle scene load.");
                    yield break;
                }

                ICombatSceneBootstrap sceneBootstrap = FindSceneBootstrap(battleController);
                if (sceneBootstrap != null)
                {
                    float bootDeadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, battleBootstrapTimeoutSeconds);
                    while (Time.realtimeSinceStartup < bootDeadline &&
                           !sceneBootstrap.IsReady &&
                           !sceneBootstrap.HasFailed)
                    {
                        yield return null;
                    }

                    if (sceneBootstrap.HasFailed)
                    {
                        if (enableDebugLogs)
                        {
                            Debug.LogError(
                                $"[NarrativeEncounterService] Combat bootstrap failed: {sceneBootstrap.FailureReason}");
                        }

                        CleanupLaunchBridge(ref launchBridge, ref launchBridgePublished, clearPayload: true);
                        yield return AbortEncounterAndReturnOverworld(
                            solanaBridge,
                            resolvedWaitForReturn,
                            resolvedReturnTimeout,
                            encounterId);
                        yield break;
                    }

                    if (!sceneBootstrap.IsReady)
                    {
                        if (enableDebugLogs)
                            Debug.LogError("[NarrativeEncounterService] Timed out waiting for combat bootstrap readiness.");

                        CleanupLaunchBridge(ref launchBridge, ref launchBridgePublished, clearPayload: true);
                        yield return AbortEncounterAndReturnOverworld(
                            solanaBridge,
                            resolvedWaitForReturn,
                            resolvedReturnTimeout,
                            encounterId);
                        yield break;
                    }
                }

                bool battleFinished = false;
                bool battleStartFailed = false;

                handleBattleEnded = result =>
                {
                    LastBattleResult = result;
                    LastOutcome = MapBattleResult(result, config);
                    battleFinished = true;
                };

                handleBattleStartFailed = reason =>
                {
                    battleStartFailed = true;

                    if (enableDebugLogs)
                        Debug.LogWarning($"[NarrativeEncounterService] Battle start failed: {reason}");
                };

                battleController.OnBattleStartFailed += handleBattleStartFailed;
                battleController.OnBattleEnded += handleBattleEnded;

                float startDeadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, battleStartGraceSeconds);
                while (Time.realtimeSinceStartup < startDeadline && !battleStartFailed &&
                       !battleController.BattleStarted)
                {
                    yield return null;
                }

                if (battleStartFailed || !battleController.BattleStarted)
                {
                    if (enableDebugLogs)
                    {
                        Debug.LogError(
                            "[NarrativeEncounterService] Combat never entered an active battle (bootstrap OK but StartBattle aborted or stalled).");
                    }

                    battleController.OnBattleStartFailed -= handleBattleStartFailed;
                    battleController.OnBattleEnded -= handleBattleEnded;

                    CleanupLaunchBridge(ref launchBridge, ref launchBridgePublished, clearPayload: true);
                    yield return AbortEncounterAndReturnOverworld(
                        solanaBridge,
                        resolvedWaitForReturn,
                        resolvedReturnTimeout,
                        encounterId);
                    yield break;
                }

                while (!battleFinished)
                    yield return null;

                battleController.OnBattleStartFailed -= handleBattleStartFailed;
                battleController.OnBattleEnded -= handleBattleEnded;

                yield return WaitForPostBattlePresentation();
                CleanupLaunchBridge(ref launchBridge, ref launchBridgePublished, clearPayload: true);

                if (resolvedWaitForReturn)
                {
                    yield return EnsureOverworldReturnLoad(
                        targetScene,
                        capturedReturn,
                        expectedReturnSceneId,
                        resolvedReturnTimeout);
                    yield return WaitForOverworldReturnReady(targetScene, expectedReturnSceneId, resolvedReturnTimeout);
                }

                bool stillOnBattleScene = string.Equals(
                    SceneManager.GetActiveScene().name,
                    targetScene,
                    StringComparison.Ordinal);

                if (entry.postEncounterDialogue != null &&
                    LastOutcome != EncounterOutcome.BootstrapFailed &&
                    !stillOnBattleScene)
                {
                    yield return PlayDialogueIfPossible(entry.postEncounterDialogue, entry.lockInputDuringPostEncounterDialogue);
                }
                else if (stillOnBattleScene && entry.postEncounterDialogue != null)
                {
                    Debug.LogWarning(
                        "[NarrativeEncounterService] Skipping post-encounter dialogue because the battle scene is still active. " +
                        "Return load did not finish before the wait elapsed.");
                }

                if (enableDebugLogs)
                    Debug.Log($"[NarrativeEncounterService] Encounter '{encounterId}' completed with outcome {LastOutcome}.");
            }
            finally
            {
                if (battleController != null)
                {
                    if (handleBattleEnded != null)
                        battleController.OnBattleEnded -= handleBattleEnded;

                    if (handleBattleStartFailed != null)
                        battleController.OnBattleStartFailed -= handleBattleStartFailed;
                }

                CleanupLaunchBridge(ref launchBridge, ref launchBridgePublished, clearPayload: !retainPayloadForInstaller);
            }
        }

        private static IEnumerator WaitForPostBattlePresentation()
        {
            // Let OnBattleEnded subscribers open their presentation before resolving the contract.
            yield return null;
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatPostBattlePresentation presentation &&
                    !presentation.IsPresentationComplete)
                {
                    yield return presentation.WaitForCompletion();
                    yield break;
                }
            }
        }

        private IEnumerator AbortEncounterAndReturnOverworld(
            SolanaServiceBridge solanaBridge,
            bool resolvedWaitForReturn,
            float resolvedReturnTimeout,
            string encounterIdForLog)
        {
            LastOutcome = EncounterOutcome.BootstrapFailed;
            LastBattleResult = null;
            solanaBridge.ClearActiveEncounter();

            OverworldReturnInfo info = EncounterOverworldSceneLoader.ResolveDefaultReturnInfo(
                combatAbortFallbackHomeScene,
                combatAbortFallbackHomeSpawnId);

            EncounterOverworldSceneLoader.LoadReturnScene(info);

            if (solanaBridge.EncounterReturnContextService != null)
                solanaBridge.EncounterReturnContextService.ClearReturnContext();

            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[NarrativeEncounterService] Encounter '{encounterIdForLog}' aborted — returning to '{info.sceneName}' (outcome={LastOutcome}).");
            }

            if (!resolvedWaitForReturn)
                yield break;

            yield return WaitForOverworldReturnReady(null, info.sceneName, resolvedReturnTimeout);
        }

        private static ICombatSceneBootstrap FindSceneBootstrap(CombatBattleController controller)
        {
            if (controller != null)
            {
                MonoBehaviour[] local = controller.GetComponents<MonoBehaviour>();
                for (int i = 0; i < local.Length; i++)
                {
                    if (local[i] is ICombatSceneBootstrap bootstrap)
                        return bootstrap;
                }
            }

            MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is ICombatSceneBootstrap found)
                    return found;
            }

            return null;
        }

        private EncounterRegistryEntry FindEntry(string encounterId)
        {
            if (string.IsNullOrWhiteSpace(encounterId))
                return null;

            for (int i = 0; i < registry.Count; i++)
            {
                EncounterRegistryEntry entry = registry[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.encounterId))
                    continue;

                if (string.Equals(entry.encounterId.Trim(), encounterId.Trim(), StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private static OverworldReturnInfo CaptureReturnInfo(
            SolanaServiceBridge solanaBridge,
            string battleSceneName,
            string fallbackSceneId)
        {
            IEncounterReturnContextService service = solanaBridge != null
                ? solanaBridge.EncounterReturnContextService
                : null;
            EncounterReturnContext returnCtx = service != null ? service.GetReturnContext() : null;
            if (returnCtx != null &&
                returnCtx.isValid &&
                !string.Equals(returnCtx.sceneId, battleSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return new OverworldReturnInfo
                {
                    sceneName = returnCtx.sceneId,
                    useExactPosition = true,
                    position = returnCtx.position,
                    rotation = returnCtx.rotation,
                    returnSpawnId = returnCtx.returnSpawnId ?? "",
                    loadedChunkIds = returnCtx.loadedChunkIds,
                };
            }

            return new OverworldReturnInfo
            {
                sceneName = fallbackSceneId,
                useExactPosition = false,
            };
        }

        private IEnumerator EnsureOverworldReturnLoad(
            string battleSceneName,
            OverworldReturnInfo capturedReturn,
            string expectedReturnSceneId,
            float timeoutSeconds)
        {
            string battle = battleSceneName != null ? battleSceneName.Trim() : "";
            float idleDeadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < idleDeadline)
            {
                SceneLoader loader = SceneLoader.Instance;
                bool transitioning = loader != null && loader.IsTransitioning;
                bool leftBattle = string.IsNullOrEmpty(battle) ||
                    !string.Equals(SceneManager.GetActiveScene().name, battle, StringComparison.Ordinal);
                if (leftBattle)
                    yield break;
                if (transitioning)
                {
                    yield return null;
                    continue;
                }

                break;
            }

            if (!string.Equals(SceneManager.GetActiveScene().name, battle, StringComparison.Ordinal))
                yield break;

            SceneLoader sceneLoader = SceneLoader.Instance;
            if (sceneLoader != null && sceneLoader.IsTransitioning)
                sceneLoader.AbortStalledTransitionWhenDestinationAlreadyActive(battle);

            if (sceneLoader != null && sceneLoader.IsTransitioning)
                yield break;

            OverworldReturnInfo info = capturedReturn;
            if (string.IsNullOrEmpty(info.sceneName))
                info.sceneName = expectedReturnSceneId;
            if (string.IsNullOrEmpty(info.sceneName))
            {
                info = EncounterOverworldSceneLoader.ResolveDefaultReturnInfo(
                    combatAbortFallbackHomeScene,
                    combatAbortFallbackHomeSpawnId);
            }

            Debug.Log(
                $"[NarrativeEncounterService] Issuing overworld return load scene='{info.sceneName}' exact={info.useExactPosition}.");
            EncounterOverworldSceneLoader.LoadReturnScene(info);
            yield return null;
        }

        private static string ResolveExpectedReturnSceneId(SolanaServiceBridge solanaBridge)
        {
            IEncounterReturnContextService service = solanaBridge != null
                ? solanaBridge.EncounterReturnContextService
                : null;
            EncounterReturnContext returnCtx = service != null ? service.GetReturnContext() : null;
            if (returnCtx == null || !returnCtx.isValid)
                return "";
            return returnCtx.sceneId != null ? returnCtx.sceneId.Trim() : "";
        }

        private IEnumerator WaitForOverworldReturnReady(
            string battleSceneName,
            string expectedReturnSceneId,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, timeoutSeconds);
            string battle = battleSceneName != null ? battleSceneName.Trim() : "";
            string expected = expectedReturnSceneId != null ? expectedReturnSceneId.Trim() : "";
            bool sawTransition = false;

            while (Time.realtimeSinceStartup < deadline)
            {
                SceneLoader sceneLoader = SceneLoader.Instance;
                bool transitioning = sceneLoader != null && sceneLoader.IsTransitioning;
                if (transitioning)
                    sawTransition = true;

                string activeName = SceneManager.GetActiveScene().name;
                bool leftBattle = string.IsNullOrEmpty(battle) ||
                    !string.Equals(activeName, battle, StringComparison.Ordinal);
                bool returnSceneReady = string.IsNullOrEmpty(expected) || IsSceneLoaded(expected);

                if (!transitioning && returnSceneReady && (sawTransition || leftBattle))
                    yield break;

                yield return null;
            }

            string active = SceneManager.GetActiveScene().name;
            Debug.LogWarning(
                $"[NarrativeEncounterService] Timed out waiting for overworld return. " +
                $"active='{active}', expectedReturn='{expected}', battle='{battle}', loaded={IsSceneLoaded(expected)}.");
        }

        private static bool IsSceneLoaded(string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId))
                return false;

            Scene scene = SceneManager.GetSceneByName(sceneId.Trim());
            return scene.IsValid() && scene.isLoaded;
        }

        private IEnumerator PlayDialogueIfPossible(DialogueSequenceSO dialogueSequence, bool? lockInputOverride = null)
        {
            if (dialogueSequence == null)
                yield break;

            DialogueService dialogueService = DialogueService.Instance;
            if (dialogueService == null)
            {
                Debug.LogWarning("[NarrativeEncounterService] DialogueService not found. Skipping dialogue.");
                yield break;
            }

            Coroutine dialogueCoroutine = dialogueService.PlayDialogue(dialogueSequence, lockInputOverride);
            if (dialogueCoroutine != null)
                yield return dialogueCoroutine;

            while (dialogueService.IsPlaying)
                yield return null;
        }

        private static string ResolveReturnSpawnId(EncounterRegistryEntry entry, EncounterConfig config)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.ReturnSpawnPointId))
                return entry.ReturnSpawnPointId;

            if (config != null && !string.IsNullOrEmpty(config.returnSpawnPointId))
                return config.returnSpawnPointId;

            return null;
        }

        private static CombatBattleController FindBattleControllerInActiveScene(string expectedScene)
        {
            CombatBattleController[] controllers = FindObjectsOfType<CombatBattleController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                CombatBattleController controller = controllers[i];
                if (controller == null)
                    continue;

                if (string.Equals(controller.gameObject.scene.name, expectedScene, StringComparison.Ordinal))
                    return controller;
            }

            return null;
        }

        private static EncounterOutcome ResolveFallbackOutcome(EncounterConfig config)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.forcedOutcome) &&
                Enum.TryParse(config.forcedOutcome, true, out EncounterOutcome forced))
            {
                return forced;
            }

            return EncounterOutcome.ForcedRun;
        }

        private static EncounterOutcome MapBattleResult(BattleResult result, EncounterConfig config)
        {
            if (result == null)
                return ResolveFallbackOutcome(config);

            if (result.DidCapture)
                return EncounterOutcome.Capture;

            if (result.wasFled)
                return EncounterOutcome.Escaped;

            if (result.PlayerWon)
                return EncounterOutcome.Win;

            return EncounterOutcome.Lose;
        }

        private static void CleanupLaunchBridge(
            ref NarrativeCombatLaunchBridge launchBridge,
            ref bool launchBridgePublished,
            bool clearPayload)
        {
            if (!launchBridgePublished && !NarrativeCombatLaunchBridge.HasPending)
                return;

            if (clearPayload)
                NarrativeCombatLaunchBridge.ClearPendingPayload();

            if (launchBridge == null)
                launchBridge = NarrativeCombatLaunchBridge.FindExisting();

            if (launchBridge != null)
                launchBridge.ReleaseObject();

            launchBridge = null;
            if (clearPayload || !NarrativeCombatLaunchBridge.HasPending)
                launchBridgePublished = false;
        }
    }
}
