using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Owns global focus and interaction gating for menu objects.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuObjectNavigator : MonoBehaviour
{
    [SerializeField] private List<MainMenuObjectHotspot> hotspots = new List<MainMenuObjectHotspot>();
    [SerializeField] private MainMenuObjectHotspot defaultHotspot;

    [Header("Availability Outline Pulse")]
    [Tooltip("Tint applied to outlines that are available but not focused.")]
    [SerializeField] private Color availabilityOutlineTint = new Color32(155, 171, 178, 255); // 9BAB B2
    [Tooltip("Tint applied to the focused outline.")]
    [SerializeField] private Color selectedOutlineTint = new Color32(251, 255, 134, 255); // FBFF86
    [Tooltip("Lowest opacity reached by nonfocused outlines during the pulse.")]
    [SerializeField, Range(0f, 1f)] private float availabilityMinAlpha = 0.35f;
    [Tooltip("Highest opacity reached by nonfocused outlines during the pulse.")]
    [SerializeField, Range(0f, 1f)] private float availabilityMaxAlpha = 0.7f;
    [Tooltip("Full fade-up/fade-down cycle duration for nonselected outlines.")]
    [SerializeField, Min(0.1f)] private float availabilityPulseSeconds = 2.4f;
    [Tooltip("Default fade duration when dismissing outlines before New Game / Continue routing.")]
    [SerializeField, Min(0f)] private float defaultOutlineFadeOutSeconds = 0.35f;

    private MainMenuObjectHotspot selectedHotspot;
    private bool hotspotsInteractable;
    private bool outlineVisualsEnabled;

    public IReadOnlyList<MainMenuObjectHotspot> Hotspots => hotspots;
    public MainMenuObjectHotspot DefaultHotspot => defaultHotspot;
    public MainMenuObjectHotspot SelectedHotspot => selectedHotspot;
    public float DefaultOutlineFadeOutSeconds => defaultOutlineFadeOutSeconds;

    /// <summary>Controls whether availability outlines can render at all.</summary>
    public void SetOutlineVisualsEnabled(bool enabled)
    {
        outlineVisualsEnabled = enabled;
        ApplyOutlineStates();
    }

    /// <summary>
    /// Fades every object outline to transparent, then disables them. Used before
    /// the intro cinematic so selection chrome does not linger into the photo snap.
    /// </summary>
    public IEnumerator FadeOutOutlines(float durationSeconds = -1f)
    {
        float duration = durationSeconds >= 0f ? durationSeconds : defaultOutlineFadeOutSeconds;
        outlineVisualsEnabled = false;
        StopOutlinePulses();

        for (int i = 0; i < hotspots.Count; i++)
        {
            MainMenuObjectHotspot hotspot = hotspots[i];
            Image outline = hotspot != null ? hotspot.OutlineImage : null;
            if (outline == null)
                continue;

            outline.DOKill(false);
            outline.enabled = true;

            if (duration <= 0.001f)
            {
                Color c = outline.color;
                c.a = 0f;
                outline.color = c;
                outline.enabled = false;
                continue;
            }

            outline.DOFade(0f, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        if (duration > 0.001f)
            yield return new WaitForSecondsRealtime(duration);

        for (int i = 0; i < hotspots.Count; i++)
        {
            Image outline = hotspots[i] != null ? hotspots[i].OutlineImage : null;
            if (outline == null)
                continue;

            outline.DOKill(false);
            outline.enabled = false;
        }
    }

    private void Awake()
    {
        RegisterHotspots();
        SetHotspotsInteractable(false);
    }

    private void OnDestroy()
    {
        StopOutlinePulses();
        for (int i = 0; i < hotspots.Count; i++)
        {
            if (hotspots[i] != null)
                hotspots[i].SelectionChanged -= HandleSelectionChanged;
        }
    }

    public void SetHotspotsInteractable(bool interactable)
    {
        hotspotsInteractable = interactable;

        for (int i = 0; i < hotspots.Count; i++)
        {
            MainMenuObjectHotspot hotspot = hotspots[i];
            if (hotspot == null)
                continue;

            hotspot.SetInteractionEnabled(interactable);
        }

        if (!interactable)
        {
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.GetComponent<MainMenuObjectHotspot>() != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            selectedHotspot = null;
        }

        ApplyOutlineStates();
    }

    public void SelectDefault()
    {
        Select(defaultHotspot);
    }

    public void Select(MainMenuObjectHotspot hotspot)
    {
        if (!hotspotsInteractable || hotspot == null || !hotspot.IsActive() || !hotspot.IsInteractable())
            return;

        if (EventSystem.current == null)
            return;

        // Setting the new target directly lets Unity issue one normal
        // deselect/select transition. Clearing first causes the current
        // hotspot's OnDeselect to restore its base scale for a frame, which
        // is visible as a pulse snap back to 1.0.
        EventSystem.current.SetSelectedGameObject(hotspot.gameObject);
    }

    private void RegisterHotspots()
    {
        for (int i = 0; i < hotspots.Count; i++)
        {
            MainMenuObjectHotspot hotspot = hotspots[i];
            if (hotspot == null)
                continue;

            hotspot.SelectionChanged -= HandleSelectionChanged;
            hotspot.SelectionChanged += HandleSelectionChanged;
        }
    }

    private void HandleSelectionChanged(MainMenuObjectHotspot hotspot, bool isSelected)
    {
        if (!hotspotsInteractable)
            return;

        if (isSelected)
            selectedHotspot = hotspot;
        else if (selectedHotspot == hotspot)
            selectedHotspot = null;

        ApplyOutlineStates();
    }

    private void ApplyOutlineStates()
    {
        float minAlpha = Mathf.Clamp01(availabilityMinAlpha);
        float maxAlpha = Mathf.Max(minAlpha, Mathf.Clamp01(availabilityMaxAlpha));
        float halfCycle = Mathf.Max(0.01f, availabilityPulseSeconds * 0.5f);

        for (int i = 0; i < hotspots.Count; i++)
        {
            MainMenuObjectHotspot hotspot = hotspots[i];
            if (hotspot == null || hotspot.OutlineImage == null)
                continue;

            Image outline = hotspot.OutlineImage;
            outline.DOKill(false);

            if (!outlineVisualsEnabled)
            {
                outline.enabled = false;
                continue;
            }

            if (hotspotsInteractable && hotspot == selectedHotspot)
            {
                hotspot.SetOutlineAppearance(selectedOutlineTint, 1f);
                continue;
            }

            hotspot.SetOutlineAppearance(availabilityOutlineTint, minAlpha);
            if (!hotspotsInteractable)
                continue;

            outline.DOFade(maxAlpha, halfCycle)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    private void StopOutlinePulses()
    {
        for (int i = 0; i < hotspots.Count; i++)
        {
            Image outline = hotspots[i] != null ? hotspots[i].OutlineImage : null;
            outline?.DOKill(false);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        availabilityMinAlpha = Mathf.Clamp01(availabilityMinAlpha);
        availabilityMaxAlpha = Mathf.Clamp(availabilityMaxAlpha, availabilityMinAlpha, 1f);
        availabilityPulseSeconds = Mathf.Max(0.1f, availabilityPulseSeconds);
        defaultOutlineFadeOutSeconds = Mathf.Max(0f, defaultOutlineFadeOutSeconds);

        if (Application.isPlaying)
            ApplyOutlineStates();
    }
#endif
}
