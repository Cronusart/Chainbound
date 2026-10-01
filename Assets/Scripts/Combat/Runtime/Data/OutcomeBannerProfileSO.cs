using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// ScriptableObject holding pixel art sprite frames for the
    /// victory / defeat / draw / capture outcome banner animation.
    /// The post-battle uGUI overlay plays these when frames are authored.
    /// </summary>
    [CreateAssetMenu(menuName = "Chainbound/Combat/Presentation/Outcome Banner Profile", order = 60)]
    public class OutcomeBannerProfileSO : ScriptableObject
    {
        [Header("Title Sprite Frames")]
        [Tooltip("Hand-drawn VICTORY title frames. Replaces the TMP label when assigned.")]
        public Sprite[] victoryFrames;

        [Tooltip("Hand-drawn DEFEAT title frames. Replaces the TMP label when assigned.")]
        public Sprite[] defeatFrames;

        [Tooltip("Hand-drawn DRAW title frames. Replaces the TMP label when assigned.")]
        public Sprite[] drawFrames;

        [Tooltip("Hand-drawn CAPTURE title frames. Empty arrays fall back to victory frames, then TMP.")]
        public Sprite[] captureFrames;

        [Header("Strip Motion")]
        [Tooltip("Height of the banner while it slides in, as a fraction of its rest height. 0.028 reads as a thin sliver.")]
        [Range(0.02f, 0.25f)]
        public float stripScaleY = 0.028f;

        [Header("Timing")]
        [Tooltip("Frames per second for the sprite animation.")]
        [Range(1f, 30f)]
        public float fps = 6f;

        [Tooltip("Seconds to pause between animation loops.")]
        [Range(0f, 10f)]
        public float loopIntervalSeconds = 4f;

        [Tooltip("Seconds before the player is allowed to dismiss the popup.")]
        [Range(0f, 10f)]
        public float initialLockoutSeconds = 3f;

        /// <summary>
        /// Returns the correct sprite array for the given outcome.
        /// Empty arrays mean the banner is TMP-only.
        /// </summary>
        public Sprite[] GetFrames(int winnerTeamIndex)
        {
            return GetFrames(BattleOutcomePresentation.Resolve(new BattleResult { winnerTeamIndex = winnerTeamIndex }));
        }

        public Sprite[] GetFrames(BattleOutcomePresentation presentation)
        {
            switch (presentation.Kind)
            {
                case BattleOutcomeKind.Victory:
                    return victoryFrames;
                case BattleOutcomeKind.Capture:
                    return HasFrames(captureFrames) ? captureFrames : victoryFrames;
                case BattleOutcomeKind.Draw:
                    return drawFrames;
                case BattleOutcomeKind.Defeat:
                case BattleOutcomeKind.Withdrawn:
                    return defeatFrames;
                default:
                    return null;
            }
        }

        public static bool HasFrames(Sprite[] frames)
        {
            return frames != null && frames.Length > 0 && frames[0] != null;
        }

        /// <summary>
        /// Duration of a single playthrough of the given animation, in seconds.
        /// Returns 0 if no frames are present.
        /// </summary>
        public float GetAnimationDuration(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
                return 0f;
            return frames.Length / Mathf.Max(fps, 0.01f);
        }
    }
}
