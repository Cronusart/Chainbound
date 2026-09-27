using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Practical baseline popup visuals per category for designer tuning.
    /// </summary>
    public static class CombatPopupVisualProfileDefaults
    {
        private static readonly Color DamageRed = new Color(1f, 0.33f, 0.28f, 1f);
        private static readonly Color HealGreen = new Color(0.26f, 1f, 0.56f, 1f);
        private static readonly Color CritGold = new Color(1f, 0.91f, 0.28f, 1f);
        private static readonly Color BlockBlue = new Color(0.35f, 0.75f, 1f, 1f);
        private static readonly Color MissGray = new Color(0.6f, 0.6f, 0.65f, 1f);
        private static readonly Color DodgeTan = new Color(1f, 0.95f, 0.6f, 1f);
        private static readonly Color ApGainGreen = new Color(0.26f, 1f, 0.56f, 1f);
        private static readonly Color ApDrainRed = new Color(1f, 0.33f, 0.28f, 1f);
        private static readonly Color ApBlockedGray = new Color(0.55f, 0.55f, 0.65f, 1f);
        private static readonly Color BuffGreen = new Color(0.26f, 1f, 0.56f, 1f);
        private static readonly Color DebuffPurple = new Color(0.85f, 0.45f, 1f, 1f);
        private static readonly Color CleanseCyan = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color ImmuneSilver = new Color(0.75f, 0.75f, 0.8f, 1f);
        private static readonly Color WallBlue = new Color(0.35f, 0.75f, 1f, 1f);
        private static readonly Color WallBrokenOrange = new Color(1f, 0.55f, 0.25f, 1f);
        private static readonly Color NeutralWhite = new Color(1f, 1f, 1f, 1f);
        private static readonly Color KoWhite = new Color(1f, 1f, 1f, 1f);
        private static readonly Color NoApOrange = new Color(1f, 0.62f, 0.28f, 1f);

        public static void ApplyDefaults(CombatPopupVisualProfileSO profile, CombatPopupCategory category)
        {
            if (profile == null)
                return;

            ResetCommon(profile);

            switch (category)
            {
                case CombatPopupCategory.Damage:
                    ApplyNumericImpact(profile, DamageRed, enableAmountScale: true, maxScale: 1.5f);
                    break;
                case CombatPopupCategory.Heal:
                    ApplyNumericImpact(profile, HealGreen, enableAmountScale: true, maxScale: 1.35f, minAmount: 1f, maxAmount: 60f);
                    break;
                case CombatPopupCategory.Crit:
                    ApplyNumericImpact(profile, CritGold, enableAmountScale: true, maxScale: 1.65f, minAmount: 1f, maxAmount: 100f);
                    profile.playScalePulse = true;
                    profile.scalePulseMultiplier = 1.28f;
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Punch, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.Miss:
                    ApplyLabelImpact(profile, MissGray, CombatPopupMotionMode.StaticFade, 0.85f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.None, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.Dodge:
                    ApplyLabelImpact(profile, DodgeTan, CombatPopupMotionMode.FloatDown, 0.9f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatDown);
                    break;
                case CombatPopupCategory.Blocked:
                    ApplyLabelImpact(profile, BlockBlue, CombatPopupMotionMode.Shake, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Shake, CombatPopupTranslateMode.FloatUp);
                    profile.shakeStrength = 0.12f;
                    break;
                case CombatPopupCategory.NoAp:
                    ApplyLabelImpact(profile, NoApOrange, CombatPopupMotionMode.Punch, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Punch, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.ApGain:
                    ApplyLabelImpact(profile, ApGainGreen, CombatPopupMotionMode.FloatUp, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.ApDrain:
                    ApplyLabelImpact(profile, ApDrainRed, CombatPopupMotionMode.FloatDown, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatDown);
                    break;
                case CombatPopupCategory.ApBlocked:
                    ApplyLabelImpact(profile, ApBlockedGray, CombatPopupMotionMode.StaticFade, 0.95f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.None, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.StatusBuff:
                    ApplyLabelImpact(profile, BuffGreen, CombatPopupMotionMode.Arc, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.Arc);
                    break;
                case CombatPopupCategory.StatusDebuff:
                    ApplyLabelImpact(profile, DebuffPurple, CombatPopupMotionMode.Arc, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.Arc);
                    break;
                case CombatPopupCategory.StatusNeutral:
                    ApplyLabelImpact(profile, NeutralWhite, CombatPopupMotionMode.FloatUp, 0.95f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.StatusCleanse:
                    ApplyLabelImpact(profile, CleanseCyan, CombatPopupMotionMode.FloatUp, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.StatusImmune:
                    ApplyLabelImpact(profile, ImmuneSilver, CombatPopupMotionMode.StaticFade, 0.9f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.None, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.StageBuff:
                    ApplyLabelImpact(profile, BuffGreen, CombatPopupMotionMode.FloatUp, 1.05f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatUp);
                    break;
                case CombatPopupCategory.StageDebuff:
                    ApplyLabelImpact(profile, DebuffPurple, CombatPopupMotionMode.FloatDown, 1.05f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatDown);
                    break;
                case CombatPopupCategory.Passive:
                    ApplyLabelImpact(profile, NeutralWhite, CombatPopupMotionMode.FloatUp, 0.92f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatUp);
                    profile.fontSize = 2.4f;
                    break;
                case CombatPopupCategory.Counter:
                    ApplyLabelImpact(profile, CritGold, CombatPopupMotionMode.Punch, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Punch, CombatPopupTranslateMode.FloatUp);
                    profile.fontSize = 2.5f;
                    break;
                case CombatPopupCategory.Modifier:
                    ApplyLabelImpact(profile, NeutralWhite, CombatPopupMotionMode.FloatUp, 0.88f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatUp);
                    profile.fontSize = 2.2f;
                    break;
                case CombatPopupCategory.WallRaised:
                    ApplyLabelImpact(profile, WallBlue, CombatPopupMotionMode.FloatUp, 1.1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatUp);
                    profile.fontSize = 2.6f;
                    break;
                case CombatPopupCategory.WallBroken:
                    ApplyLabelImpact(profile, WallBrokenOrange, CombatPopupMotionMode.Shake, 1.15f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Shake, CombatPopupTranslateMode.FloatUp);
                    profile.fontSize = 2.6f;
                    profile.shakeStrength = 0.22f;
                    break;
                case CombatPopupCategory.Ko:
                    ApplyLabelImpact(profile, KoWhite, CombatPopupMotionMode.Punch, 1.35f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Punch, CombatPopupTranslateMode.FloatUp);
                    profile.fontSize = 3.2f;
                    profile.scalePulseMultiplier = 1.3f;
                    break;
                case CombatPopupCategory.WallMitigated:
                    ApplyNumericImpact(profile, new Color(0.35f, 0.85f, 1f, 1f), enableAmountScale: true, maxScale: 1.35f, minAmount: 1f, maxAmount: 80f);
                    profile.playScalePulse = true;
                    profile.scalePulseMultiplier = 1.12f;
                    break;
                case CombatPopupCategory.WallReflect:
                    ApplyLabelImpact(profile, new Color(1f, 0.45f, 0.35f, 1f), CombatPopupMotionMode.Punch, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Punch, CombatPopupTranslateMode.FloatUp);
                    profile.fontSize = 2.5f;
                    profile.scaleByAmount = true;
                    profile.scaleFontSizeByAmount = true;
                    profile.minAmountForScale = 1f;
                    profile.maxAmountForScale = 60f;
                    profile.minAmountScaleMultiplier = 1f;
                    profile.maxAmountScaleMultiplier = 1.4f;
                    break;
                case CombatPopupCategory.WallDebuff:
                    ApplyLabelImpact(profile, DebuffPurple, CombatPopupMotionMode.FloatDown, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatDown);
                    profile.fontSize = 2.4f;
                    break;
                default:
                    ApplyLabelImpact(profile, NeutralWhite, CombatPopupMotionMode.FloatUp, 1f);
                    ApplyRootMotion(profile, CombatPopupMotionLayers.Translate, CombatPopupTranslateMode.FloatUp);
                    break;
            }
        }

        private static void ResetCommon(CombatPopupVisualProfileSO profile)
        {
            profile.showText = true;
            profile.showSprite = false;
            profile.billboardToCamera = true;
            profile.fontSize = 2.8f;
            profile.textOutlineColor = Color.black;
            profile.textOutlineWidth = 0.2f;
            profile.textAlignment = TextAlignmentOptions.Center;
            profile.worldYOffset = 0f;
            profile.duration = 0.95f;
            profile.holdBeforeFade = 0.08f;
            profile.moveOffset = new Vector3(0f, 0.75f, 0f);
            profile.moveEase = Ease.OutCubic;
            profile.scaleEase = Ease.OutBack;
            profile.fadeEase = Ease.OutQuad;
            profile.startScale = Vector3.one;
            profile.endScale = Vector3.one;
            profile.playScalePulse = true;
            profile.scalePulseMultiplier = 1.18f;
            profile.scaleByAmount = false;
            profile.minAmountForScale = 1f;
            profile.maxAmountForScale = 80f;
            profile.minAmountScaleMultiplier = 1f;
            profile.maxAmountScaleMultiplier = 1.45f;
            profile.scaleFontSizeByAmount = false;
            profile.minAmountFontSize = 2.4f;
            profile.maxAmountFontSize = 3.8f;
            profile.motionMode = CombatPopupMotionMode.FloatUp;
            profile.rootMotionLayers = CombatPopupMotionLayers.Translate;
            profile.translateMode = CombatPopupTranslateMode.FloatUp;
            profile.mirrorImpulseOnFacing = true;
            profile.textMotionLayers = CombatPopupMotionLayers.None;
            profile.spriteMotionLayers = CombatPopupMotionLayers.None;
            profile.textMoveOffset = Vector3.zero;
            profile.textArcControlOffset = Vector3.zero;
            profile.spriteMoveOffset = Vector3.zero;
            profile.spriteArcControlOffset = Vector3.zero;
            profile.textFadeDelay = 0f;
            profile.textFadeDuration = 0f;
            profile.spriteFadeDelay = 0f;
            profile.spriteFadeDuration = 0f;
            profile.fadeText = true;
            profile.fadeSprite = true;
        }

        private static void ApplyNumericImpact(
            CombatPopupVisualProfileSO profile,
            Color color,
            bool enableAmountScale,
            float maxScale,
            float minAmount = 1f,
            float maxAmount = 80f)
        {
            profile.textColor = color;
            profile.scaleByAmount = enableAmountScale;
            profile.scaleFontSizeByAmount = enableAmountScale;
            profile.minAmountForScale = minAmount;
            profile.maxAmountForScale = maxAmount;
            profile.minAmountScaleMultiplier = 1f;
            profile.maxAmountScaleMultiplier = maxScale;
            ApplyRootMotion(profile, CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Shake, CombatPopupTranslateMode.FloatUp);
        }

        private static void ApplyRootMotion(
            CombatPopupVisualProfileSO profile,
            CombatPopupMotionLayers layers,
            CombatPopupTranslateMode translateMode)
        {
            profile.rootMotionLayers = layers;
            profile.translateMode = translateMode;
            profile.motionMode = ToLegacyMotionMode(layers, translateMode);
        }

        private static CombatPopupMotionMode ToLegacyMotionMode(
            CombatPopupMotionLayers layers,
            CombatPopupTranslateMode translateMode)
        {
            if ((layers & CombatPopupMotionLayers.Punch) == CombatPopupMotionLayers.Punch &&
                (layers & CombatPopupMotionLayers.Translate) != CombatPopupMotionLayers.Translate &&
                (layers & CombatPopupMotionLayers.Shake) != CombatPopupMotionLayers.Shake)
            {
                return CombatPopupMotionMode.Punch;
            }

            if ((layers & CombatPopupMotionLayers.Shake) == CombatPopupMotionLayers.Shake &&
                (layers & CombatPopupMotionLayers.Translate) != CombatPopupMotionLayers.Translate &&
                (layers & CombatPopupMotionLayers.Punch) != CombatPopupMotionLayers.Punch)
            {
                return CombatPopupMotionMode.Shake;
            }

            if (layers == CombatPopupMotionLayers.None)
                return CombatPopupMotionMode.StaticFade;

            switch (translateMode)
            {
                case CombatPopupTranslateMode.FloatDown:
                    return CombatPopupMotionMode.FloatDown;
                case CombatPopupTranslateMode.WorldOffset:
                    return CombatPopupMotionMode.WorldOffset;
                case CombatPopupTranslateMode.Arc:
                    return CombatPopupMotionMode.Arc;
                default:
                    return CombatPopupMotionMode.FloatUp;
            }
        }

        private static void ApplyLabelImpact(
            CombatPopupVisualProfileSO profile,
            Color color,
            CombatPopupMotionMode motion,
            float startScaleMultiplier)
        {
            profile.textColor = color;
            profile.motionMode = motion;
            profile.startScale = Vector3.one * startScaleMultiplier;
            profile.playScalePulse = motion != CombatPopupMotionMode.StaticFade;
        }
    }
}
