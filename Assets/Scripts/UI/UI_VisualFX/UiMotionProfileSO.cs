using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName = "UiMotionProfile", menuName = "Chainbound/UI/VFX/Motion Profile")]
public class UiMotionProfileSO : ScriptableObject
{
    [Header("Focus Scale")]
    [SerializeField] private float focusScale = 1.06f;
    [SerializeField] private float focusInDuration = 0.12f;
    [SerializeField] private float focusOutDuration = 0.12f;
    [SerializeField] private Ease focusEase = Ease.OutCubic;

    [Header("Focus Pulse")]
    [SerializeField] private bool enableFocusPulse = true;
    [SerializeField] private float pulseScaleMin = 1.03f;
    [SerializeField] private float pulseDuration = 0.35f;

    [Header("Press")]
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float pressDuration = 0.08f;

    [Header("Stagger")]
    [SerializeField] private float staggerDelay = 0.06f;

    [Header("Shimmer")]
    [SerializeField] private float shimmerFps = 8f;
    [Tooltip("Pause (seconds) between shimmer cycles. 0 = continuous loop.")]
    [SerializeField] private float shimmerPauseDuration = 0.6f;

    public float FocusScale => focusScale;
    public float FocusInDuration => focusInDuration;
    public float FocusOutDuration => focusOutDuration;
    public Ease FocusEase => focusEase;
    public bool EnableFocusPulse => enableFocusPulse;
    public float PulseScaleMin => pulseScaleMin;
    public float PulseDuration => pulseDuration;
    public float PressScale => pressScale;
    public float PressDuration => pressDuration;
    public float StaggerDelay => staggerDelay;
    public float ShimmerFps => shimmerFps;
    public float ShimmerPauseDuration => shimmerPauseDuration;
}
