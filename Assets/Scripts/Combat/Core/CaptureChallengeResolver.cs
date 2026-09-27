using System;
using System.Collections.Generic;

namespace Chainbound.Combat.Core
{
    public static class CaptureChallengeResolver
    {
        private const int IvMinimum = -5;
        private const int IvMaximum = 5;
        private const int PositiveIvStatCount = 5;

        private static readonly CaptureIvStat[] ScannerStats =
        {
            CaptureIvStat.MeleeAttack,
            CaptureIvStat.MeleeDefense,
            CaptureIvStat.RangedAttack,
            CaptureIvStat.RangedDefense,
            CaptureIvStat.Speed,
        };

        /// <summary>
        /// Maps encounter <c>baseCaptureRate</c> (legacy 0–100) onto a volatility kit.
        /// This is not a catch probability. ≥70 Calm, ≥40 Standard, ≥20 Volatile, else Extreme.
        /// </summary>
        public static CaptureVolatilityProfile MapLegacyBaseRate(int legacyBaseCaptureRate)
        {
            if (legacyBaseCaptureRate >= 70) return CaptureVolatilityProfile.Calm;
            if (legacyBaseCaptureRate >= 40) return CaptureVolatilityProfile.Standard;
            if (legacyBaseCaptureRate >= 20) return CaptureVolatilityProfile.Volatile;
            return CaptureVolatilityProfile.Extreme;
        }

        public static CaptureChallengeSnapshot Resolve(
            string attemptId,
            string battleId,
            uint seed,
            long deadlineUnixMilliseconds,
            string configHash,
            CaptureTargetContext target,
            CaptureMotionProfile assignedProfile,
            CaptureOrbProfile orb,
            CaptureEffectVector equipmentEffects,
            CaptureEffectVector boostEffects,
            CaptureBalanceData balance)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (orb == null) throw new ArgumentNullException(nameof(orb));

            CaptureBalanceData rules = (balance ?? CaptureBalanceData.CreateDefault()).Normalized();
            CaptureVolatilityProfile difficulty = ResolveDifficulty(target, assignedProfile);
            CaptureMotionProfile profile = assignedProfile ?? rules.ResolveProfile(difficulty);
            CaptureEffectVector effective = equipmentEffects.Add(boostEffects).Add(orb.Effects);

            int maxHp = Math.Max(1, target.MaxHp);
            int currentHp = CaptureBalanceData.Clamp(target.CurrentHp, 0, maxHp);
            int hpBasisPoints = ScaleRatio(currentHp, maxHp);
            int hpResistanceBasisPoints = CalculateHpResistanceBasisPoints(hpBasisPoints, rules);
            int ivDifficultyBasisPoints = CalculatePositiveIvDifficultyBasisPoints(target.Ivs, rules);
            int traitMotionBasisPoints = CalculateTraitMotionBasisPoints(target, rules);
            int traitSpikeChanceBasisPoints = ScaleBasisPoints(
                traitMotionBasisPoints,
                rules.TraitSpikeChancePerMotionBasisPoints);
            int alertness = Math.Max(0, target.Alertness);

            int velocityMultiplier = SaturatingAdd(
                profile.MotionMultiplierBasisPoints,
                ScaleBasisPoints(rules.FullHpVelocityBonusBasisPoints, hpResistanceBasisPoints),
                ivDifficultyBasisPoints,
                traitMotionBasisPoints,
                SaturatingMultiply(alertness, rules.AlertnessVelocityBasisPoints),
                -effective.StabilityBasisPoints);
            int accelerationMultiplier = SaturatingAdd(
                profile.MotionMultiplierBasisPoints,
                ScaleBasisPoints(rules.FullHpAccelerationBonusBasisPoints, hpResistanceBasisPoints),
                ivDifficultyBasisPoints,
                traitMotionBasisPoints,
                SaturatingMultiply(alertness, rules.AlertnessAccelerationBasisPoints),
                -effective.StabilityBasisPoints);
            int timerMultiplier = SaturatingAdd(
                profile.TimerMultiplierBasisPoints,
                -ScaleBasisPoints(rules.FullHpTimerPenaltyBasisPoints, hpResistanceBasisPoints),
                effective.CaptureTimeBasisPoints);
            int spikeAmplitudeMultiplier = SaturatingAdd(
                profile.MotionMultiplierBasisPoints,
                traitMotionBasisPoints,
                SaturatingMultiply(alertness, rules.AlertnessSpikeAmplitudeBasisPoints),
                -effective.StabilityBasisPoints);

