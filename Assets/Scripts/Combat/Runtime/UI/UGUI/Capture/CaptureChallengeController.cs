using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Combat.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Predictive capture presentation. It renders immediately at 30 Hz, but only
    /// an <see cref="ICaptureSession"/> outcome is allowed to end the attempt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaptureChallengeController : MonoBehaviour
    {
        private const string TutorialSeenKey = "Chainbound.Capture.TradingTraceTutorialSeen";

        [Header("Configuration")]
        [SerializeField] private CapturePresentationConfigSO presentation;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "UI";
        [SerializeField] private string holdActionName = "Submit";

        [Header("Authored UI (optional)")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private Image graphArtwork;
        [SerializeField] private CaptureTraceGraphic graph;
        [SerializeField] private Slider meter;
        [SerializeField] private TMP_Text meterLabel;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text instructionLabel;
        [SerializeField] private TMP_Text scannerLabel;
        [SerializeField] private TMP_Text outcomeLabel;
        [SerializeField] private TMP_Text resultBannerLabel;
        [SerializeField] private CaptureHoldSurface interactionSurface;
        [Tooltip("Optional existing mobile HUD X button surface.")]
        [SerializeField] private CaptureHoldSurface hudXSurface;
        [Tooltip("Authored challenge board. Overlay dimmer stays full-screen while this projects.")]
        [SerializeField] private RectTransform panelRoot;

        private readonly object snapshotGate = new object();
        private ICaptureSession session;
        private IClientSteppedCaptureSession clientStepped;
        private CaptureChallengeSnapshot challenge;
        private CaptureSimulationState predicted;
        private CaptureAuthoritativeState pendingAuthority;
        private CaptureAuthoritativeState renderedAuthority;
        private InputAction holdAction;
        private Action<CaptureOutcome> completed;
        private float tickAccumulator;
        private float crossingHapticAt = -999f;
        private float spikeHapticAt = -999f;
        private long inputSequence;
        private bool pointerHeld;
        private bool physicalHeld;
        private bool sentHeld;
        private bool running;

        public event Action<int, bool> MeterPresented;

        public bool IsRunning => running;
        public CaptureChallengeSnapshot Challenge => challenge;

        private void Awake()
        {
            ResolveUi();
            ResolveInput();
            SetOverlayVisible(false);
        }

        private void OnEnable()
        {
            SubscribeSurfaces();
            ResolveInput();
        }

        private void OnDisable()
        {
            UnsubscribeSurfaces();
            ClearHeldInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                ClearHeldInput();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                ClearHeldInput();
        }

        private void Update()
        {
            if (!running || challenge == null || predicted == null)
                return;

            bool submitHeld = holdAction != null && holdAction.enabled && holdAction.IsPressed();
#if UNITY_EDITOR
            bool editorHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
            bool editorHeld = false;
#endif
            physicalHeld = submitHeld || editorHeld;
            ApplyHeldState(pointerHeld || physicalHeld);

            if (clientStepped == null)
            {
                CaptureAuthoritativeState authority = ConsumePendingAuthority();
                if (authority != null)
                    ApplyAuthoritativeSnapshot(authority);
            }

            float tickSeconds = 1f / Mathf.Max(1, challenge.Parameters.TickRate);
            tickAccumulator += Time.unscaledDeltaTime;
            while (tickAccumulator >= tickSeconds && predicted.Outcome == CaptureOutcomeKind.None)
            {
                tickAccumulator -= tickSeconds;
                CaptureTickResult result = clientStepped != null
                    ? clientStepped.Step(sentHeld)
                    : CaptureSimulation.Step(predicted, challenge.Parameters, sentHeld);
                predicted = result.State;
                if (clientStepped == null && predicted.Outcome != CaptureOutcomeKind.None)
                {
                    // The stream authority alone is allowed to lock, escape, or
                    // time out an online attempt. A locally predicted terminal
                    // state can otherwise freeze presentation while the server
                    // continues simulating a different held state.
                    predicted.Outcome = CaptureOutcomeKind.None;
                    predicted.MeterBasisPoints = Mathf.Clamp(predicted.MeterBasisPoints, 1, 9999);
                }
                PresentCrossing(result.EnteredBand, result.LeftBand, result.SpikeOccurred);
                graph?.PushSample(
                    predicted.TargetPositionUnits,
                    predicted.BandPositionUnits,
                    predicted.IsInsideBand,
                    result.SpikeOccurred);
            }

            int remainingTicks = Mathf.Max(0, challenge.Parameters.DurationTicks - predicted.Tick);
            RenderLabels(predicted.MeterBasisPoints, remainingTicks);
        }

        public async Task PlayAsync(ICaptureSession captureSession, Action<CaptureOutcome> onCompleted = null)
        {
            if (captureSession == null)
                throw new ArgumentNullException(nameof(captureSession));
            if (running)
                throw new InvalidOperationException("A capture challenge is already active.");

            ResolveUi();
            ResolveInput();
            session = captureSession;
            clientStepped = captureSession as IClientSteppedCaptureSession;
            challenge = session.Challenge ?? throw new InvalidOperationException("Capture authority did not provide a challenge.");
            completed = onCompleted;
            predicted = clientStepped != null
                ? clientStepped.CurrentState
                : CaptureSimulation.CreateInitial(challenge);
            renderedAuthority = null;
            pendingAuthority = null;
            tickAccumulator = 0f;
            pointerHeld = false;
            physicalHeld = false;
            sentHeld = false;
            inputSequence = 0;
            graph?.SetChallenge(challenge.Parameters);
            graph?.PushSample(predicted.TargetPositionUnits, predicted.BandPositionUnits, predicted.IsInsideBand, false);
            if (clientStepped == null)
                session.SnapshotReceived += HandleAuthoritySnapshot;
            running = true;
            SetOverlayVisible(true);
            RenderChallengeIntel();
            RenderLabels(predicted.MeterBasisPoints, challenge.Parameters.DurationTicks);
            float attemptStartedAt = Time.unscaledTime;

            CaptureOutcome outcome;
            try
            {
                outcome = await session.Completion;
            }
            catch (Exception exception)
            {
                outcome = new CaptureOutcome
                {
                    Kind = CaptureOutcomeKind.Interrupted,
                    AttemptId = challenge.AttemptId,
                    ErrorCode = "capture_stream_interrupted",
                    Message = exception.Message,
                    BattleContinues = true,
                };
            }

            StampAttemptDuration(outcome, attemptStartedAt);
            Finish(outcome);
        }

        /// <summary>
        /// Clears leftover candles, meter, timer, and banners before the board projects in
        /// so a later capture attempt never flashes the previous graph.
        /// </summary>
        public void PrepareFreshAttempt()
        {
            ResolveUi();
            predicted = null;
            challenge = null;
            pendingAuthority = null;
            renderedAuthority = null;
            tickAccumulator = 0f;
            graph?.SetChallenge(null);
            if (meter != null)
                meter.value = 0.5f;
            if (meterLabel != null)
                meterLabel.text = "ESCAPE  50%  LOCK";
            if (timerLabel != null)
                timerLabel.text = "";
            if (instructionLabel != null)
            {
                instructionLabel.text = "";
                instructionLabel.gameObject.SetActive(false);
            }
            if (scannerLabel != null)
            {
                scannerLabel.text = "";
                scannerLabel.gameObject.SetActive(false);
            }
            if (outcomeLabel != null)
            {
                outcomeLabel.text = "";
                outcomeLabel.gameObject.SetActive(false);
            }
            if (resultBannerLabel != null)
            {
                resultBannerLabel.rectTransform.DOKill();
                resultBannerLabel.text = "";
                resultBannerLabel.gameObject.SetActive(false);
            }

            RectTransform panel = ResolvePanelRoot();
            if (panel != null)
            {
                panel.DOKill();
                panel.localScale = new Vector3(0.001f, 0.001f, 1f);
            }

            SetOverlayVisible(false);
        }

        public IEnumerator PlayOpen()
        {
            ResolveUi();
            CapturePresentationConfigSO board = ResolvePresentation();
            RectTransform panel = ResolvePanelRoot();
            if (panel != null)
            {
                panel.DOKill();
                panel.localScale = new Vector3(0.001f, 0.001f, 1f);
            }

            SetOverlayVisible(true);
            if (overlay != null)
            {
                overlay.interactable = false;
                overlay.blocksRaycasts = true;
            }

            if (panel == null)
            {
                if (overlay != null)
                    overlay.interactable = true;
                yield break;
            }

            float lineHeight = Mathf.Clamp(board.panelProjectionLineHeight, 0.01f, 0.25f);
            float xDuration = Mathf.Max(0.01f, board.panelExpandXDuration);
            Sequence projection = DOTween.Sequence().SetUpdate(true);
            projection.Append(panel.DOScaleX(1f, xDuration).SetEase(Ease.OutCubic));
            projection.Join(panel.DOScaleY(lineHeight, xDuration).SetEase(Ease.OutCubic));
            projection.Append(panel.DOScaleY(1f, Mathf.Max(0.01f, board.panelExpandYDuration))
                .SetEase(Ease.OutBack, Mathf.Max(0f, board.panelExpandYOvershoot)));
            yield return projection.WaitForCompletion();

            if (overlay != null)
                overlay.interactable = true;
        }

        public IEnumerator PlayClose()
        {
            ResolveUi();
            CapturePresentationConfigSO board = ResolvePresentation();
            RectTransform panel = ResolvePanelRoot();
            if (overlay != null)
            {
                overlay.interactable = false;
                overlay.blocksRaycasts = false;
            }

            if (panel == null)
            {
                Close();
                yield break;
            }

            panel.DOKill();
            float lineHeight = Mathf.Clamp(board.panelProjectionLineHeight, 0.01f, 0.25f);
            float xDuration = Mathf.Max(0.01f, board.panelExpandXDuration);
            Sequence projection = DOTween.Sequence().SetUpdate(true);
            projection.Append(panel.DOScaleY(lineHeight, Mathf.Max(0.01f, board.panelExpandYDuration))
                .SetEase(Ease.InBack, Mathf.Max(0f, board.panelExpandYOvershoot)));
            projection.Append(panel.DOScaleX(0.001f, xDuration).SetEase(Ease.InCubic));
            projection.Join(panel.DOScaleY(0.001f, xDuration).SetEase(Ease.InCubic));
            yield return projection.WaitForCompletion();
            Close();
        }

        public void Close()
        {
            ClearHeldInput();
            if (session != null)
                session.SnapshotReceived -= HandleAuthoritySnapshot;
            session?.Dispose();
            session = null;
            clientStepped = null;
            running = false;
            PrepareFreshAttempt();
        }

        public void SetHudXSurface(CaptureHoldSurface surface)
        {
            if (hudXSurface == surface)
                return;
            UnsubscribeSurfaces();
            hudXSurface = surface;
            SubscribeSurfaces();
        }

        public void SetOutcomeMessage(string message)
        {
            if (outcomeLabel == null)
                return;
            outcomeLabel.gameObject.SetActive(true);
            outcomeLabel.text = message ?? string.Empty;
        }

        public IEnumerator PresentResultBanner(bool success, string copy, UnityEngine.Object sfx, float holdSeconds)
        {
            EnsureResultBanner();
            if (resultBannerLabel == null)
                yield break;

            if (outcomeLabel != null)
                outcomeLabel.gameObject.SetActive(false);

            resultBannerLabel.gameObject.SetActive(true);
            resultBannerLabel.text = string.IsNullOrWhiteSpace(copy)
                ? (success ? "Success!" : "Failure")
                : copy;
            resultBannerLabel.rectTransform.localScale = Vector3.one * 0.72f;
            resultBannerLabel.color = success
                ? new Color(0.45f, 1f, 0.72f, 1f)
                : new Color(1f, 0.42f, 0.42f, 1f);

            Tween punch = resultBannerLabel.rectTransform
                .DOScale(1f, 0.22f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            ICombatAudioBridge audio = CombatAudioBridgeResolver.Resolve(null, true, out _);
            audio?.PlayOneShot(sfx);

            float elapsed = 0f;
            float wait = Mathf.Max(0.2f, holdSeconds);
            while (elapsed < wait)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (punch != null && punch.IsActive())
                punch.Kill(complete: true);
        }

        private void EnsureResultBanner()
        {
            if (resultBannerLabel != null)
                return;
            ResolveUi();
            if (overlay == null)
                return;

            RectTransform parent = overlay.transform as RectTransform;
            resultBannerLabel = CreateText(
                "ResultBanner",
                parent,
                new Vector2(0.08f, 0.38f),
                new Vector2(0.92f, 0.62f),
                "",
                56f);
            resultBannerLabel.fontStyle = FontStyles.Bold;
            resultBannerLabel.gameObject.SetActive(false);
        }

        private void StampAttemptDuration(CaptureOutcome outcome, float attemptStartedAt)
        {
            if (outcome == null)
                return;

            float wallSeconds = Mathf.Max(0f, Time.unscaledTime - attemptStartedAt);
            if (wallSeconds > 0f)
            {
                outcome.DurationSeconds = wallSeconds;
                return;
            }

            int tickRate = challenge != null ? Mathf.Max(1, challenge.Parameters.TickRate) : 30;
            if (outcome.ElapsedTicks > 0)
                outcome.DurationSeconds = outcome.ElapsedTicks / (float)tickRate;
        }

        private void Finish(CaptureOutcome outcome)
        {
            running = false;
            ClearHeldInput();
            if (session != null)
                session.SnapshotReceived -= HandleAuthoritySnapshot;

            if (outcome != null && outcome.Kind == CaptureOutcomeKind.Locked)
                UiHapticsService.Instance?.TriggerCaptureSuccess();
            else if (outcome != null && outcome.RequiresAuthorityRecovery)
                UiHapticsService.Instance?.TriggerWarning();
            else
                UiHapticsService.Instance?.TriggerCaptureFailure();

            Action<CaptureOutcome> callback = completed;
            completed = null;
            callback?.Invoke(outcome);
        }

        private void ApplyAuthoritativeSnapshot(CaptureAuthoritativeState state)
        {
            if (state == null || !string.Equals(state.AttemptId, challenge.AttemptId, StringComparison.Ordinal))
                return;
            if (renderedAuthority != null && state.Version < renderedAuthority.Version)
                return;
            bool entered = renderedAuthority != null && !renderedAuthority.IsInsideBand && state.IsInsideBand;
            bool left = renderedAuthority != null && renderedAuthority.IsInsideBand && !state.IsInsideBand;
            renderedAuthority = state;
            // Remote snapshots are the source of truth. They may be a few ticks
            // behind local rendering, but skipping them permits an unacknowledged
            // local hold to become a phantom terminal capture.
            if (predicted != null)
            {
                predicted.Tick = state.Tick;
                predicted.TargetPositionUnits = state.TargetPositionUnits;
                predicted.BandPositionUnits = state.BandPositionUnits;
                predicted.MeterBasisPoints = state.MeterBasisPoints;
                predicted.MeterRemainder = 0;
                predicted.IsInsideBand = state.IsInsideBand;
                predicted.IsHeld = state.IsHeld;
                predicted.Outcome = state.Outcome;
                CaptureSimulation.RebuildTraceRegimeAfterSnapshot(predicted, challenge.Parameters);
            }
            PresentCrossing(entered, left, state.SpikeOccurred);
        }

        private void HandleAuthoritySnapshot(CaptureAuthoritativeState state)
        {
            lock (snapshotGate)
            {
                if (pendingAuthority == null || (state != null && state.Version >= pendingAuthority.Version))
                    pendingAuthority = state;
            }
        }

        private CaptureAuthoritativeState ConsumePendingAuthority()
        {
            lock (snapshotGate)
            {
                CaptureAuthoritativeState value = pendingAuthority;
                pendingAuthority = null;
                return value;
            }
        }

        private void PresentCrossing(bool entered, bool left, bool spike)
        {
            float now = Time.unscaledTime;
            if ((entered || left) && now >= crossingHapticAt)
            {
                crossingHapticAt = now + ResolvePresentation().crossingPulseCooldownSeconds;
                if (entered)
                    UiHapticsService.Instance?.TriggerCaptureEnterBand();
                else
                    UiHapticsService.Instance?.TriggerCaptureLeaveBand();
            }
            if (spike && now >= spikeHapticAt)
            {
                spikeHapticAt = now + ResolvePresentation().spikePulseCooldownSeconds;
                UiHapticsService.Instance?.TriggerCaptureSpike();
            }
        }

        private void ApplyHeldState(bool held)
        {
            if (held == sentHeld)
                return;
            sentHeld = held;
            if (session != null)
                _ = session.SetHeldAsync(held, ++inputSequence);
        }

        private void ClearHeldInput()
        {
            pointerHeld = false;
            physicalHeld = false;
            if (sentHeld)
                ApplyHeldState(false);
            session?.ClearHeldInput();
        }

        private void HandlePointerHeld(bool held)
        {
            pointerHeld = held;
            ApplyHeldState(pointerHeld || physicalHeld);
        }

        private void RenderLabels(int meterBasisPoints, int remainingTicks)
        {
            float meter01 = Mathf.Clamp01(meterBasisPoints / 10000f);
            if (meter != null)
                meter.value = meter01;
            if (meterLabel != null)
                meterLabel.text = $"ESCAPE  {Mathf.RoundToInt(meter01 * 100f)}%  LOCK";
            if (timerLabel != null)
                timerLabel.text = (remainingTicks / (float)Mathf.Max(1, challenge.Parameters.TickRate)).ToString("0.0");
            MeterPresented?.Invoke(meterBasisPoints, predicted != null && predicted.IsInsideBand);
        }

        private void RenderChallengeIntel()
        {
            CapturePresentationConfigSO copy = ResolvePresentation();
            if (instructionLabel != null)
            {
                bool first = PlayerPrefs.GetInt(TutorialSeenKey, 0) == 0;
                instructionLabel.gameObject.SetActive(first || challenge.LowHpMarkerRevealed);
                if (first)
                {
                    instructionLabel.text = copy.firstCaptureInstructions;
                    PlayerPrefs.SetInt(TutorialSeenKey, 1);
                    PlayerPrefs.Save();
                }
                else
                {
                    instructionLabel.text = challenge.IsLowHp ? copy.lowTargetHpCopy : "";
                }
            }

            if (scannerLabel != null)
            {
                var text = new StringBuilder(192);
                if (challenge.LowHpMarkerRevealed)
                    text.AppendLine(challenge.IsLowHp ? copy.lowTargetHpCopy : "LOW-HP MARKER ACTIVE");
                List<CaptureScannerIntel> intel = challenge.ScannerIntel;
                if (intel != null)
                {
                    for (int i = 0; i < intel.Count; i++)
                        text.Append(intel[i].Stat).Append(": ").Append(new string('\u2605', Mathf.Clamp(intel[i].Grade, 1, 5))).AppendLine();
                }
                scannerLabel.text = text.ToString().TrimEnd();
                scannerLabel.gameObject.SetActive(text.Length > 0);
            }
            if (outcomeLabel != null)
            {
                outcomeLabel.text = "";
                outcomeLabel.gameObject.SetActive(false);
            }
            if (resultBannerLabel != null)
            {
                resultBannerLabel.text = "";
                resultBannerLabel.gameObject.SetActive(false);
            }
        }

        private static string ResolveOutcomeCopy(CaptureOutcome outcome)
        {
            if (outcome == null)
                return "CAPTURE INTERRUPTED";
            if (!string.IsNullOrWhiteSpace(outcome.Message))
                return outcome.Message;
            switch (outcome.Kind)
            {
                case CaptureOutcomeKind.Locked: return "SIGNAL LOCKED - CHAINLING CAPTURED";
                case CaptureOutcomeKind.Escaped: return "SIGNAL LOST - TARGET FLED";
                case CaptureOutcomeKind.TimedOut: return "ORB TIMED OUT - BATTLE CONTINUES";
                case CaptureOutcomeKind.Rejected: return "CAPTURE REJECTED";
                default: return "CAPTURE INTERRUPTED - SESSION RESUMABLE";
            }
        }

        private static string ResolveAchievementCopy(CaptureOutcome outcome)
        {
            if (outcome == null || outcome.UnlockedAchievementIds == null || outcome.UnlockedAchievementIds.Count == 0)
                return "";
            var copy = new StringBuilder(96);
            for (int i = 0; i < outcome.UnlockedAchievementIds.Count; i++)
            {
                string label = (outcome.UnlockedAchievementIds[i] ?? "").Replace('_', ' ').ToUpperInvariant();
                if (label.Length > 0)
                    copy.Append("\nACHIEVEMENT UNLOCKED — ").Append(label);
            }
            return copy.ToString();
        }

        private RectTransform ResolvePanelRoot()
        {
            if (panelRoot != null)
                return panelRoot;

            Transform board = transform.Find("Board");
            if (board == null && overlay != null)
                board = overlay.transform.Find("Board");
            if (board is RectTransform boardRect)
            {
                panelRoot = boardRect;
                return panelRoot;
            }

            if (overlay != null)
                panelRoot = overlay.transform as RectTransform;
            return panelRoot != null ? panelRoot : transform as RectTransform;
        }

        private CapturePresentationConfigSO ResolvePresentation()
        {
            if (presentation == null)
                presentation = BalanceConfigRuntime.CapturePresentation;
            if (presentation == null)
                presentation = ScriptableObject.CreateInstance<CapturePresentationConfigSO>();
            return presentation;
        }

        private void ResolveInput()
        {
            if (inputActions == null)
                inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
            InputActionMap map = inputActions != null ? inputActions.FindActionMap(actionMapName, false) : null;
            holdAction = map != null ? map.FindAction(holdActionName, false) : null;
        }

        private void ResolveUi()
        {
            if (overlay == null)
                overlay = GetComponent<CanvasGroup>() ?? GetComponentInChildren<CanvasGroup>(true);
            if (graph == null)
                graph = GetComponentInChildren<CaptureTraceGraphic>(true);
            if (interactionSurface == null)
                interactionSurface = GetComponentInChildren<CaptureHoldSurface>(true);
            if (overlay == null || graph == null || interactionSurface == null)
                BuildRuntimeFallback();

            CapturePresentationConfigSO board = ResolvePresentation();
            ApplyArtwork(board);
            graph?.Configure(board, board == null || board.tradingGridArtwork == null);
        }

        private void ApplyArtwork(CapturePresentationConfigSO board)
        {
            if (graphArtwork == null)
                return;

            Sprite artwork = board != null ? board.tradingGridArtwork : null;
            graphArtwork.sprite = artwork;
            graphArtwork.enabled = artwork != null;
            graphArtwork.color = artwork != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            graphArtwork.raycastTarget = false;
        }

        private void BuildRuntimeFallback()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
                root = gameObject.AddComponent<RectTransform>();
            Stretch(root, 0f);

            RectTransform overlayRect = overlay != null
                ? overlay.transform as RectTransform
                : CreateRect("Overlay", root, Vector2.zero, Vector2.one);
            overlay = overlayRect.GetComponent<CanvasGroup>() ?? overlayRect.gameObject.AddComponent<CanvasGroup>();
            Image dim = overlayRect.GetComponent<Image>() ?? overlayRect.gameObject.AddComponent<Image>();
            dim.sprite = null;
            dim.color = new Color(0.01f, 0.02f, 0.04f, 0.92f);
            dim.raycastTarget = true;
            interactionSurface = overlayRect.GetComponent<CaptureHoldSurface>()
                ?? overlayRect.gameObject.AddComponent<CaptureHoldSurface>();

            RectTransform board = CreateRect("Board", overlayRect, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.82f));
            Image boardBack = board.gameObject.AddComponent<Image>();
            boardBack.color = new Color(0.02f, 0.07f, 0.1f, 1f);
            boardBack.raycastTarget = false;
            RectTransform artworkRect = CreateRect("GridArtwork", board, Vector2.zero, Vector2.one);
            graphArtwork = artworkRect.gameObject.AddComponent<Image>();
            graphArtwork.raycastTarget = false;
            graphArtwork.enabled = false;

            RectTransform graphRect = CreateRect("TradingTrace", board, Vector2.zero, Vector2.one);
            graph = graphRect.gameObject.AddComponent<CaptureTraceGraphic>();
            graph.raycastTarget = false;

            meter = CreateSlider("CaptureMeter", overlayRect, new Vector2(0.12f, 0.13f), new Vector2(0.88f, 0.18f));
            meterLabel = CreateText("MeterLabel", overlayRect, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.13f), "ESCAPE  50%  LOCK", 22f);
            timerLabel = CreateText("TimerLabel", overlayRect, new Vector2(0.44f, 0.84f), new Vector2(0.56f, 0.91f), "20.0", 32f);
            instructionLabel = CreateText("Instruction", overlayRect, new Vector2(0.08f, 0.91f), new Vector2(0.92f, 0.99f), "", 20f);
            scannerLabel = CreateText("ScannerIntel", overlayRect, new Vector2(0.7f, 0.82f), new Vector2(0.94f, 0.97f), "", 16f);
            scannerLabel.alignment = TextAlignmentOptions.TopRight;
            outcomeLabel = CreateText("Outcome", overlayRect, new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.58f), "", 34f);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 min, Vector2 max, string value, float size)
        {
            RectTransform rect = CreateRect(name, parent, min, max);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Slider CreateSlider(string name, Transform parent, Vector2 min, Vector2 max)
        {
            RectTransform root = CreateRect(name, parent, min, max);
            var background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.4f, 0.08f, 0.12f, 0.85f);
            background.raycastTarget = false;
            var slider = root.gameObject.AddComponent<Slider>();
            RectTransform fill = CreateRect("Fill", root, Vector2.zero, Vector2.one);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.95f, 0.55f, 0.95f);
            fillImage.raycastTarget = false;
            slider.fillRect = fill;
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            slider.interactable = false;
            return slider;
        }

        private void SetOverlayVisible(bool visible)
        {
            if (overlay == null)
                return;
            overlay.alpha = visible ? 1f : 0f;
            overlay.interactable = visible;
            overlay.blocksRaycasts = visible;
            if (overlay.gameObject != gameObject)
                overlay.gameObject.SetActive(true);
        }

        private void SubscribeSurfaces()
        {
            if (interactionSurface != null)
            {
                interactionSurface.HeldChanged -= HandlePointerHeld;
                interactionSurface.HeldChanged += HandlePointerHeld;
            }
            if (hudXSurface != null)
            {
                hudXSurface.HeldChanged -= HandlePointerHeld;
                hudXSurface.HeldChanged += HandlePointerHeld;
            }
        }

        private void UnsubscribeSurfaces()
        {
            if (interactionSurface != null)
                interactionSurface.HeldChanged -= HandlePointerHeld;
            if (hudXSurface != null)
                hudXSurface.HeldChanged -= HandlePointerHeld;
        }
    }
}
