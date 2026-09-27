using System.Collections.Generic;
using Chainbound.Combat;
using Chainbound.Combat.Core;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class CaptureCoreTests
    {
        [TestCase(70, CaptureVolatilityProfile.Calm)]
        [TestCase(69, CaptureVolatilityProfile.Standard)]
        [TestCase(40, CaptureVolatilityProfile.Standard)]
        [TestCase(39, CaptureVolatilityProfile.Volatile)]
        [TestCase(20, CaptureVolatilityProfile.Volatile)]
        [TestCase(19, CaptureVolatilityProfile.Extreme)]
        public void LegacyRateMapsToProfileInsteadOfProbability(int rate, CaptureVolatilityProfile expected)
        {
            Assert.AreEqual(expected, CaptureChallengeResolver.MapLegacyBaseRate(rate));
        }

        [TestCase(-10, 1)]
        [TestCase(-4, 1)]
        [TestCase(-3, 2)]
        [TestCase(-2, 2)]
        [TestCase(-1, 3)]
        [TestCase(1, 3)]
        [TestCase(2, 4)]
        [TestCase(3, 4)]
        [TestCase(4, 5)]
        [TestCase(10, 5)]
        public void IvGradeBoundariesAreStable(int iv, int expectedGrade)
        {
            Assert.AreEqual(expectedGrade, CaptureChallengeResolver.GradeIv(iv));
        }

        [Test]
        public void ScannerHigherRatingIsDeterministicSupersetAndNeverIncludesMaxHp()
        {
            CaptureTargetContext target = Target();
            CaptureChallengeSnapshot one = Resolve(target, new CaptureEffectVector(1, 0, 0, 0, 0));
            CaptureChallengeSnapshot five = Resolve(target, new CaptureEffectVector(5, 0, 0, 0, 0));

            Assert.AreEqual(1, one.ScannerIntel.Count);
            Assert.AreEqual(5, five.ScannerIntel.Count);
            Assert.AreEqual(one.ScannerIntel[0].Stat, five.ScannerIntel[0].Stat);
            Assert.IsTrue(one.LowHpMarkerRevealed);

            var unique = new HashSet<CaptureIvStat>();
            for (int i = 0; i < five.ScannerIntel.Count; i++)
                Assert.IsTrue(unique.Add(five.ScannerIntel[i].Stat));
        }

        [Test]
        public void GearOrbAndDifferentBoostTiersStackLinearly()
        {
            CaptureEffectVector gear = new CaptureEffectVector(3, 1500, 1500, 0, 0);
            CaptureEffectVector orb = new CaptureEffectVector(0, 0, 2500, -1500, 0);
            CaptureEffectVector boosts = new CaptureEffectVector(0, 0, 3000, 2000, 0);
            CaptureEffectVector total = gear.Add(orb).Add(boosts);

            Assert.AreEqual(3, total.ScanRating);
            Assert.AreEqual(1500, total.StabilityBasisPoints);
            Assert.AreEqual(7000, total.BandWidthBasisPoints);
            Assert.AreEqual(500, total.LockRateBasisPoints);
        }

        [Test]
        public void LowHpThresholdHasNoResistanceButFullHpAppliesConfiguredResistance()
        {
            CaptureTargetContext low = Target();
            low.CurrentHp = 15;
            CaptureTargetContext full = Target();
            full.CurrentHp = 100;

            CaptureChallengeSnapshot lowChallenge = Resolve(low, CaptureEffectVector.None);
            CaptureChallengeSnapshot fullChallenge = Resolve(full, CaptureEffectVector.None);

            Assert.AreEqual(600, lowChallenge.Parameters.DurationTicks);
            Assert.AreEqual(450, fullChallenge.Parameters.DurationTicks);
            Assert.Greater(fullChallenge.Parameters.TraceMaximumSpeedUnitsPerSecond, lowChallenge.Parameters.TraceMaximumSpeedUnitsPerSecond);
            Assert.Greater(fullChallenge.Parameters.TraceAccelerationUnitsPerSecondSquared, lowChallenge.Parameters.TraceAccelerationUnitsPerSecondSquared);
        }

        [Test]
        public void AlertnessRaisesMotionButDoesNotShortenFutureTimer()
        {
            CaptureTargetContext calm = Target();
            calm.Alertness = 0;
            CaptureTargetContext alerted = Target();
            alerted.Alertness = 2;

            CaptureChallengeSnapshot a = Resolve(calm, CaptureEffectVector.None);
            CaptureChallengeSnapshot b = Resolve(alerted, CaptureEffectVector.None);

            Assert.AreEqual(a.Parameters.DurationTicks, b.Parameters.DurationTicks);
            Assert.Greater(b.Parameters.TraceMaximumSpeedUnitsPerSecond, a.Parameters.TraceMaximumSpeedUnitsPerSecond);
            Assert.Greater(b.Parameters.SpikeVelocityImpulseUnitsPerSecond, a.Parameters.SpikeVelocityImpulseUnitsPerSecond);
        }

        [Test]
        public void PositiveIvCaptureBonusCapsAtFivePerNonHpStat()
        {
            CaptureTargetContext zero = Target();
            zero.Ivs = new CaptureIvValues();
            CaptureTargetContext maximum = Target();
            maximum.Ivs = new CaptureIvValues
            {
                MeleeAttack = 5,
                MeleeDefense = 5,
                RangedAttack = 5,
                RangedDefense = 5,
                Speed = 5,
            };
            CaptureTargetContext oversized = Target();
            oversized.Ivs = new CaptureIvValues
            {
                MeleeAttack = 50,
                MeleeDefense = 50,
                RangedAttack = 50,
                RangedDefense = 50,
                Speed = 50,
            };

            CaptureChallengeSnapshot baseline = Resolve(zero, CaptureEffectVector.None);
            CaptureChallengeSnapshot capped = Resolve(maximum, CaptureEffectVector.None);
            CaptureChallengeSnapshot clamped = Resolve(oversized, CaptureEffectVector.None);

            Assert.Greater(
                capped.Parameters.TraceMaximumSpeedUnitsPerSecond,
                baseline.Parameters.TraceMaximumSpeedUnitsPerSecond);
            Assert.AreEqual(
                capped.Parameters.TraceMaximumSpeedUnitsPerSecond,
                clamped.Parameters.TraceMaximumSpeedUnitsPerSecond);
        }

        [Test]
        public void OrbProfilesApplyTheirFixedTradeoffsWithoutProbability()
        {
            CaptureTargetContext target = Target();
            CaptureChallengeSnapshot baseline = ResolveWithOrb(
                target,
                new CaptureOrbProfile { OrbId = "signal_orb", Effects = CaptureEffectVector.None });
            CaptureChallengeSnapshot lattice = ResolveWithOrb(
                target,
                new CaptureOrbProfile
                {
                    OrbId = "lattice_orb",
                    Effects = new CaptureEffectVector(0, 0, 2500, -1500, 0),
                });
            CaptureChallengeSnapshot quick = ResolveWithOrb(
                target,
                new CaptureOrbProfile
                {
                    OrbId = "quick_orb",
                    Effects = new CaptureEffectVector(0, 0, -2000, 3000, 0),
                });
            CaptureChallengeSnapshot entangle = ResolveWithOrb(
                target,
                new CaptureOrbProfile
                {
                    OrbId = "entangle_orb",
                    Effects = new CaptureEffectVector(0, 0, -2000, 0, -2500),
                    EntanglesOnSuccess = true,
                });

            Assert.Greater(lattice.Parameters.BandHeightUnits, baseline.Parameters.BandHeightUnits);
            Assert.Less(lattice.Parameters.LockRateBasisPointsPerSecond, baseline.Parameters.LockRateBasisPointsPerSecond);
            Assert.Less(quick.Parameters.BandHeightUnits, baseline.Parameters.BandHeightUnits);
            Assert.Greater(quick.Parameters.LockRateBasisPointsPerSecond, baseline.Parameters.LockRateBasisPointsPerSecond);
            Assert.Less(entangle.Parameters.DurationTicks, baseline.Parameters.DurationTicks);
        }

        [Test]
        public void InvalidBalanceValuesNormalizeToTechnicalSafetyBounds()
        {
            var invalid = new CaptureBalanceData
            {
                TickRate = -30,
                StandardTimerMilliseconds = int.MaxValue,
                MaximumAttemptMilliseconds = -1,
                BaseBandHeightBasisPoints = int.MaxValue,
                LockMovementBasisPointsPerSecond = -1,
                EscapeMovementBasisPointsPerSecond = -1,
                BandMaximumSpeedUnitsPerSecond = 0,
                TraceMaximumSpeedUnitsPerSecond = 0,
                SpikeChanceBasisPointsPerTick = int.MaxValue,
            };

            CaptureBalanceData normalized = invalid.Normalized();
            Assert.AreEqual(1, normalized.TickRate);
            Assert.AreEqual(1, normalized.MaximumAttemptMilliseconds);
            Assert.AreEqual(1, normalized.StandardTimerMilliseconds);
            Assert.AreEqual(10000, normalized.BaseBandHeightBasisPoints);
            Assert.AreEqual(0, normalized.LockMovementBasisPointsPerSecond);
            Assert.AreEqual(0, normalized.EscapeMovementBasisPointsPerSecond);
            Assert.AreEqual(1, normalized.BandMaximumSpeedUnitsPerSecond);
            Assert.AreEqual(1, normalized.TraceMaximumSpeedUnitsPerSecond);
            Assert.AreEqual(10000, normalized.SpikeChanceBasisPointsPerTick);
        }

        [Test]
        public void SameSeedAndInputStreamProduceGoldenIdenticalState()
        {
            CaptureChallengeSnapshot challenge = Resolve(Target(), CaptureEffectVector.None);
            CaptureSimulationState left = CaptureSimulation.CreateInitial(challenge);
            CaptureSimulationState right = CaptureSimulation.CreateInitial(challenge);

            for (int tick = 0; tick < 180 && left.Outcome == CaptureOutcomeKind.None; tick++)
            {
                bool held = (tick / 17) % 2 == 0;
                left = CaptureSimulation.Step(left, challenge.Parameters, held).State;
                right = CaptureSimulation.Step(right, challenge.Parameters, held).State;
                AssertStateEqual(left, right);
            }

            Assert.AreEqual(180, left.Tick);
            Assert.AreEqual(2124024666u, left.RngState);
            Assert.AreEqual(6705, left.TargetPositionUnits);
            Assert.AreEqual(-593, left.TargetVelocityUnitsPerSecond);
            Assert.AreEqual(9986, left.BandPositionUnits);
            Assert.AreEqual(-121, left.BandVelocityUnitsPerSecond);
            Assert.AreEqual(4847, left.MeterBasisPoints);
            Assert.AreEqual(-20, left.MeterRemainder);
            Assert.IsFalse(left.IsInsideBand);
            Assert.AreEqual(CaptureOutcomeKind.None, left.Outcome);
        }

        [Test]
        public void ReenteringBandDropsOppositeRemainderAndAppliesLockOnTheNextTick()
        {
            var parameters = new CaptureSimulationParameters
            {
                TickRate = 30,
                DurationTicks = 100,
                GraphUnits = 10000,
                BandHeightUnits = 4000,
                LockRateBasisPointsPerSecond = 30,
                EscapeRateBasisPointsPerSecond = 800,
                BandLiftAccelerationUnitsPerSecondSquared = 0,
                BandGravityUnitsPerSecondSquared = 0,
                BandMaximumSpeedUnitsPerSecond = 1,
                BandEdgeBounceBasisPoints = 0,
                TraceAccelerationUnitsPerSecondSquared = 0,
                TraceMaximumSpeedUnitsPerSecond = 1,
                TraceMeanReversionUnitsPerSecondSquared = 0,
                TraceEdgeBounceBasisPoints = 0,
            };
            var state = new CaptureSimulationState
            {
                TargetPositionUnits = 5000,
                BandPositionUnits = 5000,
                MeterBasisPoints = 5000,
                MeterRemainder = -29,
                IsInsideBand = false,
            };

            CaptureSimulationState next = CaptureSimulation.Step(state, parameters, false).State;

            Assert.IsTrue(next.IsInsideBand);
            Assert.AreEqual(0, next.MeterRemainder);
            Assert.Greater(next.MeterBasisPoints, 5000);
        }

        [Test]
        public void EmptyMeterAndLockRemoveTheTargetButTimeoutDoesNot()
        {
            Assert.IsFalse(new CaptureOutcome { Kind = CaptureOutcomeKind.TimedOut }.RemovesTargetFromBattle);
            Assert.IsTrue(new CaptureOutcome { Kind = CaptureOutcomeKind.Escaped }.RemovesTargetFromBattle);
            Assert.IsTrue(new CaptureOutcome { Kind = CaptureOutcomeKind.Locked }.RemovesTargetFromBattle);
            Assert.IsFalse(new CaptureOutcome { Kind = CaptureOutcomeKind.Interrupted }.RemovesTargetFromBattle);
            Assert.IsFalse(new CaptureOutcome { Kind = CaptureOutcomeKind.Rejected }.RemovesTargetFromBattle);
        }

        [Test]
        public void FullBandReachesLockWithoutSecondaryProbabilityRoll()
        {
            var parameters = new CaptureSimulationParameters
            {
                TickRate = 10,
                DurationTicks = 100,
                GraphUnits = 10000,
                BandHeightUnits = 10000,
                LockRateBasisPointsPerSecond = 5000,
                EscapeRateBasisPointsPerSecond = 1000,
                BandMaximumSpeedUnitsPerSecond = 1,
                TraceMaximumSpeedUnitsPerSecond = 1,
            };
            var challenge = new CaptureChallengeSnapshot { Seed = 123u, Parameters = parameters };
            CaptureSimulationState state = CaptureSimulation.CreateInitial(challenge);

            for (int i = 0; i < 20 && state.Outcome == CaptureOutcomeKind.None; i++)
                state = CaptureSimulation.Step(state, parameters, false).State;

            Assert.AreEqual(CaptureOutcomeKind.Locked, state.Outcome);
            Assert.AreEqual(10000, state.MeterBasisPoints);
        }

        [Test]
        public void TimerExpiresWithoutPrefillingEscape()
        {
            var parameters = new CaptureSimulationParameters
            {
                TickRate = 30,
                DurationTicks = 1,
                GraphUnits = 10000,
                BandHeightUnits = 1,
                LockRateBasisPointsPerSecond = 0,
                EscapeRateBasisPointsPerSecond = 0,
                BandMaximumSpeedUnitsPerSecond = 1,
                TraceMaximumSpeedUnitsPerSecond = 1,
            };
            var challenge = new CaptureChallengeSnapshot { Seed = 42u, Parameters = parameters };
            CaptureSimulationState state = CaptureSimulation.CreateInitial(challenge);
            state = CaptureSimulation.Step(state, parameters, false).State;

            Assert.AreEqual(CaptureOutcomeKind.TimedOut, state.Outcome);
            Assert.AreEqual(5000, state.MeterBasisPoints);
        }

        [Test]
        public void LockMeterPitchRisesFromEscapeToLock()
        {
            Assert.AreEqual(0.72f, CaptureLockMeterPlayback.PitchForMeter(0, 0.72f, 1.35f), 0.001f);
            Assert.AreEqual(1.35f, CaptureLockMeterPlayback.PitchForMeter(10000, 0.72f, 1.35f), 0.001f);
            Assert.Greater(
                CaptureLockMeterPlayback.PitchForMeter(8000, 0.72f, 1.35f),
                CaptureLockMeterPlayback.PitchForMeter(2000, 0.72f, 1.35f));
        }

        [Test]
        public void SpeciesCaptureProfileOverridesLegacyRateMapper()
        {
            CaptureTargetContext legacyCalm = Target();
            legacyCalm.LegacyBaseCaptureRate = 70;
            legacyCalm.HasSpeciesCaptureProfile = false;

            CaptureTargetContext speciesExtreme = Target();
            speciesExtreme.LegacyBaseCaptureRate = 70;
            speciesExtreme.HasSpeciesCaptureProfile = true;
            speciesExtreme.SpeciesCaptureProfile = CaptureVolatilityProfile.Extreme;

            CaptureChallengeSnapshot calm = Resolve(legacyCalm, CaptureEffectVector.None);
            CaptureChallengeSnapshot extreme = Resolve(speciesExtreme, CaptureEffectVector.None);

            Assert.AreEqual(CaptureVolatilityProfile.Calm, calm.Difficulty);
            Assert.AreEqual(CaptureVolatilityProfile.Extreme, extreme.Difficulty);
            Assert.Greater(extreme.Parameters.TraceMaximumSpeedUnitsPerSecond, calm.Parameters.TraceMaximumSpeedUnitsPerSecond);
            Assert.Less(extreme.Parameters.DurationTicks, calm.Parameters.DurationTicks);
        }

        [Test]
        public void MutatedTraitAddsMotionAndSpikeChanceWithoutChangingTimer()
        {
            CaptureTargetContext plain = Target();
            CaptureTargetContext mutated = Target();
            mutated.IsMutated = true;

            CaptureChallengeSnapshot a = Resolve(plain, CaptureEffectVector.None);
            CaptureChallengeSnapshot b = Resolve(mutated, CaptureEffectVector.None);

            Assert.AreEqual(a.Parameters.DurationTicks, b.Parameters.DurationTicks);
            Assert.Greater(b.Parameters.TraceMaximumSpeedUnitsPerSecond, a.Parameters.TraceMaximumSpeedUnitsPerSecond);
            Assert.Greater(b.Parameters.SpikeChanceBasisPointsPerTick, a.Parameters.SpikeChanceBasisPointsPerTick);
            Assert.Greater(b.Parameters.SpikeVelocityImpulseUnitsPerSecond, a.Parameters.SpikeVelocityImpulseUnitsPerSecond);
        }

        [Test]
        public void HpCliffAppliesImmediatelyAboveLowHpThreshold()
        {
            CaptureTargetContext atFloor = Target();
            atFloor.CurrentHp = 15;
            CaptureTargetContext justAbove = Target();
            justAbove.CurrentHp = 16;

            CaptureChallengeSnapshot low = Resolve(atFloor, CaptureEffectVector.None);
            CaptureChallengeSnapshot cliff = Resolve(justAbove, CaptureEffectVector.None);

            Assert.IsTrue(low.IsLowHp);
            Assert.IsFalse(cliff.IsLowHp);
            Assert.AreEqual(600, low.Parameters.DurationTicks);
            Assert.Less(cliff.Parameters.DurationTicks, low.Parameters.DurationTicks);
            Assert.Greater(cliff.Parameters.TraceMaximumSpeedUnitsPerSecond, low.Parameters.TraceMaximumSpeedUnitsPerSecond);
        }

        [Test]
        public void PolicyDefaultsToLivingActiveWildTargetsOnly()
        {
            Assert.IsTrue(CaptureChallengeResolver.IsCaptureAllowed(
                AuthoritativeBattleKind.Wild,
                EncounterCapturePolicy.UseEncounterDefault,
                EncounterCapturePolicy.UseEncounterDefault,
                true,
                true,
                true));
            Assert.IsFalse(CaptureChallengeResolver.IsCaptureAllowed(
                AuthoritativeBattleKind.Boss,
                EncounterCapturePolicy.UseEncounterDefault,
                EncounterCapturePolicy.UseEncounterDefault,
                true,
                true,
                true));
            Assert.IsFalse(CaptureChallengeResolver.IsCaptureAllowed(
                AuthoritativeBattleKind.Wild,
                EncounterCapturePolicy.UseEncounterDefault,
                EncounterCapturePolicy.UseEncounterDefault,
                true,
                false,
                true));
        }

        private static CaptureChallengeSnapshot Resolve(CaptureTargetContext target, CaptureEffectVector equipment)
        {
            return CaptureChallengeResolver.Resolve(
                "attempt",
                "battle",
                0x12345678u,
                123456789L,
                "hash",
                target,
                null,
                new CaptureOrbProfile { OrbId = "signal_orb", Effects = CaptureEffectVector.None },
                equipment,
                CaptureEffectVector.None,
                CaptureBalanceData.CreateDefault());
        }

        private static CaptureChallengeSnapshot ResolveWithOrb(CaptureTargetContext target, CaptureOrbProfile orb)
        {
            return CaptureChallengeResolver.Resolve(
                "attempt",
                "battle",
                0x12345678u,
                123456789L,
                "hash",
                target,
                null,
                orb,
                CaptureEffectVector.None,
                CaptureEffectVector.None,
                CaptureBalanceData.CreateDefault());
        }

        private static CaptureTargetContext Target()
        {
            return new CaptureTargetContext
            {
                TargetId = "target-1",
                SpeciesId = "species-1",
                CurrentHp = 15,
                MaxHp = 100,
                LegacyBaseCaptureRate = 45,
                Ivs = new CaptureIvValues
                {
                    MeleeAttack = -10,
                    MeleeDefense = -3,
                    RangedAttack = 0,
                    RangedDefense = 6,
                    Speed = 10,
                },
            };
        }

        private static void AssertStateEqual(CaptureSimulationState expected, CaptureSimulationState actual)
        {
            Assert.AreEqual(expected.Tick, actual.Tick);
            Assert.AreEqual(expected.RngState, actual.RngState);
            Assert.AreEqual(expected.TargetPositionUnits, actual.TargetPositionUnits);
            Assert.AreEqual(expected.TargetVelocityUnitsPerSecond, actual.TargetVelocityUnitsPerSecond);
            Assert.AreEqual(expected.BandPositionUnits, actual.BandPositionUnits);
            Assert.AreEqual(expected.BandVelocityUnitsPerSecond, actual.BandVelocityUnitsPerSecond);
            Assert.AreEqual(expected.MeterBasisPoints, actual.MeterBasisPoints);
            Assert.AreEqual(expected.MeterRemainder, actual.MeterRemainder);
            Assert.AreEqual(expected.IsInsideBand, actual.IsInsideBand);
            Assert.AreEqual(expected.Outcome, actual.Outcome);
        }
    }
}
