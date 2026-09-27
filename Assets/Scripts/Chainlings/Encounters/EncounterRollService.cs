using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Context provided by the trigger when requesting an encounter roll.
    /// </summary>
    public class EncounterContext
    {
        public Vector3 position;
        public bool isNight;
        public TimeSegment segment = TimeSegment.Day;
        public int dayNumber;
        public string walletPubkey;
        public int attemptCounter;
    }

    /// <summary>
    /// Pure C# service that performs zone-based encounter rolls using local seeded RNG.
    /// Resolves Area/Zone/TimeSegment/Modifiers into an effective table, then rolls.
    /// </summary>
    public class EncounterRollService
    {
        public bool TryRollEncounter(
            EncounterZoneSO zone,
            EncounterContext ctx,
            WildRollConfig wildConfig,
            out ZoneEncounterResult result,
            int favorScore = -1,
            FavorConfigSO favorConfig = null,
            MoveDatabaseSO moveDatabase = null,
            EncounterModifierService modifierService = null)
        {
            result = null;
            if (zone == null)
                return false;

            TimeSegment segment = ctx != null ? ctx.segment : TimeSegment.Day;
            if (ctx != null && ctx.isNight && segment == TimeSegment.Day)
                segment = TimeSegment.Night;

            EffectiveEncounterTable table = EncounterTableResolver.Resolve(
                zone, wildConfig, segment, modifierService);

            if (table.entries == null || table.entries.Count == 0)
            {
                Debug.Log($"[EncounterRoll] No species available after resolve. {table.debugResolvedSummary}");
                return false;
            }

            int seed = DeriveSeed(zone.zoneId, ctx);
            var rng = new System.Random(seed);

            double chanceRoll = rng.NextDouble();
            if (chanceRoll >= table.encounterChance)
            {
                Debug.Log(
                    $"[EncounterRoll] No encounter. seed={seed}, roll={chanceRoll:F4} >= chance={table.encounterChance:F4} | {table.debugResolvedSummary}");
                return false;
            }

            int partySize = (table.minPartySize == table.maxPartySize)
                ? table.minPartySize
                : table.minPartySize + rng.Next(table.maxPartySize - table.minPartySize + 1);

            FavorConfigSO favor = favorConfig != null ? favorConfig : FavorRuntime.Config;
            int baseFavor = favorScore >= 0 ? favorScore : FavorRuntime.CurrentScore;
            int effectiveFavor = Mathf.Max(0, baseFavor + table.favorBonus);

            // Optional IV bump override from modifiers: temporarily bias FavorConfig via chance post-roll.
            var slots = new List<ZoneEncounterSlot>();
            var logParts = new List<string>();

            for (int i = 0; i < partySize; i++)
            {
                var picked = WeightedPick(table.entries, rng, effectiveFavor, favor);

                int starOffset = (picked.starVariance > 0)
                    ? rng.Next(2 * picked.starVariance + 1) - picked.starVariance
                    : 0;
                int stars = Mathf.Clamp(picked.baseStars + starOffset, 0, CombatConstants.STAR_MAX);

                var traits = WildRoller.RollTraits(table.wildConfig, rng, effectiveFavor, favor);
                if (table.ivBumpChanceOverride >= 0f && rng.NextDouble() < table.ivBumpChanceOverride)
                    BumpLowestIv(traits.ivs, table.wildConfig.ivMax);

                var moveRoll = WildMoveRoller.Roll(
                    picked.movePool,
                    picked.battleKitSizeMin,
                    picked.battleKitSizeMax,
                    rng,
                    moveDatabase,
                    picked.moveWeightMultipliers);

                var visual = WildVariantRoller.Roll(
                    picked.variantPool,
                    rng,
                    traits.isEntangled,
                    picked.variantWeightMultipliers);

                WeightedMutationEntry mutation = WildMutationRoller.RollEntry(table.mutationPool, rng);
                string mutationId = mutation != null ? mutation.mutationId : ChainlingVisualIdentity.NoneId;
                traits.mutationId = mutationId;
                traits.isMutated = !string.Equals(
                    mutationId,
                    ChainlingVisualIdentity.NoneId,
                    StringComparison.Ordinal);
                traits.mutationRarityRank = traits.isMutated && mutation != null
                    ? Mathf.Clamp((int)mutation.rarity, 0, 5)
                    : 0;
                if (traits.isMutated)
                    visual.auraId = mutationId;
                visual.Normalize();

                slots.Add(new ZoneEncounterSlot
                {
                    speciesId = picked.speciesId,
                    traits = traits,
                    stars = stars,
                    level = 0,
                    baseCaptureRate = picked.baseCaptureRate,
                    moveIds = moveRoll.battleMoveIds ?? new List<string>(),
                    knownMoveId = moveRoll.knownMoveId ?? "",
                    visualIdentity = visual,
                });

                logParts.Add(
                    $"{picked.speciesId} stars={stars} ivs=[{string.Join(",", traits.ivs)}] " +
                    $"mut={traits.isMutated}:{traits.mutationId} ent={traits.isEntangled}:{traits.entangledType} " +
                    $"moves=[{string.Join(",", moveRoll.battleMoveIds)}] Known={moveRoll.knownMoveId} " +
                    $"variant={visual.visualVariantId}");
            }

            string logLine =
                $"seed={seed} favor={effectiveFavor}(base={baseFavor}+bonus={table.favorBonus}) party={partySize} " +
                string.Join(" | ", logParts) +
                $" | resolved: {table.debugResolvedSummary}";

            Debug.Log($"[EncounterRoll] HIT: {logLine}");

            result = new ZoneEncounterResult
            {
                slots = slots,
                zoneId = table.zoneId,
                areaId = table.areaId,
                segment = table.segment,
                seed = seed,
                difficulty = table.difficulty,
                bossIntroDialogueAsset = table.bossIntroDialogueAsset != null
                    ? table.bossIntroDialogueAsset
                    : zone.bossIntroDialogueAsset,
                debugLogLine = logLine,
                activeModifierIds = new List<string>(table.activeModifierIds),
            };
            return true;
        }

        private static int DeriveSeed(string zoneId, EncounterContext ctx)
        {
            using (var sha = SHA256.Create())
            {
                int qx = ctx != null ? Mathf.RoundToInt(ctx.position.x) : 0;
                int qz = ctx != null ? Mathf.RoundToInt(ctx.position.z) : 0;
                int day = ctx != null ? ctx.dayNumber : 0;
                int attempt = ctx != null ? ctx.attemptCounter : 0;
                string wallet = ctx?.walletPubkey ?? "";

                string raw = $"{day}|{zoneId}|{qx},{qz}|{attempt}|{wallet}";
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToInt32(hash, 0);
            }
        }

        private static EffectiveEncounterEntry WeightedPick(
            List<EffectiveEncounterEntry> entries,
            System.Random rng,
            int favorScore,
            FavorConfigSO favorConfig)
        {
            FavorConfigSO favor = favorConfig != null ? favorConfig : FavorConfigSO.CreateRuntimeDefault();
            float maxWeight = 1f;
            for (int i = 0; i < entries.Count; i++)
                maxWeight = Mathf.Max(maxWeight, Mathf.Max(0f, entries[i].weight));

            const int Scale = 1000;
            int totalWeight = 0;
            var scaled = new int[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                int entryWeightInt = Mathf.Max(0, Mathf.RoundToInt(entries[i].weight));
                float mult = favor.EncounterWeightMultiplier(favorScore, entryWeightInt, Mathf.RoundToInt(maxWeight));
                scaled[i] = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0f, entries[i].weight) * mult * Scale));
                totalWeight += scaled[i];
            }

            if (totalWeight <= 0)
                return entries[0];

            int roll = rng.Next(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                cumulative += scaled[i];
                if (roll < cumulative)
                    return entries[i];
            }
            return entries[entries.Count - 1];
        }

        private static void BumpLowestIv(int[] ivs, int ivMax)
        {
            if (ivs == null || ivs.Length == 0)
                return;
            int lowestIndex = 0;
            for (int i = 1; i < ivs.Length; i++)
            {
                if (ivs[i] < ivs[lowestIndex])
                    lowestIndex = i;
            }
            if (ivs[lowestIndex] < ivMax)
                ivs[lowestIndex]++;
        }
    }
}