            velocityMultiplier = Math.Max(0, velocityMultiplier);
            accelerationMultiplier = Math.Max(0, accelerationMultiplier);
            timerMultiplier = Math.Max(1, timerMultiplier);
            spikeAmplitudeMultiplier = Math.Max(0, spikeAmplitudeMultiplier);

            int durationMilliseconds = ClampLongToInt(
                (long)rules.StandardTimerMilliseconds * timerMultiplier / CaptureBalanceData.BasisPointScale,
                1,
                rules.MaximumAttemptMilliseconds);
            int durationTicks = Math.Max(1, (durationMilliseconds * rules.TickRate + 999) / 1000);
            int bandHeight = ClampLongToInt(
                (long)CaptureBalanceData.GraphScale * rules.BaseBandHeightBasisPoints / CaptureBalanceData.BasisPointScale *
                Math.Max(0, CaptureBalanceData.BasisPointScale + effective.BandWidthBasisPoints) /
                CaptureBalanceData.BasisPointScale,
                1,
                CaptureBalanceData.GraphScale);
            int lockRate = ClampLongToInt(
                (long)rules.LockMovementBasisPointsPerSecond *
                Math.Max(0, CaptureBalanceData.BasisPointScale + effective.LockRateBasisPoints) /
                CaptureBalanceData.BasisPointScale,
                0,
                int.MaxValue);

            var parameters = new CaptureSimulationParameters
            {
                TickRate = rules.TickRate,
                DurationTicks = durationTicks,
                GraphUnits = CaptureBalanceData.GraphScale,
                BandHeightUnits = bandHeight,
                LockRateBasisPointsPerSecond = lockRate,
                EscapeRateBasisPointsPerSecond = rules.EscapeMovementBasisPointsPerSecond,
                BandLiftAccelerationUnitsPerSecondSquared = rules.BandLiftAccelerationUnitsPerSecondSquared,
                BandGravityUnitsPerSecondSquared = rules.BandGravityUnitsPerSecondSquared,
                BandDragBasisPointsPerSecond = rules.BandDragBasisPointsPerSecond,
                BandMaximumSpeedUnitsPerSecond = rules.BandMaximumSpeedUnitsPerSecond,
                BandEdgeBounceBasisPoints = rules.BandEdgeBounceBasisPoints,
                TraceAccelerationUnitsPerSecondSquared = ScaleByMultiplier(rules.TraceAccelerationUnitsPerSecondSquared, accelerationMultiplier),
                TraceMaximumSpeedUnitsPerSecond = Math.Max(1, ScaleByMultiplier(rules.TraceMaximumSpeedUnitsPerSecond, velocityMultiplier)),
                TraceMeanReversionUnitsPerSecondSquared = ScaleByMultiplier(rules.TraceMeanReversionUnitsPerSecondSquared, accelerationMultiplier),
                TraceEdgeBounceBasisPoints = rules.TraceEdgeBounceBasisPoints,
                SpikeChanceBasisPointsPerTick = CaptureBalanceData.Clamp(
                    ScaleByMultiplier(
                        rules.SpikeChanceBasisPointsPerTick,
                        SaturatingAdd(profile.SpikeChanceMultiplierBasisPoints, traitSpikeChanceBasisPoints)),
                    0,
                    CaptureBalanceData.BasisPointScale),
                SpikeVelocityImpulseUnitsPerSecond = ScaleByMultiplier(rules.SpikeVelocityImpulseUnitsPerSecond, spikeAmplitudeMultiplier),
            };

