namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Screens that already play their own button sounds hold this gate while they
    /// own input. <see cref="MobileHudButtonAudioBridge"/> stays silent for that
    /// time so one press does not also play the HUD confirm, cancel, or release click.
    /// Screens without their own cues leave the gate clear, and the HUD sounds remain.
    /// </summary>
    public static class HudButtonAudioGate
    {
        private static int suppressCount;

        public static bool IsSuppressed => suppressCount > 0;

        public static void Suppress()
        {
            suppressCount++;
        }

        public static void Release()
        {
            if (suppressCount > 0)
                suppressCount--;
        }
    }
}
