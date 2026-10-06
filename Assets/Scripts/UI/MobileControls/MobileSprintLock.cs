namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Double-tap lock for the on-screen sprint button. The press that engages
    /// the lock does not clear it; a later press does.
    /// </summary>
    public sealed class MobileSprintLock
    {
        private float lastDownUnscaled = -1f;

        public float WindowSeconds { get; set; } = 0.32f;

        public bool IsLocked { get; private set; }

        public void RegisterDown(float unscaledTime)
        {
            if (IsLocked)
            {
                IsLocked = false;
                lastDownUnscaled = -1f;
                return;
            }

            if (lastDownUnscaled >= 0f && unscaledTime - lastDownUnscaled <= WindowSeconds)
            {
                IsLocked = true;
                lastDownUnscaled = -1f;
                return;
            }

            lastDownUnscaled = unscaledTime;
        }
    }
}
