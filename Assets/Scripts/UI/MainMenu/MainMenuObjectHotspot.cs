using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Selectable hit zone for an animated object in the main-menu illustration.
/// Scene art remains non-raycastable; this component owns the deliberately
/// small object hit rect and a separate selected-bubble proxy.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuObjectHotspot : Selectable, ISubmitHandler, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Visuals")]
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image outlineImage;
    [SerializeField] private SyncedOutlineFrameAnimator frameAnimator;
    [SerializeField] private RectTransform bubbleButton;
    [SerializeField] private GameObject bubbleHitTarget;
    [SerializeField] private TMP_Text bubbleLabel;

    [Header("Availability Outline")]
    [Tooltip("Tint used by the selected object's outline.")]
    [SerializeField] private Color selectedOutlineTint = new Color32(251, 255, 134, 255); // FBFF86
    [Tooltip("Tint used by outlines that are available but not focused.")]
    [SerializeField] private Color availableOutlineTint = new Color32(155, 171, 178, 255); // 9BAB B2

    [Header("Selection Motion")]
    [SerializeField, Range(1f, 1.2f)] private float selectedScale = 1.045f;
    [SerializeField, Range(0f, 0.1f)] private float pulseAmount = 0.012f;
    [SerializeField] private float selectPopSeconds = 0.18f;
    [SerializeField] private float pulseSeconds = 0.9f;
    [SerializeField] private float bubbleBobPixels = 5f;
    [SerializeField] private float bubbleBobSeconds = 0.85f;

    [Header("Bubble Typewriter")]
    [Tooltip("Reveals the selected bubble label one character at a time.")]
    [SerializeField] private bool bubbleTypewriterEnabled = true;
    [Tooltip("Characters revealed per second. Higher values finish the dialog bubble faster.")]
    [SerializeField, Min(0.1f)] private float bubbleTypewriterCharactersPerSecond = 30f;
    [Tooltip("Optional delay after the bubble appears before its text starts typing.")]
    [SerializeField, Min(0f)] private float bubbleTypewriterStartDelaySeconds;

    [Header("Events")]
    [SerializeField] private UnityEvent onConfirmed = new UnityEvent();

    private Sequence visualSequence;
    private Sequence bubbleSequence;
    private Sequence bubbleTypewriterSequence;
    private Vector3 visualBaseScale = Vector3.one;
    private Vector3 bubbleBaseScale = Vector3.one;
    private Vector2 bubbleBasePosition;
    private bool selected;

    public event Action<MainMenuObjectHotspot, bool> SelectionChanged;
    public event Action<MainMenuObjectHotspot> Confirmed;

    public Image BodyImage => bodyImage;
    public Image FillImage => fillImage;
    public Image OutlineImage => outlineImage;
    public RectTransform VisualRoot => visualRoot;
    public SyncedOutlineFrameAnimator FrameAnimator => frameAnimator;
    public bool IsSelected => selected;
    public UnityEvent OnConfirmed => onConfirmed;

    protected override void Awake()
    {
        base.Awake();

        if (visualRoot != null)
            visualBaseScale = visualRoot.localScale;
        if (bubbleButton != null)
        {
            bubbleBaseScale = bubbleButton.localScale;
            bubbleBasePosition = bubbleButton.anchoredPosition;
        }

        if (outlineImage != null)
        {
            outlineImage.raycastTarget = false;
        }

        if (bodyImage != null)
            bodyImage.raycastTarget = false;

        if (fillImage != null)
        {
            fillImage.raycastTarget = false;
            fillImage.DOKill(false);
            fillImage.color = WithAlpha(fillImage.color, 0f);
            fillImage.enabled = false;
        }

        PrepareBubbleLabelForReveal();

        ResetSelectionVisuals();
    }

    protected override void OnDisable()
    {
        ResetSelectionVisuals();
        base.OnDisable();
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        if (!IsInteractable())
            return;

        // Duplicate/input-module focus churn should not restart the same
        // selection animation while this hotspot is already selected.
        if (selected)
            return;

        selected = true;
        ShowSelectedVisuals();
        SelectionChanged?.Invoke(this, true);
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        bool wasSelected = selected;
        ResetSelectionVisuals();
        if (wasSelected)
            SelectionChanged?.Invoke(this, false);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Confirm();
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        SelectFromPointer();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        ConfirmFromPointer();
    }

    public void SelectFromPointer()
    {
        if (!IsActive() || !IsInteractable() || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void ConfirmFromPointer()
    {
        SelectFromPointer();
        Confirm();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactable = enabled;
        if (targetGraphic != null)
            targetGraphic.raycastTarget = enabled;
        if (bubbleHitTarget != null)
        {
            var graphics = bubbleHitTarget.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = enabled;
            }
        }

        if (!enabled)
            ResetSelectionVisuals();
    }

    public void SetBubbleLabel(string label)
    {
        if (bubbleLabel != null)
        {
            bubbleLabel.text = label ?? string.Empty;

            if (selected && bubbleButton != null && bubbleButton.gameObject.activeSelf)
                StartBubbleTypewriter();
            else
                PrepareBubbleLabelForReveal();
        }
    }

    /// <summary>Applies an externally controlled outline tint and opacity.</summary>
    public void SetOutlineAppearance(Color tint, float alpha)
    {
        if (outlineImage == null)
            return;

        outlineImage.DOKill(false);
        tint.a = Mathf.Clamp01(alpha);
        outlineImage.enabled = true;
        outlineImage.color = tint;
    }

    /// <summary>Sets the temporary activation fill without affecting focus or input.</summary>
    public void SetFillAppearance(float alpha, bool visible)
    {
        if (fillImage == null)
            return;

        fillImage.raycastTarget = false;
        fillImage.enabled = visible;
        fillImage.color = WithAlpha(fillImage.color, alpha);
    }

    private void Confirm()
    {
        if (!IsActive() || !IsInteractable())
            return;

        onConfirmed?.Invoke();
        Confirmed?.Invoke(this);
    }

    private void ShowSelectedVisuals()
    {
        KillMotion();

        SetOutlineTint(selectedState: true);

        if (visualRoot != null)
        {
            // A cancelled tween or an external UI reset can leave a focus
            // root at zero. Never begin the focus pulse from that collapsed
            // state; sprite-frame swaps themselves must not change scale.
            if (IsCollapsedScale(visualBaseScale))
                visualBaseScale = Vector3.one;
            if (IsCollapsedScale(visualRoot.localScale))
                visualRoot.localScale = visualBaseScale;

            // The pop always starts from the authored rest scale. This is a
            // one-time selection baseline, not part of the repeating pulse.
            visualRoot.localScale = visualBaseScale;

            Vector3 focusScale = visualBaseScale * selectedScale;
            Vector3 pulseScale = focusScale * (1f + pulseAmount);
            float popDuration = Mathf.Max(0.01f, selectPopSeconds);
            float pulseDuration = Mathf.Max(0.01f, pulseSeconds);

            // Keep the one-shot focus pop separate from the repeating pulse.
            // A looping Sequence rewinds every child tween to the values it
            // captured when the sequence was created. Since the first child
            // used to start at the rest scale, each loop briefly wrote 1.0
            // before returning to focusScale. The pulse sequence is created
            // only after the pop reaches focusScale, so its loop boundary is
            // focusScale -> pulseScale -> focusScale with no rest-scale snap.
            visualSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Append(visualRoot.DOScale(focusScale, popDuration).SetEase(Ease.OutBack))
                .OnComplete(() => BeginVisualPulse(focusScale, pulseScale, pulseDuration));
        }

        if (bubbleButton != null)
        {
            bubbleButton.gameObject.SetActive(true);
            if (bubbleHitTarget != null)
                bubbleHitTarget.SetActive(true);

            bubbleButton.anchoredPosition = bubbleBasePosition;
            // Bubble selection should only bob vertically. Starting at zero scale
            // created an unintended pop-in/collapse every time focus changed.
            bubbleButton.localScale = bubbleBaseScale;
            bubbleSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Append(bubbleButton.DOAnchorPosY(bubbleBasePosition.y + bubbleBobPixels, Mathf.Max(0.01f, bubbleBobSeconds)).SetEase(Ease.InOutSine))
                .Append(bubbleButton.DOAnchorPosY(bubbleBasePosition.y, Mathf.Max(0.01f, bubbleBobSeconds)).SetEase(Ease.InOutSine))
                .SetLoops(-1);

            StartBubbleTypewriter();
        }
    }

    private void ResetSelectionVisuals()
    {
        selected = false;
        KillMotion();

        if (visualRoot != null)
            visualRoot.localScale = visualBaseScale;
        SetOutlineTint(selectedState: false);

        if (bubbleButton != null)
        {
            bubbleButton.anchoredPosition = bubbleBasePosition;
            bubbleButton.localScale = bubbleBaseScale;
            bubbleButton.gameObject.SetActive(false);
        }

        if (bubbleHitTarget != null)
            bubbleHitTarget.SetActive(false);

        PrepareBubbleLabelForReveal();
    }

    private void KillMotion()
    {
        visualSequence?.Kill();
        visualSequence = null;
        bubbleSequence?.Kill();
        bubbleSequence = null;
        bubbleTypewriterSequence?.Kill();
        bubbleTypewriterSequence = null;
        visualRoot?.DOKill();
        bubbleButton?.DOKill();
        bubbleLabel?.DOKill();
    }

    private void PrepareBubbleLabelForReveal()
    {
        if (bubbleLabel == null)
            return;

        bubbleTypewriterSequence?.Kill();
        bubbleTypewriterSequence = null;
        bubbleLabel.maxVisibleCharacters = bubbleTypewriterEnabled ? 0 : int.MaxValue;
    }

    private void StartBubbleTypewriter()
    {
        if (bubbleLabel == null)
            return;

        bubbleTypewriterSequence?.Kill();
        bubbleTypewriterSequence = null;
        bubbleLabel.DOKill(false);
        bubbleLabel.ForceMeshUpdate();

        if (!bubbleTypewriterEnabled)
        {
            bubbleLabel.maxVisibleCharacters = int.MaxValue;
            return;
        }

        int characterCount = bubbleLabel.textInfo.characterCount;
        bubbleLabel.maxVisibleCharacters = 0;
        if (characterCount <= 0)
            return;

        float charactersPerSecond = Mathf.Max(0.1f, bubbleTypewriterCharactersPerSecond);
        float revealDuration = characterCount / charactersPerSecond;

        bubbleTypewriterSequence = DOTween.Sequence()
            .SetUpdate(true);

        float startDelay = Mathf.Max(0f, bubbleTypewriterStartDelaySeconds);
        if (startDelay > 0f)
            bubbleTypewriterSequence.AppendInterval(startDelay);

        bubbleTypewriterSequence
            .Append(DOVirtual.Int(0, characterCount, revealDuration, value =>
            {
                if (bubbleLabel != null)
                    bubbleLabel.maxVisibleCharacters = value;
            }).SetEase(Ease.Linear).SetUpdate(true))
            .OnComplete(() =>
            {
                if (bubbleLabel != null)
                    bubbleLabel.maxVisibleCharacters = int.MaxValue;
            });
    }

    private void SetOutlineTint(bool selectedState)
    {
        if (outlineImage == null)
            return;

        // The navigator may immediately replace this fallback tint with its
        // global availability pulse. This method never changes object scale.
        SetOutlineAppearance(selectedState ? selectedOutlineTint : availableOutlineTint, 1f);
    }

    private void BeginVisualPulse(Vector3 focusScale, Vector3 pulseScale, float pulseDuration)
    {
        if (!selected || visualRoot == null)
            return;

        // This sequence is instantiated after the focus pop has completed;
        // DOTween therefore captures focusScale as its loop-start value.
        visualSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(visualRoot.DOScale(pulseScale, pulseDuration).SetEase(Ease.InOutSine))
            .Append(visualRoot.DOScale(focusScale, pulseDuration).SetEase(Ease.InOutSine))
            .SetLoops(-1);
    }

    private static bool IsCollapsedScale(Vector3 scale)
    {
        const float epsilon = 0.0001f;
        return Mathf.Abs(scale.x) <= epsilon ||
               Mathf.Abs(scale.y) <= epsilon ||
               Mathf.Abs(scale.z) <= epsilon;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        bubbleTypewriterCharactersPerSecond = Mathf.Max(0.1f, bubbleTypewriterCharactersPerSecond);
        bubbleTypewriterStartDelaySeconds = Mathf.Max(0f, bubbleTypewriterStartDelaySeconds);
    }
#endif
}
