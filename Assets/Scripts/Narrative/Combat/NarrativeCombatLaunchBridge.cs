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

        // The payload is static so CombatSceneInstaller can still read it if this
        // object is destroyed while the battle scene activates. Scene unload can
        // dispose the launch coroutine before the destination's Awake.
        private static NarrativeCombatLaunchContext _pendingPayload;

        private NarrativeCombatLaunchContext _pendingContext;

        public static bool HasPending => _pendingPayload != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _pendingPayload = null;
        }

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

            Transform parent = servicesContainer != null ? servicesContainer.transform : null;
            if (parent == null && fallbackParent != null && IsPersistent(fallbackParent.gameObject))
                parent = fallbackParent;

            if (parent != null)
                bridgeObject.transform.SetParent(parent, false);

            NarrativeCombatLaunchBridge created = bridgeObject.AddComponent<NarrativeCombatLaunchBridge>();
            // DontDestroyOnLoad is ignored on children. A scene-parented bridge is
            // destroyed with the overworld before the battle scene can read it.
            if (created.transform.parent == null)
                DontDestroyOnLoad(bridgeObject);

            return created;
        }

        public void Publish(NarrativeCombatLaunchContext context)
        {
            _pendingContext = context;
            _pendingPayload = context;
        }

        public static bool TryPeekPending(out NarrativeCombatLaunchContext context)
        {
            context = _pendingPayload;
            return context != null;
        }

        public static void ClearPendingPayload()
        {
            _pendingPayload = null;
        }

        public bool TryConsume(out NarrativeCombatLaunchContext context)
        {
            context = _pendingContext ?? _pendingPayload;
            _pendingContext = null;
            _pendingPayload = null;
            return context != null;
        }

        /// <summary>
        /// Read pending context without removing it so callers can validate before consuming.
        /// </summary>
        public bool TryPeek(out NarrativeCombatLaunchContext context)
        {
            context = _pendingContext ?? _pendingPayload;
            return context != null;
        }

        /// <summary>
        /// Clears pending context without destroying the bridge (use when validation fails after peek).
        /// </summary>
        public void ConsumePending()
        {
            _pendingContext = null;
            _pendingPayload = null;
        }

        private static bool IsPersistent(GameObject candidate)
        {
            return candidate != null && candidate.scene.name == "DontDestroyOnLoad";
        }

        /// <summary>
        /// Drops the bridge object. The static payload stays until
        /// <see cref="ConsumePending"/> or <see cref="ClearPendingPayload"/>.
        /// </summary>
        public void ReleaseObject()
        {
            _pendingContext = null;
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        public void Clear()
        {
            ClearPendingPayload();
            ReleaseObject();
        }
    }
}
