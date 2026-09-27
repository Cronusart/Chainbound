#nullable enable
using System;
using System.Collections.Generic;

namespace Chainbound.Combat.Core
{
    [Serializable]
    public sealed class CaptureBalanceData
    {
        public const int BasisPointScale = 10000;
        public const int GraphScale = 10000;

        public int TickRate { get; set; } = 30;
        public int StandardTimerMilliseconds { get; set; } = 20000;
        public int BaseBandHeightBasisPoints { get; set; } = 2400;
        public int LockMovementBasisPointsPerSecond { get; set; } = 1250;
        public int EscapeMovementBasisPointsPerSecond { get; set; } = 800;
        public int LowHpThresholdBasisPoints { get; set; } = 1500;
        public int HpAboveThresholdFloorBasisPoints { get; set; } = 5000;
        public int HpResistanceCurveMilli { get; set; } = 1000;
        public int FullHpVelocityBonusBasisPoints { get; set; } = 3500;
        public int FullHpAccelerationBonusBasisPoints { get; set; } = 4500;
        public int FullHpTimerPenaltyBasisPoints { get; set; } = 2500;
        public int PositiveIvMotionBonusMaximumBasisPoints { get; set; } = 2000;
        public int AlertnessVelocityBasisPoints { get; set; } = 1000;
        public int AlertnessAccelerationBasisPoints { get; set; } = 1200;
        public int AlertnessSpikeAmplitudeBasisPoints { get; set; } = 1000;
        public int MaximumAttemptMilliseconds { get; set; } = 600000;

        public int BandLiftAccelerationUnitsPerSecondSquared { get; set; } = 18000;
        public int BandGravityUnitsPerSecondSquared { get; set; } = 14000;
        public int BandDragBasisPointsPerSecond { get; set; } = 2200;
        public int BandMaximumSpeedUnitsPerSecond { get; set; } = 8000;
        public int BandEdgeBounceBasisPoints { get; set; } = 2500;

        public int TraceAccelerationUnitsPerSecondSquared { get; set; } = 12000;
        public int TraceMaximumSpeedUnitsPerSecond { get; set; } = 6500;
        public int TraceMeanReversionUnitsPerSecondSquared { get; set; } = 8000;
        public int TraceEdgeBounceBasisPoints { get; set; } = 6500;
        public int SpikeChanceBasisPointsPerTick { get; set; } = 65;
        public int SpikeVelocityImpulseUnitsPerSecond { get; set; } = 2400;

        public int[] VariantRarityMotionBasisPoints { get; set; } = DefaultVariantRarityMotion();
        public int MutatedMotionBasisPoints { get; set; } = 1500;
        public int[] MutationRarityMotionBasisPoints { get; set; } = DefaultMutationRarityMotion();
        public int EntangledMotionBasisPoints { get; set; } = 2000;
        public int TraitSpikeChancePerMotionBasisPoints { get; set; } = 4000;

        public List<CaptureMotionProfile> Profiles { get; set; } = CreateDefaultProfiles();

        public static CaptureBalanceData CreateDefault()
        {
            return new CaptureBalanceData();
        }

