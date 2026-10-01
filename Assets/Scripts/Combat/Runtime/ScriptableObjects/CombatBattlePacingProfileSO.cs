using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Designer-tunable battle pacing and feedback colors for combat presentation.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatBattlePacingProfile", menuName = "Chainbound/Combat/Config/Battle Pacing Profile")]
    public class CombatBattlePacingProfileSO : ScriptableObject
    {
        [Header("Turn Flow")]
        [Min(0f)] public float postSelectionConfirmDelay = 0.35f;
        [Min(0f)] public float minStepDelay = 0.15f;
        [Min(0f)] public float turnEndDelay = 0.4f;
        [Min(0f)] public float moveAnnounceHold = 0.12f;
        [Min(0f)] public float utilityCastHold = 0.3f;

        [Header("Impact Holds")]
        [Min(0f)] public float damageHold = 0.9f;
        [Min(0f)] public float healHold = 0.9f;
        [Min(0f)] public float missDodgeHold = 0.75f;
        [Min(0f)] public float apFeedbackHold = 0.6f;
        [Min(0f)] public float statusLabelHold = 0.8f;
        [Min(0f)] public float stageLabelHold = 0.8f;
        [Min(0f)] public float passiveLabelHold = 0.95f;
        [Min(0f)] public float modifierLabelHold = 0.9f;
        [Min(0f)] public float noApHold = 0.55f;
        [Min(0f)] public float barrierBlockedHold = 1f;
        [Min(0f)] public float barrierRaisedHold = 0.9f;
        [Min(0f)] public float swapSettleDelay = 0.4f;
        [Min(0f)] public float faintPostImpactDelay = 0.25f;

        [Header("AP Presentation")]
        [Tooltip("When true, ordinary move AP costs also show AP drain feedback.")]
        public bool showMoveCostApFeedback = false;

        [Header("Turn Start AP Grant")]
        [Tooltip("Single hold for the simultaneous turn-start AP grant beat.")]
        [Min(0f)] public float turnStartApGrantHold = 0.35f;
        [Tooltip("Outline flash hold multiplier during the turn-start AP batch.")]
        [Min(0f)] public float turnStartApOutlineFlashHoldScale = 0.35f;
        [Tooltip("When true, HUD AP pip animations refresh once after the full turn-start AP batch.")]
        public bool batchTurnStartApHudRefresh = true;

        [Header("Feedback Colors")]
        public Color apGainColor = new Color(0.26f, 1f, 0.56f, 0.95f);
        public Color apDrainColor = new Color(1f, 0.33f, 0.28f, 0.95f);
        public Color apBlockedColor = new Color(0.55f, 0.55f, 0.65f, 0.95f);
        public Color buffColor = new Color(0.26f, 1f, 0.56f, 0.95f);
        public Color debuffColor = new Color(1f, 0.33f, 0.28f, 0.95f);
        public Color cleanseColor = new Color(0.55f, 0.85f, 1f, 0.95f);
        public Color immuneColor = new Color(0.75f, 0.75f, 0.8f, 0.95f);
        public Color wallColor = new Color(0.35f, 0.75f, 1f, 0.95f);
        public Color wallBrokenColor = new Color(1f, 0.55f, 0.25f, 0.95f);
        public Color neutralLabelColor = new Color(1f, 1f, 1f, 0.95f);

        [Header("Outline Flash")]
        [Min(0f)] public float outlineFlashWidthPixels = 5f;
        [Range(0f, 1f)] public float outlineFlashSoftness = 0.45f;
        [Min(0f)] public float outlineFlashFadeInSeconds = 0.06f;
        [Min(0f)] public float outlineFlashFadeOutSeconds = 0.14f;
    }
}
