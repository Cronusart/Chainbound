using System.Collections.Generic;
using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Logical menu intents that may arrive over more than one transport.
    /// </summary>
    public enum MenuInputIntent
    {
        Submit = 0,
        Cancel = 1,
        CloseMenu = 2,
        DetailedView = 3,
        SpecialInteraction = 4,
        Guide = 5,
    }

    /// <summary>
    /// Collapses a single physical press into a single logical menu intent.
    ///
    /// A mobile HUD tap reaches menus over two independent transports: the
    /// <c>GameboyButton</c> C# event (via <see cref="MobileInputManager"/>) and the sibling
    /// <c>OnScreenButton</c>, which writes real device state so the InputSystem <c>UI</c>
    /// map fires too. Both are wanted — the device path is what makes a physical PSG1
    /// gamepad and the on-screen pad behave identically — but together they deliver one
    /// tap twice, one frame apart, producing double-confirms.
    ///
    /// Every handler that acts on an intent claims it here first. The first transport to
    /// arrive wins and the rest of the window is swallowed, so the behaviour is identical
    /// whether the press came from touch, gamepad, or keyboard.
    /// </summary>
    public static class MenuInputIntentGate
    {
        /// <summary>
        /// Duplicate-suppression window. Must exceed the frame gap between the two
        /// transports (the OnScreenButton write is observed on the following input update)
        /// while staying far below a deliberate double tap.
        /// </summary>
        public const float DefaultWindowSeconds = 0.08f;

        private static readonly Dictionary<MenuInputIntent, float> lastConsumedTime =
            new Dictionary<MenuInputIntent, float>();

        /// <summary>
        /// Claims <paramref name="intent"/> for this press. Returns false when an equivalent
        /// intent was already claimed inside the suppression window.
        /// </summary>
        public static bool TryConsume(MenuInputIntent intent, float windowSeconds = DefaultWindowSeconds)
        {
            float now = Time.unscaledTime;

            if (lastConsumedTime.TryGetValue(intent, out float previous)
                && now - previous < Mathf.Max(0f, windowSeconds))
            {
                return false;
            }

            lastConsumedTime[intent] = now;
            return true;
        }

        /// <summary>
        /// Forgets all pending windows. Call when tearing down or hard-switching UI so a
        /// press made immediately after the transition is never mistaken for a duplicate.
        /// </summary>
        public static void Clear()
        {
            lastConsumedTime.Clear();
        }
    }
}
