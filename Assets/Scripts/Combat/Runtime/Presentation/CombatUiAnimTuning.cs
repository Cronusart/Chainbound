using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class CombatUiAnimTuning : MonoBehaviour
    {
        [Header("Selection")]
        [Min(0f)]
        public float selectionAutoCommitDelay = 0.18f;

        [Header("HUD Pulses")]
        [Min(1f)]
        public float activePulseScale = 1.06f;
        [Min(1f)]
        public float targetPulseScale = 1.04f;
        [Min(0.01f)]
        public float pulseUpDuration = 0.12f;
        [Min(0.01f)]
        public float pulseDownDuration = 0.14f;
        [Min(0f)]
        public float targetPulseHold = 0.04f;
        public Ease pulseEaseOut = Ease.OutSine;
        public Ease pulseEaseIn = Ease.InOutSine;
        public bool useUnscaledTime = true;

        [Header("Target Highlight")]
        [Min(0f)]
        public float previewTargetFlashDuration = 0.18f;

        [Header("Ground Circle")]
        [Min(1f)]
        public float groundCirclePulseScale = 1.08f;
        [Min(0.01f)]
        public float groundCirclePulseUpDuration = 0.48f;
        [Min(0.01f)]
        public float groundCirclePulseDownDuration = 0.38f;
        [Min(0f)]
        public float groundCirclePulseHold = 0.06f;
        public Ease groundCirclePulseEaseUp = Ease.OutElastic;
        public Ease groundCirclePulseEaseDown = Ease.InOutSine;

        [Header("Damage Popup")]
        [Min(0f)]
        public float popupWorldYOffset = 1.15f;
        [Min(0.05f)]
        public float popupDuration = 0.95f;
        [Min(0.05f)]
        public float popupFloatDistance = 0.75f;
    }
}
