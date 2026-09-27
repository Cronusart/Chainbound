using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Immutable launch context copied from narrative content into a transient bridge before combat scene load.
    /// </summary>
    public sealed class NarrativeCombatLaunchContext
    {
        public NarrativeCombatLaunchContext(
            string encounterId,
            ManualEncounterProviderSO provider,
            ManualPartyProviderSO playerPartyOverrideOrFallback,
            bool disableSupabasePartyResolution,
            bool disableOnChainLoadoutResolution,
            bool armIntroAutoEscaper,
            EncounterFleePolicy fleePolicyOverride)
        {
            EncounterId = encounterId ?? string.Empty;
            Provider = provider;
            PlayerPartyOverrideOrFallback = playerPartyOverrideOrFallback;
            DisableSupabasePartyResolution = disableSupabasePartyResolution;
            DisableOnChainLoadoutResolution = disableOnChainLoadoutResolution;
            ArmIntroAutoEscaper = armIntroAutoEscaper;
            FleePolicyOverride = fleePolicyOverride;
        }

        public string EncounterId { get; }
        public ManualEncounterProviderSO Provider { get; }
        public ManualPartyProviderSO PlayerPartyOverrideOrFallback { get; }
        public bool DisableSupabasePartyResolution { get; }
        public bool DisableOnChainLoadoutResolution { get; }
        public bool ArmIntroAutoEscaper { get; }
        public EncounterFleePolicy FleePolicyOverride { get; }

        public static NarrativeCombatLaunchContext Create(
            string encounterId,
            ManualEncounterProviderSO provider,
            NarrativeCombatLaunchProfileSO launchProfile,
            EncounterConfig config)
        {
            return new NarrativeCombatLaunchContext(
                encounterId,
                provider,
                launchProfile != null ? launchProfile.PlayerPartyOverrideOrFallback : null,
                launchProfile != null && launchProfile.DisableSupabasePartyResolution,
                launchProfile != null && launchProfile.DisableOnChainLoadoutResolution,
                launchProfile != null && launchProfile.ArmIntroAutoEscaper,
                config != null && !config.canRun
                    ? EncounterFleePolicy.Locked
                    : EncounterFleePolicy.UseDifficultyDefault);
        }
    }

    /// <summary>
    /// One-shot runtime bridge used to carry narrative-authored combat launch context across scene load.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarrativeCombatLaunchBridge : MonoBehaviour
    {
        private static NarrativeCombatLaunchBridge _instance;

        private NarrativeCombatLaunchContext _pendingContext;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public static NarrativeCombatLaunchBridge FindExisting()
        {
            return _instance != null ? _instance : FindObjectOfType<NarrativeCombatLaunchBridge>(true);
        }

        public static NarrativeCombatLaunchBridge GetOrCreate(Transform fallbackParent)
        {
            NarrativeCombatLaunchBridge existing = FindExisting();
            if (existing != null)
                return existing;

            GameObject bridgeObject = new GameObject(nameof(NarrativeCombatLaunchBridge));
            GameServicesContainer servicesContainer = GameServicesContainer.Instance != null
                ? GameServicesContainer.Instance
                : FindObjectOfType<GameServicesContainer>(true);

            if (servicesContainer != null)
            {
                bridgeObject.transform.SetParent(servicesContainer.transform, false);
            }
            else if (fallbackParent != null)
            {
                bridgeObject.transform.SetParent(fallbackParent, false);
            }

            NarrativeCombatLaunchBridge created = bridgeObject.AddComponent<NarrativeCombatLaunchBridge>();
            if (servicesContainer == null)
                DontDestroyOnLoad(bridgeObject);

            return created;
        }

        public void Publish(NarrativeCombatLaunchContext context)
        {
            _pendingContext = context;
        }

        public bool TryConsume(out NarrativeCombatLaunchContext context)
        {
            context = _pendingContext;
            _pendingContext = null;
            return context != null;
        }

        /// <summary>
        /// Read pending context without removing it so callers can validate before consuming.
        /// </summary>
        public bool TryPeek(out NarrativeCombatLaunchContext context)
        {
            context = _pendingContext;
            return context != null;
        }

        /// <summary>
        /// Clears pending context without destroying the bridge (use when validation fails after peek).
        /// </summary>
        public void ConsumePending()
        {
            _pendingContext = null;
        }

        public void Clear()
        {
            _pendingContext = null;
            Destroy(gameObject);
        }
    }
}
