using UnityEngine;
using UnityEngine.Rendering;

namespace Chainbound.Combat
{
    /// <summary>
    /// Authored combat post-FX cue. Playback only weight-blends a combat-owned Volume;
    /// it never mutates this profile asset or the TimeOfDay / GameServices Global Volume.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CombatPostFxCue_",
        menuName = "Chainbound/Combat/Presentation/PostFX/Post FX Cue")]
    public sealed class CombatPostFxCueSO : ScriptableObject
    {
        [Tooltip("URP VolumeProfile with the overrides for this cue (bloom, vignette, chromatic, etc.).")]
        public VolumeProfile profile;

        [Range(0f, 1f)]
        [Tooltip("Peak blend weight applied to the combat-owned Volume.")]
        public float peakWeight = 1f;

        [Min(0f)]
        public float fadeInSeconds = 0.05f;

        [Tooltip("Seconds to hold at peak. Use -1 to hold until Release / replaced / ForceClear.")]
        public float holdSeconds = 0.15f;

        [Min(0f)]
        public float fadeOutSeconds = 0.1f;

        public bool IsPlayable => profile != null;

        public float ResolvePeakWeight() => Mathf.Clamp01(peakWeight);

        private void OnValidate()
        {
            peakWeight = Mathf.Clamp01(peakWeight);
            fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
            fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
            if (holdSeconds < -1f)
                holdSeconds = -1f;
        }
    }
}