            int scanRating = CaptureBalanceData.Clamp(effective.ScanRating, 0, ScannerStats.Length);
            return new CaptureChallengeSnapshot
            {
                AttemptId = attemptId ?? string.Empty,
                BattleId = battleId ?? string.Empty,
                Seed = seed,
                DeadlineUnixMilliseconds = deadlineUnixMilliseconds,
                TargetId = target.TargetId ?? string.Empty,
                SpeciesId = target.SpeciesId ?? string.Empty,
                ProfileId = string.IsNullOrWhiteSpace(profile.ProfileId) ? difficulty.ToString().ToLowerInvariant() : profile.ProfileId,
                OrbId = orb.OrbId ?? string.Empty,
                ConfigHash = configHash ?? string.Empty,
                Difficulty = difficulty,
                EffectiveEffects = effective,
                Parameters = parameters,
                LowHpMarkerRevealed = scanRating > 0,
                IsLowHp = hpBasisPoints <= rules.LowHpThresholdBasisPoints,
                ScannerIntel = ResolveScannerIntel(seed, target.TargetId ?? string.Empty, target.Ivs, scanRating),
            };
        }

        public static int GradeIv(int iv)
        {
            int value = CaptureBalanceData.Clamp(iv, IvMinimum, IvMaximum);
            if (value <= -4) return 1;
            if (value <= -2) return 2;
            if (value <= 1) return 3;
            if (value <= 3) return 4;
            return 5;
        }

        public static bool IsCaptureAllowed(
            AuthoritativeBattleKind battleKind,
            EncounterCapturePolicy encounterPolicy,
            EncounterCapturePolicy runtimeOverride,
            bool encounterCaptureEnabled,
            bool targetIsActive,
            bool targetIsAlive)
        {
            EncounterCapturePolicy resolved = runtimeOverride != EncounterCapturePolicy.UseEncounterDefault
                ? runtimeOverride
                : encounterPolicy;
            if (resolved == EncounterCapturePolicy.Disabled)
                return false;
            if (!targetIsActive || !targetIsAlive)
                return false;
            if (resolved == EncounterCapturePolicy.Enabled)
                return battleKind != AuthoritativeBattleKind.PvP;
            return encounterCaptureEnabled && battleKind == AuthoritativeBattleKind.Wild;
        }

        private static CaptureVolatilityProfile ResolveDifficulty(
            CaptureTargetContext target,
            CaptureMotionProfile assignedProfile)
        {
            if (assignedProfile != null)
                return assignedProfile.Difficulty;
            if (target != null && target.HasSpeciesCaptureProfile)
            {
                int rank = (int)target.SpeciesCaptureProfile;
                if (rank >= 0 && rank <= (int)CaptureVolatilityProfile.Extreme)
                    return target.SpeciesCaptureProfile;
            }

            return MapLegacyBaseRate(target != null ? target.LegacyBaseCaptureRate : 45);
        }

        private static int CalculateHpResistanceBasisPoints(int hpBasisPoints, CaptureBalanceData balance)
        {
            if (hpBasisPoints <= balance.LowHpThresholdBasisPoints)
                return 0;

            double numerator = hpBasisPoints - balance.LowHpThresholdBasisPoints;
            double denominator = CaptureBalanceData.BasisPointScale - balance.LowHpThresholdBasisPoints;
            double normalized = denominator <= 0 ? 1d : numerator / denominator;
            if (normalized < 0d)
                normalized = 0d;
            if (normalized > 1d)
                normalized = 1d;
            double curve = balance.HpResistanceCurveMilli / 1000d;
            double ramp = Math.Pow(normalized, curve);
            double floor = balance.HpAboveThresholdFloorBasisPoints / (double)CaptureBalanceData.BasisPointScale;
            double resistance = floor + (1d - floor) * ramp;
            return CaptureBalanceData.Clamp(
                (int)Math.Round(resistance * CaptureBalanceData.BasisPointScale, MidpointRounding.AwayFromZero),
                0,
                CaptureBalanceData.BasisPointScale);
        }

