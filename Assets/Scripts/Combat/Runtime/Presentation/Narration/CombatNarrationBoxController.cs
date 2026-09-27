using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatNarrationBoxController : MonoBehaviour
    {
        [SerializeField] private CombatNarrationProfileSO profile;

        [Header("Authored Panel")]
        [Tooltip("Preferred combat narration panel (authored under CombatBattleCanvas).")]
        [SerializeField] private RectTransform authoredPanel;
        [SerializeField] private TextMeshProUGUI authoredBodyText;
        [SerializeField] private TextMeshProUGUI authoredSpeakerText;
        [SerializeField] private DialogueBoxView authoredDialogueView;

        [Header("Legacy / Fallback")]
        [Tooltip("Optional shared DialoguePanel visual. Used only when authoredPanel is unset.")]
        [SerializeField] private RectTransform visualTemplate;
        [SerializeField] private RectTransform visualParent;
        [SerializeField] private bool buildFallbackWhenTemplateMissing = true;

        private DialogueBoxView _view;
        private bool _advanceRequested;
        private Button _touchAdvanceButton;
        private int _inputEligibleFrame;

        public CombatNarrationProfileSO Profile => profile;
        public bool IsConfigured => EnsureView();
        public bool IsVisible => _view != null && _view.IsShowing;

        private void Awake()
        {
            EnsureView();
            _view?.InitializeHidden();
        }

        private void OnDisable()
        {
            _advanceRequested = false;
            UnbindMobileChoiceInput();
            SetTouchAdvanceRaycastEnabled(true);
            // Avoid StartCoroutine while this host (or the panel-hosted view) is disabling.
            if (_view != null)
                _view.Hide();
        }

        public void SetProfile(CombatNarrationProfileSO value)
        {
            profile = value;
        }

        public void BindAuthoredPanel(
            RectTransform panel,
            TextMeshProUGUI bodyText,
            TextMeshProUGUI speakerText = null,
            DialogueBoxView dialogueView = null)
        {
            authoredPanel = panel;
            authoredBodyText = bodyText;
            authoredSpeakerText = speakerText;
            authoredDialogueView = dialogueView;
            _view = null;
            EnsureView();
            _view?.InitializeHidden();
        }

        public IEnumerator PresentPage(CombatNarrationPage page)
        {
            if (page == null || !page.IsReadable || !EnsureView())
                yield break;

            _advanceRequested = false;
            _inputEligibleFrame = Time.frameCount + 1;
            _view.Show();
            float charactersPerSecond = profile != null ? profile.CharactersPerSecond : 60f;
            IEnumerator reveal = _view.PresentLine(page.speaker, page.text, charactersPerSecond);
            while (reveal.MoveNext())
            {
                PollAdvanceInput();
                if (_advanceRequested)
                {
                    _advanceRequested = false;
                    _view.RequestAdvance();
                }
                yield return reveal.Current;
            }

            _view.AllowImmediateAdvance();
            float elapsed = 0f;
            CombatNarrationAdvanceMode mode = profile != null
                ? profile.AdvanceMode
                : CombatNarrationAdvanceMode.AutoWithOptionalAdvance;

            while (true)
            {
                PollAdvanceInput();
                if (_advanceRequested)
                {
                    _advanceRequested = false;
                    if (mode != CombatNarrationAdvanceMode.Automatic &&
                        _view.RequestAdvance() == DialogueBoxAdvanceResult.Advanced)
                    {
                        yield break;
                    }
                }

                elapsed += Time.unscaledDeltaTime;
                if (mode != CombatNarrationAdvanceMode.ConfirmEveryPage &&
                    elapsed >= Mathf.Max(0f, page.minimumDisplaySeconds))
                {
                    yield break;
                }
                yield return null;
            }
        }

        public IEnumerator PresentUntilAdvanced(CombatNarrationPage page)
        {
            if (page == null || !page.IsReadable || !EnsureView())
                yield break;

            _advanceRequested = false;
            _inputEligibleFrame = Time.frameCount + 1;
            _view.Show();
            float charactersPerSecond = profile != null ? profile.CharactersPerSecond : 60f;
            IEnumerator reveal = _view.PresentLine(page.speaker, page.text, charactersPerSecond);
            while (reveal.MoveNext())
            {
                PollAdvanceInput();
                if (_advanceRequested)
                {
                    _advanceRequested = false;
                    _view.RequestAdvance();
                }
                yield return reveal.Current;
            }

            _view.AllowImmediateAdvance();
            while (true)
            {
                PollAdvanceInput();
                if (_advanceRequested)
                {
                    _advanceRequested = false;
                    if (_view.RequestAdvance() == DialogueBoxAdvanceResult.Advanced)
                        yield break;
                }
                yield return null;
            }
        }

        public IEnumerator PresentChoices(
            CombatNarrationPage page,
            IReadOnlyList<string> labels,
            Action<int> onPicked)
        {
            if (page == null || !page.IsReadable || !EnsureView() || labels == null || labels.Count == 0)
            {
                onPicked?.Invoke(0);
                yield break;
            }

            _advanceRequested = false;
            _inputEligibleFrame = Time.frameCount + 1;
            if (_touchAdvanceButton != null)
                _touchAdvanceButton.interactable = false;

            _view.Show();
            float charactersPerSecond = profile != null ? profile.CharactersPerSecond : 60f;
            IEnumerator reveal = _view.PresentLine(page.speaker, page.text, charactersPerSecond);
            while (reveal.MoveNext())
            {
                PollAdvanceInput();
                if (_advanceRequested)
                {
                    _advanceRequested = false;
                    _view.RequestAdvance();
                }
                yield return reveal.Current;
            }

            int picked = -1;
            EnsureChoiceInputInfrastructure();
            _view.ShowChoices(labels, index => picked = index);
            SetTouchAdvanceRaycastEnabled(false);
            while (picked < 0)
            {
                yield return null;
            }

            UnbindMobileChoiceInput();
            SetTouchAdvanceRaycastEnabled(true);

            yield return _view.WaitForChoiceMotion();

            if (_touchAdvanceButton != null)
                _touchAdvanceButton.interactable = true;
            onPicked?.Invoke(picked);
        }

        public void HideForSelection()
        {
            _advanceRequested = false;
            // Use Unity's overloaded null check (destroyed objects), not ?. 
            if (_view != null)
                _view.Hide();
        }

        public static CombatNarrationBoxController FindOrCreate(Combat2BattlefieldPresenter presenter)
        {
            CombatNarrationBoxController existing = FindFirstObjectByType<CombatNarrationBoxController>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            Canvas canvas = FindCombatCanvas();
            GameObject host = canvas != null
                ? canvas.gameObject
                : presenter != null ? presenter.gameObject : null;
            if (host == null)
                return null;
            return host.AddComponent<CombatNarrationBoxController>();
        }

        private bool EnsureView()
        {
            if (_view != null)
                return true;

            if (TryBindAuthoredView())
                return true;

            RectTransform parent = visualParent;
            if (parent == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                    canvas = FindCombatCanvas();
                parent = canvas != null ? FindPreferredParent(canvas, "HudRoot", "SafeAreaRoot") : transform as RectTransform;
            }

            RectTransform template = visualTemplate != null ? visualTemplate : FindCanonicalDialoguePanel(parent);
            RectTransform panel = null;
            if (template != null && parent != null)
            {
                panel = Instantiate(template, parent, false);
                panel.name = "CombatNarrationPanel";
                ConfigureCombatRect(panel);
            }
            else if (buildFallbackWhenTemplateMissing && parent != null)
            {
                panel = BuildFallbackPanel(parent);
            }

            if (panel == null)
                return false;

            TextMeshProUGUI body = FindText(panel, "DialogText", "DialogueText", "BodyText");
            TextMeshProUGUI speaker = FindText(panel, "SpeakerName", "SpeakerText");
            _view = panel.GetComponent<DialogueBoxView>();
            if (_view == null)
                _view = panel.gameObject.AddComponent<DialogueBoxView>();
            _view.Configure(
                panel.gameObject,
                body,
                speaker,
                prefixSpeakerInBody: speaker == null,
                panelFadeDuration: 0.12f,
                requiredBlinkCycles: 0,
                blinkHalfCycle: 0.18f,
                continueDimAlpha: 0.35f);
            InstallTouchAdvance(panel);
            return true;
        }

        private bool TryBindAuthoredView()
        {
            if (authoredPanel == null)
                return false;

            // Prefer texts under CombatNarrationBG when present; do not rebuild/relayout the panel.
            TextMeshProUGUI body = authoredBodyText != null
                ? authoredBodyText
                : FindText(authoredPanel, "DialogText", "DialogueText", "BodyText");
            if (body == null)
                return false;

            TextMeshProUGUI speaker = authoredSpeakerText != null
                ? authoredSpeakerText
                : FindText(authoredPanel, "SpeakerName", "SpeakerText");

            _view = authoredDialogueView != null
                ? authoredDialogueView
                : authoredPanel.GetComponent<DialogueBoxView>();
            if (_view == null)
                _view = authoredPanel.gameObject.AddComponent<DialogueBoxView>();

            _view.Configure(
                authoredPanel.gameObject,
                body,
                speaker,
                prefixSpeakerInBody: speaker == null,
                panelFadeDuration: 0.12f,
                requiredBlinkCycles: 0,
                blinkHalfCycle: 0.18f,
                continueDimAlpha: 0.35f);
            EnsureAuthoredBackgroundPresentation(authoredPanel);
            InstallTouchAdvance(authoredPanel);
            return true;
        }

        /// <summary>
        /// Keeps the authored CombatNarrationBG hierarchy intact and ensures scanlines are active
        /// when the component is present. Never repositions/resizes the panel or background.
        /// </summary>
        private static void EnsureAuthoredBackgroundPresentation(RectTransform panel)
        {
            if (panel == null)
                return;

            Transform background = panel.Find("CombatNarrationBG");
            if (background == null)
                return;

            var scanlines = background.GetComponent<CombatUiScanlineBackground>();
            if (scanlines == null)
                return;

            scanlines.ApplyPresentation();
        }

        private void PollAdvanceInput()
        {
            if (_advanceRequested || Time.frameCount < _inputEligibleFrame)
                return;
            _advanceRequested =
                (Keyboard.current != null && (Keyboard.current.anyKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
        }

        private void UnbindMobileChoiceInput()
        {
        }

        private static void EnsureChoiceInputInfrastructure()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            if (!eventSystem.enabled)
                eventSystem.enabled = true;

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
                return;

            if (!inputModule.enabled)
                inputModule.enabled = true;

            InputActionAsset actions = inputModule.actionsAsset;
            InputActionMap uiMap = actions != null ? actions.FindActionMap("UI", false) : null;
            if (uiMap != null && !uiMap.enabled)
                uiMap.Enable();
        }

        private void SetTouchAdvanceRaycastEnabled(bool enabled)
        {
            if (_touchAdvanceButton == null)
                return;

            // AdvanceInput is a full-panel transparent button. Deactivate it while
            // choices are open so it can never sit above and consume touch/mouse hits.
            _touchAdvanceButton.gameObject.SetActive(enabled);
            _touchAdvanceButton.interactable = enabled;
            Graphic graphic = _touchAdvanceButton.targetGraphic;
            if (graphic != null)
                graphic.raycastTarget = enabled;
        }

        private void InstallTouchAdvance(RectTransform panel)
        {
            if (panel == null)
                return;

            Transform existing = panel.Find("AdvanceInput");
            if (existing != null)
            {
                _touchAdvanceButton = existing.GetComponent<Button>();
                if (_touchAdvanceButton != null)
                {
                    _touchAdvanceButton.onClick.RemoveAllListeners();
                    _touchAdvanceButton.onClick.AddListener(() => _advanceRequested = true);
                }
                return;
            }

            GameObject input = new GameObject("AdvanceInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            input.transform.SetParent(panel, false);
            RectTransform rect = (RectTransform)input.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = input.GetComponent<Image>();
            image.color = Color.clear;
            _touchAdvanceButton = input.GetComponent<Button>();
            _touchAdvanceButton.transition = Selectable.Transition.None;
            _touchAdvanceButton.onClick.AddListener(() => _advanceRequested = true);
        }

        private static Canvas FindCombatCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name.IndexOf("CombatBattleCanvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    canvases[i].transform.root.name.IndexOf("CombatBattleCanvas", StringComparison.OrdinalIgnoreCase) >= 0)
                    return canvases[i];
            }
            return canvases.Length > 0 ? canvases[0] : null;
        }

        private static RectTransform FindPreferredParent(Canvas canvas, params string[] names)
        {
            RectTransform[] rects = canvas.GetComponentsInChildren<RectTransform>(true);
            for (int n = 0; n < names.Length; n++)
            {
                for (int i = 0; i < rects.Length; i++)
                {
                    if (string.Equals(rects[i].name, names[n], StringComparison.OrdinalIgnoreCase))
                        return rects[i];
                }
            }
            return canvas.transform as RectTransform;
        }

        private static RectTransform FindCanonicalDialoguePanel(RectTransform combatParent)
        {
            RectTransform[] rects = Resources.FindObjectsOfTypeAll<RectTransform>();
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect == null || !rect.gameObject.scene.IsValid() || rect == combatParent ||
                    !string.Equals(rect.name, "DialoguePanel", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (combatParent != null && rect.IsChildOf(combatParent))
                    continue;
                if (FindText(rect, "DialogText", "DialogueText") != null)
                    return rect;
            }
            return null;
        }

        private static void ConfigureCombatRect(RectTransform panel)
        {
            // Fallback clones only — never call this on an authored CombatNarrationPanel instance.
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 48f);
            panel.sizeDelta = new Vector2(-64f, 420f);
            panel.localScale = Vector3.one;
            panel.localRotation = Quaternion.identity;
        }

        private static RectTransform BuildFallbackPanel(RectTransform parent)
        {
            GameObject panelObject = new GameObject(
                "CombatNarrationPanel",
                typeof(RectTransform),
                typeof(CanvasGroup));
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            ConfigureCombatRect(panel);

            GameObject backgroundObject = new GameObject(
                "CombatNarrationBG",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CombatUiScanlineBackground));
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.SetParent(panel, false);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            Image background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.96f, 0.94f, 0.82f, 0.98f);

            GameObject textObject = new GameObject("DialogText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(backgroundRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(56f, 40f);
            textRect.offsetMax = new Vector2(-56f, -40f);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 48f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 28f;
            text.fontSizeMax = 52f;
            text.color = new Color(0.18f, 0.13f, 0.18f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            return panel;
        }

        private static TextMeshProUGUI FindText(RectTransform root, params string[] names)
        {
            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int n = 0; n < names.Length; n++)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    if (string.Equals(texts[i].name, names[n], StringComparison.OrdinalIgnoreCase))
                        return texts[i];
                }
            }
            return null;
        }
    }
}
