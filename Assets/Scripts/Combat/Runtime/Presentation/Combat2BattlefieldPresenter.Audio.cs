using UnityEngine;

namespace Chainbound.Combat
{
    public partial class Combat2BattlefieldPresenter
    {
        [Header("Audio")]
        [Tooltip("Optional catalog used to create a local CombatSfxOrchestrator when no orchestrator is assigned or discoverable.")]
        [SerializeField] private CombatBattleSfxCatalogSO battleSfxCatalog;
        [Tooltip("Optional MonoBehaviour implementing ICombatSfxOrchestrator.")]
        [SerializeField] private MonoBehaviour combatSfxOrchestratorBehaviour;
        [Tooltip("Optional MonoBehaviour implementing ICombatAudioBridge. Phase-authored VFX sounds use this shared bridge resolver.")]
        [SerializeField] private MonoBehaviour combatAudioBridgeBehaviour;
        [SerializeField] private bool autoDiscoverCombatAudioBridge = true;

        private ICombatSfxOrchestrator _combatSfxOrchestrator;
        private CombatSfxOrchestrator _runtimeCombatSfxOrchestrator;
        private ICombatAudioBridge _combatAudioBridge;

        private ICombatSfxOrchestrator CombatSfx => ResolveCombatSfxOrchestrator();

        private ICombatAudioBridge CombatAudioBridge => ResolveCombatAudioBridge();

        private void PlayPhaseAudio(
            SoundEventSO soundEvent,
            Vector3 position,
            float delaySeconds,
            float volumeScale = 1f)
        {
            if (soundEvent == null)
                return;

            float clampedScale = Mathf.Clamp01(volumeScale);
            if (clampedScale <= 0f)
                return;

            if (delaySeconds > 0f)
            {
                StartCoroutine(PlayPhaseAudioDelayed(soundEvent, position, delaySeconds, clampedScale));
                return;
            }

            CombatAudioBridge?.PlayOneShot(soundEvent, position, null, clampedScale);
        }

        private System.Collections.IEnumerator PlayPhaseAudioDelayed(
            SoundEventSO soundEvent,
            Vector3 position,
            float delaySeconds,
            float volumeScale)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));
            CombatAudioBridge?.PlayOneShot(soundEvent, position, null, Mathf.Clamp01(volumeScale));
        }

        private float ResolveImpactPhaseAudioVolumeScale(CombatVFXLibrary.VFXEntry entry)
        {
            bool hasOutcomeImpact = entry != null && entry.impactSound != null;
            CombatBattleSfxCatalogSO catalog = battleSfxCatalog;
            if (catalog == null && CombatSfx is CombatSfxOrchestrator concrete)
                catalog = concrete.Catalog;

            if (catalog != null)
                return catalog.ResolveImpactPhaseAccentVolumeScale(hasOutcomeImpact);

            return hasOutcomeImpact ? 0.6f : 1f;
        }

        private ICombatAudioBridge ResolveCombatAudioBridge()
        {
            if (_combatAudioBridge != null)
                return _combatAudioBridge;

            MonoBehaviour resolvedBehaviour;
            _combatAudioBridge = CombatAudioBridgeResolver.Resolve(
                combatAudioBridgeBehaviour,
                autoDiscoverCombatAudioBridge,
                out resolvedBehaviour);
            if (resolvedBehaviour != null)
                combatAudioBridgeBehaviour = resolvedBehaviour;
            return _combatAudioBridge;
        }

        private ICombatSfxOrchestrator ResolveCombatSfxOrchestrator()
        {
            if (_combatSfxOrchestrator != null)
                return _combatSfxOrchestrator;

            if (combatSfxOrchestratorBehaviour != null && combatSfxOrchestratorBehaviour is ICombatSfxOrchestrator assigned)
            {
                _combatSfxOrchestrator = assigned;
                ConfigureConcreteSfxOrchestrator(assigned);
                return _combatSfxOrchestrator;
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatSfxOrchestrator discovered)
                {
                    combatSfxOrchestratorBehaviour = behaviours[i];
                    _combatSfxOrchestrator = discovered;
                    ConfigureConcreteSfxOrchestrator(discovered);
                    return _combatSfxOrchestrator;
                }
            }

            if (battleSfxCatalog == null)
                return null;

            if (_runtimeCombatSfxOrchestrator == null)
                _runtimeCombatSfxOrchestrator = gameObject.AddComponent<CombatSfxOrchestrator>();

            _runtimeCombatSfxOrchestrator.SetCatalog(battleSfxCatalog);
            combatSfxOrchestratorBehaviour = _runtimeCombatSfxOrchestrator;
            _combatSfxOrchestrator = _runtimeCombatSfxOrchestrator;
            return _combatSfxOrchestrator;
        }

        private void ConfigureConcreteSfxOrchestrator(ICombatSfxOrchestrator orchestrator)
        {
            if (battleSfxCatalog == null || !(orchestrator is CombatSfxOrchestrator concrete))
                return;

            if (concrete.Catalog == null)
                concrete.SetCatalog(battleSfxCatalog);
        }
    }
}
