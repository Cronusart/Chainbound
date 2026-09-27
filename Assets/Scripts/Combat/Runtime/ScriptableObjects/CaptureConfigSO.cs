using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Configuration for the capture system.
    /// Defines base rates, HP factor curve, and modifier defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "CaptureConfig", menuName = "Chainbound/Combat/Config/Capture Config")]
    public class CaptureConfigSO : ScriptableObject
    {
        [Header("Default Capture Rate")]
        [Tooltip("Default base capture rate if species doesn't specify one (0-100).")]
        [Range(0, 100)]
        public int defaultBaseCaptureRate = 45;

        [Header("HP Factor")]
        [Tooltip("HP influence weight. At 0.5: half the bonus comes from HP reduction.")]
        [Range(0f, 1f)]
        public float hpBaseWeight = 0.5f;

        [Tooltip("HP bonus weight. At 0.5: half the bonus scales with HP reduction.")]
        [Range(0f, 1f)]
        public float hpBonusWeight = 0.5f;

        [Header("Status Modifiers")]
        [Tooltip("Capture modifier when target is paralyzed.")]
        public float paralysisModifier = 1.5f;

        [Tooltip("Capture modifier when target is asleep.")]
        public float sleepModifier = 2.0f;

        [Tooltip("Capture modifier when target is frozen.")]
        public float freezeModifier = 1.5f;

        [Header("Device Modifiers (future)")]
        [Tooltip("Default device modifier (1.0 = standard).")]
        public float defaultDeviceModifier = 1.0f;

        [Header("Demo Constraints")]
        [Tooltip("Maximum Chainling level in demo (prevents evolution stage advancement).")]
        public int demoLevelCap = 25;

        [Tooltip("Allow capture attempts at any HP (true for easier demo).")]
        public bool allowCaptureAtAnyHP = true;
    }
}
