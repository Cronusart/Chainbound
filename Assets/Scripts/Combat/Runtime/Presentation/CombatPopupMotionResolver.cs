using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Resolves composable popup motion layers, battlefield mirroring, and DOTween-safe sequence assembly.
    /// </summary>
    public static class CombatPopupMotionResolver
    {
        public static CombatPopupMotionLayers ResolveRootLayers(CombatPopupVisualProfileSO profile)
        {
            if (profile == null)
                return CombatPopupMotionLayers.None;

            if (profile.rootMotionLayers != CombatPopupMotionLayers.None)
                return profile.rootMotionLayers;

            return FromLegacyMotionMode(profile.motionMode);
        }

        public static CombatPopupTranslateMode ResolveTranslateMode(CombatPopupVisualProfileSO profile)
        {
            if (profile == null)
                return CombatPopupTranslateMode.FloatUp;

            if (profile.rootMotionLayers != CombatPopupMotionLayers.None)
                return profile.translateMode;

            return FromLegacyTranslateMode(profile.motionMode);
        }

        public static CombatPopupMotionLayers ResolveChannelLayers(CombatPopupMotionLayers channelLayers)
        {
            return channelLayers;
        }

        public static bool HasChannelOverride(CombatPopupMotionLayers channelLayers)
        {
            return channelLayers != CombatPopupMotionLayers.None;
        }

        public static Vector3 MirrorVector(Vector3 value, int facingSign)
        {
            if (facingSign >= 0)
                return value;

            return new Vector3(-value.x, value.y, value.z);
        }

        public static Vector3 ResolveMoveOffset(
            CombatPopupVisualProfileSO profile,
            int facingSign,
            Vector3 channelOverride)
        {
            Vector3 offset = channelOverride != Vector3.zero ? channelOverride : profile.moveOffset;
            return MirrorVector(offset, facingSign);
        }

        public static Vector3 ResolveArcControlOffset(
            CombatPopupVisualProfileSO profile,
            int facingSign,
            Vector3 channelOverride)
        {
            Vector3 offset = channelOverride != Vector3.zero ? channelOverride : profile.arcControlOffset;
            return MirrorVector(offset, facingSign);
        }

        public static Vector3 ResolveEndPosition(
            Vector3 startPos,
            CombatPopupVisualProfileSO profile,
            int facingSign,
            Vector3 channelMoveOverride = default)
        {
            Vector3 moveOffset = ResolveMoveOffset(profile, facingSign, channelMoveOverride);
            CombatPopupTranslateMode translateMode = ResolveTranslateMode(profile);

            switch (translateMode)
            {
                case CombatPopupTranslateMode.FloatDown:
                    return startPos - moveOffset;
                case CombatPopupTranslateMode.WorldOffset:
                case CombatPopupTranslateMode.Arc:
                case CombatPopupTranslateMode.FloatUp:
                    return startPos + moveOffset;
                default:
                    return startPos + moveOffset;
            }
        }

        public static void AppendLayerMotion(
            Sequence sequence,
            CombatPopupVisualProfileSO profile,
            int facingSign,
            float duration,
            Transform translateTransform,
            Transform impulseTransform,
            CombatPopupMotionLayers layers,
            bool useLocalSpace,
            Vector3 channelMoveOverride = default,
            Vector3 channelArcOverride = default)
        {
            if (sequence == null || profile == null || layers == CombatPopupMotionLayers.None)
                return;

            Transform impulseTarget = impulseTransform != null ? impulseTransform : translateTransform;
            if (translateTransform == null)
                return;

            bool mirrorImpulse = profile.mirrorImpulseOnFacing;
            int impulseFacing = mirrorImpulse ? facingSign : 1;

            if (HasLayer(layers, CombatPopupMotionLayers.Translate))
            {
                CombatPopupTranslateMode translateMode = ResolveTranslateMode(profile);
                Vector3 arcOffset = ResolveArcControlOffset(profile, facingSign, channelArcOverride);

                if (useLocalSpace)
                {
                    Vector3 startLocal = translateTransform.localPosition;
                    Vector3 moveOffset = ResolveMoveOffset(profile, facingSign, channelMoveOverride);
                    Vector3 endLocal = translateMode == CombatPopupTranslateMode.FloatDown
                        ? startLocal - moveOffset
                        : startLocal + moveOffset;

                    if (translateMode == CombatPopupTranslateMode.Arc)
                    {
                        sequence.Join(translateTransform.DOLocalPath(
                            new[]
                            {
                                startLocal,
                                startLocal + arcOffset,
                                endLocal,
                            },
                            duration,
                            PathType.CatmullRom).SetEase(profile.moveEase));
                    }
                    else
                    {
                        sequence.Join(translateTransform.DOLocalMove(endLocal, duration).SetEase(profile.moveEase));
                    }
                }
                else
                {
                    Vector3 startWorld = translateTransform.position;
                    Vector3 endWorld = ResolveEndPosition(startWorld, profile, facingSign, channelMoveOverride);

                    if (translateMode == CombatPopupTranslateMode.Arc)
                    {
                        sequence.Join(translateTransform.DOPath(
                            new[]
                            {
                                startWorld,
                                startWorld + arcOffset,
                                endWorld,
                            },
                            duration,
                            PathType.CatmullRom).SetEase(profile.moveEase));
                    }
                    else
                    {
                        sequence.Join(translateTransform.DOMove(endWorld, duration).SetEase(profile.moveEase));
                    }
                }
            }

            if (HasLayer(layers, CombatPopupMotionLayers.Shake))
            {
                Vector3 shakeStrength = new Vector3(
                    MirrorVector(new Vector3(profile.shakeStrength, 0f, 0f), impulseFacing).x,
                    profile.shakeStrength,
                    profile.shakeStrength);

                sequence.Join(impulseTarget.DOShakePosition(
                    duration,
                    shakeStrength,
                    profile.shakeVibrato,
                    profile.shakeRandomness,
                    false,
                    true));
            }

            if (HasLayer(layers, CombatPopupMotionLayers.Punch))
            {
                Vector3 punch = MirrorVector(profile.punchStrength, impulseFacing);
                sequence.Join(impulseTarget.DOPunchPosition(
                    punch,
                    duration,
                    profile.punchVibrato,
                    profile.punchElasticity));
            }
        }

        public static CombatPopupMotionLayers FromLegacyMotionMode(CombatPopupMotionMode motionMode)
        {
            switch (motionMode)
            {
                case CombatPopupMotionMode.FloatUp:
                case CombatPopupMotionMode.FloatDown:
                case CombatPopupMotionMode.WorldOffset:
                case CombatPopupMotionMode.Arc:
                    return CombatPopupMotionLayers.Translate;
                case CombatPopupMotionMode.Punch:
                    return CombatPopupMotionLayers.Punch;
                case CombatPopupMotionMode.Shake:
                    return CombatPopupMotionLayers.Shake;
                default:
                    return CombatPopupMotionLayers.None;
            }
        }

        private static CombatPopupTranslateMode FromLegacyTranslateMode(CombatPopupMotionMode motionMode)
        {
            switch (motionMode)
            {
                case CombatPopupMotionMode.FloatDown:
                    return CombatPopupTranslateMode.FloatDown;
                case CombatPopupMotionMode.WorldOffset:
                    return CombatPopupTranslateMode.WorldOffset;
                case CombatPopupMotionMode.Arc:
                    return CombatPopupTranslateMode.Arc;
                default:
                    return CombatPopupTranslateMode.FloatUp;
            }
        }

        private static bool HasLayer(CombatPopupMotionLayers layers, CombatPopupMotionLayers layer)
        {
            return (layers & layer) == layer;
        }
    }
}
