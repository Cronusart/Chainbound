using DG.Tweening;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    [CreateAssetMenu(
        fileName = "PlayerMenuPanelTransitionProfile",
        menuName = "Chainbound/UI/Player Menu/Panel Transition Profile")]
    public sealed class PlayerMenuPanelTransitionProfileSO : ScriptableObject
    {
        [Header("Material")]
        [SerializeField] private Material glitchMaterial;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.14f;
        [SerializeField, Min(0f)] private float swapHoldDuration = 0.02f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.16f;
        [SerializeField] private Ease fadeOutEase = Ease.InSine;
        [SerializeField] private Ease fadeInEase = Ease.OutSine;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Glitch")]
        [SerializeField, Range(0f, 1f)] private float peakGlitchIntensity = 1f;
        [Tooltip("Runtime source of truth for the UI glitch material's _SliceDisplacement value during player-menu transitions.")]
        [SerializeField, Range(0f, 0.2f)] private float sliceDisplacement = 0.2f;
        [Tooltip("Runtime source of truth for the UI glitch material's _SliceBandCount value during player-menu transitions.")]
        [SerializeField, Range(2f, 64f)] private float sliceBandCount = 12f;
        [Tooltip("Runtime source of truth for the UI glitch material's _ScanlineCount value during player-menu transitions.")]
        [SerializeField, Range(4f, 128f)] private float scanlineCount = 11f;
        [SerializeField] private float scanlineSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float scanlineAlpha = 0.555f;
        [SerializeField, Range(0f, 1f)] private float staticAmount = 0.8f;
        [SerializeField, Range(0f, 1f)] private float dissolveThreshold;
        [SerializeField] private Color tint = new Color(0.78039217f, 0.8627451f, 0.8156863f, 1f);

        public Material GlitchMaterial => glitchMaterial;
        public float FadeOutDuration => Mathf.Max(0f, fadeOutDuration);
        public float SwapHoldDuration => Mathf.Max(0f, swapHoldDuration);
        public float FadeInDuration => Mathf.Max(0f, fadeInDuration);
        public Ease FadeOutEase => fadeOutEase;
        public Ease FadeInEase => fadeInEase;
        public bool UseUnscaledTime => useUnscaledTime;
        public float PeakGlitchIntensity => Mathf.Clamp01(peakGlitchIntensity);
        public float SliceDisplacement => Mathf.Clamp(sliceDisplacement, 0f, 0.2f);
        public float SliceBandCount => Mathf.Clamp(sliceBandCount, 2f, 64f);
        public float ScanlineCount => Mathf.Clamp(scanlineCount, 4f, 128f);
        public float ScanlineSpeed => scanlineSpeed;
        public float ScanlineAlpha => Mathf.Clamp01(scanlineAlpha);
        public float StaticAmount => Mathf.Clamp01(staticAmount);
        public float DissolveThreshold => Mathf.Clamp01(dissolveThreshold);
        public Color Tint => tint;
    }
}
