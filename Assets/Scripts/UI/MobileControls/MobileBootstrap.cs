using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Applies mobile-wide initialization that should happen exactly once per session:
    /// landscape orientation lock, autorotate policy, target frame rate, sleep timeout.
    ///
    /// Attach to a single boot GameObject (e.g. the <c>GameServices</c> prefab) so this
    /// runs before any scene-scoped UI. Kept separate from <see cref="MobileInputManager"/>
    /// so per-scene managers don't re-run orientation side effects in their Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileBootstrap : MonoBehaviour
    {
        [Header("Orientation")]
        [SerializeField] private bool lockToLandscape = true;
        [Tooltip("When landscape is locked, allow both left and right landscape rotations.")]
        [SerializeField] private bool allowLandscapeRight = true;

        [Header("Performance")]
        [Tooltip("Target frame rate on mobile. 60 recommended for gameplay; -1 uses platform default.")]
        [SerializeField] private int targetFrameRateMobile = 60;
        [Tooltip("Prevent the screen from sleeping during play.")]
        [SerializeField] private bool neverSleepScreen = true;

        private static bool initialized;

        private void Awake()
        {
            if (initialized) return;
            initialized = true;

            if (IsMobile())
            {
                if (lockToLandscape)
                {
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                    Screen.autorotateToPortrait = false;
                    Screen.autorotateToPortraitUpsideDown = false;
                    Screen.autorotateToLandscapeLeft = true;
                    Screen.autorotateToLandscapeRight = allowLandscapeRight;
                    Screen.orientation = ScreenOrientation.AutoRotation;
                }

                if (targetFrameRateMobile > 0)
                {
                    Application.targetFrameRate = targetFrameRateMobile;
                }
            }

            if (neverSleepScreen)
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }
        }

        private static bool IsMobile()
        {
            return Application.isMobilePlatform
                   || Application.platform == RuntimePlatform.Android
                   || Application.platform == RuntimePlatform.IPhonePlayer;
        }
    }
}