        public CaptureBalanceData Normalized()
        {
            var normalized = new CaptureBalanceData
            {
                TickRate = Clamp(TickRate, 1, 120),
                StandardTimerMilliseconds = Clamp(StandardTimerMilliseconds, 1, MaximumAttemptMilliseconds > 0 ? MaximumAttemptMilliseconds : 600000),
                BaseBandHeightBasisPoints = Clamp(BaseBandHeightBasisPoints, 1, BasisPointScale),
                LockMovementBasisPointsPerSecond = Math.Max(0, LockMovementBasisPointsPerSecond),
                EscapeMovementBasisPointsPerSecond = Math.Max(0, EscapeMovementBasisPointsPerSecond),
                LowHpThresholdBasisPoints = Clamp(LowHpThresholdBasisPoints, 0, BasisPointScale - 1),
                HpAboveThresholdFloorBasisPoints = Clamp(HpAboveThresholdFloorBasisPoints, 0, BasisPointScale),
                HpResistanceCurveMilli = Clamp(HpResistanceCurveMilli, 1, 10000),
                FullHpVelocityBonusBasisPoints = Math.Max(0, FullHpVelocityBonusBasisPoints),
                FullHpAccelerationBonusBasisPoints = Math.Max(0, FullHpAccelerationBonusBasisPoints),
                FullHpTimerPenaltyBasisPoints = Clamp(FullHpTimerPenaltyBasisPoints, 0, BasisPointScale - 1),
                PositiveIvMotionBonusMaximumBasisPoints = Math.Max(0, PositiveIvMotionBonusMaximumBasisPoints),
                AlertnessVelocityBasisPoints = Math.Max(0, AlertnessVelocityBasisPoints),
                AlertnessAccelerationBasisPoints = Math.Max(0, AlertnessAccelerationBasisPoints),
                AlertnessSpikeAmplitudeBasisPoints = Math.Max(0, AlertnessSpikeAmplitudeBasisPoints),
                MaximumAttemptMilliseconds = Clamp(MaximumAttemptMilliseconds, 1, 600000),
                BandLiftAccelerationUnitsPerSecondSquared = Math.Max(0, BandLiftAccelerationUnitsPerSecondSquared),
                BandGravityUnitsPerSecondSquared = Math.Max(0, BandGravityUnitsPerSecondSquared),
                BandDragBasisPointsPerSecond = Math.Max(0, BandDragBasisPointsPerSecond),
                BandMaximumSpeedUnitsPerSecond = Math.Max(1, BandMaximumSpeedUnitsPerSecond),
                BandEdgeBounceBasisPoints = Clamp(BandEdgeBounceBasisPoints, 0, BasisPointScale),
                TraceAccelerationUnitsPerSecondSquared = Math.Max(0, TraceAccelerationUnitsPerSecondSquared),
                TraceMaximumSpeedUnitsPerSecond = Math.Max(1, TraceMaximumSpeedUnitsPerSecond),
                TraceMeanReversionUnitsPerSecondSquared = Math.Max(0, TraceMeanReversionUnitsPerSecondSquared),
                TraceEdgeBounceBasisPoints = Clamp(TraceEdgeBounceBasisPoints, 0, BasisPointScale),
                SpikeChanceBasisPointsPerTick = Clamp(SpikeChanceBasisPointsPerTick, 0, BasisPointScale),
                SpikeVelocityImpulseUnitsPerSecond = Math.Max(0, SpikeVelocityImpulseUnitsPerSecond),
                VariantRarityMotionBasisPoints = NormalizeRarityTable(VariantRarityMotionBasisPoints, DefaultVariantRarityMotion()),
                MutatedMotionBasisPoints = Math.Max(0, MutatedMotionBasisPoints),
                MutationRarityMotionBasisPoints = NormalizeRarityTable(MutationRarityMotionBasisPoints, DefaultMutationRarityMotion()),
                EntangledMotionBasisPoints = Math.Max(0, EntangledMotionBasisPoints),
                TraitSpikeChancePerMotionBasisPoints = Clamp(TraitSpikeChancePerMotionBasisPoints, 0, BasisPointScale),
                Profiles = NormalizeProfiles(Profiles),
            };

            normalized.StandardTimerMilliseconds = Clamp(
                normalized.StandardTimerMilliseconds,
                1,
                normalized.MaximumAttemptMilliseconds);
            return normalized;
        }

        public CaptureMotionProfile ResolveProfile(CaptureVolatilityProfile difficulty)
        {
            List<CaptureMotionProfile> source = Profiles ?? CreateDefaultProfiles();
            for (int i = 0; i < source.Count; i++)
            {
                CaptureMotionProfile profile = source[i];
                if (profile != null && profile.Difficulty == difficulty)
                    return profile;
            }

            return CreateDefaultProfiles()[(int)difficulty];
        }

