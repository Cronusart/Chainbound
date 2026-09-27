using System.Collections;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Optional Assembly-CSharp bridge that routes combat intro requests into DialogueService.
    /// </summary>
    public class BattleIntroBossDialogueBridge : MonoBehaviour
    {
        [SerializeField] private BattleIntroController introController;
        [SerializeField] private DialogueSequenceSO defaultBossIntroDialogue;
        [SerializeField] private bool playOnlyForBoss = true;
        [SerializeField] private bool enableDebugLogs = false;

        private DialogueService _activeDialogueService;
        private DialogueSequenceSO _activeDialogueSequence;
        private DialogueSequenceSO _runtimeBossIntroDialogue;
        private Coroutine _dialogueStartWatchdog;
        private bool _awaitingDialogueComplete;

        private void OnEnable()
        {
            if (introController == null)
                introController = FindObjectOfType<BattleIntroController>(true);

            Subscribe();
        }

        private void OnDisable()
        {
            bool wasAwaitingDialogue = _awaitingDialogueComplete;
            DetachDialogueCallbacks();
            Unsubscribe();

            if (wasAwaitingDialogue && introController != null)
                introController.NotifyExternalIntroComplete();
        }

        public void SetIntroController(BattleIntroController controller)
        {
            if (ReferenceEquals(introController, controller))
                return;

            Unsubscribe();
            introController = controller;
            Subscribe();
        }

        public void SetBossIntroDialogue(DialogueSequenceSO dialogue)
        {
            _runtimeBossIntroDialogue = dialogue;
        }

        public void ClearBossIntroDialogueOverride()
        {
            _runtimeBossIntroDialogue = null;
        }

        private void Subscribe()
        {
            if (introController != null)
                introController.OnExternalIntroRequested += HandleExternalIntroRequested;
        }

        private void Unsubscribe()
        {
            if (introController != null)
                introController.OnExternalIntroRequested -= HandleExternalIntroRequested;
        }

        private bool HandleExternalIntroRequested(BattleIntroRequest request)
        {
            if (playOnlyForBoss && !request.isBoss)
                return false;

            DialogueSequenceSO dialogueToPlay = _runtimeBossIntroDialogue != null
                ? _runtimeBossIntroDialogue
                : defaultBossIntroDialogue;

            if (dialogueToPlay == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[BattleIntroBossDialogueBridge] No boss dialogue assigned. Falling back to combat-local intro.", this);

                return false;
            }

            var dialogueService = DialogueService.Instance;
            if (dialogueService == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[BattleIntroBossDialogueBridge] DialogueService unavailable. Falling back to combat-local intro.", this);

                return false;
            }

            DetachDialogueCallbacks();

            _activeDialogueService = dialogueService;
            _activeDialogueSequence = dialogueToPlay;
            _awaitingDialogueComplete = true;
            _activeDialogueService.OnDialogueComplete += HandleDialogueComplete;
            _activeDialogueService.PlayDialogue(dialogueToPlay);

            _dialogueStartWatchdog = StartCoroutine(WatchForDialogueStartFailure());

            if (enableDebugLogs)
                Debug.Log($"[BattleIntroBossDialogueBridge] Routed intro to dialogue '{dialogueToPlay.name}'.", this);

            return true;
        }

        private IEnumerator WatchForDialogueStartFailure()
        {
            yield return null;
            _dialogueStartWatchdog = null;

            if (!_awaitingDialogueComplete || _activeDialogueService == null)
                yield break;

            bool started = _activeDialogueService.IsPlaying && _activeDialogueService.CurrentSequence == _activeDialogueSequence;
            if (started)
                yield break;

            if (enableDebugLogs)
                Debug.LogWarning("[BattleIntroBossDialogueBridge] Dialogue did not start cleanly. Releasing combat intro wait.", this);

            DetachDialogueCallbacks();
            introController?.NotifyExternalIntroComplete();
        }

        private void HandleDialogueComplete(DialogueSequenceSO sequence)
        {
            if (!_awaitingDialogueComplete)
                return;

            if (_activeDialogueSequence != null && sequence != _activeDialogueSequence)
                return;

            if (enableDebugLogs)
                Debug.Log("[BattleIntroBossDialogueBridge] Dialogue intro complete.", this);

            DetachDialogueCallbacks();
            introController?.NotifyExternalIntroComplete();
        }

        private void DetachDialogueCallbacks()
        {
            if (_dialogueStartWatchdog != null)
            {
                StopCoroutine(_dialogueStartWatchdog);
                _dialogueStartWatchdog = null;
            }

            if (_activeDialogueService != null)
                _activeDialogueService.OnDialogueComplete -= HandleDialogueComplete;

            _activeDialogueService = null;
            _activeDialogueSequence = null;
            _awaitingDialogueComplete = false;
        }
    }
}
