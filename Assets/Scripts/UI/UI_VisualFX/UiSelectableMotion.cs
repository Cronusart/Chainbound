using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;

/// <summary>
/// Reusable focus / press / sprite-swap / shimmer component for any menu button.
/// Driven entirely by EventSystem selection events — no Update allocations.
/// All tweens use SetUpdate(true) to run at timeScale 0.
///
/// Confirm SFX fires on both keyboard/gamepad Submit (ISubmitHandler) and
/// mouse/touch click (IPointerClickHandler) so it works for all input paths.
///
/// Shimmer plays in a play-once → pause → repeat cycle while selected,
/// controlled by the profile's ShimmerPauseDuration. Loop is forced off
/// on the animator at runtime.
/// </summary>
public class UiSelectableMotion : MonoBehaviour,
    ISelectHandler, IDeselectHandler, ISubmitHandler,
    IMoveHandler, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Sprite Swap")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite deselectedSprite;
    [SerializeField] private Sprite selectedSprite;
    [Tooltip("When enabled, the shimmer overlay supplies the selected-state artwork while the base button image remains unchanged. This prevents two full button sprites from stacking on focus.")]
    [SerializeField] private bool shimmerOwnsSelectedVisual = true;

    [Header("Scale Target")]
    [Tooltip("Transform to tween. Defaults to this GameObject's transform if null.")]
    [SerializeField] private RectTransform targetTransform;

    [Header("Motion Profile")]
    [SerializeField] private UiMotionProfileSO profile;

    [Header("SFX")]
    [SerializeField] private UiSfxRouter sfxRouter;

    [Header("Shimmer (Optional)")]
    [Tooltip("Frame animator on a child overlay Image. Loop is forced off at runtime; this component manages play→pause→repeat using ShimmerPauseDuration.")]
    [SerializeField] private UIImageFrameAnimator shimmerAnimator;

    private Vector3 _baseScale;
    private bool _baseScaleCaptured;
    private Tweener _pulseTween;
    private Coroutine _shimmerCoroutine;

    private void Awake()
    {
        if (targetTransform == null)
            targetTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        // Shimmer overlay hidden until selected; unselected buttons never get OnDeselect on panel init.
        bool isSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        ApplySelectionVisuals(isSelected);
        SetShimmerOverlayVisible(isSelected);
        if (isSelected && shimmerAnimator != null && _shimmerCoroutine == null)
            StartShimmerCycle();
    }

    private void CaptureBaseScaleIfNeeded()
    {
        if (_baseScaleCaptured || targetTransform == null) return;
        _baseScale = targetTransform.localScale;
        _baseScaleCaptured = true;
    }

    private void ApplySelectionVisuals(bool selected)
    {
        bool shimmerProvidesSelectedVisual = shimmerOwnsSelectedVisual && shimmerAnimator != null;
        if (buttonImage == null)
            return;

        if (selected && !shimmerProvidesSelectedVisual && selectedSprite != null)
            buttonImage.sprite = selectedSprite;
        else if (deselectedSprite != null)
            buttonImage.sprite = deselectedSprite;
    }

    // ── ISelectHandler ──────────────────────────────────────────────────────

    public void OnSelect(BaseEventData eventData)
    {
        CaptureBaseScaleIfNeeded();

        // The authored shimmer frames are full button visuals. Keep the base
        // image on its deselected sprite while the shimmer layer animates so
        // the selected sprite is not drawn twice in the same row.
        ApplySelectionVisuals(true);

        if (sfxRouter != null)
            sfxRouter.PlayNavigate(gameObject);

        if (targetTransform != null && profile != null)
        {
            targetTransform.DOKill();

            Vector3 focusTarget = _baseScale * profile.FocusScale;

            targetTransform
                .DOScale(focusTarget, profile.FocusInDuration)
                .SetEase(profile.FocusEase)
                .SetUpdate(true)
                .OnComplete(() => StartPulse(focusTarget));
        }

        StartShimmerCycle();
    }

    // ── IDeselectHandler ────────────────────────────────────────────────────

    public void OnDeselect(BaseEventData eventData)
    {
        StopPulse();
        StopShimmerCycle();

        ApplySelectionVisuals(false);

        if (targetTransform != null && profile != null && _baseScaleCaptured)
        {
            targetTransform.DOKill();
            targetTransform
                .DOScale(_baseScale, profile.FocusOutDuration)
                .SetEase(profile.FocusEase)
                .SetUpdate(true);
        }
    }

    // ── ISubmitHandler (keyboard / gamepad Submit) ──────────────────────────

    public void OnSubmit(BaseEventData eventData)
    {
        PlayConfirmAndPress();
    }

    // ── IPointerClickHandler (mouse / touch click) ──────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayConfirmAndPress();
    }

    // ── IPointerEnterHandler (mouse / touch hover → select) ─────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayerMenuSfxResolver.MarkPlayerNavigationIntent();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData != null && eventData.moveDir != MoveDirection.None)
            PlayerMenuSfxResolver.MarkPlayerNavigationIntent();
    }

    // ── Shared confirm + press squash ───────────────────────────────────────

    private void PlayConfirmAndPress()
    {
        if (sfxRouter != null)
            sfxRouter.PlayConfirm();

        if (targetTransform == null || profile == null || !_baseScaleCaptured) return;

        StopPulse();
        targetTransform.DOKill();

        Vector3 pressTarget = _baseScale * profile.PressScale;
        Vector3 focusTarget = _baseScale * profile.FocusScale;

        DOTween.Sequence()
            .Append(targetTransform.DOScale(pressTarget, profile.PressDuration).SetEase(Ease.OutQuad))
            .Append(targetTransform.DOScale(focusTarget, profile.PressDuration).SetEase(Ease.OutBack))
            .SetUpdate(true)
            .OnComplete(() => StartPulse(focusTarget));
    }

    // ── Pulse Loop ──────────────────────────────────────────────────────────

    private void StartPulse(Vector3 focusTarget)
    {
        if (profile == null || !profile.EnableFocusPulse || targetTransform == null) return;

        Vector3 pulseMin = _baseScale * profile.PulseScaleMin;

        _pulseTween = targetTransform
            .DOScale(pulseMin, profile.PulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);

        targetTransform.localScale = focusTarget;
    }

    private void StopPulse()
    {
        if (_pulseTween != null && _pulseTween.IsActive())
        {
            _pulseTween.Kill();
            _pulseTween = null;
        }
    }

    // ── Shimmer Cycle (play-once → pause → repeat) ──────────────────────────

    private void StartShimmerCycle()
    {
        if (shimmerAnimator == null) return;

        StopShimmerCycle();

        SetShimmerOverlayVisible(true);
        shimmerAnimator.SetLoop(false); // Shimmer cycle manages play→pause→repeat; animator must run once then stop
        if (profile != null)
            shimmerAnimator.SetFPS(profile.ShimmerFps);

        _shimmerCoroutine = StartCoroutine(ShimmerCycleCoroutine());
    }

    private void StopShimmerCycle()
    {
        if (_shimmerCoroutine != null)
        {
            StopCoroutine(_shimmerCoroutine);
            _shimmerCoroutine = null;
        }

        if (shimmerAnimator != null)
        {
            shimmerAnimator.Stop();
            SetShimmerOverlayVisible(false);
        }
    }

    private void SetShimmerOverlayVisible(bool visible)
    {
        if (shimmerAnimator == null || shimmerAnimator.gameObject == null)
            return;

        shimmerAnimator.gameObject.SetActive(visible);
        Image shimmerImage = shimmerAnimator.GetComponent<Image>();
        if (shimmerImage != null)
            shimmerImage.raycastTarget = false;
    }

    private IEnumerator ShimmerCycleCoroutine()
    {
        float pause = (profile != null) ? profile.ShimmerPauseDuration : 0.6f;

        while (true)
        {
            shimmerAnimator.Play();

            while (shimmerAnimator.IsPlaying)
                yield return null;

            if (pause > 0f)
                yield return new WaitForSecondsRealtime(pause);
        }
    }

    // ── Cleanup ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        StopShimmerCycle();
        if (targetTransform != null)
            targetTransform.DOKill();
    }
}
