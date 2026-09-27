using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Settings panel: tabbed Audio volume controls + static PSG1 key-binding info.
///
/// Navigation layout:
///   [AUDIO TAB] ←L/R→ [CONTROLS TAB]
///         ↓ (down from either tab)
///   [MASTER] ←L/R→ [MUSIC] ←L/R→ [SFX] ←L/R→ [UI] ←L/R→ [AMBIENCE]
///         ↑ (up from any slider = tab bar)
///
/// Slider interaction (handled by UiSliderFocus on each slider):
///   • Select slider   → handle scales up (focus effect)
///   • Press A/Submit  → locked-in: Up/Down now steps the value, handle swaps sprite
///   • Press B/Cancel  → if locked: unlock (first press); if not locked: close panel
///
/// Volume state persisted to PlayerPrefs on ClosePanel().
/// All DOTween calls use SetUpdate(true) for timeScale = 0 safety.
/// </summary>
public class SettingsPanelController : MonoBehaviour
{
    // ── Panel root ──────────────────────────────────────────────────────────

    [Header("Panel Root")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private RectTransform panelRect;
    [SerializeField, Tooltip("Y offset used for the slide-in / slide-out animation.")]
    private float slideOffsetY = 64f;
    [SerializeField] private float slideInDuration = 0.22f;

    // ── Tabs ────────────────────────────────────────────────────────────────

    [Header("Tabs")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button controlsTabButton;
    [SerializeField] private Image audioTabImage;
    [SerializeField] private Image controlsTabImage;
    [SerializeField] private Sprite tabSelectedSprite;
    [SerializeField] private Sprite tabDeselectedSprite;

    [Header("Tab Panels")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private Toggle uiHapticsToggle;

    // ── Audio mixer ─────────────────────────────────────────────────────────

    [Header("Audio Mixer")]
    [Tooltip("Assign the same MainAudioMixer used by AudioService.")]
    [SerializeField] private AudioMixer audioMixer;

    // ── Sliders ─────────────────────────────────────────────────────────────

    [Header("Volume Sliders (left → right order)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider uiSlider;
    [SerializeField] private Slider ambienceSlider;

    [Header("Slider Value Labels")]
    [SerializeField] private TMP_Text masterValueLabel;
    [SerializeField] private TMP_Text musicValueLabel;
    [SerializeField] private TMP_Text sfxValueLabel;
    [SerializeField] private TMP_Text uiValueLabel;
    [SerializeField] private TMP_Text ambienceValueLabel;

    // ── Internal constants ──────────────────────────────────────────────────

    private static readonly string[] MixerParams = { "MasterVolDb", "MusicVolDb", "SFXVolDb", "UIVolDb", "AmbVolDb" };
    private static readonly string[] PrefKeys    = { "vol_master",  "vol_music",  "vol_sfx",  "vol_ui",  "vol_amb" };

    private const float MinDb = -40f;
    private const float MaxDb =   0f;

    // ── Internal state ───────────────────────────────────────────────────────

    private Slider[]        _sliders;
    private TMP_Text[]      _valueLabels;
    private UiSliderFocus[] _sliderFocuses;
    private Action          _onClosedCallback;
    private bool            _isOpen;
    private bool            _suppressUiHapticsToggleCallbacks;
    private Vector2         _panelRestPos;

    public bool IsOpen => _isOpen;
    public Transform NavigationRoot => panelRect != null ? panelRect : transform;
    public Selectable DefaultNavigationSelection
    {
        get
        {
            if (_sliders != null)
            {
                for (int i = 0; i < _sliders.Length; i++)
                {
                    if (_sliders[i] != null && _sliders[i].gameObject.activeInHierarchy)
                        return _sliders[i];
                }
            }

            return audioTabButton;
        }
    }

    public IReadOnlyList<Selectable> GetNavigationSelectables()
    {
        var result = new List<Selectable>();
        AddIfUsable(result, audioTabButton);
        AddIfUsable(result, controlsTabButton);

        if (_sliders != null)
        {
            for (int i = 0; i < _sliders.Length; i++)
                AddIfUsable(result, _sliders[i]);
        }

        AddIfUsable(result, uiHapticsToggle);
        return result;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _sliders     = new[] { masterSlider, musicSlider, sfxSlider, uiSlider, ambienceSlider };
        _valueLabels = new[] { masterValueLabel, musicValueLabel, sfxValueLabel, uiValueLabel, ambienceValueLabel };

        if (panelRect != null)
            _panelRestPos = panelRect.anchoredPosition;

        // Start hidden
        if (panelGroup != null)
        {
            panelGroup.alpha          = 0f;
            panelGroup.interactable   = false;
            panelGroup.blocksRaycasts = false;
        }

        SetupSliderListeners();
        LoadVolumes();
        BindUiHapticsToggle();
        GatherSliderFocuses();
        ConfigureNavigation();

        if (audioTabButton    != null) audioTabButton.onClick.AddListener(() => ActivateTab(true));
        if (controlsTabButton != null) controlsTabButton.onClick.AddListener(() => ActivateTab(false));

        ActivateTab(audioActive: true, focusFirst: false);
    }

    private void OnDestroy()
    {
        panelGroup?.DOKill();
        panelRect?.DOKill();

        if (uiHapticsToggle != null)
            uiHapticsToggle.onValueChanged.RemoveListener(OnUiHapticsToggleChanged);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Slide the panel in. <paramref name="onClosed"/> fires when the panel fully closes.
    /// </summary>
    public void OpenPanel(Action onClosed = null)
    {
        _onClosedCallback = onClosed;
        _isOpen = true;

        RefreshUiHapticsToggle();
        ActivateTab(audioActive: true, focusFirst: false);

        if (panelGroup == null) return;

        panelGroup.DOKill();
        panelGroup.interactable   = true;
        panelGroup.blocksRaycasts = true;

        if (panelRect != null)
        {
            panelRect.DOKill();
            panelRect.anchoredPosition = _panelRestPos + Vector2.down * slideOffsetY;
            panelRect.DOAnchorPos(_panelRestPos, slideInDuration)
                     .SetEase(Ease.OutCubic)
                     .SetUpdate(true);
        }

        panelGroup.DOFade(1f, slideInDuration * 0.7f)
                  .SetUpdate(true)
                  .OnComplete(FocusFirstSlider);
    }

    /// <summary>
    /// B / Cancel pressed while settings is open.
    /// First call: exits slider adjustment mode if any slider is locked-in.
    /// Second call (or no slider was locked): slides the panel out.
    /// </summary>
    public void ClosePanel()
    {
        // If a slider is currently locked-in, unlock it instead of closing.
        for (int i = 0; i < _sliderFocuses.Length; i++)
        {
            if (_sliderFocuses[i] != null && _sliderFocuses[i].IsLocked)
            {
                _sliderFocuses[i].ForceUnlock();
                return; // first B press consumed — panel stays open
            }
        }

        if (!_isOpen) return;
        _isOpen = false;

        // Deactivate the controls panel immediately so its image doesn't linger
        // during or after the fade-out animation. Reset to audio tab state.
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (audioPanel    != null) audioPanel.SetActive(true);

        if (audioTabImage    != null) audioTabImage.sprite    = tabSelectedSprite;
        if (controlsTabImage != null) controlsTabImage.sprite = tabDeselectedSprite;

        SaveVolumes();

        if (panelGroup == null)
        {
            _onClosedCallback?.Invoke();
            _onClosedCallback = null;
            return;
        }

        panelGroup.DOKill();
        panelGroup.DOFade(0f, slideInDuration * 0.7f)
                  .SetUpdate(true)
                  .OnComplete(() =>
                  {
                      panelGroup.interactable   = false;
                      panelGroup.blocksRaycasts = false;
                      _onClosedCallback?.Invoke();
                      _onClosedCallback = null;
                  });

        if (panelRect != null)
        {
            panelRect.DOKill();
            panelRect.DOAnchorPos(_panelRestPos + Vector2.down * slideOffsetY, slideInDuration)
                     .SetEase(Ease.InCubic)
                     .SetUpdate(true);
        }
    }

    // ── Tabs ─────────────────────────────────────────────────────────────────

    private void ActivateTab(bool audioActive, bool focusFirst = true)
    {
        if (audioPanel    != null) audioPanel.SetActive(audioActive);
        if (controlsPanel != null) controlsPanel.SetActive(!audioActive);

        if (audioTabImage != null)
            audioTabImage.sprite = audioActive ? tabSelectedSprite : tabDeselectedSprite;
        if (controlsTabImage != null)
            controlsTabImage.sprite = audioActive ? tabDeselectedSprite : tabSelectedSprite;

        if (!_isOpen || !focusFirst) return;

        if (audioActive)
            FocusFirstSlider();
        else if (controlsTabButton != null)
            EventSystem.current?.SetSelectedGameObject(controlsTabButton.gameObject);
    }

    private void FocusFirstSlider()
    {
        foreach (var s in _sliders)
        {
            if (s != null)
            {
                EventSystem.current?.SetSelectedGameObject(s.gameObject);
                return;
            }
        }
        // Fallback to audio tab if no sliders are assigned
        if (audioTabButton != null)
            EventSystem.current?.SetSelectedGameObject(audioTabButton.gameObject);
    }

    // ── Navigation wiring ────────────────────────────────────────────────────

    private void GatherSliderFocuses()
    {
        _sliderFocuses = new UiSliderFocus[_sliders.Length];
        for (int i = 0; i < _sliders.Length; i++)
            if (_sliders[i] != null)
                _sliderFocuses[i] = _sliders[i].GetComponent<UiSliderFocus>();
    }

    /// <summary>
    /// Wires explicit navigation for tabs and hands left/right/up targets to
    /// each UiSliderFocus so they can route without polling.
    ///
    /// Layout:
    ///   Tabs row:     AudioTab ←L/R→ ControlsTab
    ///                      ↓ down = first slider
    ///   Sliders row:  [M] ←L/R→ [Mu] ←L/R→ [SFX] ←L/R→ [UI] ←L/R→ [Amb]
    ///                      ↑ up = AudioTab
    /// </summary>
    private void ConfigureNavigation()
    {
        Selectable firstSlider = null;
        foreach (var s in _sliders) { if (s != null) { firstSlider = s; break; } }

        // ── Tab buttons ─────────────────────────────────────────────────────

        if (audioTabButton != null)
        {
            audioTabButton.navigation = new Navigation
            {
                mode          = Navigation.Mode.Explicit,
                selectOnRight = controlsTabButton,
                selectOnDown  = firstSlider
                // No up, no left — top-left corner of the panel
            };
        }

        if (controlsTabButton != null)
        {
            controlsTabButton.navigation = new Navigation
            {
                mode         = Navigation.Mode.Explicit,
                selectOnLeft = audioTabButton,
                selectOnDown = uiHapticsToggle,
                // No up, no right.
            };
        }

        if (uiHapticsToggle != null)
        {
            uiHapticsToggle.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = controlsTabButton,
            };
        }

        // ── UiSliderFocus targets ────────────────────────────────────────────

        for (int i = 0; i < _sliderFocuses.Length; i++)
        {
            if (_sliderFocuses[i] == null) continue;

            _sliderFocuses[i].selectOnLeft  = FindAdjacentSlider(i, -1);
            _sliderFocuses[i].selectOnRight = FindAdjacentSlider(i, +1);
            _sliderFocuses[i].selectOnUp    = audioTabButton;
            // Down is intentionally unset — sliders are the bottom row.
        }
    }

    /// <summary>Finds the nearest non-null Slider in the given direction (+1 right, -1 left).</summary>
    private Selectable FindAdjacentSlider(int fromIndex, int direction)
    {
        for (int i = fromIndex + direction; i >= 0 && i < _sliders.Length; i += direction)
            if (_sliders[i] != null) return _sliders[i];
        return null;
    }

    // ── Volume helpers ────────────────────────────────────────────────────────

    private void SetupSliderListeners()
    {
        for (int i = 0; i < _sliders.Length; i++)
        {
            if (_sliders[i] == null) continue;
            int idx = i;
            _sliders[i].onValueChanged.AddListener(val => ApplySliderValue(idx, val));
        }
    }

    /// <summary>
    /// Called by onValueChanged — receives the raw slider value (which may be 0–100
    /// or any other range). Always reads normalizedValue (0–1) from the slider so
    /// the dB math and % display are correct regardless of the slider's Min/Max.
    /// </summary>
    private void ApplySliderValue(int idx, float _rawValue)
    {
        if (_sliders[idx] == null) return;
        float normalized = _sliders[idx].normalizedValue; // always 0–1
        float db = NormalizedToDb(normalized);
        audioMixer?.SetFloat(MixerParams[idx], db);

        if (_valueLabels[idx] != null)
            _valueLabels[idx].text = Mathf.RoundToInt(normalized * 100f) + "%";
    }

    private void LoadVolumes()
    {
        for (int i = 0; i < _sliders.Length; i++)
        {
            if (_sliders[i] == null) continue;
            // Stored as normalizedValue (0–1). Default 1.0 = 100% / full volume.
            float saved = PlayerPrefs.GetFloat(PrefKeys[i], 1f);
            _sliders[i].normalizedValue = saved; // range-agnostic assignment
            ApplySliderValue(i, _sliders[i].value);
        }
    }

    private void SaveVolumes()
    {
        for (int i = 0; i < _sliders.Length; i++)
        {
            if (_sliders[i] == null) continue;
            PlayerPrefs.SetFloat(PrefKeys[i], _sliders[i].normalizedValue); // save 0–1
        }
        PlayerPrefs.Save();
    }

    /// <summary>Linear 0–1 → dB with hard mute at exactly 0.</summary>
    private static float NormalizedToDb(float t) =>
        Mathf.Approximately(t, 0f) ? -80f : Mathf.Lerp(MinDb, MaxDb, t);
    private void BindUiHapticsToggle()
    {
        if (uiHapticsToggle == null)
            return;

        RefreshUiHapticsToggle();
        uiHapticsToggle.onValueChanged.AddListener(OnUiHapticsToggleChanged);
    }

    private void RefreshUiHapticsToggle()
    {
        if (uiHapticsToggle == null)
            return;

        UiHapticsService hapticsService = UiHapticsService.Instance;
        uiHapticsToggle.interactable = hapticsService != null;

        if (hapticsService == null)
            return;

        _suppressUiHapticsToggleCallbacks = true;
        uiHapticsToggle.isOn = hapticsService.IsUiHapticsEnabled;
        _suppressUiHapticsToggleCallbacks = false;
    }

    private void OnUiHapticsToggleChanged(bool enabled)
    {
        if (_suppressUiHapticsToggleCallbacks)
            return;

        UiHapticsService.Instance?.SetUiHapticsEnabled(enabled);
    }

    private static void AddIfUsable(List<Selectable> result, Selectable selectable)
    {
        if (selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable())
            result.Add(selectable);
    }
}
