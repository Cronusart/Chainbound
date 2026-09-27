using Chainbound.Combat;
using UnityEngine;
using UnityEngine.Serialization;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Applies Chainlings view prefabs and optional dev harness data to a runtime <see cref="PlayerMenuController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMenuDevBootstrap : MonoBehaviour
    {
        private const string MenuViewsResourcePath = "ChainlingsMenuViews";

        [Header("Views")]
        [SerializeField] private ChainlingsMenuViewsSO menuViews;

        [Header("Config")]
        [SerializeField] private PlayerMenuDevConfigSO devConfig;

        [Header("Data References")]
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private ChainlingVisualIdentityDatabaseSO identityDatabase;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [FormerlySerializedAs("rewardConfig")]
        [SerializeField] private ProgressionConfigSO progressionConfig;
        [SerializeField] private ChainlingProgressionRewardTablesSO progressionRewardTables;
        [SerializeField] private ChainlingProgressionEventCatalogSO progressionEventCatalog;
        [SerializeField] private ModifierCatalogSO modifierCatalog;
        [SerializeField] private ChainlingBadgeCatalogSO badgeCatalog;

        [Header("Behavior")]
        [SerializeField] private bool applyInEditor = true;
        [SerializeField] private bool applyInDevelopmentBuilds = true;
        [SerializeField] private bool enableDebugLogs;

        private PlayerMenuController appliedController;

        public PlayerMenuDevConfigSO DevConfig => devConfig;
        public ChainlingsMenuViewsSO MenuViews => menuViews;

        public void ApplyTo(PlayerMenuController controller)
        {
            if (controller == null)
                return;

            ResolveDataReferences();
            controller.EnsureTabScreensInstalled();

            ChainlingsPartyScreen partyScreen = controller.GetChainlingsPartyScreen();
            ChainlingDetailScreen detailScreen = FindDetailScreen(controller);

            if (partyScreen == null)
                return;

            if (appliedController == controller)
                return;

            appliedController = controller;

            bool useHarness = devConfig != null &&
                              ShouldApply() &&
                              devConfig.ShouldUseHarness(IsEditor(), IsDevelopmentBuild());

            if (devConfig != null &&
                ShouldApply() &&
                devConfig.ShouldBypassAvailabilityGate(IsEditor(), IsDevelopmentBuild()))
            {
                controller.SetBypassAvailabilityGate(true);
            }

            if (useHarness)
            {
                ChainlingsMenuTestHarness harness = EnsureHarness(partyScreen);
                WireHarnessServices(partyScreen, detailScreen, harness);

                if (enableDebugLogs)
                    Debug.Log("[PlayerMenuDevBootstrap] Chainlings harness data enabled.", this);
                return;
            }

            WireProductionServices(partyScreen, detailScreen);
        }

        private bool ShouldApply()
        {
            if (devConfig == null)
                return false;

            if (IsEditor())
                return applyInEditor;

            return applyInDevelopmentBuilds && IsDevelopmentBuild();
        }

        private void WireProductionServices(ChainlingsPartyScreen partyScreen, ChainlingDetailScreen detailScreen)
        {
            if (partyScreen == null)
                return;

            partyScreen.SetDataSourceMode(ChainlingsDataSourceMode.Production);
            if (detailScreen != null)
                detailScreen.SetDataSourceMode(ChainlingsDataSourceMode.Production);

            partyScreen.Configure(
                supabaseConfig,
                speciesDatabase,
                visualDatabase,
                identityDatabase,
                moveDatabase,
                progressionConfig,
                null,
                null,
                null,
                null,
                null,
                null,
                partyScreen.GetScreenStack(),
                null,
                null);

            if (detailScreen != null)
                WireDetailScreenReferences(detailScreen, null);

            partyScreen.ConfigurePartyReadModelResources(
                badgeCatalog,
                modifierCatalog,
                false,
                progressionRewardTables,
                progressionEventCatalog);

            InventoryScreen inventoryScreen = appliedController != null
                ? appliedController.GetInventoryScreen() ?? appliedController.GetComponentInChildren<InventoryScreen>(true)
                : partyScreen.GetComponentInParent<PlayerMenuController>(true)?.GetInventoryScreen();
            inventoryScreen?.ConfigureSpeciesDatabase(speciesDatabase);
        }

        private ChainlingsMenuTestHarness EnsureHarness(ChainlingsPartyScreen partyScreen)
        {
            if (partyScreen == null)
                return null;

            ChainlingsMenuTestHarness harness = partyScreen.GetComponent<ChainlingsMenuTestHarness>();
            if (harness == null)
                harness = partyScreen.gameObject.AddComponent<ChainlingsMenuTestHarness>();

            harness.Configure(speciesDatabase, visualDatabase, identityDatabase, moveDatabase, progressionConfig);
            harness.ApplyGenerationSettings(
                devConfig.HarnessSeed,
                devConfig.GeneratedRosterSize,
                devConfig.GeneratedPartySize,
                devConfig.GeneratedMoveInventorySize,
                devConfig.RegenerateHarnessOnEnable,
                devConfig.UnlockAllCosmetics,
                devConfig.IncludeCycleReadyChainling,
                devConfig.AllowEntangledVariants);

            return harness;
        }

        private void WireHarnessServices(
            ChainlingsPartyScreen partyScreen,
            ChainlingDetailScreen detailScreen,
            ChainlingsMenuTestHarness harness)
        {
            if (partyScreen == null || harness == null)
                return;

            partyScreen.SetDataSourceMode(ChainlingsDataSourceMode.Harness);
            if (detailScreen != null)
                detailScreen.SetDataSourceMode(ChainlingsDataSourceMode.Harness);

            partyScreen.Configure(
                supabaseConfig,
                speciesDatabase,
                visualDatabase,
                identityDatabase,
                moveDatabase,
                progressionConfig,
                harness,
                harness,
                harness,
                harness,
                harness,
                harness,
                partyScreen.GetScreenStack(),
                harness,
                harness);

            if (detailScreen != null)
                WireDetailScreenReferences(detailScreen, harness);

            partyScreen.ConfigurePartyReadModelResources(
                badgeCatalog,
                modifierCatalog,
                enableDebugLogs,
                progressionRewardTables,
                progressionEventCatalog);

            InventoryScreen inventoryScreen = appliedController != null
                ? appliedController.GetInventoryScreen() ?? appliedController.GetComponentInChildren<InventoryScreen>(true)
                : partyScreen.GetComponentInParent<PlayerMenuController>(true)?.GetInventoryScreen();
            inventoryScreen?.ConfigureSpeciesDatabase(speciesDatabase);
        }

        private void WireDetailScreenReferences(ChainlingDetailScreen detailScreen, MonoBehaviour serviceProvider)
        {
            detailScreen.Configure(
                supabaseConfig,
                speciesDatabase,
                visualDatabase,
                identityDatabase,
                moveDatabase,
                progressionConfig,
                serviceProvider,
                serviceProvider,
                serviceProvider,
                serviceProvider,
                serviceProvider);
        }

        private static ChainlingDetailScreen FindDetailScreen(PlayerMenuController controller)
        {
            if (controller == null)
                return null;

            MenuScreenStack stack = controller.GetScreenStack();
            if (stack == null)
                return null;

            ChainlingDetailScreen[] screens = stack.GetComponentsInChildren<ChainlingDetailScreen>(true);
            return screens.Length > 0 ? screens[0] : null;
        }

        private void ResolveDataReferences()
        {
            if (supabaseConfig == null)
                supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");

            if (menuViews == null)
                menuViews = Resources.Load<ChainlingsMenuViewsSO>(MenuViewsResourcePath);
        }

        private static bool IsEditor()
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private static bool IsDevelopmentBuild()
        {
#if DEVELOPMENT_BUILD
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }
    }
}
