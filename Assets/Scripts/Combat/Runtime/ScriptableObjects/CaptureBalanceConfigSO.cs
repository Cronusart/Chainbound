using System;
using System.Text;
using Chainbound.Combat.Core;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>Global capture simulation, HP, IV, Alertness, physics, and stacking lever board.</summary>
    [CreateAssetMenu(fileName = "CaptureBalanceConfig", menuName = "Chainbound/Combat/Capture/Balance Config")]
    public sealed class CaptureBalanceConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Min(1)] public int configVersion = 1;
        public CaptureProfileCatalogSO profileCatalog;

        [Header("Meter and Timer")]
        [Range(1, 120)] public int tickRate = 30;
        [Min(1)] public int standardTimerMilliseconds = 20000;
        [Range(1, 10000)] public int baseBandHeightBasisPoints = 2400;
        [Min(0)] public int lockMovementBasisPointsPerSecond = 1250;
        [Min(0)] public int escapeMovementBasisPointsPerSecond = 800;
        [Range(1, 600000)] public int maximumAttemptMilliseconds = 600000;

        [Header("Target HP Resistance")]
        [Tooltip("HP% at or below this (10000 = 100%) applies zero HP motion/timer penalty. 1500 = 15%.")]
        [Range(0, 9999)] public int lowHpThresholdBasisPoints = 1500;
        [Tooltip("Share of the full-HP bonus applied immediately above the low-HP threshold. 5000 = 50% cliff, then the curve ramps to 100% at max HP.")]
        [Range(0, 10000)] public int hpAboveThresholdFloorBasisPoints = 5000;
        [Tooltip("Ramp from the cliff floor up to max HP. 1000 = linear.")]
        [Range(1, 10000)] public int hpResistanceCurveMilli = 1000;
        [Tooltip("Added to trace max speed at 100% HP, scaled by the HP curve. 10000 = +1.0× on top of the profile. The shipped asset may exceed this code default during playtests.")]
        [Min(0)] public int fullHpVelocityBonusBasisPoints = 3500;
        [Tooltip("Added to trace accel and mean reversion at 100% HP, scaled by the HP curve.")]
        [Min(0)] public int fullHpAccelerationBonusBasisPoints = 4500;
        [Tooltip("Subtracted from the profile timer at 100% HP, scaled by the HP curve. 2500 on Standard = 15s. Does not apply at or below the low-HP threshold.")]
        [Range(0, 9999)] public int fullHpTimerPenaltyBasisPoints = 2500;

        [Header("IV and Alertness")]
        [Tooltip("Max extra motion from the sum of positive non-HP IVs (0 to 50). Negatives currently do nothing. See CAPTURE_DIFFICULTY_COMPOSITION_v1.md for the planned net-IV spectrum.")]
        [Min(0)] public int positiveIvMotionBonusMaximumBasisPoints = 2000;
        [Min(0)] public int alertnessVelocityBasisPoints = 1000;
        [Min(0)] public int alertnessAccelerationBasisPoints = 1200;
        [Min(0)] public int alertnessSpikeAmplitudeBasisPoints = 1000;

        [Header("Band Physics")]
        [Min(0)] public int bandLiftAccelerationUnitsPerSecondSquared = 18000;
        [Min(0)] public int bandGravityUnitsPerSecondSquared = 14000;
        [Min(0)] public int bandDragBasisPointsPerSecond = 2200;
        [Min(1)] public int bandMaximumSpeedUnitsPerSecond = 8000;
        [Range(0, 10000)] public int bandEdgeBounceBasisPoints = 2500;

        [Header("Trace Physics")]
        [Min(0)] public int traceAccelerationUnitsPerSecondSquared = 12000;
        [Min(1)] public int traceMaximumSpeedUnitsPerSecond = 6500;
        [Min(0)] public int traceMeanReversionUnitsPerSecondSquared = 8000;
        [Range(0, 10000)] public int traceEdgeBounceBasisPoints = 6500;
        [Range(0, 10000)] public int spikeChanceBasisPointsPerTick = 65;
        [Min(0)] public int spikeVelocityImpulseUnitsPerSecond = 2400;

        [Header("Trait capture difficulty")]
        [Tooltip("Motion added by cosmetic variant rarity (Common should stay 0). Looked up from the species visual set.")]
        public CaptureRarityMotionTable variantRarityMotion = CaptureRarityMotionTable.VariantDefaults();
        [Tooltip("Flat motion added when the target is mutated, on top of mutation rarity.")]
        [Min(0)] public int mutatedMotionBasisPoints = 1500;
        [Tooltip("Extra motion by mutation-aura rarity. Common should stay 0.")]
        public CaptureRarityMotionTable mutationRarityMotion = CaptureRarityMotionTable.MutationDefaults();
        [Tooltip("Flat motion added when the target is already entangled.")]
        [Min(0)] public int entangledMotionBasisPoints = 2000;
        [Tooltip("How much trait motion also raises spike chance. 4000 = 40% of trait motion.")]
        [Range(0, 10000)] public int traitSpikeChancePerMotionBasisPoints = 4000;

        public CaptureBalanceData ToCoreData()
        {
            CaptureProfileCatalogSO catalog = profileCatalog != null
                ? profileCatalog
                : CaptureProfileCatalogSO.CreateRuntimeDefault();
            return new CaptureBalanceData
            {
                TickRate = tickRate,
                StandardTimerMilliseconds = standardTimerMilliseconds,
                BaseBandHeightBasisPoints = baseBandHeightBasisPoints,
                LockMovementBasisPointsPerSecond = lockMovementBasisPointsPerSecond,
                EscapeMovementBasisPointsPerSecond = escapeMovementBasisPointsPerSecond,
                LowHpThresholdBasisPoints = lowHpThresholdBasisPoints,
                HpAboveThresholdFloorBasisPoints = hpAboveThresholdFloorBasisPoints,
                HpResistanceCurveMilli = hpResistanceCurveMilli,
                FullHpVelocityBonusBasisPoints = fullHpVelocityBonusBasisPoints,
                FullHpAccelerationBonusBasisPoints = fullHpAccelerationBonusBasisPoints,
                FullHpTimerPenaltyBasisPoints = fullHpTimerPenaltyBasisPoints,
                PositiveIvMotionBonusMaximumBasisPoints = positiveIvMotionBonusMaximumBasisPoints,
                AlertnessVelocityBasisPoints = alertnessVelocityBasisPoints,
                AlertnessAccelerationBasisPoints = alertnessAccelerationBasisPoints,
                AlertnessSpikeAmplitudeBasisPoints = alertnessSpikeAmplitudeBasisPoints,
                MaximumAttemptMilliseconds = maximumAttemptMilliseconds,
                BandLiftAccelerationUnitsPerSecondSquared = bandLiftAccelerationUnitsPerSecondSquared,
                BandGravityUnitsPerSecondSquared = bandGravityUnitsPerSecondSquared,
                BandDragBasisPointsPerSecond = bandDragBasisPointsPerSecond,
                BandMaximumSpeedUnitsPerSecond = bandMaximumSpeedUnitsPerSecond,
                BandEdgeBounceBasisPoints = bandEdgeBounceBasisPoints,
                TraceAccelerationUnitsPerSecondSquared = traceAccelerationUnitsPerSecondSquared,
                TraceMaximumSpeedUnitsPerSecond = traceMaximumSpeedUnitsPerSecond,
                TraceMeanReversionUnitsPerSecondSquared = traceMeanReversionUnitsPerSecondSquared,
                TraceEdgeBounceBasisPoints = traceEdgeBounceBasisPoints,
                SpikeChanceBasisPointsPerTick = spikeChanceBasisPointsPerTick,
                SpikeVelocityImpulseUnitsPerSecond = spikeVelocityImpulseUnitsPerSecond,
                VariantRarityMotionBasisPoints = variantRarityMotion != null
                    ? variantRarityMotion.ToArray()
                    : CaptureRarityMotionTable.VariantDefaults().ToArray(),
                MutatedMotionBasisPoints = mutatedMotionBasisPoints,
                MutationRarityMotionBasisPoints = mutationRarityMotion != null
                    ? mutationRarityMotion.ToArray()
                    : CaptureRarityMotionTable.MutationDefaults().ToArray(),
                EntangledMotionBasisPoints = entangledMotionBasisPoints,
                TraitSpikeChancePerMotionBasisPoints = traitSpikeChancePerMotionBasisPoints,
                Profiles = catalog.ToCoreProfiles(),
            }.Normalized();
        }

        public string ComputeConfigHash()
        {
            CaptureBalanceData data = ToCoreData();
            var sb = new StringBuilder(512);
            sb.Append("capture|v=").Append(configVersion)
                .Append("|tick=").Append(data.TickRate)
                .Append("|timer=").Append(data.StandardTimerMilliseconds)
                .Append("|band=").Append(data.BaseBandHeightBasisPoints)
                .Append("|lock=").Append(data.LockMovementBasisPointsPerSecond)
                .Append("|escape=").Append(data.EscapeMovementBasisPointsPerSecond)
                .Append("|lowHp=").Append(data.LowHpThresholdBasisPoints)
                .Append("|hpFloor=").Append(data.HpAboveThresholdFloorBasisPoints)
                .Append("|hpCurve=").Append(data.HpResistanceCurveMilli)
                .Append("|hpVel=").Append(data.FullHpVelocityBonusBasisPoints)
                .Append("|hpAccel=").Append(data.FullHpAccelerationBonusBasisPoints)
                .Append("|hpTime=").Append(data.FullHpTimerPenaltyBasisPoints)
                .Append("|iv=").Append(data.PositiveIvMotionBonusMaximumBasisPoints)
                .Append("|alertVel=").Append(data.AlertnessVelocityBasisPoints)
                .Append("|alertAccel=").Append(data.AlertnessAccelerationBasisPoints)
                .Append("|alertSpike=").Append(data.AlertnessSpikeAmplitudeBasisPoints)
                .Append("|max=").Append(data.MaximumAttemptMilliseconds)
                .Append("|lift=").Append(data.BandLiftAccelerationUnitsPerSecondSquared)
                .Append("|gravity=").Append(data.BandGravityUnitsPerSecondSquared)
                .Append("|drag=").Append(data.BandDragBasisPointsPerSecond)
                .Append("|bandMax=").Append(data.BandMaximumSpeedUnitsPerSecond)
                .Append("|bandBounce=").Append(data.BandEdgeBounceBasisPoints)
                .Append("|traceAccel=").Append(data.TraceAccelerationUnitsPerSecondSquared)
                .Append("|traceMax=").Append(data.TraceMaximumSpeedUnitsPerSecond)
                .Append("|mean=").Append(data.TraceMeanReversionUnitsPerSecondSquared)
                .Append("|traceBounce=").Append(data.TraceEdgeBounceBasisPoints)
                .Append("|spikeChance=").Append(data.SpikeChanceBasisPointsPerTick)
                .Append("|spikeImpulse=").Append(data.SpikeVelocityImpulseUnitsPerSecond)
                .Append("|mutMotion=").Append(data.MutatedMotionBasisPoints)
                .Append("|entMotion=").Append(data.EntangledMotionBasisPoints)
                .Append("|traitSpike=").Append(data.TraitSpikeChancePerMotionBasisPoints);
            AppendRarityTable(sb, "varMotion", data.VariantRarityMotionBasisPoints);
            AppendRarityTable(sb, "mutRarity", data.MutationRarityMotionBasisPoints);
            for (int i = 0; i < data.Profiles.Count; i++)
            {
                CaptureMotionProfile profile = data.Profiles[i];
                sb.Append("|profile=").Append(profile.ProfileId)
                    .Append(',').Append((int)profile.Difficulty)
                    .Append(',').Append(profile.MotionMultiplierBasisPoints)
                    .Append(',').Append(profile.TimerMultiplierBasisPoints)
                    .Append(',').Append(profile.SpikeChanceMultiplierBasisPoints);
            }
            return BalanceConfigHashUtility.HashUtf8(sb.ToString());
        }

        private static void AppendRarityTable(StringBuilder sb, string key, int[] table)
        {
            sb.Append('|').Append(key).Append('=');
            if (table == null)
                return;
            for (int i = 0; i < table.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(table[i]);
            }
        }

        public static CaptureBalanceConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<CaptureBalanceConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.profileCatalog = CaptureProfileCatalogSO.CreateRuntimeDefault();
            return config;
        }

        private void OnValidate()
        {
            CaptureBalanceData normalized = ToCoreData();
            configVersion = Mathf.Max(1, configVersion);
            tickRate = normalized.TickRate;
            standardTimerMilliseconds = normalized.StandardTimerMilliseconds;
            baseBandHeightBasisPoints = normalized.BaseBandHeightBasisPoints;
            lockMovementBasisPointsPerSecond = normalized.LockMovementBasisPointsPerSecond;
            escapeMovementBasisPointsPerSecond = normalized.EscapeMovementBasisPointsPerSecond;
            maximumAttemptMilliseconds = normalized.MaximumAttemptMilliseconds;
            lowHpThresholdBasisPoints = normalized.LowHpThresholdBasisPoints;
            hpAboveThresholdFloorBasisPoints = normalized.HpAboveThresholdFloorBasisPoints;
            hpResistanceCurveMilli = normalized.HpResistanceCurveMilli;
        }
    }

    [Serializable]
    public sealed class CaptureRarityMotionTable
    {
        [Min(0)] public int common;
        [Min(0)] public int uncommon;
        [Min(0)] public int rare;
        [Min(0)] public int epic;
        [Min(0)] public int legendary;
        [Min(0)] public int mythic;

        public int[] ToArray()
        {
            return new[] { common, uncommon, rare, epic, legendary, mythic };
        }

        public static CaptureRarityMotionTable VariantDefaults()
        {
            return new CaptureRarityMotionTable
            {
                uncommon = 500,
                rare = 1200,
                epic = 2000,
                legendary = 3000,
                mythic = 4500,
            };
        }

        public static CaptureRarityMotionTable MutationDefaults()
        {
            return new CaptureRarityMotionTable
            {
                uncommon = 400,
                rare = 800,
                epic = 1500,
                legendary = 2500,
                mythic = 4000,
            };
        }
    }
}
