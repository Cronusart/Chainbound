using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SignalJam.UI.MenuNavigation
{
    public enum ChainlingsDataSourceMode
    {
        Production = 0,
        Harness = 1,
    }

    /// <summary>
    /// Editor/dev-build settings for player menu data sources.
    /// Release builds always force production Chainlings data regardless of this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerMenuDevConfig",
        menuName = "Chainbound/UI/Player Menu Dev Config")]
    public sealed class PlayerMenuDevConfigSO : ScriptableObject
    {
        [Header("Chainlings Data")]
        [Tooltip("Production uses Supabase providers. Harness uses in-memory sandbox roster data.")]
        [SerializeField] private ChainlingsDataSourceMode chainlingsDataSource = ChainlingsDataSourceMode.Production;

        [Header("Menu Gates")]
        [Tooltip("When enabled, the player menu opens even if gameplay menu availability is false.")]
        [SerializeField] private bool bypassAvailabilityGate;

        [Header("Harness Generation")]
        [SerializeField] private int harnessSeed = 1776;
        [SerializeField, Range(1, 24)] private int generatedRosterSize = 9;
        [SerializeField, Range(1, 3)] private int generatedPartySize = 3;
        [SerializeField, Range(1, 60)] private int generatedMoveInventorySize = 30;
        [SerializeField] private bool regenerateHarnessOnEnable = true;
        [SerializeField] private bool unlockAllCosmetics = true;
        [FormerlySerializedAs("includeRebirthReadyChainling")]
        [SerializeField] private bool includeCycleReadyChainling = true;
        [SerializeField] private bool allowEntangledVariants = true;

        public ChainlingsDataSourceMode ChainlingsDataSource => chainlingsDataSource;
        public bool BypassAvailabilityGate => bypassAvailabilityGate;
        public int HarnessSeed => harnessSeed;
        public int GeneratedRosterSize => generatedRosterSize;
        public int GeneratedPartySize => generatedPartySize;
        public int GeneratedMoveInventorySize => generatedMoveInventorySize;
        public bool RegenerateHarnessOnEnable => regenerateHarnessOnEnable;
        public bool UnlockAllCosmetics => unlockAllCosmetics;
        public bool IncludeCycleReadyChainling => includeCycleReadyChainling;
        [Obsolete("Use IncludeCycleReadyChainling.")]
        public bool IncludeRebirthReadyChainling => includeCycleReadyChainling;
        public bool AllowEntangledVariants => allowEntangledVariants;

        public void SetChainlingsDataSource(ChainlingsDataSourceMode mode)
        {
            chainlingsDataSource = mode;
        }

        public void SetBypassAvailabilityGate(bool bypass)
        {
            bypassAvailabilityGate = bypass;
        }

        public bool ShouldUseHarness(bool inEditor, bool isDevelopmentBuild)
        {
            if (!inEditor && !isDevelopmentBuild)
                return false;

            return chainlingsDataSource == ChainlingsDataSourceMode.Harness;
        }

        public bool ShouldBypassAvailabilityGate(bool inEditor, bool isDevelopmentBuild)
        {
            if (!inEditor && !isDevelopmentBuild)
                return false;

            return bypassAvailabilityGate;
        }
    }
}
