using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [CreateAssetMenu(fileName = "PartyTabMotionProfile", menuName = "Chainbound/UI/Party Tab Motion Profile")]
    public class PartyTabMotionProfileSO : ScriptableObject
    {
        [Header("Screen Enter (T1)")]
        [SerializeField] private float screenEnterDuration = 0.12f;
        [SerializeField] private float screenEnterStagger = 0.04f;
        [SerializeField] private float screenEnterStartScale = 0.99f;
        [SerializeField] private float screenEnterProgressDuration = 0.46f;

        [Header("Column Focus (T2/T3)")]
        [SerializeField] private float visualStageFocusScale = 1.08f;
        [SerializeField] private float focusedColumnScale = 1f;
        [SerializeField] private float unfocusedColumnScale = 0.93f;
        [SerializeField] private float focusedVisualStageScale = 1f;
        [SerializeField] private float unfocusedVisualStageScale = 0.885f;
        [SerializeField] private float visualStageFocusInDuration = 0.24f;
        [SerializeField] private float visualStageFocusOutDuration = 0.2f;
        [SerializeField] private float normalScaleOvershoot = 0.05f;
        [SerializeField] private float normalScaleOvershootDuration = 0.14f;
        [SerializeField] private float normalScaleSettleDuration = 0.2f;
        [SerializeField] private float unfocusedInfoPanelAlpha = 0.72f;
        [SerializeField] private float columnSelectionPunchStrength = 0.08f;
        [SerializeField] private float columnSelectionPunchDuration = 0.22f;
        [SerializeField] private int columnSelectionPunchVibrato = 8;

        [Header("View Panel (T5)")]
        [SerializeField] private float viewPanelFadeDuration = 0.12f;

        [Header("HP Bar (T6/T7/T8)")]
        [SerializeField] private float hpFillDuration = 0.2f;
        [SerializeField] private float hpLabelPunchStrength = 0.15f;
        [SerializeField] private float hpLabelPunchDuration = 0.15f;
        [SerializeField] private float faintedColorDuration = 0.2f;
        [SerializeField] private Color healthyHpColor = new Color(0.22f, 0.78f, 0.34f, 1f);
        [SerializeField] private Color faintedHpColor = new Color(0.45f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color mutedHpColor = new Color(0.18f, 0.22f, 0.2f, 1f);

        [Header("View Rail (T9/T10)")]
        [SerializeField] private float viewRailOpenDuration = 0.15f;
        [SerializeField] private float viewRailRowStagger = 0.04f;

        [Header("Buttons (T9/T14)")]
        [SerializeField] private float buttonFocusScale = 1.06f;
        [SerializeField] private float buttonPressScale = 0.95f;
        [SerializeField] private float buttonPressDuration = 0.08f;

        public float ScreenEnterDuration => screenEnterDuration;
        public float ScreenEnterStagger => screenEnterStagger;
        public float ScreenEnterStartScale => screenEnterStartScale;
        public float ScreenEnterProgressDuration => screenEnterProgressDuration;
        public float VisualStageFocusScale => visualStageFocusScale;
        public float FocusedColumnScale => focusedColumnScale;
        public float UnfocusedColumnScale => unfocusedColumnScale;
        public float FocusedVisualStageScale => focusedVisualStageScale;
        public float UnfocusedVisualStageScale => unfocusedVisualStageScale;
        public float VisualStageFocusInDuration => visualStageFocusInDuration;
        public float VisualStageFocusOutDuration => visualStageFocusOutDuration;
        public float NormalScaleOvershoot => normalScaleOvershoot;
        public float NormalScaleOvershootDuration => normalScaleOvershootDuration;
        public float NormalScaleSettleDuration => normalScaleSettleDuration;
        public float UnfocusedInfoPanelAlpha => unfocusedInfoPanelAlpha;
        public float ColumnSelectionPunchStrength => columnSelectionPunchStrength;
        public float ColumnSelectionPunchDuration => columnSelectionPunchDuration;
        public int ColumnSelectionPunchVibrato => columnSelectionPunchVibrato;
        public float ViewPanelFadeDuration => viewPanelFadeDuration;
        public float HpFillDuration => hpFillDuration;
        public float HpLabelPunchStrength => hpLabelPunchStrength;
        public float HpLabelPunchDuration => hpLabelPunchDuration;
        public float FaintedColorDuration => faintedColorDuration;
        public Color HealthyHpColor => healthyHpColor;
        public Color FaintedHpColor => faintedHpColor;
        public Color MutedHpColor => mutedHpColor;
        public float ViewRailOpenDuration => viewRailOpenDuration;
        public float ViewRailRowStagger => viewRailRowStagger;
        public float ButtonFocusScale => buttonFocusScale;
        public float ButtonPressScale => buttonPressScale;
        public float ButtonPressDuration => buttonPressDuration;
    }
}
