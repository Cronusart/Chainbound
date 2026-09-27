using System;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum WallInterceptMode
    {
        None,
        WallMitigated,
        WallBlocked,
        WallBrokenThrough,
        Bypass,
        WallHitNoDamage
    }

    [Serializable]
    public sealed class WallInterceptPayload
    {
        public int hitIndex;
        public int totalHits;
        public string interceptMode;
        public bool wallActiveBeforeHit;
        public bool wallActiveAfterHit;
        public bool bypassBarrier;
        public string bypassReason;
        public string contactType;
        public string moveCategory;
        public float mitigationPercent;
        public int incomingDamage;
        public int finalHpDamage;
        public bool barrierFullyBlocked;
        public int mitigationSourcesExpired;
        public int barriersExpired;
        public bool wallExpiredThisStep;
        public bool partialPenetration;
        public string wallSourceTag;
        public int damageAbsorbed;
        public int reflectedDamage;
        public int attackerApDrained;
        public float wallReflectPercent;

        public WallInterceptMode Mode => ParseMode(interceptMode);

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static WallInterceptPayload Create(
            int hitIndex,
            int totalHits,
            WallInterceptMode mode,
            bool wallActiveBeforeHit,
            bool wallActiveAfterHit,
            bool bypassBarrier,
            string bypassReason,
            ContactType contactType,
            MoveCategory moveCategory,
            float mitigationPercent,
            int incomingDamage,
            int finalHpDamage,
            bool barrierFullyBlocked,
            int mitigationSourcesExpired,
            int barriersExpired,
            bool wallExpiredThisStep,
            bool partialPenetration,
            string wallSourceTag,
            int damageAbsorbed = 0,
            int reflectedDamage = 0,
            int attackerApDrained = 0,
            float wallReflectPercent = 0f)
        {
            return new WallInterceptPayload
            {
                hitIndex = hitIndex,
                totalHits = totalHits,
                interceptMode = mode.ToString(),
                wallActiveBeforeHit = wallActiveBeforeHit,
                wallActiveAfterHit = wallActiveAfterHit,
                bypassBarrier = bypassBarrier,
                bypassReason = bypassReason ?? string.Empty,
                contactType = contactType.ToString(),
                moveCategory = moveCategory.ToString(),
                mitigationPercent = Mathf.Max(0f, mitigationPercent),
                incomingDamage = Math.Max(0, incomingDamage),
                finalHpDamage = Math.Max(0, finalHpDamage),
                barrierFullyBlocked = barrierFullyBlocked,
                mitigationSourcesExpired = Math.Max(0, mitigationSourcesExpired),
                barriersExpired = Math.Max(0, barriersExpired),
                wallExpiredThisStep = wallExpiredThisStep,
                partialPenetration = partialPenetration,
                wallSourceTag = wallSourceTag ?? string.Empty,
                damageAbsorbed = Math.Max(0, damageAbsorbed),
                reflectedDamage = Math.Max(0, reflectedDamage),
                attackerApDrained = Math.Max(0, attackerApDrained),
                wallReflectPercent = Mathf.Max(0f, wallReflectPercent),
            };
        }

        public static bool TryFromEvent(CombatEvent evt, out WallInterceptPayload payload)
        {
            payload = null;
            switch (evt.eventType)
            {
                case CombatEventType.DamageDealt:
                case CombatEventType.BarrierBlocked:
                case CombatEventType.WallExpired:
                    return TryParse(evt.metadataJson, out payload);
                default:
                    return false;
            }
        }

        public static bool TryParse(string json, out WallInterceptPayload payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                payload = JsonUtility.FromJson<WallInterceptPayload>(json);
            }
            catch
            {
                payload = null;
            }

            return payload != null && !string.IsNullOrWhiteSpace(payload.interceptMode);
        }

        public static WallInterceptMode ParseMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return WallInterceptMode.None;
            if (Enum.TryParse(value, true, out WallInterceptMode mode))
                return mode;
            return WallInterceptMode.None;
        }

        public bool RoutesAttackToWall()
        {
            switch (Mode)
            {
                case WallInterceptMode.WallMitigated:
                case WallInterceptMode.WallBlocked:
                case WallInterceptMode.WallHitNoDamage:
                    return wallActiveBeforeHit && !bypassBarrier;
                default:
                    return false;
            }
        }

        public bool HasWallOnHitFeedback()
        {
            return reflectedDamage > 0 || attackerApDrained > 0;
        }

        public bool UsesWallForInitialAim()
        {
            switch (Mode)
            {
                case WallInterceptMode.WallMitigated:
                case WallInterceptMode.WallBlocked:
                case WallInterceptMode.WallBrokenThrough:
                case WallInterceptMode.WallHitNoDamage:
                    return wallActiveBeforeHit && !bypassBarrier;
                default:
                    return false;
            }
        }
    }
}
