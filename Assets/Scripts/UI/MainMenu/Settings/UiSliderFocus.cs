using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// PSG1-friendly interaction layer for a vertical EQ-style Slider.
/// 
/// Two modes:
///   Navigation mode  — Left/Right moves to adjacent sliders, Up goes to the tab bar.
///   Adjustment mode  — Entered with A/Submit. Up/Down steps the slider value.
///                      Left/Right and Up/Down navigation are blocked.
///                      Exit with B/Cancel (intercepted by SettingsPanelController).
///
/// Visual feedback:
///   Selected        : handle scales up via UiMotionProfileSO.
///   Locked-in       : handle swaps to handleLockedSprite + punch-scale pop.
///   Deselected      : handle scales back to rest, sprite restores.
///
/// Add this component to the same GameObject as the Slider.
/// SettingsPanelController wires selectOnLeft/Right/Up at runtime.
/// </summary>
[RequireComponent(typeof(Slider))]
public class UiSliderFocus : MonoBehaviour,
    ISelectHandler,
    IDeselectHandler,
    ISubmitHandler,
    IMoveHandler
{
    // ── Handle visuals ──────────────────────────────────────────────────────

    [Header("Handle Visuals")]
    [Tooltip("RectTransform of the slider handle. Drives scale animations.")]
    [SerializeField] private RectTransform handleRect;
    [Tooltip("Image on the handle. Swapped between normal and locked states.")]
    [SerializeField] private Image handleImage;
    [Tooltip("Sprite shown when the slider is idle / navigating.")]
    [SerializeField] private Sprite handleNormalSprite;
    [Tooltip("Sprite shown when the slider is locked-in for adjustment (e.g. orange).")]
    [SerializeField] private Sprite handleLockedSprite;

    // ── Motion & SFX ────────────────────────────────────────────────────────

    [Header("Motion & SFX")]
    [SerializeField] private UiMotionProfileSO profile;
    [SerializeField] private UiSfxRouter sfxRouter;

    // ── Adjustment ──────────────────────────────────────────────────────────

    [Header("Adjustment")]
    [Tooltip("How much the slider value changes per Up/Down press in adjustment mode.")]
    [SerializeField, Range(0.01f, 0.25f)] private float adjustStep = 0.05f;

    // ── Explicit navigation targets (set by SettingsPanelController) ────────

    [HideInInspector] public Selectable selectOnLeft;
    [HideInInspector] public Selectable selectOnRight;
    [HideInInspector] public Selectable selectOnUp;

    // ── Public state ────────────────────────────────────────────────────────

    public bool IsLocked => _isLocked;

    // ── Internal ────────────────────────────────────────────────────────────

    private Slider   _slider;
    private Vector3  _handleRestScale;
    private bool     _isLocked;
    private bool     _isSelected;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _handleRestScale = handleRect != null ? handleRect.localScale : Vector3.one;

        // Disable Unity's built-in Selectable navigation entirely.
        // All movement is handled manually inside OnMove.
        var nav = _slider.navigation;
        nav.mode = Navigation.Mode.None;
        _slider.navigation = nav;
    }

    private void OnDestroy() => handleRect?.DOKill();

    // ── ISelectHandler ──────────────────────────────────────────────────────

    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
        sfxRouter?.PlayNavigate(gameObject);
        AnimateHandle(selected: true);
    }

    // ── IDeselectHandler ────────────────────────────────────────────────────

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        if (_isLocked) UnlockInternal();
        AnimateHandle(selected: false);
    }

    // ── ISubmitHandler ──────────────────────────────────────────────────────

    public void OnSubmit(BaseEventData eventData)
    {
        if (_isLocked)
            UnlockInternal();
        else
            LockInternal();
    }

    // ── IMoveHandler ────────────────────────────────────────────────────────

    public void OnMove(AxisEventData eventData)
    {
        if (_isLocked)
        {
            // Adjustment mode — Up/Down step the normalizedValue (0–1) so the step
            // is always a consistent percentage regardless of slider Min/Max range.
            if (eventData.moveDir == MoveDirection.Up)
            {
                _slider.normalizedValue = Mathf.Clamp01(_slider.normalizedValue + adjustStep);
            }
            else if (eventData.moveDir == MoveDirection.Down)
            {
                _slider.normalizedValue = Mathf.Clamp01(_slider.normalizedValue - adjustStep);
            }
            // All directions blocked while locked — caller handles B/Cancel to unlock.
            return;
        }

        // Navigation mode — route to explicit neighbors.
        switch (eventData.moveDir)
        {
            case MoveDirection.Left:
                MoveTo(selectOnLeft);
                break;
            case MoveDirection.Right:
                MoveTo(selectOnRight);
                break;
            case MoveDirection.Up:
                MoveTo(selectOnUp);
                break;
            // Down is intentionally ignored — sliders sit at the bottom of the layout.
        }
    }

    // ── Public API (called by SettingsPanelController) ───────────────────────

    /// <summary>
    /// Exits adjustment mode without playing SFX — caller is responsible for the
    /// cancel sound so it only plays once per Cancel input.
    /// </summary>
    public void ForceUnlock()
    {
        if (_isLocked) UnlockInternal(playSfx: false);
    }

    // ── Lock / Unlock ────────────────────────────────────────────────────────

    private void LockInternal()
    {
        _isLocked = true;
        sfxRouter?.PlayConfirm();

        if (handleImage != null && handleLockedSprite != null)
            handleImage.sprite = handleLockedSprite;

        // Punch-scale the handle to signal "locked in"
        if (handleRect != null && profile != null)
        {
            handleRect.DOKill();
            handleRect
                .DOPunchScale(Vector3.one * 0.25f, 0.15f, 2, 0.4f)
                .SetUpdate(true);
        }
    }

    private void UnlockInternal(bool playSfx = true)
    {
        _isLocked = false;

        if (handleImage != null && handleNormalSprite != null)
            handleImage.sprite = handleNormalSprite;

        // Restore focus scale (stay scaled up since we're still selected)
        AnimateHandle(selected: _isSelected);
    }

    // ── Animations ──────────────────────────────────────────────────────────

    private void AnimateHandle(bool selected)
    {
        if (handleRect == null || profile == null) return;

        handleRect.DOKill();
        float targetScale = selected ? profile.FocusScale : 1f;
        float duration    = selected ? profile.FocusInDuration : profile.FocusOutDuration;

        handleRect
            .DOScale(_handleRestScale * targetScale, duration)
            .SetEase(profile.FocusEase)
            .SetUpdate(true);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void MoveTo(Selectable target)
    {
        if (target != null)
            EventSystem.current?.SetSelectedGameObject(target.gameObject);
    }
}
