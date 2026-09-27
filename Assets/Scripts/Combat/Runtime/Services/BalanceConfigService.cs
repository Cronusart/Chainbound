using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// GameServices-hosted holder for default lever boards and active ruleset/overlay.
    /// Discovery via <see cref="BalanceConfigRuntime"/> (same pattern as FavorRuntime).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BalanceConfigService : MonoBehaviour
    {
        [Header("Defaults")]
        [SerializeField] private CombatRulesetSO defaultRuleset;
        [SerializeField] private CombatResolverConfigSO defaultResolverConfig;
        [SerializeField] private PostBattleRewardsConfigSO postBattleRewardsConfig;
        [SerializeField] private ProgressionConfigSO progressionConfig;
        [SerializeField] private VendingConfigSO vendingConfig;
        [SerializeField] private ChainlingRecoveryConfigSO chainlingRecoveryConfig;
        [SerializeField] private CaptureBalanceConfigSO captureBalanceConfig;
        [SerializeField] private CapturePresentationConfigSO capturePresentationConfig;
        [SerializeField] private ModifierBalanceOverlaySO modifierOverlay;
        [SerializeField] private BattleIntroCopyConfigSO battleIntroCopyConfig;

        private CombatRulesetSO _activeRulesetOverride;
        private ModifierBalanceOverlaySO _activeOverlayOverride;
        private VendingConfigSO _activeVendingOverride;
        private ChainlingRecoveryConfigSO _activeChainlingRecoveryOverride;
        private CaptureBalanceConfigSO _activeCaptureBalanceOverride;

        public CombatRulesetSO DefaultRuleset => defaultRuleset;
        public CombatResolverConfigSO DefaultResolverConfig => defaultResolverConfig;
        public PostBattleRewardsConfigSO PostBattleRewardsConfig => postBattleRewardsConfig;
        public ProgressionConfigSO ProgressionConfig => progressionConfig;
        public VendingConfigSO VendingConfig => _activeVendingOverride != null ? _activeVendingOverride : vendingConfig;
        public ChainlingRecoveryConfigSO ChainlingRecoveryConfig =>
            _activeChainlingRecoveryOverride != null ? _activeChainlingRecoveryOverride : chainlingRecoveryConfig;
        public CaptureBalanceConfigSO CaptureBalanceConfig =>
            _activeCaptureBalanceOverride != null ? _activeCaptureBalanceOverride : captureBalanceConfig;
        public CapturePresentationConfigSO CapturePresentationConfig => capturePresentationConfig;
        public ModifierBalanceOverlaySO ModifierOverlay =>
            _activeOverlayOverride != null ? _activeOverlayOverride : modifierOverlay;
        public BattleIntroCopyConfigSO BattleIntroCopyConfig => battleIntroCopyConfig;

        public CombatRulesetSO ActiveRuleset =>
            _activeRulesetOverride != null
                ? _activeRulesetOverride
                : defaultRuleset;

        private void OnEnable()
        {
            BalanceConfigRuntime.Register(this);
        }

        private void OnDisable()
        {
            BalanceConfigRuntime.Unregister(this);
        }

        public void Configure(
            CombatRulesetSO ruleset = null,
            CombatResolverConfigSO resolver = null,
            PostBattleRewardsConfigSO rewards = null,
            ProgressionConfigSO progression = null,
            VendingConfigSO vending = null,
            ChainlingRecoveryConfigSO chainlingRecovery = null,
            ModifierBalanceOverlaySO overlay = null,
            BattleIntroCopyConfigSO introCopy = null,
            CaptureBalanceConfigSO captureBalance = null,
            CapturePresentationConfigSO capturePresentation = null)
        {
            if (ruleset != null)
                defaultRuleset = ruleset;
            if (resolver != null)
                defaultResolverConfig = resolver;
            if (rewards != null)
                postBattleRewardsConfig = rewards;
            if (progression != null)
                progressionConfig = progression;
            if (vending != null)
                vendingConfig = vending;
            if (chainlingRecovery != null)
                chainlingRecoveryConfig = chainlingRecovery;
            if (captureBalance != null)
                captureBalanceConfig = captureBalance;
            if (capturePresentation != null)
                capturePresentationConfig = capturePresentation;
            if (overlay != null)
                modifierOverlay = overlay;
            if (introCopy != null)
                battleIntroCopyConfig = introCopy;
        }

        public void SetActiveRulesetOverride(CombatRulesetSO ruleset)
        {
            _activeRulesetOverride = ruleset;
        }

        public void ClearActiveRulesetOverride()
        {
            _activeRulesetOverride = null;
        }

        public void SetModifierOverlayOverride(ModifierBalanceOverlaySO overlay)
        {
            _activeOverlayOverride = overlay;
        }

        public void ClearModifierOverlayOverride()
        {
            _activeOverlayOverride = null;
        }

        public void SetVendingOverride(VendingConfigSO config)
        {
            _activeVendingOverride = config;
        }

        public void ClearVendingOverride()
        {
            _activeVendingOverride = null;
        }

        public void SetChainlingRecoveryOverride(ChainlingRecoveryConfigSO config)
        {
            _activeChainlingRecoveryOverride = config;
        }

        public void ClearChainlingRecoveryOverride()
        {
            _activeChainlingRecoveryOverride = null;
        }

        public void SetCaptureBalanceOverride(CaptureBalanceConfigSO config)
        {
            _activeCaptureBalanceOverride = config;
        }

        public void ClearCaptureBalanceOverride()
        {
            _activeCaptureBalanceOverride = null;
        }
    }

    /// <summary>
    /// Scene-scoped balance board lookup with CreateRuntimeDefault fallbacks.
    /// </summary>
    public static class BalanceConfigRuntime
    {
        private static BalanceConfigService active;

        public static BalanceConfigService Active => active;

        public static CombatRulesetSO Ruleset
        {
            get
            {
                if (active != null && active.ActiveRuleset != null)
                    return active.ActiveRuleset;
                return CombatRulesetSO.CreateRuntimeDefault();
            }
        }

        public static CombatResolverConfigSO ResolverConfig
        {
            get
            {
                CombatRulesetSO ruleset = active != null ? active.ActiveRuleset : null;
                if (ruleset != null && ruleset.resolverConfig != null)
                    return ruleset.resolverConfig;
                if (active != null && active.DefaultResolverConfig != null)
                    return active.DefaultResolverConfig;
                return CombatResolverConfigSO.CreateRuntimeDefault();
            }
        }

        public static PostBattleRewardsConfigSO PostBattleRewards
        {
            get
            {
                if (active != null && active.PostBattleRewardsConfig != null)
                    return active.PostBattleRewardsConfig;
                return PostBattleRewardsConfigSO.CreateRuntimeDefault();
            }
        }

        public static ProgressionConfigSO Progression
        {
            get
            {
                if (active != null && active.ProgressionConfig != null)
                    return active.ProgressionConfig;
                return ProgressionConfigSO.CreateRuntimeDefault();
            }
        }

        public static VendingConfigSO Vending
        {
            get
            {
                if (active != null && active.VendingConfig != null)
                    return active.VendingConfig;
                return VendingConfigSO.CreateRuntimeDefault();
            }
        }

        public static ChainlingRecoveryConfigSO ChainlingRecovery
        {
            get
            {
                if (active != null && active.ChainlingRecoveryConfig != null)
                    return active.ChainlingRecoveryConfig;
                return ChainlingRecoveryConfigSO.CreateRuntimeDefault();
            }
        }

        public static CaptureBalanceConfigSO CaptureBalance
        {
            get
            {
                if (active != null && active.CaptureBalanceConfig != null)
                    return active.CaptureBalanceConfig;
                return CaptureBalanceConfigSO.CreateRuntimeDefault();
            }
        }

        public static CapturePresentationConfigSO CapturePresentation
        {
            get
            {
                if (active != null && active.CapturePresentationConfig != null)
                    return active.CapturePresentationConfig;
                var config = ScriptableObject.CreateInstance<CapturePresentationConfigSO>();
                config.hideFlags = HideFlags.HideAndDontSave;
                return config;
            }
        }

        public static ModifierBalanceOverlaySO ActiveModifierOverlay
        {
            get
            {
                if (active != null)
                    return active.ModifierOverlay;
                return null;
            }
        }

        public static BattleIntroCopyConfigSO BattleIntroCopy
        {
            get
            {
                if (active != null && active.BattleIntroCopyConfig != null)
                    return active.BattleIntroCopyConfig;
                return BattleIntroCopyConfigSO.CreateRuntimeDefault();
            }
        }

        public static void Register(BalanceConfigService service)
        {
            if (service != null)
                active = service;
        }

        public static void Unregister(BalanceConfigService service)
        {
            if (active == service)
                active = null;
        }
    }
}
