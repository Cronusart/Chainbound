using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Vertical EQ-style slider that delegates ALL keyboard/gamepad input to UiSliderFocus.
///
/// Unity's built-in Slider.OnMove intercepts Up/Down for "Bottom To Top" sliders
/// and changes the value before our custom component can respond. This subclass
/// overrides OnMove to do nothing, giving UiSliderFocus complete control.
///
/// Mouse drag, pointer down, and all other interaction still work normally.
///
/// Setup: Replace the Slider component on each settings slider GameObject with
/// this component. Re-assign the inspector fields on SettingsPanelController.
/// </summary>
public class UiVerticalSlider : Slider
{
    public override void OnMove(AxisEventData eventData)
    {
        // Intentionally empty.
        // UiSliderFocus.OnMove handles all navigation and value adjustment.
        // Pointer/drag interaction (OnPointerDown, OnDrag) is unaffected.
    }
}
