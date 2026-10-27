using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatWallPresentationPlannerTests
    {
        [Test]
        public void Wall_RaisesOnCastBeat_NotOnPoll()
        {
            Assert.That(CombatWallPresentationPlanner.ShouldPollRaise(true, false, true), Is.False);
            Assert.That(CombatWallPresentationPlanner.ShouldPollRaise(true, false, false), Is.True);
            Assert.That(CombatWallPresentationPlanner.ShouldPollRaise(true, true, false), Is.False);
        }

        [Test]
        public void BlockedHit_SingleAttackerImpact_NoDoublePlay()
        {
            IReadOnlyList<CombatWallPresentationBeat> beats = CombatWallPresentationPlanner.PlanImpact(
                Payload(WallInterceptMode.WallBlocked, activeAfter: true),
                false);

            Assert.That(CombatWallPresentationPlanner.CountAttackerImpacts(beats), Is.EqualTo(1));
            Assert.That(beats.Contains(CombatWallPresentationBeat.AttackerImpactAtWall), Is.True);
            Assert.That(beats.Contains(CombatWallPresentationBeat.AttackerImpactOnBody), Is.False);
        }

        [Test]
        public void MultiHit_BreaksOnce_OnFirstHit()
        {
            IReadOnlyList<CombatWallPresentationBeat> first = CombatWallPresentationPlanner.PlanImpact(
                Payload(WallInterceptMode.WallBrokenThrough, activeAfter: false, expired: true, finalDamage: 20),
                true);
            IReadOnlyList<CombatWallPresentationBeat> second = CombatWallPresentationPlanner.PlanImpact(
                Payload(WallInterceptMode.None, activeAfter: false, finalDamage: 18),
                true);

            int breaks = first.Count(x => x == CombatWallPresentationBeat.WallBreak) +
                         second.Count(x => x == CombatWallPresentationBeat.WallBreak);
            Assert.That(breaks, Is.EqualTo(1));
            Assert.That(CombatWallPresentationPlanner.CountAttackerImpacts(first), Is.EqualTo(1));
            Assert.That(CombatWallPresentationPlanner.CountAttackerImpacts(second), Is.EqualTo(1));
        }

        [Test]
        public void TimerExpire_PlaysExpireOnce_Faded()
        {
            IReadOnlyList<CombatWallPresentationBeat> beats = CombatWallPresentationPlanner.PlanExpire();
            Assert.That(beats.Count(x => x == CombatWallPresentationBeat.WallExpire), Is.EqualTo(1));
            Assert.That(beats.Contains(CombatWallPresentationBeat.WallBreak), Is.False);
            Assert.That(CombatWallPresentationPlanner.ExpirePopupLabel, Is.EqualTo("FADED"));
        }

        [Test]
        public void PriorityHitBeforeCast_ShowsNoWall()
        {
            Assert.That(CombatWallPresentationPlanner.ShouldShowWallForPriorityHit(false), Is.False);
            Assert.That(CombatWallPresentationPlanner.ShouldShowWallForPriorityHit(true), Is.True);
        }

        [Test]
        public void PassiveOverride_RefreshesWallArt()
        {
            Assert.That(CombatWallPresentationPlanner.ShouldRefreshArt("wall_old", "wall_new", true), Is.True);
            Assert.That(CombatWallPresentationPlanner.ShouldRefreshArt("wall_old", "wall_old", true), Is.False);
            Assert.That(CombatWallPresentationPlanner.ShouldRefreshArt("wall_old", "wall_new", false), Is.False);
        }

        [Test]
        public void Bypass_NoWallBeat()
        {
            IReadOnlyList<CombatWallPresentationBeat> beats = CombatWallPresentationPlanner.PlanImpact(
                Payload(WallInterceptMode.Bypass, activeAfter: true, finalDamage: 24),
                true);

            Assert.That(beats.Contains(CombatWallPresentationBeat.AttackerImpactOnBody), Is.True);
            Assert.That(beats.Any(IsWallBeat), Is.False);
        }

        [Test]
        public void WallProfile_ResolvesKindTier_ThenKind_ThenDefault()
        {
            CombatVFXFallbacks asset = ScriptableObject.CreateInstance<CombatVFXFallbacks>();
            try
            {
                var kindDefault = new CombatVFXFallbacks.WallStateRow { kind = BlockKind.Wall, tier = BlockTier.None };
                var exact = new CombatVFXFallbacks.WallStateRow { kind = BlockKind.Wall, tier = BlockTier.Mid };
                SetRows(asset, kindDefault, exact);

                Assert.That(asset.ResolveWallState(BlockKind.Wall, BlockTier.Mid), Is.SameAs(exact));
                Assert.That(asset.ResolveWallState(BlockKind.Wall, BlockTier.Greater), Is.SameAs(kindDefault));
                Assert.That(asset.ResolveWallState(BlockKind.Barrier, BlockTier.Lower), Is.Null,
                    "Null delegates the final fallback to CombatWallVisualProfileSO.");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void WallProfile_GreaterBarrierRow_Warns()
        {
            CombatVFXFallbacks asset = ScriptableObject.CreateInstance<CombatVFXFallbacks>();
            try
            {
                SetRows(asset, new CombatVFXFallbacks.WallStateRow
                {
                    kind = BlockKind.Barrier,
                    tier = BlockTier.Greater,
                });
                var warnings = new List<string>();
                asset.CollectValidationWarnings(warnings);
                Assert.That(warnings.Any(x => x.Contains("Greater Barrier")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Preview_WallBeatsMatchRuntimePlanner()
        {
            IReadOnlyList<CombatWallPresentationBeat> preview = CombatWallPresentationPlanner.PlanPreview(true);
            IReadOnlyList<CombatWallPresentationBeat> runtime = CombatWallPresentationPlanner.PlanImpact(
                Payload(WallInterceptMode.WallBlocked, activeAfter: true),
                false);
            CollectionAssert.AreEqual(runtime, preview);
        }

        private static bool IsWallBeat(CombatWallPresentationBeat beat)
        {
            return beat == CombatWallPresentationBeat.AttackerImpactAtWall ||
                   beat == CombatWallPresentationBeat.WallHit ||
                   beat == CombatWallPresentationBeat.WallBreak ||
                   beat == CombatWallPresentationBeat.WallExpire;
        }

        private static WallInterceptPayload Payload(
            WallInterceptMode mode,
            bool activeAfter,
            bool expired = false,
            int finalDamage = 0)
        {
            return WallInterceptPayload.Create(
                hitIndex: 0,
                totalHits: 1,
                mode: mode,
                wallActiveBeforeHit: mode != WallInterceptMode.None,
                wallActiveAfterHit: activeAfter,
                bypassBarrier: mode == WallInterceptMode.Bypass,
                bypassReason: string.Empty,
                contactType: ContactType.Contact,
                moveCategory: MoveCategory.DamagingMelee,
                mitigationPercent: 0.5f,
                incomingDamage: finalDamage,
                finalHpDamage: finalDamage,
                barrierFullyBlocked: mode == WallInterceptMode.WallBlocked,
                mitigationSourcesExpired: expired ? 1 : 0,
                barriersExpired: 0,
                wallExpiredThisStep: expired,
                partialPenetration: finalDamage > 0,
                wallSourceTag: "wall_test");
        }

        private static void SetRows(CombatVFXFallbacks asset, params CombatVFXFallbacks.WallStateRow[] rows)
        {
            typeof(CombatVFXFallbacks)
                .GetField("wallStates", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(asset, rows);
        }
    }
}
