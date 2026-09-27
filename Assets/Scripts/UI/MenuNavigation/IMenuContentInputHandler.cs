using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    public interface IMenuContentInputHandler
    {
        bool TryHandleCancel();
        bool TryHandleMove(Vector2 move);
        bool TryHandleSubmit();
    }

    /// <summary>
    /// While a long confirm sequence owns the menu, blocked presses stay silent.
    /// </summary>
    public interface IMenuBlockedInputSilence
    {
        bool MuteBlockedInput { get; }
    }

    public interface IMenuAdvancedInputHandler
    {
        bool TryHandleDetailedView();
        bool TryHandleCloseMenu();
        bool TryHandleSpecialInteraction();
        bool TryHandleGuide();
    }
}
