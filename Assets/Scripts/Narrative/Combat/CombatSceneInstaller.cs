using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using Chainbound.Combat.Core;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Scene bootstrapper that wires encounter and player lineup providers before the combat auto-starter runs.
    /// Narrative-launched battles take precedence over stray zone encounters. No silent ScriptableObject fallbacks;
    /// enable <see cref="enableCombatTestProviders"/> for harness/testing encounters.
    /// </summary>
    public class CombatSceneInstaller : MonoBehaviour, ICombatSceneBootstrap
    {
        private struct EncounterSelection
        {
            public IEncounterProvider provider;
            public UnityEngine.Object providerObject;
            public EncounterFleePolicy fleePolicyOverride;
            public bool isBoss;
            public UnityEngine.Object bossIntroAsset;
            public BattleIntroPresentationSO introPresentation;
            public ManualPartyProviderSO playerPartyOverride;
            public bool disableSupabasePartyResolution;
            public bool disableOnChainLoadoutResolution;
            public bool armIntroAutoEscaper;
            public bool usePureManualPartyProvider;
            public string description;
        }

        [Header("Scene References")]
        [SerializeField] private CombatBattleController battleController;
        [SerializeField] private SolanaServiceBridge solanaServiceBridge;
        [SerializeField] private ZoneEncounterProvider zoneEncounterProviderOverride;
        [SerializeField, FormerlySerializedAs("playerPartyProvider")] private SupabasePlayerLineupPartyProvider lineupPartyProvider;
        [SerializeField] private OnChainLoadoutResolver onChainLoadoutResolver;
        [SerializeField] private BattleIntroBossDialogueBridge bossIntroDialogueBridge;
        [SerializeField] private IntroEncounterAutoEscaper introAutoEscaper;

        [Header("Config")]
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [Tooltip("Default intro presentation for zone/wild encounters when the encounter itself has no authored presentation.")]
        [SerializeField] private BattleIntroPresentationSO defaultWildIntroPresentation;

        [Header("Combat test (explicit only)")]
        [Tooltip(
            "When enabled, encounters that do not originate from narrative, zone or Solana can use manual providers below.")]
        [SerializeField] private bool enableCombatTestProviders;

        [Tooltip("Manual player party when Combat Test is enabled and \"Use Supabase lineup\" is off.")]
        [SerializeField, FormerlySerializedAs("partyFallback")]
        private ManualPartyProviderSO combatTestPlayerPartyProvider;

        [Tooltip("Manual encounter roster for combat harness / mechanics testing.")]
        [SerializeField, FormerlySerializedAs("encounterFallback")]
        private ManualEncounterProviderSO combatTestEncounterProvider;

        [Header("Behavior")]
        [SerializeField] private bool autoCreateMissingComponents = true;
        [SerializeField] private bool useSupabaseProfileProvider = true;
        [SerializeField] private bool useOnChainLoadoutResolver = true;
        [SerializeField] private bool loadChainlingVaultIfMissing = true;
        [SerializeField] private bool clearBossIntroOverrideWhenNoneAssigned = true;
        [SerializeField] private bool logDiagnostics = true;

        public bool IsReady { get; private set; }
        public bool HasFailed { get; private set; }
        public string FailureReason { get; private set; }

        private bool _bootstrapStarted;

        private async void Awake()
        {
            if (_bootstrapStarted)
                return;

            _bootstrapStarted = true;
            await BootstrapAsync();
        }

        private async Task BootstrapAsync()
        {
            try
            {
                ResolveReferences();

                if (battleController == null)
                {
                    Fail("CombatBattleController is missing.");
                    return;
                }

                if (TryBootstrapCasualPvp())
                    return;

                EncounterSelection encounterSelection = ResolveEncounterSelection();
                if (encounterSelection.provider == null)
                {
                    Fail("No encounter provider was available for this battle scene.");
                    return;
                }

                battleController.SetEncounterProviderOverride(encounterSelection.provider, encounterSelection.providerObject);
                battleController.SetFleePolicyOverride(encounterSelection.fleePolicyOverride);

                // Event / narrative rulesets: prefer BalanceConfigRuntime active override, else default.
                CombatRulesetSO activeRuleset = BalanceConfigRuntime.Active != null
                    ? BalanceConfigRuntime.Active.ActiveRuleset
                    : null;
                if (activeRuleset != null)
                    battleController.SetRulesetOverride(activeRuleset);

                ConfigureIntroPresentation(encounterSelection);
                ApplyBossIntroOverride(encounterSelection);
                ConfigureIntroAutoEscaper(encounterSelection);

                bool playerReady = await ConfigurePlayerPartyAsync(encounterSelection);
                if (!playerReady)
                    return;

                IsReady = true;

                if (logDiagnostics)
                    Debug.Log($"[CombatSceneInstaller] Bootstrap ready. Encounter={encounterSelection.description}");
            }
            catch (Exception ex)
            {
                Fail($"Bootstrap exception: {ex.Message}");
            }
        }

        private bool TryBootstrapCasualPvp()
        {
            if (!CasualPvpLaunchContext.HasPending || battleController == null)
                return false;

            CasualPvpLaunchPayload payload = CasualPvpLaunchContext.Current;
            if (payload == null || payload.PartyA == null || payload.PartyB == null)
            {
                CasualPvpLaunchContext.Clear();
                Fail("Casual match was missing locked parties.");
                return true;
            }

            CasualPvpLaunchContext.Consume();

            battleController.BeginCasualPvp(payload);
            if (payload.Ruleset != null)
                battleController.SetRulesetOverride(payload.Ruleset);
            battleController.SetFleePolicyOverride(EncounterFleePolicy.Locked);
            battleController.SetPlayerPartyProviderOverride(new SnapshotPartyProvider(payload.PartyA));
            battleController.SetEncounterProviderOverride(
                new SnapshotEncounterProvider(PvpPartyMapper.ToEncounter(payload.PartyB, payload.Seed)));
            if (battleController.GetComponent<CasualPvpBattleBootstrap>() == null)
                battleController.gameObject.AddComponent<CasualPvpBattleBootstrap>();
            IsReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logDiagnostics)
                Debug.Log($"[PvPMatch] Combat scene armed for match {payload.MatchId} seat={payload.Seat}.");
#endif
            return true;
        }

        private void ResolveReferences()
        {
            if (battleController == null)
                battleController = FindObjectOfType<CombatBattleController>(true);

            if (solanaServiceBridge == null)
                solanaServiceBridge = SolanaServiceBridge.Instance != null
                    ? SolanaServiceBridge.Instance
                    : FindObjectOfType<SolanaServiceBridge>(true);

            if (zoneEncounterProviderOverride == null)
                zoneEncounterProviderOverride = solanaServiceBridge != null
                    ? solanaServiceBridge.ZoneEncounterProvider
                    : FindObjectOfType<ZoneEncounterProvider>(true);

            if (bossIntroDialogueBridge == null)
                bossIntroDialogueBridge = FindObjectOfType<BattleIntroBossDialogueBridge>(true);

            if (introAutoEscaper == null && battleController != null)
                introAutoEscaper = battleController.GetComponent<IntroEncounterAutoEscaper>();

            if (introAutoEscaper == null)
                introAutoEscaper = FindObjectOfType<IntroEncounterAutoEscaper>(true);

            if (lineupPartyProvider == null)
                lineupPartyProvider = GetComponent<SupabasePlayerLineupPartyProvider>();

            if (lineupPartyProvider == null && autoCreateMissingComponents)
                lineupPartyProvider = gameObject.AddComponent<SupabasePlayerLineupPartyProvider>();

            if (onChainLoadoutResolver == null)
                onChainLoadoutResolver = GetComponent<OnChainLoadoutResolver>();

            if (onChainLoadoutResolver == null && autoCreateMissingComponents && useOnChainLoadoutResolver)
                onChainLoadoutResolver = gameObject.AddComponent<OnChainLoadoutResolver>();
        }

        private EncounterSelection ResolveEncounterSelection()
        {
            string battleSceneName = gameObject.scene.IsValid()
                ? gameObject.scene.name
                : SceneManager.GetActiveScene().name;
            bool hasGameplayLaunchTicket = TryConsumeGameplayLaunchTicket(
                battleSceneName,
                out EncounterTransitionBridge.EncounterLaunchSource requiredSource);

            bool shouldUseAuthoritativeEncounter = !hasGameplayLaunchTicket ||
                requiredSource == EncounterTransitionBridge.EncounterLaunchSource.Zone ||
                requiredSource == EncounterTransitionBridge.EncounterLaunchSource.AuthoritativeResume;
            if (shouldUseAuthoritativeEncounter &&
                AuthoritativeBattleLaunchContext.HasSession &&
                AuthoritativeBattleLaunchContext.Current.IsResumable &&
                AuthoritativeBattleLaunchContext.TryReadState(out AuthoritativeClientBattleState authoritativeState))
            {
                if (requiredSource != EncounterTransitionBridge.EncounterLaunchSource.Narrative)
                    NarrativeCombatLaunchBridge.ClearPendingPayload();
                var authoritativeProvider = new AuthoritativeBattleEncounterProvider(authoritativeState);
                return new EncounterSelection
                {
                    provider = authoritativeProvider,
                    providerObject = null,
                    isBoss = (AuthoritativeBattleKind)authoritativeState.battleKind == AuthoritativeBattleKind.Boss,
                    introPresentation = defaultWildIntroPresentation,
                    usePureManualPartyProvider = false,
                    description = $"Authority:{AuthoritativeBattleLaunchContext.Current.BattleId}",
                };
            }

            if (!AllowsSource(
                    hasGameplayLaunchTicket,
                    requiredSource,
                    EncounterTransitionBridge.EncounterLaunchSource.Narrative))
            {
                NarrativeCombatLaunchBridge.ClearPendingPayload();
            }
            else if (NarrativeCombatLaunchBridge.TryPeekPending(out NarrativeCombatLaunchContext narrativeContext))
            {
                ManualEncounterProviderSO manualProvider = narrativeContext.Provider;
                if (manualProvider != null)
                {
                    NarrativeCombatLaunchBridge narrativeBridge = NarrativeCombatLaunchBridge.FindExisting();
                    if (narrativeBridge != null)
                        narrativeBridge.ConsumePending();
                    else
                        NarrativeCombatLaunchBridge.ClearPendingPayload();

                    ManualPartyProviderSO partyOverride =
                        narrativeContext.PlayerPartyOverrideOrFallback;

                    bool usePureManualParty = narrativeContext.DisableSupabasePartyResolution;

                    if (usePureManualParty && partyOverride == null)
                    {
                        Debug.LogWarning(
                            "[CombatSceneInstaller] Narrative launch disabled Supabase party resolution without assigning a scripted player party override.");
                    }

                    if (logDiagnostics && narrativeBridge == null)
                    {
                        Debug.Log(
                            "[CombatSceneInstaller] Recovered narrative launch payload after the bridge object was destroyed.");
                    }

                    return new EncounterSelection
                    {
                        provider = manualProvider,
                        providerObject = manualProvider,
                        fleePolicyOverride = narrativeContext.FleePolicyOverride,
                        isBoss = manualProvider.Difficulty == EncounterDifficulty.Boss,
                        bossIntroAsset = manualProvider.BossIntroDialogueAsset,
                        introPresentation = manualProvider.IntroPresentation,
                        playerPartyOverride = partyOverride,
                        disableSupabasePartyResolution = narrativeContext.DisableSupabasePartyResolution,
                        disableOnChainLoadoutResolution = narrativeContext.DisableOnChainLoadoutResolution,
                        armIntroAutoEscaper = narrativeContext.ArmIntroAutoEscaper,
                        usePureManualPartyProvider = usePureManualParty,
                        description = string.IsNullOrWhiteSpace(narrativeContext.EncounterId)
                            ? $"Narrative:{manualProvider.name}"
                            : $"Narrative:{narrativeContext.EncounterId}",
                    };
                }

                Debug.LogWarning(
                    "[CombatSceneInstaller] NarrativeCombatLaunchBridge had pending context without a valid encounter provider. Discarding stale context.");

                NarrativeCombatLaunchBridge.ClearPendingPayload();
            }

            if (AllowsSource(
                    hasGameplayLaunchTicket,
                    requiredSource,
                    EncounterTransitionBridge.EncounterLaunchSource.Zone) &&
                zoneEncounterProviderOverride != null &&
                zoneEncounterProviderOverride.ActiveResult != null)
            {
                return new EncounterSelection
                {
                    provider = zoneEncounterProviderOverride,
                    providerObject = zoneEncounterProviderOverride,
                    isBoss = zoneEncounterProviderOverride.ActiveResult.difficulty == EncounterDifficulty.Boss,
                    bossIntroAsset = zoneEncounterProviderOverride.ActiveResult.bossIntroDialogueAsset,
                    introPresentation = defaultWildIntroPresentation,
                    usePureManualPartyProvider = false,
                    description =
                        $"Zone:{zoneEncounterProviderOverride.ActiveResult.zoneId}",
                };
            }

            if (AllowsSource(
                    hasGameplayLaunchTicket,
                    requiredSource,
                    EncounterTransitionBridge.EncounterLaunchSource.ManualEncounter) &&
                solanaServiceBridge != null &&
                solanaServiceBridge.ActiveManualEncounter != null)
            {
                ManualEncounterProviderSO manual = solanaServiceBridge.ActiveManualEncounter;
                return new EncounterSelection
                {
                    provider = manual,
                    providerObject = manual,
                    isBoss = manual.Difficulty == EncounterDifficulty.Boss,
                    bossIntroAsset = manual.BossIntroDialogueAsset,
                    introPresentation = manual.IntroPresentation,
                    usePureManualPartyProvider = false,
                    description = $"Manual:{manual.name}",
                };
            }

            if (hasGameplayLaunchTicket)
            {
                Debug.LogError(
                    $"[CombatSceneInstaller] Gameplay encounter launch reached scene " +
                    $"'{battleSceneName}' without its required " +
                    $"'{requiredSource}' payload. Combat-test providers were intentionally not used.",
                    this);
                return default;
            }

            if (enableCombatTestProviders && combatTestEncounterProvider != null)
            {
                ManualEncounterProviderSO testEncounter = combatTestEncounterProvider;
                bool testManualPartyPath = !useSupabaseProfileProvider &&
                                          combatTestPlayerPartyProvider != null;

                return new EncounterSelection
                {
                    provider = testEncounter,
                    providerObject = testEncounter,
                    isBoss = testEncounter.Difficulty == EncounterDifficulty.Boss,
                    bossIntroAsset = testEncounter.BossIntroDialogueAsset,
                    introPresentation = testEncounter.IntroPresentation,
                    playerPartyOverride = combatTestPlayerPartyProvider,
                    usePureManualPartyProvider = testManualPartyPath,
                    description = $"CombatTest:{testEncounter.name}",
                };
            }

            return default;
        }

        private static bool AllowsSource(
            bool hasGameplayLaunchTicket,
            EncounterTransitionBridge.EncounterLaunchSource requiredSource,
            EncounterTransitionBridge.EncounterLaunchSource candidateSource)
        {
            return !hasGameplayLaunchTicket ||
                   requiredSource == EncounterTransitionBridge.EncounterLaunchSource.Unspecified ||
                   requiredSource == candidateSource;
        }

        private static bool TryConsumeGameplayLaunchTicket(
            string battleSceneName,
            out EncounterTransitionBridge.EncounterLaunchSource launchSource)
        {
            launchSource = EncounterTransitionBridge.EncounterLaunchSource.Unspecified;

            // The persistent bridge owns the ticket. FindFirst can hit an inactive
            // scene duplicate whose ticket is empty and discard the real launch.
            EncounterTransitionBridge transitionBridge = EncounterTransitionBridge.Instance;
            if (transitionBridge == null)
            {
                transitionBridge = FindFirstObjectByType<EncounterTransitionBridge>(FindObjectsInactive.Include);
            }

            return transitionBridge != null &&
                   transitionBridge.TryConsumeBattleSceneLaunchTicket(
                       battleSceneName,
                       out launchSource);
        }

        private void ConfigureIntroPresentation(EncounterSelection selection)
        {
            var presenter = FindObjectOfType<Combat2BattlefieldPresenter>(true);
            if (presenter == null)
                return;

            if (selection.introPresentation != null)
                presenter.SetIntroPresentation(selection.introPresentation);
            else
                presenter.ClearIntroPresentation();
        }

        private void ApplyBossIntroOverride(EncounterSelection selection)
        {
            if (bossIntroDialogueBridge == null)
                return;

            if (selection.isBoss && selection.bossIntroAsset != null)
            {
                if (selection.bossIntroAsset is DialogueSequenceSO dialogue)
                {
                    bossIntroDialogueBridge.SetBossIntroDialogue(dialogue);

                    if (logDiagnostics)
                        Debug.Log($"[CombatSceneInstaller] Boss intro override set to '{dialogue.name}'.");
                }
                else
                {
                    Debug.LogWarning(
                        $"[CombatSceneInstaller] Boss intro asset '{selection.bossIntroAsset.name}' is not a DialogueSequenceSO.");
                    if (clearBossIntroOverrideWhenNoneAssigned)
                        bossIntroDialogueBridge.ClearBossIntroDialogueOverride();
                }

                return;
            }

            if (clearBossIntroOverrideWhenNoneAssigned)
                bossIntroDialogueBridge.ClearBossIntroDialogueOverride();
        }

        private void ConfigureIntroAutoEscaper(EncounterSelection selection)
        {
            if (introAutoEscaper == null)
            {
                if (logDiagnostics)
                {
                    Debug.Log(
                        "[CombatSceneInstaller] ConfigureIntroAutoEscaper: introAutoEscaper is null; skipping SetArmed.");
                }

                return;
            }

            introAutoEscaper.SetArmed(selection.armIntroAutoEscaper);

            if (logDiagnostics)
            {
                Debug.Log(
                    $"[CombatSceneInstaller] ConfigureIntroAutoEscaper: armIntroAutoEscaper={selection.armIntroAutoEscaper}, " +
                    $"SetArmed on escaper go='{introAutoEscaper.gameObject.name}', " +
                    $"instanceId={introAutoEscaper.GetEntityId()}, enabled={introAutoEscaper.enabled}.",
                    introAutoEscaper);
            }
        }

        private async Task<bool> ConfigurePlayerPartyAsync(EncounterSelection selection)
        {
            string wallet = solanaServiceBridge != null ? solanaServiceBridge.WalletService?.PublicKey : null;

            if (selection.usePureManualPartyProvider)
            {
                if (selection.playerPartyOverride == null)
                {
                    Fail("Scripted narrative or combat-test flow requires a manual player party ScriptableObject.");
                    return false;
                }

                battleController.SetPlayerPartyProviderOverride(selection.playerPartyOverride, null);
                await Task.CompletedTask;

                PartySnapshot scriptedParty = selection.playerPartyOverride.GetCurrentParty();
                if (scriptedParty == null || scriptedParty.Count == 0)
                {
                    Fail("Manual player party override produced an empty party.");
                    return false;
                }

                if (!ShouldResolveLoadout(wallet, selection, fetchedFromSupabase: false))
                    return true;

                if (onChainLoadoutResolver == null)
                {
                    Debug.LogWarning(
                        "[CombatSceneInstaller] Loadout resolver is enabled but missing. Continuing with existing/default loadout.");
                }
                else
                {
                    onChainLoadoutResolver.Configure(moveDatabase != null ? moveDatabase : battleController.MoveDatabase);
                    await EnsureChainlingVaultLoadedAsync(wallet);
                    await onChainLoadoutResolver.ResolveLoadoutsAsync(
                        scriptedParty,
                        wallet,
                        solanaServiceBridge.MoveVaultService,
                        solanaServiceBridge.EquippedRegistryService,
                        solanaServiceBridge.PlayerState);
                }

                return true;
            }

            if (!useSupabaseProfileProvider)
            {
                Fail("\"Use Supabase lineup\" is disabled but no combat-test manual party path was activated for this encounter.");
                return false;
            }

            if (lineupPartyProvider == null)
            {
                Fail("SupabasePlayerLineupPartyProvider is missing and auto-create is disabled.");
                return false;
            }

            lineupPartyProvider.Configure(supabaseConfig);
            battleController.SetPlayerPartyProviderOverride(lineupPartyProvider, lineupPartyProvider);

            PartySnapshot party = await lineupPartyProvider.FetchAndCacheAsync(wallet);

            if (party == null || party.Count == 0)
            {
                Fail($"Player lineup could not be loaded from Supabase. {lineupPartyProvider.LastError}");
                return false;
            }

            if (ShouldResolveLoadout(wallet, selection, fetchedFromSupabase: true))
            {
                if (onChainLoadoutResolver == null)
                {
                    Debug.LogWarning(
                        "[CombatSceneInstaller] Loadout resolver is enabled but missing. Continuing with existing/default loadout.");
                }
                else
                {
                    onChainLoadoutResolver.Configure(moveDatabase != null ? moveDatabase : battleController.MoveDatabase);
                    await EnsureChainlingVaultLoadedAsync(wallet);
                    await onChainLoadoutResolver.ResolveLoadoutsAsync(
                        party,
                        wallet,
                        solanaServiceBridge.MoveVaultService,
                        solanaServiceBridge.EquippedRegistryService,
                        solanaServiceBridge.PlayerState);
                }
            }
            else if (logDiagnostics)
            {
                Debug.Log("[CombatSceneInstaller] Skipping on-chain loadout resolution.");
            }

            lineupPartyProvider.SetResolvedParty(party);
            return true;
        }

        private bool ShouldResolveLoadout(string wallet, EncounterSelection selection, bool fetchedFromSupabase)
        {
            if (selection.disableOnChainLoadoutResolution)
                return false;

            if (!useSupabaseProfileProvider)
                return false;

            if (!useOnChainLoadoutResolver)
                return false;

            if (!fetchedFromSupabase)
                return false;

            if (string.IsNullOrWhiteSpace(wallet))
                return false;

            if (solanaServiceBridge == null)
                return false;

            return true;
        }

        private async Task EnsureChainlingVaultLoadedAsync(string wallet)
        {
            if (!loadChainlingVaultIfMissing || solanaServiceBridge == null || solanaServiceBridge.PlayerState == null)
                return;

            if (solanaServiceBridge.PlayerState.ChainlingVault != null)
                return;

            if (solanaServiceBridge.ChainlingVaultService == null || string.IsNullOrWhiteSpace(wallet))
                return;

            solanaServiceBridge.PlayerState.ChainlingVault =
                await solanaServiceBridge.ChainlingVaultService.LoadVaultAsync(wallet);
        }

        private void Fail(string reason)
        {
            FailureReason = reason;
            HasFailed = true;
            IsReady = false;
            Debug.LogError($"[CombatSceneInstaller] {reason}", this);
        }
    }
}
