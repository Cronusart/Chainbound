using System;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Pure C# static utility for calculating capture probability.
    ///
    /// Formula (MVP):
    ///   hpRatio = currentHP / maxHP
    ///   hpBonus = clamp01(1.0 - hpRatio)
    ///   effectiveRate = BaseRate * (0.5 + 0.5 * hpBonus)
    ///   P = effectiveRate / 100
    ///
    /// Then: VRF roll in [0,1) determines success.
    /// </summary>
    public static class CaptureRateCalculator
    {
        /// <summary>
        /// Calculate capture probability from combat state.
        /// </summary>
        /// <param name="baseCaptureRate">Species base capture rate (0-100). Higher = easier to catch.</param>
        /// <param name="currentHP">Target's current HP.</param>
        /// <param name="maxHP">Target's maximum HP.</param>
        /// <param name="statusModifier">Status effect modifier (default 1.0). Paralysis/Sleep can boost.</param>
        /// <param name="deviceModifier">Capture device modifier (default 1.0). Better devices = higher.</param>
        /// <returns>Probability in [0, 1]. Compare against VRF roll for success.</returns>
        public static float CalculateProbability(
            int baseCaptureRate,
            int currentHP,
            int maxHP,
            float statusModifier = 1.0f,
            float deviceModifier = 1.0f,
            int favorScore = 0,
            FavorConfigSO favorConfig = null)
        {
            if (maxHP <= 0) return 0f;
            if (currentHP <= 0) return 1f; // KO'd = guaranteed

            float hpRatio = Mathf.Clamp01((float)currentHP / maxHP);
            float hpBonus = Mathf.Clamp01(1.0f - hpRatio);

            float effectiveRate = baseCaptureRate * (0.5f + 0.5f * hpBonus);
            effectiveRate *= statusModifier;
            effectiveRate *= deviceModifier;

            float probability = Mathf.Clamp01(effectiveRate / 100f);
            FavorConfigSO favor = favorConfig != null ? favorConfig : FavorConfigSO.CreateRuntimeDefault();
            probability = Mathf.Clamp01(probability + favor.CaptureProbabilityBonus(favorScore));
            return probability;
        }

        /// <summary>
        /// Determine if a capture succeeds given a VRF-derived roll.
        /// </summary>
        /// <param name="probability">Calculated probability from CalculateProbability.</param>
        /// <param name="vrfRoll">A value in [0, 1) derived from VRF randomness.</param>
        public static bool IsSuccess(float probability, float vrfRoll)
        {
            return vrfRoll < probability;
        }

        /// <summary>
        /// Convert 4 bytes of VRF randomness into a roll in [0, 1).
        /// </summary>
        /// <param name="randomnessBytes">At least 4 bytes of randomness.</param>
        /// <param name="offset">Byte offset to start reading from.</param>
        public static float VrfBytesToRoll(byte[] randomnessBytes, int offset = 0)
        {
            if (randomnessBytes == null || randomnessBytes.Length < offset + 4)
                throw new ArgumentException("Need at least 4 bytes for capture roll.");

            uint raw = BitConverter.ToUInt32(randomnessBytes, offset);
            return (float)(raw / (double)uint.MaxValue);
        }
    }
}
