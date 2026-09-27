using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum CombatPopupMotionMode
    {
        FloatUp,
        FloatDown,
        WorldOffset,
        Arc,
        Punch,
        Shake,
        StaticFade,
    }

    /// <summary>
    /// Designer-tunable visual profile for one combat popup category.
    /// Supports text-only, sprite-only, or combined billboard presentation.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatPopupVisual_", menuName = "Chainbound/Combat/Presentation/Popups/Popup Visual Profile")]
    public class CombatPopupVisualProfileSO : ScriptableObject
    {
        [Header("Channels")]
        public bool showText = true;
        public bool showSprite = false;
        public bool billboardToCamera = true;

        [Header("Text")]
        public TMP_FontAsset fontAsset;
        public Material textMaterialPreset;
        [Min(0.01f)] public float fontSize = 2.8f;
        public Color textColor = Color.white;
        public Color textOutlineColor = Color.black;
        [Min(0f)] public float textOutlineWidth = 0.2f;
        public TextAlignmentOptions textAlignment = TextAlignmentOptions.Center;
        public Vector3 textLocalOffset = Vector3.zero;
        public Vector3 textLocalScale = Vector3.one;

        [Header("Sprite Billboard")]
        public Sprite sprite;
        public Material spriteMaterial;
        public Color spriteColor = Color.white;
        public Vector3 spriteLocalOffset = Vector3.zero;
        public Vector3 spriteLocalScale = Vector3.one;
        public int spriteSortingOrder = 120;
        [Tooltip("Optional shader color property name for MaterialPropertyBlock tinting.")]
        public string spriteColorPropertyName = "_Color";

        [Header("Timing")]
        [Min(0f)] public float worldYOffset = 1.15f;
        [Min(0.05f)] public float duration = 0.95f;
        [Min(0f)] public float startDelay = 0f;
        [Min(0f)] public float holdBeforeFade = 0f;
        public bool useUnscaledTime = true;

        [Header("Motion")]
        [Tooltip("Composable root motion layers. When None, legacy motionMode is used for migration.")]
        public CombatPopupMotionLayers rootMotionLayers = CombatPopupMotionLayers.Translate;
        public CombatPopupTranslateMode translateMode = CombatPopupTranslateMode.FloatUp;
        [Tooltip("Deprecated single-mode field kept for legacy assets. Prefer rootMotionLayers.")]
        public CombatPopupMotionMode motionMode = CombatPopupMotionMode.FloatUp;
        [Tooltip("Chainling-relative offset. Positive X drifts toward field center on the default side.")]
        public Vector3 moveOffset = new Vector3(0f, 0.75f, 0f);
        public Vector3 arcControlOffset = new Vector3(0.35f, 0.35f, 0f);
        public Ease moveEase = Ease.OutCubic;
        public Ease scaleEase = Ease.OutBack;
        public Ease fadeEase = Ease.OutQuad;
        [Tooltip("When enabled, punch/shake X strength mirrors with battlefield facing.")]
        public bool mirrorImpulseOnFacing = true;

        [Header("Scale")]
        public Vector3 startScale = Vector3.one;
        public Vector3 endScale = Vector3.one;
        public bool playScalePulse = true;
        [Min(1f)] public float scalePulseMultiplier = 1.18f;

        [Header("Amount Scaling")]
        [Tooltip("When enabled, numeric popups scale based on the absolute amount value.")]
        public bool scaleByAmount = false;
        [Min(0f)] public float minAmountForScale = 1f;
        [Min(0f)] public float maxAmountForScale = 80f;
        [Min(0.01f)] public float minAmountScaleMultiplier = 1f;
        [Min(0.01f)] public float maxAmountScaleMultiplier = 1.45f;
        [Tooltip("When enabled, scales TMP font size from fontSize using the same amount thresholds as scaleByAmount.")]
        public bool scaleFontSizeByAmount = false;
        [Min(0.01f)] public float minAmountFontSize = 2.4f;
        [Min(0.01f)] public float maxAmountFontSize = 3.8f;

        [Header("Punch / Shake")]
        public Vector3 punchStrength = new Vector3(0.12f, 0.12f, 0f);
        public int punchVibrato = 8;
        [Min(0f)] public float punchElasticity = 0.8f;
        public float shakeStrength = 0.18f;
        public int shakeVibrato = 12;
        [Range(0f, 90f)] public float shakeRandomness = 45f;

        [Header("Per-Channel Motion")]
        [Tooltip("Optional text-only motion layers. None inherits root + motion pivot.")]
        public CombatPopupMotionLayers textMotionLayers = CombatPopupMotionLayers.None;
        [Tooltip("Optional sprite-only motion layers. None inherits root + motion pivot.")]
        public CombatPopupMotionLayers spriteMotionLayers = CombatPopupMotionLayers.None;
        public Vector3 textMoveOffset = Vector3.zero;
        public Vector3 textArcControlOffset = Vector3.zero;
        public Vector3 spriteMoveOffset = Vector3.zero;
        public Vector3 spriteArcControlOffset = Vector3.zero;

        [Header("Fade")]
        public bool fadeText = true;
        public bool fadeSprite = true;
        [Min(0f)] public float textFadeDelay = 0f;
        [Min(0f)] public float textFadeDuration = 0f;
        [Min(0f)] public float spriteFadeDelay = 0f;
        [Min(0f)] public float spriteFadeDuration = 0f;
    }
}