        private static List<CaptureMotionProfile> CreateDefaultProfiles()
        {
            return new List<CaptureMotionProfile>
            {
                new CaptureMotionProfile { ProfileId = "calm", Difficulty = CaptureVolatilityProfile.Calm, MotionMultiplierBasisPoints = 7500, TimerMultiplierBasisPoints = 11500, SpikeChanceMultiplierBasisPoints = 7500 },
                new CaptureMotionProfile { ProfileId = "standard", Difficulty = CaptureVolatilityProfile.Standard, MotionMultiplierBasisPoints = 10000, TimerMultiplierBasisPoints = 10000, SpikeChanceMultiplierBasisPoints = 10000 },
                new CaptureMotionProfile { ProfileId = "volatile", Difficulty = CaptureVolatilityProfile.Volatile, MotionMultiplierBasisPoints = 13000, TimerMultiplierBasisPoints = 8750, SpikeChanceMultiplierBasisPoints = 13000 },
                new CaptureMotionProfile { ProfileId = "extreme", Difficulty = CaptureVolatilityProfile.Extreme, MotionMultiplierBasisPoints = 16000, TimerMultiplierBasisPoints = 6500, SpikeChanceMultiplierBasisPoints = 16000 },
            };
        }

        private static List<CaptureMotionProfile> NormalizeProfiles(List<CaptureMotionProfile> source)
        {
            var defaults = CreateDefaultProfiles();
            var result = new List<CaptureMotionProfile>(4);
            for (int difficulty = 0; difficulty < 4; difficulty++)
            {
                CaptureMotionProfile? found = null;
                if (source != null)
                {
                    for (int i = 0; i < source.Count; i++)
                    {
                        if (source[i] != null && (int)source[i].Difficulty == difficulty)
                        {
                            found = source[i];
                            break;
                        }
                    }
                }

                CaptureMotionProfile fallback = defaults[difficulty];
                string? foundId = found != null ? found.ProfileId : null;
                result.Add(new CaptureMotionProfile
                {
                    ProfileId = string.IsNullOrWhiteSpace(foundId) ? fallback.ProfileId : foundId.Trim(),
                    Difficulty = (CaptureVolatilityProfile)difficulty,
                    MotionMultiplierBasisPoints = Math.Max(0, found != null ? found.MotionMultiplierBasisPoints : fallback.MotionMultiplierBasisPoints),
                    TimerMultiplierBasisPoints = Math.Max(1, found != null ? found.TimerMultiplierBasisPoints : fallback.TimerMultiplierBasisPoints),
                    SpikeChanceMultiplierBasisPoints = Math.Max(0, found != null ? found.SpikeChanceMultiplierBasisPoints : fallback.SpikeChanceMultiplierBasisPoints),
                });
            }

            return result;
        }

        public int LookupRarityMotion(int[] table, int rarityRank)
        {
            int[] source = table != null && table.Length > 0 ? table : DefaultVariantRarityMotion();
            int index = Clamp(rarityRank, 0, source.Length - 1);
            return Math.Max(0, source[index]);
        }

        private static int[] DefaultVariantRarityMotion()
        {
            return new[] { 0, 500, 1200, 2000, 3000, 4500 };
        }

        private static int[] DefaultMutationRarityMotion()
        {
            return new[] { 0, 400, 800, 1500, 2500, 4000 };
        }

        private static int[] NormalizeRarityTable(int[] source, int[] fallback)
        {
            int[] result = new int[6];
            for (int i = 0; i < result.Length; i++)
            {
                int value = fallback[i];
                if (source != null && i < source.Length)
                    value = source[i];
                result[i] = Math.Max(0, value);
            }

            return result;
        }

        internal static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
