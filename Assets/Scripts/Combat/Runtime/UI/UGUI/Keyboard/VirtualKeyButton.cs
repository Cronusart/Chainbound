using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    public enum VirtualKeyType
    {
        Character = 0,
        Shift = 1,
        Backspace = 2,
        Clear = 3,
        Cancel = 4,
        Confirm = 5,
    }

    [RequireComponent(typeof(Button))]
    public class VirtualKeyButton : MonoBehaviour,
        ISubmitHandler,
        ICancelHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private const string MotionRootName = "KeyPressVisual";
        private const float DefaultPressScale = 0.94f;
        private const float DefaultPressYOffset = 5f;
        private const float PressInDuration = 0.035f;
        private const float PressOutDuration = 0.08f;
        private const float PulseHoldDuration = 0.02f;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [Tooltip("Child rect that receives press scale/offset. Must NOT be the layout-driven key root.")]
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private VirtualKeyType keyType = VirtualKeyType.Character;
        [SerializeField] private string unshiftedValue = "";
        [SerializeField] private string shiftedValue = "";
        [SerializeField] private string unshiftedLabel = "";
        [SerializeField] private string shiftedLabel = "";
        [SerializeField] private float pressScale = DefaultPressScale;
        [SerializeField] private float pressYOffset = DefaultPressYOffset;

        private string runtimeUnshiftedLabel;
        private string runtimeShiftedLabel;
        private bool lastShiftEnabled;
        private Vector3 baseScale = Vector3.one;
        private Vector2 baseAnchoredPosition;
        private bool baseTransformCaptured;
        private bool isPointerPressed;
        private Sequence pressPulseSequence;

        public event Action<VirtualKeyButton> Pressed;
        public event Action<VirtualKeyButton> Selected;
        public event Action<VirtualKeyButton> Deselected;
        public event Action CancelRequested;

        public VirtualKeyType KeyType => keyType;
        public string UnshiftedValue => unshiftedValue ?? "";
        public string ShiftedValue => shiftedValue ?? "";
        public Button Button => button;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            CaptureBaseTransform(force: true);
        }

        private void OnDisable()
        {
            KillPressTween(restore: true);
            isPointerPressed = false;

            if (button != null)
                button.onClick.RemoveListener(OnUnityButtonClicked);
        }

        private void OnDestroy()
        {
            KillPressTween(restore: false);
        }

        public string GetOutput(bool shiftEnabled)
        {
            if (keyType != VirtualKeyType.Character)
                return "";

            if (shiftEnabled && !string.IsNullOrEmpty(shiftedValue))
                return shiftedValue;

            return unshiftedValue ?? "";
        }

        public bool MatchesCharacterOutput(string value)
        {
            if (keyType != VirtualKeyType.Character || string.IsNullOrEmpty(value))
                return false;

            return string.Equals(unshiftedValue, value, StringComparison.Ordinal) ||
                   string.Equals(shiftedValue, value, StringComparison.Ordinal);
        }

        public void RefreshVisual(bool shiftEnabled)
        {
            lastShiftEnabled = shiftEnabled;

            if (label == null)
                return;

            string displayValue;
            if (shiftEnabled && !string.IsNullOrEmpty(runtimeShiftedLabel))
            {
                displayValue = runtimeShiftedLabel;
            }
            else if (!shiftEnabled && !string.IsNullOrEmpty(runtimeUnshiftedLabel))
            {
                displayValue = runtimeUnshiftedLabel;
            }
            else if (shiftEnabled && !string.IsNullOrEmpty(shiftedLabel))
            {
                displayValue = shiftedLabel;
            }
            else if (!string.IsNullOrEmpty(unshiftedLabel))
            {
                displayValue = unshiftedLabel;
            }
            else
            {
                displayValue = GetOutput(shiftEnabled);
            }

            label.text = displayValue;
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        public void Configure(
            VirtualKeyType configuredKeyType,
            string configuredUnshiftedValue,
            string configuredShiftedValue,
            string configuredUnshiftedLabel,
            string configuredShiftedLabel)
        {
            keyType = configuredKeyType;
            unshiftedValue = configuredUnshiftedValue ?? "";
            shiftedValue = configuredShiftedValue ?? "";
            unshiftedLabel = configuredUnshiftedLabel ?? "";
            shiftedLabel = configuredShiftedLabel ?? "";

            if (button == null)
                button = GetComponent<Button>();

            if (label == null)
                label = GetComponentInChildren<TMP_Text>(true) ?? CreateRuntimeLabel();

            RefreshVisual(lastShiftEnabled);
        }

        public void SetRuntimeLabels(string unshifted, string shifted = null)
        {
            runtimeUnshiftedLabel = unshifted ?? "";
            runtimeShiftedLabel = shifted ?? runtimeUnshiftedLabel;
            RefreshVisual(lastShiftEnabled);
        }

        public void ClearRuntimeLabels()
        {
            runtimeUnshiftedLabel = "";
            runtimeShiftedLabel = "";
            RefreshVisual(lastShiftEnabled);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            // Keyboard Enter/Space are owned by OnScreenKeyboardService hardware remap
            // (Enter=Confirm, Space=character). Keep Submit for gamepad / PSG1 South only.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame))
            {
                return;
            }

            PlayPressPulse();
            TriggerPressed();
        }

        public void OnCancel(BaseEventData eventData)
        {
            CancelRequested?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInteract())
                return;

            isPointerPressed = true;
            ApplyPressedVisual(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isPointerPressed)
                return;

            isPointerPressed = false;
            ApplyPressedVisual(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isPointerPressed)
                return;

            isPointerPressed = false;
            ApplyPressedVisual(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            Selected?.Invoke(this);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Deselected?.Invoke(this);
        }

        /// <summary>
        /// Brief press animation for hardware / submit paths that do not drive pointer down/up.
        /// </summary>
        public void PlayPressPulse()
        {
            if (!isActiveAndEnabled)
                return;

            RectTransform target = GetVisualRoot();
            if (target == null)
                return;

            CaptureBaseTransform(force: true);
            KillPressTween(restore: true);

            Vector3 pressedScale = baseScale * pressScale;
            Vector2 pressedPosition = baseAnchoredPosition + new Vector2(0f, -pressYOffset);

            pressPulseSequence = DOTween.Sequence()
                .Append(target.DOScale(pressedScale, PressInDuration).SetEase(Ease.OutQuad))
                .Join(target.DOAnchorPos(pressedPosition, PressInDuration).SetEase(Ease.OutQuad))
                .AppendInterval(PulseHoldDuration)
                .Append(target.DOScale(baseScale, PressOutDuration).SetEase(Ease.OutQuad))
                .Join(target.DOAnchorPos(baseAnchoredPosition, PressOutDuration).SetEase(Ease.OutQuad))
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(() => RestoreVisualImmediate());
        }

        private void Initialize()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (label == null)
                label = GetComponentInChildren<TMP_Text>(true);

            if (label == null)
                label = CreateRuntimeLabel();

            if (label != null)
                label.raycastTarget = false;

            EnsureMotionVisualRoot();
            EnsureTargetGraphic();
            WireUnityClick();
            CaptureBaseTransform(force: false);

            RefreshVisual(lastShiftEnabled);
        }

        private void WireUnityClick()
        {
            if (button == null)
                return;

            button.onClick.RemoveListener(OnUnityButtonClicked);
            button.onClick.AddListener(OnUnityButtonClicked);
        }

        private void OnUnityButtonClicked()
        {
            TriggerPressed();
        }

        private void TriggerPressed()
        {
            if (!CanInteract())
                return;

            Pressed?.Invoke(this);
        }

        private bool CanInteract()
        {
            if (button == null)
                button = GetComponent<Button>();

            return button != null && button.IsActive() && button.IsInteractable();
        }

        /// <summary>
        /// HorizontalLayoutGroup drives the key root. Animating that root's anchoredPosition
        /// fights the layout and teleports keys left. Press motion must use a child rect.
        /// </summary>
        private void EnsureMotionVisualRoot()
        {
            if (visualRoot != null && visualRoot != (RectTransform)transform)
                return;

            Transform existing = transform.Find(MotionRootName);
            if (existing is RectTransform existingRect)
            {
                visualRoot = existingRect;
                return;
            }

            Image rootImage = GetComponent<Image>();

            GameObject motionObject = new GameObject(MotionRootName, typeof(RectTransform), typeof(CanvasRenderer));
            motionObject.layer = gameObject.layer;
            visualRoot = motionObject.GetComponent<RectTransform>();
            visualRoot.SetParent(transform, false);
            visualRoot.anchorMin = Vector2.zero;
            visualRoot.anchorMax = Vector2.one;
            visualRoot.offsetMin = Vector2.zero;
            visualRoot.offsetMax = Vector2.zero;
            visualRoot.pivot = new Vector2(0.5f, 0.5f);
            visualRoot.localScale = Vector3.one;
            visualRoot.anchoredPosition = Vector2.zero;
            visualRoot.SetAsFirstSibling();

            if (rootImage != null)
            {
                Image visualImage = motionObject.AddComponent<Image>();
                visualImage.sprite = rootImage.sprite;
                visualImage.type = rootImage.type;
                visualImage.color = rootImage.color;
                visualImage.material = rootImage.material;
                visualImage.pixelsPerUnitMultiplier = rootImage.pixelsPerUnitMultiplier;
                visualImage.raycastTarget = true;
                visualImage.fillCenter = rootImage.fillCenter;

                // Keep a transparent raycast target on the layout root for Button hit area stability.
                Color rootColor = rootImage.color;
                rootImage.color = new Color(rootColor.r, rootColor.g, rootColor.b, 0f);
                rootImage.raycastTarget = true;

                if (button != null)
                    button.targetGraphic = visualImage;
            }

            // Reparent existing visual children (labels, icons) under the motion root.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child == visualRoot)
                    continue;

                child.SetParent(visualRoot, false);
            }
        }

        private void CaptureBaseTransform(bool force)
        {
            RectTransform target = GetVisualRoot();
            if (target == null)
                return;

            if (baseTransformCaptured && !force)
                return;

            // Always capture resting pose from a non-pressed state.
            target.DOKill();
            if (baseTransformCaptured)
            {
                target.localScale = baseScale;
                target.anchoredPosition = baseAnchoredPosition;
            }

            baseScale = target.localScale;
            if (baseScale.sqrMagnitude < 0.0001f)
                baseScale = Vector3.one;

            // Child motion root rests at zero; never capture a mid-press offset as base.
            baseAnchoredPosition = Vector2.zero;
            target.anchoredPosition = baseAnchoredPosition;
            target.localScale = baseScale;
            baseTransformCaptured = true;
        }

        private RectTransform GetVisualRoot()
        {
            if (visualRoot == null || visualRoot == (RectTransform)transform)
                EnsureMotionVisualRoot();

            return visualRoot;
        }

        private void ApplyPressedVisual(bool pressed)
        {
            RectTransform target = GetVisualRoot();
            if (target == null)
                return;

            CaptureBaseTransform(force: false);
            KillPressTween(restore: false);

            Vector3 targetScale = pressed ? baseScale * pressScale : baseScale;
            Vector2 targetPosition = pressed
                ? baseAnchoredPosition + new Vector2(0f, -pressYOffset)
                : baseAnchoredPosition;

            float duration = pressed ? PressInDuration : PressOutDuration;
            target.DOKill();
            target.DOScale(targetScale, duration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            target.DOAnchorPos(targetPosition, duration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        private void RestoreVisualImmediate()
        {
            RectTransform target = GetVisualRoot();
            if (target == null || !baseTransformCaptured)
                return;

            target.localScale = baseScale;
            target.anchoredPosition = baseAnchoredPosition;
        }

        private void KillPressTween(bool restore)
        {
            if (pressPulseSequence != null && pressPulseSequence.IsActive())
                pressPulseSequence.Kill();

            pressPulseSequence = null;

            RectTransform target = GetVisualRoot();
            target?.DOKill();

            if (restore)
                RestoreVisualImmediate();
        }

        private void EnsureTargetGraphic()
        {
            if (button == null)
                return;

            Graphic targetGraphic = button.targetGraphic;
            if (targetGraphic == null && visualRoot != null)
                targetGraphic = visualRoot.GetComponent<Graphic>();

            if (targetGraphic == null)
                targetGraphic = GetComponent<Graphic>();

            if (targetGraphic == null)
            {
                Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    Graphic graphic = graphics[i];
                    if (graphic == null || graphic == label)
                        continue;

                    targetGraphic = graphic;
                    break;
                }
            }

            if (targetGraphic == null)
            {
                Image fallbackImage = GetComponent<Image>();
                if (fallbackImage == null)
                    fallbackImage = gameObject.AddComponent<Image>();

                fallbackImage.color = new Color(1f, 1f, 1f, 0f);
                targetGraphic = fallbackImage;
            }

            targetGraphic.raycastTarget = true;
            button.targetGraphic = targetGraphic;
        }

        private TMP_Text CreateRuntimeLabel()
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            labelObject.transform.SetParent(transform, false);

            RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(12f, 8f);
            rectTransform.offsetMax = new Vector2(-12f, -8f);

            TextMeshProUGUI tmp = labelObject.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
