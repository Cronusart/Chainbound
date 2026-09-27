using System;
using System.Collections.Generic;
using Chainbound.Combat.Core;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public sealed class CaptureMotionProfileEntry
    {
        [Tooltip("Stable id used by assignments and events (calm, standard, volatile, extreme).")]
        public string profileId = "standard";
        [Tooltip("Named motion kit. This is capture volatility, not encounter Elite/Boss and not a catch %.")]
        public CaptureVolatilityProfile difficulty = CaptureVolatilityProfile.Standard;
        [Tooltip("Scales trace max speed, accel, mean reversion, and spike size. 10000 = 1.0×.")]
        [Min(0)] public int motionMultiplierBasisPoints = 10000;
        [Tooltip("Scales attempt length. 10000 = standard timer (20s). Extreme ships at 6500.")]
        [Min(1)] public int timerMultiplierBasisPoints = 10000;
        [Tooltip("Scales spike chance per tick. 10000 = global spike chance unchanged.")]
        [Min(0)] public int spikeChanceMultiplierBasisPoints = 10000;

        public CaptureMotionProfile ToCore()
        {
            return new CaptureMotionProfile
            {
                ProfileId = string.IsNullOrWhiteSpace(profileId) ? difficulty.ToString().ToLowerInvariant() : profileId.Trim(),
                Difficulty = difficulty,
                MotionMultiplierBasisPoints = Mathf.Max(0, motionMultiplierBasisPoints),
                TimerMultiplierBasisPoints = Mathf.Max(1, timerMultiplierBasisPoints),
                SpikeChanceMultiplierBasisPoints = Mathf.Max(0, spikeChanceMultiplierBasisPoints),
            };
        }
    }

    /// <summary>Capture motion profiles referenced by stable IDs from server assignments and events.</summary>
    [CreateAssetMenu(fileName = "CaptureProfileCatalog", menuName = "Chainbound/Combat/Capture/Profile Catalog")]
    public sealed class CaptureProfileCatalogSO : ScriptableObject
    {
        [Min(1)] public int catalogVersion = 1;
        public List<CaptureMotionProfileEntry> profiles = new List<CaptureMotionProfileEntry>();

        public List<CaptureMotionProfile> ToCoreProfiles()
        {
            EnsureDefaults();
            var result = new List<CaptureMotionProfile>(profiles.Count);
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null)
                    result.Add(profiles[i].ToCore());
            }
            return result;
        }

        public static CaptureProfileCatalogSO CreateRuntimeDefault()
        {
            var catalog = CreateInstance<CaptureProfileCatalogSO>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            catalog.ResetToDefaults();
            return catalog;
        }

        public void ResetToDefaults()
        {
            catalogVersion = 1;
            profiles = new List<CaptureMotionProfileEntry>
            {
                Entry("calm", CaptureVolatilityProfile.Calm, 7500, 11500, 7500),
                Entry("standard", CaptureVolatilityProfile.Standard, 10000, 10000, 10000),
                Entry("volatile", CaptureVolatilityProfile.Volatile, 13000, 8750, 13000),
                Entry("extreme", CaptureVolatilityProfile.Extreme, 16000, 6500, 16000),
            };
        }

        private void OnValidate()
        {
            catalogVersion = Mathf.Max(1, catalogVersion);
            EnsureDefaults();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var difficulties = new HashSet<CaptureVolatilityProfile>();
            for (int i = 0; i < profiles.Count; i++)
            {
                CaptureMotionProfileEntry entry = profiles[i];
                if (entry == null)
                    continue;
                entry.profileId = string.IsNullOrWhiteSpace(entry.profileId)
                    ? entry.difficulty.ToString().ToLowerInvariant()
                    : entry.profileId.Trim();
                entry.motionMultiplierBasisPoints = Mathf.Max(0, entry.motionMultiplierBasisPoints);
                entry.timerMultiplierBasisPoints = Mathf.Max(1, entry.timerMultiplierBasisPoints);
                entry.spikeChanceMultiplierBasisPoints = Mathf.Max(0, entry.spikeChanceMultiplierBasisPoints);
                if (!ids.Add(entry.profileId))
                    Debug.LogError($"[CaptureProfileCatalogSO] Duplicate profile id '{entry.profileId}'.", this);
                if (!difficulties.Add(entry.difficulty))
                    Debug.LogError($"[CaptureProfileCatalogSO] Multiple defaults for difficulty '{entry.difficulty}'.", this);
            }
        }

        private void EnsureDefaults()
        {
            if (profiles == null || profiles.Count == 0)
                ResetToDefaults();
        }

        private static CaptureMotionProfileEntry Entry(
            string id,
            CaptureVolatilityProfile difficulty,
            int motion,
            int timer,
            int spike)
        {
            return new CaptureMotionProfileEntry
            {
                profileId = id,
                difficulty = difficulty,
                motionMultiplierBasisPoints = motion,
                timerMultiplierBasisPoints = timer,
                spikeChanceMultiplierBasisPoints = spike,
            };
        }
    }
}
