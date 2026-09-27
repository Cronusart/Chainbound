using System.Collections.Generic;
using DG.Tweening;
using SignalJam.UI.VisualFx;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "BattleIntroVfxProfile", menuName = "Chainbound/Combat/Presentation/Battle Intro/VFX Profile")]
    public class BattleIntroVfxProfileSO : ScriptableObject
    {
        [Header("Palette")]
        public Color panelTint = new Color(0.07f, 0.10f, 0.15f, 0.96f);
        public Color accentColor = new Color(1f, 0.78f, 0.22f, 1f);
        public Color textColor = new Color(1f, 0.97f, 0.86f, 1f);
        public Color textOutlineColor = new Color(0f, 0f, 0f, 0.85f);
        [Range(0f, 1f)] public float textOutlineWidth = 0.16f;

        [Header("Motion")]
        [Min(0f)] public float anticipationDuration = 0.06f;
        [Range(0.5f, 1.2f)] public float anticipationScale = 0.96f;
        [Min(0f)] public float entryDuration = 0.22f;
        public Ease entryEase = Ease.OutCubic;
        public Vector2 entryOffset = new Vector2(960f, 0f);
        [Range(0.02f, 0.25f)] public float stripScaleY = 0.07f;
        [Range(1f, 1.35f)] public float overshootScale = 1.07f;
        [Min(0f)] public float settleDuration = 0.1f;
        public Ease settleEase = Ease.OutQuad;
        [Min(0f)] public float holdDuration = 0.95f;
        [Min(0f)] public float exitDuration = 0.18f;
        public Ease exitEase = Ease.InCubic;
        public Vector2 exitOffset = new Vector2(0f, 24f);

        [Header("Banner Glint")]
        [Tooltip("Optional UI Image used for framed banner glints (e.g., Intro_CardSweep). Leave unset to use BattleIntroController defaults.")]
        public Image bannerGlintImageTarget;
        [Tooltip("Optional UIImageFrameAnimator for banner glints. Leave unset and assign Image only to auto-use GetComponent.")]
        public MonoBehaviour bannerGlintFrameAnimatorTarget;
        [Tooltip("Optional frame override for the assigned UIImageFrameAnimator. Leave empty to use the animator's Inspector frames.")]
        public List<Sprite> bannerGlintFrames = new List<Sprite>();
        [Range(1f, 60f)] public float bannerGlintFps = 14f;
        [Tooltip("Minimum display time for a framed sprite glint. Automatically extended based on FPS + sprite count.")]
        [Min(0f)] public float bannerGlintDuration = 0.36f;
        [Tooltip("Extra hold after the sprite sequence so the final frame stays visible briefly before fading out.")]
        [Min(0f)] public float bannerGlintFrameTailSeconds = 0.05f;
        [Tooltip("Used only by the procedural Image sweep fallback when no banner frame animator is assigned.")]
        [Min(1f)] public float bannerGlintWidth = 96f;
        [Tooltip("Alpha controls opacity for frame-animated glints. Full color is used by the procedural fallback.")]
        public Color bannerGlintColor = new Color(1f, 0.95f, 0.55f, 0.55f);

        [Header("TMP Text Emphasis")]
        [Min(0f)] public float textPunchDuration = 0.24f;
        [Range(0f, 0.6f)] public float textPunchScale = 0.18f;
        [Min(0f)] public float textPunchStagger = 0.012f;
        public bool enableTextGlint = true;
        [Min(0f)] public float textGlintDelay = 0.04f;
        [Min(0f)] public float textGlintDuration = 0.34f;
        [Min(1f)] public float textGlintWidth = 120f;
        public Color textGlintColor = new Color(1f, 1f, 1f, 0.74f);

        [Header("Pixel Burst")]
        public bool enablePixelBurst = true;
        [Tooltip("Optional paired-edge sweep preset. Leave empty to fall back on legacy single-frame Emit tuning (lifetime/speed/count only).")]
        public UiSweepParticlePresetSO pixelSweepPreset;
        [Tooltip("If greater than zero, overrides sweep duration from pixelSweepPreset for this profile only (emitter travel + emission scatter timing).")]
        [Min(0f)] public float pixelSweepDurationOverrideSeconds;
        public Color pixelBurstColor = new Color(1f, 0.78f, 0.22f, 1f);
        [Range(0, 2056)] public int pixelBurstCount = 12;
        [Tooltip("Legacy single-frame burst: lifetime in seconds. With pixelSweepPreset, this only scales procedural preset lifetime when Preserve Authoring Modules is off.")]
        [Min(0.01f)] public float pixelBurstLifetime = 0.28f;
        [Tooltip("Legacy single-frame burst: start speed. With pixelSweepPreset, this only scales procedural preset speed when Preserve Authoring Modules is off.")]
        [Min(0.01f)] public float pixelBurstSpeed = 70f;

        [Header("Screen Pulse")]
        public bool enableScreenPulse;
        public Color screenPulseColor = new Color(1f, 1f, 1f, 0.08f);
        [Min(0f)] public float screenPulseDuration = 0.2f;

        public static BattleIntroVfxProfileSO CreateRuntimeFallback()
        {
            var profile = CreateInstance<BattleIntroVfxProfileSO>();
            profile.name = "Runtime Standard Battle Intro VFX Profile";
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }

        private void OnValidate()
        {
            anticipationDuration = Mathf.Max(0f, anticipationDuration);
            anticipationScale = Mathf.Max(0.01f, anticipationScale);
            entryDuration = Mathf.Max(0f, entryDuration);
            stripScaleY = Mathf.Clamp(stripScaleY, 0.02f, 0.25f);
            overshootScale = Mathf.Max(1f, overshootScale);
            settleDuration = Mathf.Max(0f, settleDuration);
            holdDuration = Mathf.Max(0f, holdDuration);
            exitDuration = Mathf.Max(0f, exitDuration);
            bannerGlintFps = Mathf.Max(1f, bannerGlintFps);
            bannerGlintDuration = Mathf.Max(0f, bannerGlintDuration);
            bannerGlintWidth = Mathf.Max(1f, bannerGlintWidth);
            textPunchDuration = Mathf.Max(0f, textPunchDuration);
            textPunchScale = Mathf.Max(0f, textPunchScale);
            textPunchStagger = Mathf.Max(0f, textPunchStagger);
            textGlintDelay = Mathf.Max(0f, textGlintDelay);
            textGlintDuration = Mathf.Max(0f, textGlintDuration);
            textGlintWidth = Mathf.Max(1f, textGlintWidth);
            pixelBurstCount = Mathf.Clamp(pixelBurstCount, 0, 2056);
            pixelBurstLifetime = Mathf.Max(0.01f, pixelBurstLifetime);
            pixelBurstSpeed = Mathf.Max(0.01f, pixelBurstSpeed);
            pixelSweepDurationOverrideSeconds = Mathf.Max(0f, pixelSweepDurationOverrideSeconds);
            screenPulseDuration = Mathf.Max(0f, screenPulseDuration);
            bannerGlintFrameTailSeconds = Mathf.Max(0f, bannerGlintFrameTailSeconds);
        }
    }
}
