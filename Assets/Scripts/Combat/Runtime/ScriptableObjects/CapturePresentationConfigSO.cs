using UnityEngine;
using DG.Tweening;

namespace Chainbound.Combat
{
    /// <summary>Presentation-only capture board. It never participates in authoritative hashes.</summary>
    [CreateAssetMenu(fileName = "CapturePresentationConfig", menuName = "Chainbound/Combat/Capture/Presentation Config")]
    public sealed class CapturePresentationConfigSO : ScriptableObject
    {
        [Header("Artwork")]
        [Tooltip("Optional player-provided trading-grid artwork. A procedural grid is drawn when null.")]
        public Sprite tradingGridArtwork;

        [Header("Colors")]
        public Color traceColor = new Color(0.24f, 1f, 0.72f, 1f);
        public Color bandColor = new Color(0.12f, 0.65f, 1f, 0.25f);
        public Color bandLockedColor = new Color(0.25f, 1f, 0.55f, 0.42f);
        public Color edgeWarningColor = new Color(1f, 0.25f, 0.25f, 0.5f);
        public Color spikeFlashColor = new Color(1f, 0.8f, 0.2f, 0.4f);
        [Tooltip("Dashed capture-band edges. Default #f57d4a.")]
        public Color bandEdgeColor = new Color(0.9607843f, 0.4901961f, 0.2901961f, 1f);
        public Color bullishCandleColor = new Color(0.22f, 0.92f, 0.48f, 1f);
        public Color bearishCandleColor = new Color(0.95f, 0.32f, 0.48f, 1f);

        [Header("Trace")]
        [Range(32, 512)] public int pooledTracePointCount = 160;
        [Range(1f, 12f)] public float traceThickness = 3f;
        [Min(0.01f)] public float authoritativeCorrectionSeconds = 0.1f;

        [Header("Candles")]
        [Range(4, 8)] public int ticksPerCandle = 5;
        [Range(24, 64)] public int visibleCandleCount = 40;
        [Range(0.35f, 0.9f)] public float candleBodyWidthRatio = 0.62f;
        [Min(1f)] public float wickThickness = 2f;
        [Min(1f)] public float dojiMinBodyPixels = 3f;

        [Header("Dashed overlay")]
        public Material dashedLineMaterial;
        [Range(1f, 8f)] public float dashedLineThickness = 2f;
        [Range(1f, 8f)] public float priceLineThickness = 2f;

        [Header("Motion")]
        [Min(0.02f)] public float priceTweenSeconds = 0.08f;
        [Min(0.02f)] public float candleGrowSeconds = 0.12f;
        [Min(0.02f)] public float candleClosePunchSeconds = 0.16f;
        [Range(0f, 0.4f)] public float candleClosePunchScale = 0.16f;

        [Header("Haptics")]
        [Min(0.05f)] public float crossingPulseCooldownSeconds = 0.12f;
        [Min(0.05f)] public float spikePulseCooldownSeconds = 0.2f;

        [Header("Copy")]
        [TextArea(2, 4)]
        public string firstCaptureInstructions = "HOLD A / X / SCREEN TO LIFT — RELEASE TO FALL — KEEP THE SIGNAL IN THE BAND";
        public string lowTargetHpCopy = "LOW TARGET HP";
        public string unavailableCopy = "SIGNAL SYNC UNAVAILABLE — RETRYING";

        [Header("Attempt result copy")]
        [Tooltip("{subject} is replaced with the captured wild qualifier + species name.")]
        [TextArea(2, 4)]
        public string successNamePromptCopy = "You've captured a {subject}! Would you like to give it a name?";
        [TextArea(2, 4)]
        public string failureNarrativeCopy = "The capture attempt was unsuccessful. The signal slipped the lock.";
        public string nameChoiceYesLabel = "Yes";
        public string nameChoiceNoLabel = "No";
        public string successBannerCopy = "Success!";
        public string failureBannerCopy = "Failure";

        [Header("Challenge panel motion")]
        [Min(0.01f)] public float panelExpandXDuration = 0.22f;
        [Min(0.01f)] public float panelExpandYDuration = 0.38f;
        [Range(0.01f, 0.25f)] public float panelProjectionLineHeight = 0.05f;
        [Min(0f)] public float panelExpandYOvershoot = 1.35f;

        [Header("Attempt orb motion")]
        [Min(0f)] public float riseHeight = 6f;
        [Min(0.05f)] public float riseSeconds = 0.45f;
        public Ease riseEase = Ease.OutBack;
        [Min(0f)] public float riseOvershoot = 1.35f;
        [Min(0f)] public float postRiseHoldSeconds = 1f;
        [Min(0f)] public float tossHoverHeight = 10f;
        [Min(0.05f)] public float tossSeconds = 0.7f;
        public Ease tossEase = Ease.InOutQuad;
        [Min(0f)] public float tossArcHeight = 4f;
        [Min(0f)] public float preBeamHoverSeconds = 0.35f;
        [Min(0.05f)] public float beamSeconds = 0.55f;
        [Min(0.05f)] public float glitchFallbackSeconds = 0.8f;
        [Min(0.05f)] public float resultHoldSeconds = 0.85f;
        [Min(0.05f)] public float failDisintegrateSeconds = 0.45f;
        [Min(0.05f)] public float successFloatSeconds = 1.1f;
        [Min(0f)] public float successFloatHeight = 8f;
        [Min(0.05f)] public float hoverBobHeight = 0.35f;
        [Min(0.2f)] public float hoverBobSeconds = 1.1f;
        [Min(0.2f)] public float stageTimeoutPadding = 0.35f;

        [Header("Attempt beam")]
        [Tooltip("Particle material for the capture cone. Assign M_PixelBattle_Additive or another additive VFX material.")]
        public Material beamMaterial;
        public Color beamColor = new Color(0.35f, 0.95f, 1f, 0.85f);
        [Min(1f)] public float beamRange = 18f;
        [Range(4f, 40f)] public float beamAngle = 18f;

        [Header("Attempt audio")]
        [Tooltip("SoundEventSO played when the orb is tossed.")]
        public UnityEngine.Object orbTossSound;
        [Tooltip("SoundEventSO played when the capture beam fires.")]
        public UnityEngine.Object beamSound;
        [Tooltip("Optional looping SoundEventSO for the lock-meter synth. Leave empty to use the procedural wobble.")]
        public UnityEngine.Object lockLoopSound;
        [Tooltip("SoundEventSO played with the Failure banner.")]
        public UnityEngine.Object failureBannerSound;
        [Tooltip("SoundEventSO played when the failed orb disintegrates.")]
        public UnityEngine.Object failureVanishSound;
        [Tooltip("SoundEventSO played with the Success banner.")]
        public UnityEngine.Object successBannerSound;
        [Tooltip("SoundEventSO played while a captured orb floats away.")]
        public UnityEngine.Object successVanishSound;
        [Range(0.4f, 1.2f)] public float lockLoopEscapePitch = 0.72f;
        [Range(0.8f, 2f)] public float lockLoopLockPitch = 1.35f;
        [Range(0f, 1f)] public float lockLoopVolume = 0.55f;
        [Range(0f, 1f)] public float musicDuckMultiplier = 0.22f;
        [Min(0f)] public float musicDuckAttackSeconds = 0.12f;
        [Min(0f)] public float musicDuckReleaseSeconds = 0.45f;
    }
}