        private static int CalculateTraitMotionBasisPoints(CaptureTargetContext target, CaptureBalanceData balance)
        {
            if (target == null || balance == null)
                return 0;

            int total = balance.LookupRarityMotion(balance.VariantRarityMotionBasisPoints, target.VariantRarityRank);
            bool mutated = target.IsMutated || IsMutatedId(target.MutationId);
            if (mutated)
            {
                total = SaturatingAdd(
                    total,
                    balance.MutatedMotionBasisPoints,
                    balance.LookupRarityMotion(balance.MutationRarityMotionBasisPoints, target.MutationRarityRank));
            }

            if (target.IsEntangled)
                total = SaturatingAdd(total, balance.EntangledMotionBasisPoints);

            return Math.Max(0, total);
        }

        private static bool IsMutatedId(string mutationId)
        {
            if (string.IsNullOrWhiteSpace(mutationId))
                return false;
            string value = mutationId.Trim();
            return !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);
        }

        private static int CalculatePositiveIvDifficultyBasisPoints(CaptureIvValues ivs, CaptureBalanceData balance)
        {
            CaptureIvValues values = ivs ?? new CaptureIvValues();
            int sum = 0;
            for (int i = 0; i < ScannerStats.Length; i++)
                sum += Math.Max(0, CaptureBalanceData.Clamp(values.Get(ScannerStats[i]), IvMinimum, IvMaximum));

            int maximumPositiveIvSum = PositiveIvStatCount * IvMaximum;
            return (int)((long)balance.PositiveIvMotionBonusMaximumBasisPoints * sum / maximumPositiveIvSum);
        }

        private static List<CaptureScannerIntel> ResolveScannerIntel(
            uint seed,
            string targetId,
            CaptureIvValues ivs,
            int scanRating)
        {
            var order = (CaptureIvStat[])ScannerStats.Clone();
            var rng = new DeterministicCaptureRng(seed ^ DeterministicCaptureRng.StableSeed(targetId));
            for (int i = order.Length - 1; i > 0; i--)
            {
                int swap = rng.NextInt(0, i + 1);
                CaptureIvStat temp = order[i];
                order[i] = order[swap];
                order[swap] = temp;
            }

            var result = new List<CaptureScannerIntel>(scanRating);
            CaptureIvValues values = ivs ?? new CaptureIvValues();
            for (int i = 0; i < scanRating; i++)
            {
                result.Add(new CaptureScannerIntel
                {
                    Stat = order[i],
                    Grade = GradeIv(values.Get(order[i])),
                });
            }

            return result;
        }

        private static int ScaleRatio(int numerator, int denominator)
        {
            if (denominator <= 0) return 0;
            return CaptureBalanceData.Clamp(
                (int)((long)numerator * CaptureBalanceData.BasisPointScale / denominator),
                0,
                CaptureBalanceData.BasisPointScale);
        }

        private static int ScaleBasisPoints(int value, int scaleBasisPoints)
        {
            return ClampLongToInt(
                (long)value * scaleBasisPoints / CaptureBalanceData.BasisPointScale,
                int.MinValue,
                int.MaxValue);
        }

        private static int ScaleByMultiplier(int value, int multiplierBasisPoints)
        {
            return ClampLongToInt(
                (long)value * Math.Max(0, multiplierBasisPoints) / CaptureBalanceData.BasisPointScale,
                0,
                int.MaxValue);
        }

        private static int SaturatingMultiply(int left, int right)
        {
            return ClampLongToInt((long)left * right, int.MinValue, int.MaxValue);
        }

        private static int SaturatingAdd(params int[] values)
        {
            long total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
                if (total > int.MaxValue) return int.MaxValue;
                if (total < int.MinValue) return int.MinValue;
            }

            return (int)total;
        }

        private static int ClampLongToInt(long value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return (int)value;
        }
    }
}
