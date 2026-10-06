using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    public enum DialogueBoxAdvanceResult
    {
        Inactive = 0,
        RevealCompleted = 1,
        WaitingForIndicator = 2,
        WaitingForExternalGate = 3,
        Advanced = 4,
    }

    /// <summary>
    /// Shared, context-free dialogue box renderer. Narrative and combat own their
    /// respective input and lifecycle policies while this view owns presentation.
    /// </summary>
    public sealed class DialogueBoxView : MonoBehaviour
    {
        private const char ContinueGlyph = '\u2026';

        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private GameObject portraitContainer;
        [SerializeField] private bool prefixSpeakerWhenUnbound;

        [Header("Panel Motion")]
        [Tooltip("Moves the panel up from below the screen while expanding it on Y. The same motion is mirrored on exit.")]
        [SerializeField] private bool animateVerticalMotion = true;
        [Tooltip("Distance below the authored anchored position used by the hidden pose.")]
        [Min(0f)] [SerializeField] private float hiddenYOffset = 460f;
        [Tooltip("Y scale multiplier used by the hidden pose. X and Z scale remain authored.")]
        [Range(0.01f, 1f)] [SerializeField] private float hiddenScaleY = 0.08f;
        [Tooltip("Unscaled-time duration of the bottom-up entry motion.")]
        [Min(0f)] [SerializeField] private float showMotionDuration = 0.34f;
        [Tooltip("Unscaled-time duration of the mirrored exit motion.")]
        [Min(0f)] [SerializeField] private float hideMotionDuration = 0.24f;
        [Tooltip("Strength of the Back ease overshoot used by entry and exit.")]
        [Range(0f, 2f)] [SerializeField] private float motionOvershoot = 0.85f;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private int minimumBlinkCycles = 2;
        [SerializeField] private float blinkHalfCycleSeconds = 0.18f;
        [Range(0f, 1f)] [SerializeField] private float dimAlpha = 0.35f;

        [Header("Choices")]
        [Tooltip("Authored row that holds cloned choice buttons. Inactive until a line presents choices.")]
        [SerializeField] private GameObject choiceRow;
        [Tooltip("Inactive button prefab/child cloned once per dialogue option.")]
        [SerializeField] private Button choiceButtonTemplate;
        [Tooltip("CombatNarrationBG height while choices are showing. Grows upward from the rest pose.")]
        [Min(1f)] [SerializeField] private float choicePlateHeight = 600f;
        [Tooltip("Unscaled-time duration of the plate height overshoot.")]
        [Min(0f)] [SerializeField] private float choicePlateExpandDuration = 0.34f;
        [Tooltip("How far below the authored row the buttons start before lifting in.")]
        [Min(0f)] [SerializeField] private float choiceLiftDistance = 140f;
        [Tooltip("Unscaled-time duration of the choice-row Y lift.")]
        [Min(0f)] [SerializeField] private float choiceLiftDuration = 0.38f;
        [Tooltip("Back-ease overshoot used by the plate expand and button lift.")]
        [Range(0f, 2f)] [SerializeField] private float choiceMotionOvershoot = 1.15f;

        private CanvasGroup _canvasGroup;
        private RectTransform _panelRect;
        private Coroutine _fadeRoutine;
        private Coroutine _blinkRoutine;
        private bool _isShowing;
        private bool _isRevealing;
        private bool _isLineComplete;
        private bool _canAdvance;
        private bool _skipRevealRequested;
        private string _displayText = string.Empty;
        private bool _shownPoseCaptured;
        private Vector2 _shownAnchoredPosition;
        private Vector3 _shownLocalScale;
        private GameObject _choiceRow;
        private Button _choiceButtonTemplate;
        private readonly List<Button> _choiceButtons = new List<Button>();
        private Action<int> _onChoicePicked;
        private bool _isWaitingForChoice;
        private RectTransform _choicePlateRect;
        private CombatUiScanlineBackground _choicePlateScanlines;
        private Vector2 _choicePlateRestSize;
        private Vector2 _choicePlateRestAnchored;
        private bool _choicePlateRestCaptured;
        private Vector2 _choiceRowRestAnchored;
        private bool _choiceRowRestCaptured;
        private float _choicePlateHeightCurrent;
        private Sequence _choiceMotion;
        private MobileInputManager _choiceMobileInput;
        private bool _mobileChoiceSubmitRequested;
        private int _heldChoiceDirection;
        private float _nextChoiceNavigationTime;

        public bool IsShowing => _isShowing;
        public bool IsRevealing => _isRevealing;
        public bool IsLineComplete => _isLineComplete;
        public bool CanAdvance => _canAdvance;
        public GameObject Panel => panel;
        public TextMeshProUGUI BodyText => bodyText;
        public bool UsesVerticalMotion => animateVerticalMotion;
        public float HiddenYOffset => hiddenYOffset;
        public float HiddenScaleY => hiddenScaleY;
        public bool IsWaitingForChoice => _isWaitingForChoice;
        public bool IsChoiceMotionPlaying => _choiceMotion != null && _choiceMotion.IsActive();

        public void Configure(
            GameObject targetPanel,
            TextMeshProUGUI targetBody,
            TextMeshProUGUI targetSpeaker = null,
            Image targetPortrait = null,
            GameObject targetPortraitContainer = null,
            bool prefixSpeakerInBody = false,
            float panelFadeDuration = 0.2f,
            int requiredBlinkCycles = 2,
            float blinkHalfCycle = 0.18f,
            float continueDimAlpha = 0.35f)
        {
            panel = targetPanel;
            bodyText = targetBody;
            speakerText = targetSpeaker;
            portraitImage = targetPortrait;
            portraitContainer = targetPortraitContainer;
            prefixSpeakerWhenUnbound = prefixSpeakerInBody;
            fadeDuration = Mathf.Max(0f, panelFadeDuration);
            minimumBlinkCycles = Mathf.Max(0, requiredBlinkCycles);
            blinkHalfCycleSeconds = Mathf.Max(0.01f, blinkHalfCycle);
            dimAlpha = Mathf.Clamp01(continueDimAlpha);
            _shownPoseCaptured = false;
            ResolveCanvasGroup();
        }

        private void Awake()
        {
            if (panel != null || bodyText != null)
            {
                ResolveReferences();
                ResolveCanvasGroup();
                ResolveChoiceUi();
            }
        }

        private void OnDisable()
        {
            StopViewCoroutines();
            ResetChoicePlateImmediate();
            UnbindChoiceInput();
            _isWaitingForChoice = false;
            _isRevealing = false;
        }

        public void InitializeHidden()
        {
            ResolveReferences();
            ResolveCanvasGroup();
            StopViewCoroutines();
            CaptureShownPose();
            _isShowing = false;
            _isRevealing = false;
            _isLineComplete = false;
            _canAdvance = false;
            HideChoices();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            ApplyMotionPose(showing: false);
            if (panel != null)
                panel.SetActive(false);
        }

        public void Show()
        {
            ResolveReferences();
            ResolveCanvasGroup();
            StopFade();
            bool hadCapturedPose = _shownPoseCaptured;
            CaptureShownPose();
            if (panel != null)
                panel.SetActive(true);
            if (animateVerticalMotion && !hadCapturedPose)
                ApplyMotionPose(showing: false);

            _isShowing = true;
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (CanAnimateVisibility())
                _fadeRoutine = StartCoroutine(AnimateVisibility(showing: true));
            else
                ApplyVisibleImmediate();

            AutoSprintIndicator.SetDialogueShowing(true);
        }

        public void Hide()
        {
            _isShowing = false;
            AutoSprintIndicator.SetDialogueShowing(false);
            _isRevealing = false;
            _isLineComplete = false;
            _canAdvance = false;
            HideChoices();
            if (!isActiveAndEnabled)
            {
                // Panel hosts this view; once deactivated we cannot StartCoroutine.
                ApplyHiddenImmediate();
                return;
            }

            StopBlink();
            StopFade();
            if (bodyText != null)
                bodyText.maxVisibleCharacters = int.MaxValue;
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (CanAnimateVisibility())
                _fadeRoutine = StartCoroutine(AnimateVisibility(showing: false));
            else
                ApplyHiddenImmediate();
        }

        private void ApplyHiddenImmediate()
        {
            StopViewCoroutines();
            if (bodyText != null)
                bodyText.maxVisibleCharacters = int.MaxValue;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            ApplyMotionPose(showing: false);
            if (panel != null)
                panel.SetActive(false);
        }

        private void ApplyVisibleImmediate()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            ApplyMotionPose(showing: true);
        }

        public IEnumerator PresentLine(
            string speaker,
            string text,
            float charactersPerSecond,
            Sprite portrait = null,
            Color? portraitTint = null)
        {
            ResolveReferences();
            if (!_isShowing)
                Show();

            StopBlink();
            _skipRevealRequested = false;
            _isRevealing = true;
            _isLineComplete = false;
            _canAdvance = false;
            ApplyIdentity(speaker, portrait, portraitTint ?? Color.white);

            string body = text ?? string.Empty;
            if (speakerText == null && prefixSpeakerWhenUnbound && !string.IsNullOrWhiteSpace(speaker))
                body = $"<b>{speaker.Trim()}</b>\n{body}";
            _displayText = body + ContinueGlyph;

            if (bodyText == null)
            {
                CompleteReveal();
                yield break;
            }

            bodyText.text = _displayText;
            bodyText.ForceMeshUpdate(true);
            bodyText.maxVisibleCharacters = 0;
            int totalCharacters = bodyText.textInfo.characterCount;
            int bodyCharacters = Mathf.Max(0, totalCharacters - 1);
            int visible = 0;
            float leftoverSeconds = 0f;

            if (charactersPerSecond <= 0f || bodyCharacters <= 0)
            {
                bodyText.maxVisibleCharacters = bodyCharacters;
                CompleteReveal();
                yield break;
            }

            while (visible < bodyCharacters && !_skipRevealRequested)
            {
                visible = AdvanceVisibleCharacterCount(
                    visible,
                    bodyCharacters,
                    charactersPerSecond,
                    Time.unscaledDeltaTime,
                    ref leftoverSeconds);
                bodyText.maxVisibleCharacters = visible;
                if (visible >= bodyCharacters || _skipRevealRequested)
                    break;
                yield return null;
            }

            CompleteReveal();
        }

        /// <summary>
        /// Advances a typewriter by wall-clock time so characters-per-second
        /// stays accurate below the target rate. One glyph per frame would cap
        /// reveal speed at the current FPS.
        /// </summary>
        public static int AdvanceVisibleCharacterCount(
            int currentVisible,
            int totalCharacters,
            float charactersPerSecond,
            float unscaledDeltaTime,
            ref float leftoverSeconds)
        {
            if (totalCharacters <= 0)
                return 0;

            currentVisible = Mathf.Clamp(currentVisible, 0, totalCharacters);
            if (currentVisible >= totalCharacters)
                return totalCharacters;
            if (charactersPerSecond <= 0f)
                return totalCharacters;

            leftoverSeconds = Mathf.Max(0f, leftoverSeconds) + Mathf.Max(0f, unscaledDeltaTime);
            float delay = 1f / charactersPerSecond;
            int additional = Mathf.FloorToInt(leftoverSeconds / delay);
            if (additional <= 0)
                return currentVisible;

            leftoverSeconds -= additional * delay;
            return Mathf.Min(totalCharacters, currentVisible + additional);
        }

        public DialogueBoxAdvanceResult RequestAdvance(Func<bool> externalAdvanceReady = null)
        {
            if (!_isShowing)
                return DialogueBoxAdvanceResult.Inactive;
            if (_isWaitingForChoice)
                return DialogueBoxAdvanceResult.WaitingForExternalGate;
            if (_isRevealing)
            {
                SkipReveal();
                return DialogueBoxAdvanceResult.RevealCompleted;
            }
            if (!_isLineComplete || !_canAdvance)
                return DialogueBoxAdvanceResult.WaitingForIndicator;
            if (externalAdvanceReady != null && !externalAdvanceReady())
                return DialogueBoxAdvanceResult.WaitingForExternalGate;

            StopBlink();
            return DialogueBoxAdvanceResult.Advanced;
        }

        public void SkipReveal()
        {
            if (!_isRevealing)
                return;
            _skipRevealRequested = true;
            if (bodyText != null)
            {
                bodyText.text = _displayText;
                bodyText.ForceMeshUpdate(true);
                bodyText.maxVisibleCharacters = int.MaxValue;
            }
        }

        public void AllowImmediateAdvance()
        {
            if (_isLineComplete)
                _canAdvance = true;
        }

        /// <summary>
        /// Presents a compact button row for a caller-owned list of labels.
        /// This view intentionally knows nothing about dialogue or sequence
        /// data; the caller maps the selected index to its own value.
        /// </summary>
        public void ShowChoices(IReadOnlyList<string> labels, Action<int> onPicked)
        {
            HideChoices(animate: false);
            if (labels == null || labels.Count == 0 || panel == null)
                return;

            ResolveChoiceUi();
            EnsureChoiceRow();
            if (_choiceRow == null)
                return;

            _onChoicePicked = onPicked;
            _isWaitingForChoice = true;
            _canAdvance = false;
            StopBlink();
            BindChoiceInput();

            for (int i = 0; i < labels.Count; i++)
            {
                int choiceIndex = i;
                Button button = CreatePresentedChoiceButton(labels[i]);
                if (button == null)
                    continue;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => PickChoice(choiceIndex));
                _choiceButtons.Add(button);
            }

            WireChoiceNavigation();
            _choiceRow.SetActive(_choiceButtons.Count > 0);
            if (_choiceRow != null)
                _choiceRow.transform.SetAsLastSibling();
            if (_choiceButtons.Count > 0)
                PlayChoiceEnterMotion();
            if (_choiceButtons.Count > 0 && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_choiceButtons[0].gameObject);
                _choiceButtons[0].Select();
                SyncChoiceSelectionVisuals();
            }
        }

        public void HideChoices()
        {
            HideChoices(animate: false);
        }

        public IEnumerator WaitForChoiceMotion()
        {
            while (IsChoiceMotionPlaying)
                yield return null;
        }

        public void SelectChoiceRelative(int direction)
        {
            if (!_isWaitingForChoice || _choiceButtons.Count == 0 || direction == 0)
                return;

            int current = 0;
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                if (_choiceButtons[i] != null && _choiceButtons[i].gameObject == selected)
                {
                    current = i;
                    break;
                }
            }

            int step = direction > 0 ? 1 : -1;
            int next = Mathf.Clamp(current + step, 0, _choiceButtons.Count - 1);
            if (next == current)
                return;

            Button button = _choiceButtons[next];
            if (button == null || !button.IsInteractable())
                return;

            EventSystem.current?.SetSelectedGameObject(button.gameObject);
            button.Select();
            SyncChoiceSelectionVisuals();
        }

        public void SubmitSelectedChoice()
        {
            if (!_isWaitingForChoice || _choiceButtons.Count == 0)
                return;

            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            Button button = null;
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                if (_choiceButtons[i] != null && _choiceButtons[i].gameObject == selected)
                {
                    button = _choiceButtons[i];
                    break;
                }
            }

            button ??= _choiceButtons[0];
            if (button == null || !button.IsInteractable())
                return;

            // Hardware/mobile submit should commit the selected choice itself.
            // Button.onClick remains the pointer bridge and is free to be
            // rebuilt by UI setup without breaking non-pointer input.
            int choiceIndex = _choiceButtons.IndexOf(button);
            if (choiceIndex >= 0)
                PickChoice(choiceIndex);
        }

        public void EnsureChoiceSelection()
        {
            if (!_isWaitingForChoice || _choiceButtons.Count == 0 || EventSystem.current == null)
                return;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                Button candidate = _choiceButtons[i];
                if (candidate != null && candidate.gameObject == selected && candidate.IsInteractable())
                    return;
            }

            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                Button candidate = _choiceButtons[i];
                if (candidate == null || !candidate.IsInteractable() || !candidate.gameObject.activeInHierarchy)
                    continue;

                EventSystem.current.SetSelectedGameObject(candidate.gameObject);
                candidate.Select();
                SyncChoiceSelectionVisuals();
                return;
            }
        }

        private void HideChoices(bool animate)
        {
            _isWaitingForChoice = false;
            _onChoicePicked = null;
            UnbindChoiceInput();
            if (animate && CanAnimateChoiceMotion() && _choiceButtons.Count > 0)
            {
                PlayChoiceExitMotion();
                return;
            }

            KillChoiceMotion();
            ClearPresentedChoiceButtons();
            ResetChoicePlateImmediate();
        }

        private void PlayChoiceEnterMotion()
        {
            CaptureChoicePlateRest();
            CaptureChoiceRowRest();
            RectTransform rowRect = _choiceRow != null ? _choiceRow.transform as RectTransform : null;
            float targetHeight = Mathf.Max(_choicePlateRestSize.y, choicePlateHeight);
            if (rowRect != null)
            {
                Vector2 start = _choiceRowRestAnchored;
                start.y -= choiceLiftDistance;
                rowRect.anchoredPosition = start;
            }

            if (!CanAnimateChoiceMotion())
            {
                ApplyPlateHeight(targetHeight);
                if (rowRect != null)
                    rowRect.anchoredPosition = _choiceRowRestAnchored;
                return;
            }

            KillChoiceMotion();
            ApplyPlateHeight(_choicePlateRestSize.y);
            float overshoot = Mathf.Max(0.01f, choiceMotionOvershoot);
            _choiceMotion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            if (_choicePlateRect != null && choicePlateExpandDuration > 0f)
            {
                _choiceMotion.Join(
                    DOTween.To(() => _choicePlateHeightCurrent, ApplyPlateHeight, targetHeight, choicePlateExpandDuration)
                        .SetEase(Ease.OutBack, overshoot));
            }
            else
            {
                ApplyPlateHeight(targetHeight);
            }

            if (rowRect != null && choiceLiftDuration > 0f)
            {
                _choiceMotion.Join(
                    rowRect.DOAnchorPosY(_choiceRowRestAnchored.y, choiceLiftDuration)
                        .SetEase(Ease.OutBack, overshoot));
            }
            else if (rowRect != null)
            {
                rowRect.anchoredPosition = _choiceRowRestAnchored;
            }
        }

        private void PlayChoiceExitMotion()
        {
            CaptureChoicePlateRest();
            CaptureChoiceRowRest();
            RectTransform rowRect = _choiceRow != null ? _choiceRow.transform as RectTransform : null;
            KillChoiceMotion();

            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                if (_choiceButtons[i] != null)
                    _choiceButtons[i].interactable = false;
            }

            float overshoot = Mathf.Max(0.01f, choiceMotionOvershoot);
            float exitDuration = Mathf.Max(choicePlateExpandDuration, hideMotionDuration);
            _choiceMotion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            if (_choicePlateRect != null && exitDuration > 0f)
            {
                _choiceMotion.Join(
                    DOTween.To(
                            () => _choicePlateHeightCurrent,
                            ApplyPlateHeight,
                            _choicePlateRestSize.y,
                            exitDuration)
                        .SetEase(Ease.InBack, overshoot));
            }

            if (rowRect != null && choiceLiftDuration > 0f)
            {
                _choiceMotion.Join(
                    rowRect.DOAnchorPosY(_choiceRowRestAnchored.y - choiceLiftDistance, choiceLiftDuration)
                        .SetEase(Ease.InBack, overshoot));
            }

            _choiceMotion.OnComplete(() =>
            {
                _choiceMotion = null;
                ClearPresentedChoiceButtons();
                ResetChoicePlateImmediate();
            });
        }

        private bool CanAnimateChoiceMotion()
        {
            return Application.isPlaying &&
                   isActiveAndEnabled &&
                   (choicePlateExpandDuration > 0f || choiceLiftDuration > 0f);
        }

        private void KillChoiceMotion()
        {
            if (_choiceMotion != null && _choiceMotion.IsActive())
                _choiceMotion.Kill(complete: false);
            _choiceMotion = null;
        }

        private void CaptureChoicePlateRest()
        {
            if (_choicePlateRestCaptured || panel == null)
                return;

            Transform background = FindDeepChild(panel.transform, "CombatNarrationBG");
            _choicePlateRect = background as RectTransform;
            if (_choicePlateRect == null)
                return;

            _choicePlateRestSize = _choicePlateRect.sizeDelta;
            _choicePlateRestAnchored = _choicePlateRect.anchoredPosition;
            _choicePlateHeightCurrent = _choicePlateRestSize.y;
            _choicePlateScanlines = _choicePlateRect.GetComponent<CombatUiScanlineBackground>();
            _choicePlateScanlines?.SetTilingRestHeight(_choicePlateRestSize.y);
            _choicePlateRestCaptured = true;
        }

        private void CaptureChoiceRowRest()
        {
            if (_choiceRowRestCaptured || _choiceRow == null)
                return;

            RectTransform rowRect = _choiceRow.transform as RectTransform;
            if (rowRect == null)
                return;

            _choiceRowRestAnchored = rowRect.anchoredPosition;
            _choiceRowRestCaptured = true;
        }

        private void ApplyPlateHeight(float height)
        {
            _choicePlateHeightCurrent = height;
            if (_choicePlateRect == null || !_choicePlateRestCaptured)
                return;

            Vector2 size = _choicePlateRestSize;
            size.y = height;
            _choicePlateRect.sizeDelta = size;
            _choicePlateRect.anchoredPosition = _choicePlateRestAnchored;
            _choicePlateScanlines?.SyncTilingToCurrentHeight();
        }

        private void ResetChoicePlateImmediate()
        {
            KillChoiceMotion();
            if (_choicePlateRestCaptured)
                ApplyPlateHeight(_choicePlateRestSize.y);
            if (!_choiceRowRestCaptured || _choiceRow == null)
                return;

            RectTransform rowRect = _choiceRow.transform as RectTransform;
            if (rowRect != null)
                rowRect.anchoredPosition = _choiceRowRestAnchored;
        }

        private void ClearPresentedChoiceButtons()
        {
            for (int i = _choiceButtons.Count - 1; i >= 0; i--)
            {
                Button button = _choiceButtons[i];
                if (button == null)
                    continue;
                if (_choiceButtonTemplate != null && button == _choiceButtonTemplate)
                    continue;
                if (Application.isPlaying)
                    Destroy(button.gameObject);
                else
                    DestroyImmediate(button.gameObject);
            }

            _choiceButtons.Clear();
            if (EventSystem.current != null && _choiceRow != null &&
                EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_choiceRow.transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            if (_choiceButtonTemplate != null)
                _choiceButtonTemplate.gameObject.SetActive(false);
            if (_choiceRow != null)
                _choiceRow.SetActive(false);
        }

        private void CompleteReveal()
        {
            if (bodyText != null)
            {
                bodyText.text = _displayText;
                bodyText.ForceMeshUpdate(true);
                bodyText.maxVisibleCharacters = int.MaxValue;
            }
            _isRevealing = false;
            _isLineComplete = true;
            _canAdvance = minimumBlinkCycles <= 0;
            _blinkRoutine = StartCoroutine(BlinkContinueIndicator());
        }

        private void PickChoice(int choiceIndex)
        {
            if (!_isWaitingForChoice)
                return;

            Action<int> callback = _onChoicePicked;
            HideChoices(animate: true);
            callback?.Invoke(choiceIndex);
        }

        private void LateUpdate()
        {
            if (_isWaitingForChoice)
            {
                PollChoiceInput();
                SyncChoiceSelectionVisuals();
            }
        }

        private void BindChoiceInput()
        {
            UnbindChoiceInput();
            _choiceMobileInput = FindFirstObjectByType<MobileInputManager>(FindObjectsInactive.Exclude);
            if (_choiceMobileInput == null)
                _choiceMobileInput = FindFirstObjectByType<MobileInputManager>(FindObjectsInactive.Include);
            if (_choiceMobileInput != null)
                _choiceMobileInput.OnAPressed += HandleMobileChoiceSubmit;
        }

        private void UnbindChoiceInput()
        {
            if (_choiceMobileInput != null)
                _choiceMobileInput.OnAPressed -= HandleMobileChoiceSubmit;
            _choiceMobileInput = null;
            _mobileChoiceSubmitRequested = false;
            _heldChoiceDirection = 0;
        }

        private void HandleMobileChoiceSubmit()
        {
            _mobileChoiceSubmitRequested = true;
        }

        private void PollChoiceInput()
        {
            EnsureChoiceSelection();

            bool submit = _mobileChoiceSubmitRequested ||
                          (Keyboard.current != null &&
                           (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) ||
                          (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            _mobileChoiceSubmitRequested = false;
            if (submit)
            {
                SubmitSelectedChoice();
                return;
            }

            int direction = ReadChoiceDirection(_choiceMobileInput);
            float now = Time.unscaledTime;
            if (direction == 0)
            {
                _heldChoiceDirection = 0;
                return;
            }

            if (direction != _heldChoiceDirection || now >= _nextChoiceNavigationTime)
            {
                SelectChoiceRelative(direction);
                _nextChoiceNavigationTime = now + (direction == _heldChoiceDirection ? 0.12f : 0.35f);
                _heldChoiceDirection = direction;
            }
        }

        private static int ReadChoiceDirection(MobileInputManager mobileInput)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed ||
                    Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
                    return -1;
                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed ||
                    Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
                    return 1;
            }

            if (Gamepad.current != null)
            {
                Vector2 navigation = Gamepad.current.dpad.ReadValue() + Gamepad.current.leftStick.ReadValue();
                if (navigation.x < -0.5f || navigation.y > 0.5f)
                    return -1;
                if (navigation.x > 0.5f || navigation.y < -0.5f)
                    return 1;
            }

            if (mobileInput != null)
            {
                Vector2 navigation = mobileInput.GetMovementInput();
                if (navigation.x < -0.5f || navigation.y > 0.5f)
                    return -1;
                if (navigation.x > 0.5f || navigation.y < -0.5f)
                    return 1;
            }

            return 0;
        }

        private void ResolveChoiceUi()
        {
            if (_choiceRow == null)
                _choiceRow = choiceRow;
            if (_choiceButtonTemplate == null)
                _choiceButtonTemplate = choiceButtonTemplate;

            if (_choiceRow == null && panel != null)
            {
                Transform found = FindDeepChild(panel.transform, "DialogueChoices");
                if (found != null)
                    _choiceRow = found.gameObject;
            }

            if (_choiceButtonTemplate == null && _choiceRow != null)
                _choiceButtonTemplate = _choiceRow.GetComponentInChildren<Button>(true);
        }

        private void EnsureChoiceRow()
        {
            ResolveChoiceUi();
            if (_choiceRow != null)
            {
                if (_choiceButtonTemplate != null)
                    _choiceButtonTemplate.gameObject.SetActive(false);
                return;
            }

            if (panel == null)
                return;

            _choiceRow = new GameObject("DialogueChoices", typeof(RectTransform));
            _choiceRow.transform.SetParent(panel.transform, false);
            RectTransform rowRect = _choiceRow.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(1f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.anchoredPosition = new Vector2(0f, 18f);
            rowRect.sizeDelta = new Vector2(-600f, 86f);

            HorizontalLayoutGroup layout = _choiceRow.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 16f;
            _choiceRow.SetActive(false);
        }

        private Button CreatePresentedChoiceButton(string label)
        {
            Button button;
            if (_choiceButtonTemplate != null)
            {
                _choiceButtonTemplate.gameObject.SetActive(false);
                button = Instantiate(_choiceButtonTemplate, _choiceRow.transform);
                button.gameObject.name = "ChoiceButton";
                button.gameObject.SetActive(true);
            }
            else
            {
                button = CreateChoiceButton(_choiceRow.transform, label);
            }

            ApplyChoiceLabel(button, label);
            return button;
        }

        private void WireChoiceNavigation()
        {
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                Button button = _choiceButtons[i];
                if (button == null)
                    continue;

                // Choice movement is owned by PollChoiceInput. Explicit UI navigation
                // would step again in the same direction and land on the opposite option.
                button.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        private void SyncChoiceSelectionVisuals()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                Button button = _choiceButtons[i];
                if (button == null)
                    continue;
                CombatUiSelectableButtonVisual visual = button.GetComponent<CombatUiSelectableButtonVisual>();
                visual?.SetSelected(selected != null && selected == button.gameObject);
            }
        }

        private static void ApplyChoiceLabel(Button button, string label)
        {
            if (button == null)
                return;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = string.IsNullOrWhiteSpace(label) ? "Continue" : label;
        }

        private static Transform FindDeepChild(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Button CreateChoiceButton(Transform parent, string label)
        {
            GameObject buttonObject = new GameObject(
                "ChoiceButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.3f, 0.42f, 0.96f);
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.18f, 0.48f, 0.63f, 1f);
            colors.pressedColor = new Color(0.08f, 0.2f, 0.29f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 74f;
            layoutElement.flexibleWidth = 1f;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(buttonObject.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 28f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = string.IsNullOrWhiteSpace(label) ? "Continue" : label;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private IEnumerator BlinkContinueIndicator()
        {
            int completedCycles = 0;
            SetContinueGlyphAlpha(1f);
            while (_isLineComplete && _isShowing)
            {
                yield return WaitUnscaled(blinkHalfCycleSeconds);
                SetContinueGlyphAlpha(dimAlpha);
                yield return WaitUnscaled(blinkHalfCycleSeconds);
                SetContinueGlyphAlpha(1f);
                completedCycles++;
                if (completedCycles >= minimumBlinkCycles)
                    _canAdvance = true;
            }
            _blinkRoutine = null;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void ApplyIdentity(string speaker, Sprite portrait, Color tint)
        {
            if (speakerText != null)
            {
                speakerText.text = speaker ?? string.Empty;
                speakerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(speaker));
            }
            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.color = tint;
            }
            if (portraitContainer != null)
                portraitContainer.SetActive(portrait != null);
        }

        private void SetContinueGlyphAlpha(float alphaFactor)
        {
            if (bodyText == null)
                return;
            bodyText.ForceMeshUpdate(true);
            TMP_TextInfo info = bodyText.textInfo;
            int index = info.characterCount - 1;
            if (index < 0 || !info.characterInfo[index].isVisible)
                return;

            TMP_CharacterInfo character = info.characterInfo[index];
            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            Color32[] colors = info.meshInfo[materialIndex].colors32;
            Color baseColor = bodyText.color;
            Color32 color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * Mathf.Clamp01(alphaFactor));
            for (int i = 0; i < 4; i++)
                colors[vertexIndex + i] = color;
            bodyText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private IEnumerator AnimateVisibility(bool showing)
        {
            CaptureShownPose();

            float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : showing ? 0f : 1f;
            float targetAlpha = showing ? 1f : 0f;
            Vector2 startPosition = _panelRect != null ? _panelRect.anchoredPosition : Vector2.zero;
            Vector3 startScale = _panelRect != null ? _panelRect.localScale : Vector3.one;
            Vector2 targetPosition = showing ? _shownAnchoredPosition : GetHiddenAnchoredPosition();
            Vector3 targetScale = showing ? _shownLocalScale : GetHiddenLocalScale();

            float motionDuration = animateVerticalMotion
                ? showing ? showMotionDuration : hideMotionDuration
                : 0f;
            float duration = Mathf.Max(motionDuration, fadeDuration);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                if (_canvasGroup != null)
                {
                    float fadeT = fadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeDuration);
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, fadeT));
                }

                if (animateVerticalMotion && _panelRect != null)
                {
                    float motionT = motionDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / motionDuration);
                    float easedT = showing
                        ? EaseOutBack(motionT, motionOvershoot)
                        : EaseInBack(motionT, motionOvershoot);
                    _panelRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, easedT);
                    _panelRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, easedT);
                }

                yield return null;
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = targetAlpha;

            if (animateVerticalMotion && _panelRect != null)
            {
                _panelRect.anchoredPosition = targetPosition;
                _panelRect.localScale = targetScale;
            }

            _fadeRoutine = null;
            if (!showing && panel != null)
                panel.SetActive(false);
        }

        private bool CanAnimateVisibility()
        {
            return isActiveAndEnabled &&
                   ((_canvasGroup != null && fadeDuration > 0f) ||
                    (animateVerticalMotion && _panelRect != null &&
                     (showMotionDuration > 0f || hideMotionDuration > 0f)));
        }

        private void CaptureShownPose()
        {
            if (_shownPoseCaptured || panel == null)
                return;

            _panelRect = panel.transform as RectTransform;
            if (_panelRect == null)
                return;

            _shownAnchoredPosition = _panelRect.anchoredPosition;
            _shownLocalScale = _panelRect.localScale;
            _shownPoseCaptured = true;
        }

        private void ApplyMotionPose(bool showing)
        {
            if (!animateVerticalMotion)
                return;

            CaptureShownPose();
            if (_panelRect == null)
                return;

            _panelRect.anchoredPosition = showing ? _shownAnchoredPosition : GetHiddenAnchoredPosition();
            _panelRect.localScale = showing ? _shownLocalScale : GetHiddenLocalScale();
        }

        private Vector2 GetHiddenAnchoredPosition()
        {
            return _shownAnchoredPosition + Vector2.down * Mathf.Max(0f, hiddenYOffset);
        }

        private Vector3 GetHiddenLocalScale()
        {
            return new Vector3(
                _shownLocalScale.x,
                _shownLocalScale.y * Mathf.Clamp(hiddenScaleY, 0.01f, 1f),
                _shownLocalScale.z);
        }

        private static float EaseOutBack(float t, float overshootStrength)
        {
            float c1 = 1.70158f * Mathf.Max(0f, overshootStrength);
            float c3 = c1 + 1f;
            float shifted = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
        }

        private static float EaseInBack(float t, float overshootStrength)
        {
            float clamped = Mathf.Clamp01(t);
            float c1 = 1.70158f * Mathf.Max(0f, overshootStrength);
            float c3 = c1 + 1f;
            return c3 * clamped * clamped * clamped - c1 * clamped * clamped;
        }

        private void ResolveReferences()
        {
            if (panel == null)
                panel = gameObject;
            if (bodyText == null && panel != null)
            {
                TextMeshProUGUI[] texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (string.Equals(texts[i].name, "DialogText", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(texts[i].name, "DialogueText", StringComparison.OrdinalIgnoreCase))
                    {
                        bodyText = texts[i];
                        break;
                    }
                }
                if (bodyText == null && texts.Length > 0)
                    bodyText = texts[0];
            }

            ResolveChoiceUi();
        }

        private void ResolveCanvasGroup()
        {
            if (panel == null)
                return;
            _panelRect = panel.transform as RectTransform;
            _canvasGroup = panel.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = panel.AddComponent<CanvasGroup>();
        }

        private void StopViewCoroutines()
        {
            StopFade();
            StopBlink();
            KillChoiceMotion();
        }

        private void StopFade()
        {
            if (_fadeRoutine == null)
                return;
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        private void StopBlink()
        {
            if (_blinkRoutine == null)
                return;
            StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
        }
    }
}
