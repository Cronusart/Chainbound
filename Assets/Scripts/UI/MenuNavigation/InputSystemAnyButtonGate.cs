using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Small reusable one-shot gate for "press any button" handoffs. It listens
    /// at the Input System device level, so keyboard, mouse/touch, gamepad,
    /// PSG1, and virtual on-screen button presses share the same path.
    /// </summary>
    public sealed class InputSystemAnyButtonGate : IDisposable
    {
        private IDisposable subscription;
        private Action callback;

        public bool IsArmed { get; private set; }

        public void Arm(Action onPressed)
        {
            if (onPressed == null)
                throw new ArgumentNullException(nameof(onPressed));

            Disarm();
            callback = onPressed;
            IsArmed = true;
            subscription = InputSystem.onAnyButtonPress.CallOnce(_ => Signal());
        }

        public void Disarm()
        {
            subscription?.Dispose();
            subscription = null;
            callback = null;
            IsArmed = false;
        }

        public void Dispose()
        {
            Disarm();
        }

        private void Signal()
        {
            if (!IsArmed)
                return;

            Action pressed = callback;
            subscription = null; // CallOnce disposes its own subscription.
            callback = null;
            IsArmed = false;
            pressed?.Invoke();
        }
    }
}
