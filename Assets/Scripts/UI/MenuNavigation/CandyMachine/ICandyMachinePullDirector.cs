using System;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>Assembly-safe bridge from the persistent menu presenter to a scene-local machine.</summary>
    public interface ICandyMachinePullDirector
    {
        bool IsPlaying { get; }
        void BeginPull(Action revealReady);
        void RestoreAfterReveal();
        void CancelAndRestore();
    }
}
