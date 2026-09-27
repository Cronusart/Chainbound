using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    public enum PlayerMenuSfxCue
    {
        MenuOpen,
        MenuClose,
        ButtonSwitch,
        PanelSwitch,
        Confirm,
        Cancel,
        Invalid,
        CarouselMove,
        ChainlingSelect,
        HealTick,
        HealComplete,
        FavorActivate,
        PartyHealComplete,
        ReviveComplete,
    }

    /// <summary>
    /// Optional menu SFX hook implemented by runtime audio routers in Assembly-CSharp.
    /// </summary>
    public interface IMenuUiSfx
    {
        void PlayConfirm();
    }

    public interface IPlayerMenuSfxPlayer
    {
        void PlayCue(PlayerMenuSfxCue cue, Object overrideSound = null, float volumeScale = 1f);

        /// <summary>
        /// Plays a sound event without the confirm/navigate dedupe window.
        /// Used when a second cue must sit under the first at a reduced volume.
        /// </summary>
        void PlayLayered(Object soundEvent, float volumeScale = 1f);
    }

    public static class PlayerMenuSfxResolver
    {
        private static float suppressButtonSwitchUntil;
        private static float suppressPanelSwitchUntil;
        private static float suppressCancelUntil;
        private static float suppressConfirmUntil;
        private static float playerNavigationIntentUntil;

        public static IPlayerMenuSfxPlayer Resolve(MonoBehaviour explicitBehaviour, ref MonoBehaviour cachedBehaviour)
        {
            if (explicitBehaviour is IPlayerMenuSfxPlayer explicitPlayer)
            {
                cachedBehaviour = explicitBehaviour;
                return explicitPlayer;
            }

            if (cachedBehaviour is IPlayerMenuSfxPlayer cachedPlayer)
                return cachedPlayer;

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerMenuSfxPlayer player)
                {
                    cachedBehaviour = behaviours[i];
                    return player;
                }
            }

            return null;
        }

        public static void SuppressCue(PlayerMenuSfxCue cue, float seconds)
        {
            float until = Time.unscaledTime + Mathf.Max(0f, seconds);
            switch (cue)
            {
                case PlayerMenuSfxCue.ButtonSwitch:
                    suppressButtonSwitchUntil = Mathf.Max(suppressButtonSwitchUntil, until);
                    break;
                case PlayerMenuSfxCue.PanelSwitch:
                    suppressPanelSwitchUntil = Mathf.Max(suppressPanelSwitchUntil, until);
                    break;
                case PlayerMenuSfxCue.Cancel:
                    suppressCancelUntil = Mathf.Max(suppressCancelUntil, until);
                    break;
                case PlayerMenuSfxCue.Confirm:
                    suppressConfirmUntil = Mathf.Max(suppressConfirmUntil, until);
                    break;
            }
        }

        public static void MarkPlayerNavigationIntent(float seconds = 0.18f)
        {
            float until = Time.unscaledTime + Mathf.Max(0f, seconds);
            playerNavigationIntentUntil = Mathf.Max(playerNavigationIntentUntil, until);
        }

        public static bool HasRecentPlayerNavigationIntent()
        {
            return Time.unscaledTime <= playerNavigationIntentUntil;
        }

        public static void SuppressNavigationCues(float seconds)
        {
            SuppressCue(PlayerMenuSfxCue.ButtonSwitch, seconds);
            SuppressCue(PlayerMenuSfxCue.PanelSwitch, seconds);
        }

        public static void SuppressSubmitTransitionCues(float seconds)
        {
            SuppressNavigationCues(seconds);
        }

        public static void SuppressWholeMenuCloseCues(float seconds)
        {
            SuppressNavigationCues(seconds);
            SuppressCue(PlayerMenuSfxCue.Cancel, seconds);
        }

        public static bool IsCueSuppressed(PlayerMenuSfxCue cue)
        {
            if (IsOnScreenKeyboardBlockingMenuSfx())
                return true;

            float now = Time.unscaledTime;
            switch (cue)
            {
                case PlayerMenuSfxCue.ButtonSwitch:
                    return now < suppressButtonSwitchUntil;
                case PlayerMenuSfxCue.PanelSwitch:
                    return now < suppressPanelSwitchUntil;
                case PlayerMenuSfxCue.Cancel:
                    return now < suppressCancelUntil;
                case PlayerMenuSfxCue.Confirm:
                    return now < suppressConfirmUntil;
                default:
                    return false;
            }
        }

        private static bool IsOnScreenKeyboardBlockingMenuSfx()
        {
            OnScreenKeyboardService keyboard = OnScreenKeyboardService.Instance;
            return keyboard != null && keyboard.IsOpen;
        }
    }
}
