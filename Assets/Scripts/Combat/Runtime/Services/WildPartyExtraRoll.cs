using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Chance of one extra wild after the authored party is rolled.
    /// Boss encounters and parties already at 2 skip the roll and do not consume RNG.
    /// </summary>
    public static class WildPartyExtraRoll
    {
        public const float DefaultChanceAtOne = 0.15f;
        public const float DefaultChanceAtTwo = 0.45f;
        public const float DefaultChanceAtThree = 0.70f;
        public const int MaxWildPartySize = 2;

        public static float ResolveChance(EncounterZoneSO zone, int equippedPartyCount)
        {
            EncounterAreaSO area = zone != null ? zone.area : null;
            float atOne = Inherit(
                zone != null ? zone.extraWildChanceAtOne : -1f,
                area != null ? area.extraWildChanceAtOne : -1f,
                DefaultChanceAtOne);
            float atTwo = Inherit(
                zone != null ? zone.extraWildChanceAtTwo : -1f,
                area != null ? area.extraWildChanceAtTwo : -1f,
                DefaultChanceAtTwo);
            float atThree = Inherit(
                zone != null ? zone.extraWildChanceAtThree : -1f,
                area != null ? area.extraWildChanceAtThree : -1f,
                DefaultChanceAtThree);

            int band = equippedPartyCount <= 1 ? 1 : equippedPartyCount == 2 ? 2 : 3;
            float chance = band == 1 ? atOne : band == 2 ? atTwo : atThree;
            return Mathf.Clamp01(chance);
        }

        public static bool TryAddPartner(
            EncounterZoneSO zone,
            EncounterDifficulty difficulty,
            bool hasBossIntro,
            int currentPartySize,
            int equippedPartyCount,
            System.Random rng)
        {
            if (currentPartySize >= MaxWildPartySize)
                return false;
            if (difficulty == EncounterDifficulty.Boss || hasBossIntro)
                return false;
            if (rng == null)
                return false;

            float chance = ResolveChance(zone, equippedPartyCount);
            if (chance <= 0f)
                return false;

            return rng.NextDouble() < chance;
        }

        private static float Inherit(float zoneValue, float areaValue, float fallback)
        {
            if (zoneValue >= 0f)
                return zoneValue;
            if (areaValue >= 0f)
                return areaValue;
            return fallback;
        }
    }
}
