using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public enum CombatWallPresentationBeat
    {
        AttackerImpactAtWall,
        AttackerImpactOnBody,
        WallHit,
        WallBreak,
        WallExpire,
        BodyHitReaction,
    }

    /// <summary>
    /// Pure presentation policy for wall outcomes. The planner deliberately emits exactly one
    /// attacker impact beat; wall break-through uses a wall impact followed by the body reaction.
    /// </summary>
    public static class CombatWallPresentationPlanner
    {
        public const string ExpirePopupLabel = "FADED";
        private static readonly CombatWallPresentationBeat[] BodyHit =
        {
            CombatWallPresentationBeat.AttackerImpactOnBody,
            CombatWallPresentationBeat.BodyHitReaction,
        };

        private static readonly CombatWallPresentationBeat[] WallHit =
        {
            CombatWallPresentationBeat.AttackerImpactAtWall,
            CombatWallPresentationBeat.WallHit,
        };

        private static readonly CombatWallPresentationBeat[] WallBreakThrough =
        {
            CombatWallPresentationBeat.AttackerImpactAtWall,
            CombatWallPresentationBeat.WallHit,
            CombatWallPresentationBeat.WallBreak,
            CombatWallPresentationBeat.BodyHitReaction,
        };

        private static readonly CombatWallPresentationBeat[] WallExpire =
        {
            CombatWallPresentationBeat.WallExpire,
        };

        public static IReadOnlyList<CombatWallPresentationBeat> PlanImpact(
            WallInterceptPayload payload,
            bool bodyDamageApplied)
        {
            if (payload == null || payload.Mode == WallInterceptMode.None || payload.Mode == WallInterceptMode.Bypass)
                return bodyDamageApplied ? BodyHit : Array.Empty<CombatWallPresentationBeat>();

            switch (payload.Mode)
            {
                case WallInterceptMode.WallBrokenThrough:
                    return WallBreakThrough;

                case WallInterceptMode.WallMitigated:
                    if (!payload.wallActiveAfterHit || payload.wallExpiredThisStep)
                    {
                        return bodyDamageApplied
                            ? WallBreakThrough
                            : new[]
                            {
                                CombatWallPresentationBeat.AttackerImpactAtWall,
                                CombatWallPresentationBeat.WallHit,
                                CombatWallPresentationBeat.WallBreak,
                            };
                    }
                    if (bodyDamageApplied)
                    {
                        return new[]
                        {
                            CombatWallPresentationBeat.AttackerImpactAtWall,
                            CombatWallPresentationBeat.WallHit,
                            CombatWallPresentationBeat.BodyHitReaction,
                        };
                    }
                    return WallHit;

                case WallInterceptMode.WallBlocked:
                case WallInterceptMode.WallHitNoDamage:
                    if (!payload.wallActiveAfterHit || payload.wallExpiredThisStep)
                    {
                        return new[]
                        {
                            CombatWallPresentationBeat.AttackerImpactAtWall,
                            CombatWallPresentationBeat.WallHit,
                            CombatWallPresentationBeat.WallBreak,
                        };
                    }
                    return WallHit;

                default:
                    return bodyDamageApplied ? BodyHit : Array.Empty<CombatWallPresentationBeat>();
            }
        }

        public static IReadOnlyList<CombatWallPresentationBeat> PlanExpire()
        {
            return WallExpire;
        }

        public static IReadOnlyList<CombatWallPresentationBeat> PlanPreview(bool defenderHasWall)
        {
            if (!defenderHasWall)
                return PlanImpact(null, true);

            var payload = WallInterceptPayload.Create(
                hitIndex: 0,
                totalHits: 1,
                mode: WallInterceptMode.WallBlocked,
                wallActiveBeforeHit: true,
                wallActiveAfterHit: true,
                bypassBarrier: false,
                bypassReason: string.Empty,
                contactType: ContactType.None,
                moveCategory: MoveCategory.Support,
                mitigationPercent: 0f,
                incomingDamage: 0,
                finalHpDamage: 0,
                barrierFullyBlocked: true,
                mitigationSourcesExpired: 0,
                barriersExpired: 0,
                wallExpiredThisStep: false,
                partialPenetration: false,
                wallSourceTag: "preview_wall");
            return PlanImpact(payload, false);
        }

        public static bool ShouldShowWallForPriorityHit(bool wallActiveAtTurnStart)
        {
            return wallActiveAtTurnStart;
        }

        public static bool Contains(IReadOnlyList<CombatWallPresentationBeat> beats, CombatWallPresentationBeat beat)
        {
            if (beats == null)
                return false;
            for (int i = 0; i < beats.Count; i++)
            {
                if (beats[i] == beat)
                    return true;
            }
            return false;
        }

        public static int CountAttackerImpacts(IReadOnlyList<CombatWallPresentationBeat> beats)
        {
            int count = 0;
            if (beats == null)
                return count;
            for (int i = 0; i < beats.Count; i++)
            {
                if (beats[i] == CombatWallPresentationBeat.AttackerImpactAtWall ||
                    beats[i] == CombatWallPresentationBeat.AttackerImpactOnBody)
                    count++;
            }
            return count;
        }

        public static bool ShouldPollRaise(bool hasActiveWall, bool wasActive, bool pendingMoveUsedForTarget)
        {
            return hasActiveWall && !wasActive && !pendingMoveUsedForTarget;
        }

        public static bool ShouldRefreshArt(string previousSourceTag, string currentSourceTag, bool hasActiveWall)
        {
            return hasActiveWall && !string.Equals(
                Normalize(previousSourceTag),
                Normalize(currentSourceTag),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
