using System.Collections;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Auto-flees a scripted intro encounter after the intro presentation has completed.
    /// Kept disarmed by default because the combat scene is shared.
    /// </summary>
    [RequireComponent(typeof(CombatBattleController))]
    public class IntroEncounterAutoEscaper : MonoBehaviour
    {
        [SerializeField] private CombatBattleController battleController;
        [SerializeField] private float delayAfterStart = 0.25f;
        [SerializeField] private bool waitForBattleIntroToComplete = true;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool armedOnEnable = false;
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Dialogue (before flee)")]
        [Tooltip("Played after the battle intro panel finishes and before the scripted escape. Requires DialogueService in scene.")]
        [SerializeField] private DialogueSequenceSO postIntroDialogue;
        [Tooltip("Override input lock during post-intro dialogue. Null = DialogueService default.")]
        [SerializeField] private bool lockInputDuringPostIntroDialogue = true;

        private Coroutine _fleeRoutine;
        private bool _isArmed;

        /// <summary>
        /// True after <see cref="SetArmed"/> runs. Prevents <see cref="Awake"/> from overwriting
        /// installer-driven arming when bootstrap runs before this component's Awake (script order).
        /// </summary>
        private bool _armedSetExternally;

        public bool IsArmed => _isArmed;

        private void Awake()
        {
            if (battleController == null)
                battleController = GetComponent<CombatBattleController>();

            // CombatSceneInstaller may call SetArmed(true) during bootstrap before this Awake runs.
            // Do not clobber that with armedOnEnable=false here.
            if (!_armedSetExternally)
                _isArmed = armedOnEnable;

            LogTrace(
                $"Awake (instanceId={GetEntityId()}): activeInHierarchy={gameObject.activeInHierarchy}, " +
                $"componentEnabled={enabled}, armedOnEnable={armedOnEnable}, _armedSetExternally={_armedSetExternally}, " +
                $"_isArmed={_isArmed}, battleController={(battleController != null ? battleController.name : "null")}");
        }

        private void OnEnable()
        {
            if (battleController == null)
                battleController = GetComponent<CombatBattleController>();

            if (battleController != null)
            {
                battleController.OnBattleStarted += HandleBattleStarted;
                LogTrace(
                    $"OnEnable: subscribed to OnBattleStarted on '{battleController.name}' " +
                    $"(instanceId={GetEntityId()}, _isArmed={_isArmed})");
            }
            else
            {
                Debug.LogError(
                    "[IntroEncounterAutoEscaper] OnEnable: CombatBattleController missing; cannot subscribe to OnBattleStarted.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (battleController != null)
            {
                battleController.OnBattleStarted -= HandleBattleStarted;
                LogTrace($"OnDisable: unsubscribed from OnBattleStarted on '{battleController.name}'.");
            }

            CancelPendingFlee();
        }

        public void SetArmed(bool armed)
        {
            _armedSetExternally = true;
            bool was = _isArmed;
            _isArmed = armed;
            LogTrace($"SetArmed: {was} -> {armed} (instanceId={GetEntityId()}, go='{gameObject.name}')");
            if (!armed)
                CancelPendingFlee();
        }

        private void HandleBattleStarted(CombatState state)
        {
            LogTrace(
                $"HandleBattleStarted: _isArmed={_isArmed}, battleControllerNull={battleController == null}, " +
                $"introPending={(battleController != null ? battleController.IsBattleIntroPending.ToString() : "n/a")}");

            if (!_isArmed || battleController == null)
            {
                LogTrace(
                    $"HandleBattleStarted ignored: _isArmed={_isArmed}, battleControllerNull={battleController == null}");
                return;
            }

            CancelPendingFlee();
            LogTrace("HandleBattleStarted: starting FleeAfterBattleStart coroutine.");
            _fleeRoutine = StartCoroutine(FleeAfterBattleStart());
        }

        private IEnumerator FleeAfterBattleStart()
        {
            LogTrace(
                $"FleeAfterBattleStart: begin. waitForBattleIntro={waitForBattleIntroToComplete}, " +
                $"postIntroDialogue={(postIntroDialogue != null ? postIntroDialogue.name : "null")}, " +
                $"delayAfterStart={delayAfterStart}");

            if (waitForBattleIntroToComplete)
            {
                LogTrace("FleeAfterBattleStart: waiting until IsBattleIntroPending is false...");

                float nextHeartbeat = 0f;
                while (battleController != null
                    && battleController.BattleStarted
                    && battleController.State != null
                    && battleController.State.isActive
                    && battleController.IsBattleIntroPending)
                {
                    if (enableDebugLogs && Time.unscaledTime >= nextHeartbeat)
                    {
                        nextHeartbeat = Time.unscaledTime + 1f;
                        Debug.Log(
                            "[IntroEncounterAutoEscaper] (heartbeat) still waiting for battle intro: " +
                            $"IsBattleIntroPending={battleController.IsBattleIntroPending}, " +
                            $"BattleStarted={battleController.BattleStarted}, StateActive={battleController.State.isActive}",
                            this);
                    }

                    yield return null;
                }

                LogTrace(
                    $"FleeAfterBattleStart: intro wait ended. IsBattleIntroPending={(battleController != null ? battleController.IsBattleIntroPending.ToString() : "n/a")}");
            }
            else
            {
                LogTrace("FleeAfterBattleStart: skipping intro wait (waitForBattleIntroToComplete=false).");
            }

            if (postIntroDialogue != null)
            {
                LogTrace($"FleeAfterBattleStart: entering post-intro dialogue pipeline for '{postIntroDialogue.name}'.");
                yield return PlayPostIntroDialogueCoroutine();
                LogTrace("FleeAfterBattleStart: post-intro dialogue pipeline returned.");
            }
            else
            {
                LogTrace("FleeAfterBattleStart: no postIntroDialogue assigned; skipping dialogue.");
            }

            float remainingDelay = Mathf.Max(0f, delayAfterStart);
            LogTrace($"FleeAfterBattleStart: pre-flee delay {remainingDelay}s (useUnscaledTime={useUnscaledTime}).");
            while (remainingDelay > 0f)
            {
                remainingDelay -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            _fleeRoutine = null;

            if (!_isArmed || battleController == null || !battleController.BattleStarted)
            {
                LogTrace(
                    $"FleeAfterBattleStart: aborting flee — _isArmed={_isArmed}, battleControllerNull={battleController == null}, " +
                    $"BattleStarted={(battleController != null && battleController.BattleStarted)}");
                yield break;
            }

            if (battleController.State == null || !battleController.State.isActive)
            {
                LogTrace("FleeAfterBattleStart: aborting flee — battle state inactive or null.");
                yield break;
            }

            LogTrace("FleeAfterBattleStart: calling ForceFleeForScriptedEncounter().");
            FleeRequestResult result = battleController.ForceFleeForScriptedEncounter();
            if (!result.Accepted)
                LogTrace($"FleeAfterBattleStart: scripted flee was denied: {result.Message}");
        }

        private IEnumerator PlayPostIntroDialogueCoroutine()
        {
            LogTrace($"PlayPostIntroDialogue: sequence='{postIntroDialogue?.name ?? "null"}'");

            DialogueService dialogueService = DialogueService.Instance;
            if (dialogueService == null)
            {
                Debug.LogWarning(
                    "[IntroEncounterAutoEscaper] DialogueService.Instance is null — post-intro dialogue skipped. " +
                    "Ensure DialogueService exists in this scene (e.g. bootstrap / UI root).",
                    this);
                yield break;
            }

            LogTrace($"PlayPostIntroDialogue: DialogueService found, calling PlayDialogue (lockInput={lockInputDuringPostIntroDialogue}).");

            Coroutine dialogueCoroutine = dialogueService.PlayDialogue(postIntroDialogue, lockInputDuringPostIntroDialogue);
            if (dialogueCoroutine != null)
            {
                LogTrace("PlayPostIntroDialogue: yielding PlayDialogue coroutine.");
                yield return dialogueCoroutine;
                LogTrace("PlayPostIntroDialogue: PlayDialogue coroutine completed.");
            }
            else
            {
                LogTrace("PlayPostIntroDialogue: PlayDialogue returned null coroutine (dialogue may still run async).");
            }

            float stuckCheck = 0f;
            while (dialogueService.IsPlaying)
            {
                if (enableDebugLogs && Time.unscaledTime >= stuckCheck)
                {
                    stuckCheck = Time.unscaledTime + 2f;
                    Debug.Log(
                        "[IntroEncounterAutoEscaper] (heartbeat) waiting for DialogueService.IsPlaying to clear...",
                        this);
                }

                yield return null;
            }

            LogTrace("PlayPostIntroDialogue: IsPlaying is false; dialogue phase complete.");
        }

        private void CancelPendingFlee()
        {
            if (_fleeRoutine == null)
                return;

            LogTrace("CancelPendingFlee: stopping pending flee coroutine.");
            StopCoroutine(_fleeRoutine);
            _fleeRoutine = null;
        }

        private void LogTrace(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.Log($"[IntroEncounterAutoEscaper] {message}", this);
        }
    }
}
