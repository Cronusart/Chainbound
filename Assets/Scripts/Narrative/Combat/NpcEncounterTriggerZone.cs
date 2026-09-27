using System.Collections;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Collider trigger for NPC and boss encounters. Optionally plays dialogue,
    /// then launches combat using a configured manual encounter provider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NpcEncounterTriggerZone : MonoBehaviour
    {
        [Header("Encounter")]
        [SerializeField] private ManualEncounterProviderSO encounterConfig;
        [SerializeField] private DialogueSequenceSO preEncounterDialogue;

        [Header("Battle Scene")]
        [SerializeField] private string battleSceneName = "Combat2_TestHarness";
        [SerializeField] private bool useSceneLoader = true;
        [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.QuickFade;

        [Header("Trigger")]
        [SerializeField] private LayerMask playerLayer = ~0;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private bool requireExitBeforeRetrigger = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private bool _hasTriggered;
        private bool _isProcessing;
        private bool _canTrigger = true;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isProcessing || !_canTrigger || (triggerOnce && _hasTriggered))
                return;

            if (!IsPlayer(other))
                return;

            if (InputLockService.BlocksWorldGameplay)
            {
                if (enableDebugLogs)
                    Debug.Log("[NpcEncounterTriggerZone] Skipped launch because world gameplay is frozen.");
                return;
            }

            SanctuaryLatchZone.TryLatchNearestPortal(other.transform.position);
            StartCoroutine(BeginEncounter(other.transform));
        }

        private void OnTriggerExit(Collider other)
        {
            if (!requireExitBeforeRetrigger || !IsPlayer(other))
                return;

            _canTrigger = true;
        }

        private IEnumerator BeginEncounter(Transform playerTransform)
        {
            if (encounterConfig == null)
            {
                Debug.LogError("[NpcEncounterTriggerZone] No ManualEncounterProviderSO assigned.");
                yield break;
            }

            SolanaServiceBridge solanaBridge = SolanaServiceBridge.Instance != null
                ? SolanaServiceBridge.Instance
                : FindObjectOfType<SolanaServiceBridge>(true);

            if (solanaBridge == null)
            {
                Debug.LogError("[NpcEncounterTriggerZone] SolanaServiceBridge not found. Cannot launch encounter.");
                yield break;
            }

            EncounterTransitionBridge transitionBridge = EncounterTransitionBridge.Instance;
            if (transitionBridge == null)
            {
                Debug.LogError("[NpcEncounterTriggerZone] EncounterTransitionBridge not found. Cannot launch encounter.");
                yield break;
            }

            _isProcessing = true;
            _canTrigger = !requireExitBeforeRetrigger;
            _hasTriggered = true;

            if (preEncounterDialogue != null)
                yield return PlayDialogueIfPossible(preEncounterDialogue);

            if (enableDebugLogs)
            {
                Debug.Log($"[NpcEncounterTriggerZone] Launching battle scene '{battleSceneName}' for encounter '{encounterConfig.name}'.");
            }

            yield return transitionBridge.BeginEncounterTransition(new EncounterTransitionBridge.EncounterTransitionRequest
            {
                playerTransform = playerTransform,
                battleSceneName = battleSceneName,
                transitionStyle = transitionStyle,
                useSceneLoader = useSceneLoader,
                launchSource = EncounterTransitionBridge.EncounterLaunchSource.ManualEncounter,
                authoritativeNarrativeEncounterId = encounterConfig != null ? encounterConfig.name : "",
                onReadyToLoad = () => solanaBridge.SetActiveManualEncounter(encounterConfig),
            });

            _isProcessing = false;
        }

        private static IEnumerator PlayDialogueIfPossible(DialogueSequenceSO dialogueSequence)
        {
            DialogueService dialogueService = DialogueService.Instance;
            if (dialogueService == null)
                yield break;

            Coroutine dialogueCoroutine = dialogueService.PlayDialogue(dialogueSequence);
            if (dialogueCoroutine != null)
                yield return dialogueCoroutine;

            while (dialogueService.IsPlaying)
                yield return null;
        }

        private bool IsPlayer(Collider other)
        {
            if (other == null)
                return false;

            if (playerLayer != ~0 && ((1 << other.gameObject.layer) & playerLayer) == 0)
                return false;

            return other.CompareTag("Player");
        }
    }
}
