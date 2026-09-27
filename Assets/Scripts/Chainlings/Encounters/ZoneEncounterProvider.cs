using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Bridges the zone-based encounter system into <see cref="CombatBattleController"/>
    /// via <see cref="IEncounterProvider"/>.
    /// </summary>
    public class ZoneEncounterProvider : MonoBehaviour, IEncounterProvider
    {
        [Header("Fallback (if no zone result)")]
        [SerializeField] private string fallbackSpeciesId = "gilarime";
        [SerializeField] private List<string> defaultMoveIds = new List<string>();
        [SerializeField] private AIPersonality defaultAI = AIPersonality.Aggressive;
        [SerializeField] private EncounterDifficulty defaultDifficulty = EncounterDifficulty.Standard;
        [SerializeField] [Range(0, 5)] private int fallbackStars = 1;
        [SerializeField] [Range(0, 5)] private int fallbackRebirthCount = 0;

        [System.NonSerialized]
        public ZoneEncounterResult ActiveResult;

        public EncounterSnapshot GenerateEncounter(int seed)
        {
            var snapshot = new EncounterSnapshot
            {
                difficulty = ActiveResult != null ? ActiveResult.difficulty : defaultDifficulty,
                teamName = "Wild",
                aiPersonality = defaultAI,
                encounterSeed = seed,
                areaId = ActiveResult != null ? ActiveResult.areaId : "",
                zoneId = ActiveResult != null ? ActiveResult.zoneId : "",
                timeSegment = ActiveResult != null ? ActiveResult.segment : TimeSegment.Day,
                captureEnabled = true,
                capturePolicy = Chainbound.Combat.Core.EncounterCapturePolicy.UseEncounterDefault,
                authoritativeBattleKind = snapshotDifficultyToKind(ActiveResult != null ? ActiveResult.difficulty : defaultDifficulty),
            };

            if (ActiveResult != null && ActiveResult.slots != null && ActiveResult.slots.Count > 0)
            {
                foreach (var slot in ActiveResult.slots)
                    snapshot.chainlings.Add(BuildFromSlot(slot));
                Debug.Log($"[ZoneEncounterProvider] Encounter: {ActiveResult.slots.Count} chainling(s) " +
                          $"(area={ActiveResult.areaId}, zone={ActiveResult.zoneId}, seed={ActiveResult.seed})");
            }
            else
            {
                snapshot.chainlings.Add(BuildFallback(seed));
                Debug.LogWarning("[ZoneEncounterProvider] No zone encounter result; using fallback.");
            }

            return snapshot;
        }

        private ChainlingSnapshot BuildFromSlot(ZoneEncounterSlot slot)
        {
            var moveIds = slot.moveIds != null && slot.moveIds.Count > 0
                ? new List<string>(slot.moveIds)
                : new List<string>(defaultMoveIds);

            var identity = ChainlingVisualIdentity.CloneOrDefault(slot.visualIdentity);
            identity.Normalize();
            if (slot.traits.isMutated &&
                !string.IsNullOrWhiteSpace(slot.traits.mutationId) &&
                !string.Equals(slot.traits.mutationId, ChainlingVisualIdentity.NoneId, System.StringComparison.Ordinal))
            {
                identity.auraId = slot.traits.mutationId.Trim();
            }

            return new ChainlingSnapshot
            {
                speciesId = slot.speciesId,
                displayName = "",
                stars = ChainlingProgressionUtility.NormalizeStars(slot.stars),
                rebirthCount = 0,
                level = 0,
                evolutionStage = 1,
                moveIds = moveIds,
                knownMoveId = slot.knownMoveId ?? "",
                legacyBaseCaptureRate = Mathf.Clamp(slot.baseCaptureRate, 0, 100),
                ivs = slot.traits.ivs ?? new int[6],
                isMutated = slot.traits.isMutated,
                mutationId = slot.traits.mutationId ?? ChainlingVisualIdentity.NoneId,
                captureMutationRarityRank = slot.traits.mutationRarityRank,
                isEntangled = slot.traits.isEntangled,
                entangledType = slot.traits.isEntangled ? slot.traits.entangledType : ChainlingType.None,
                visualIdentity = identity,
            };
        }

        private ChainlingSnapshot BuildFallback(int seed)
        {
            int stars = ChainlingProgressionUtility.NormalizeStars(fallbackStars);
            int rebirthCount = ChainlingProgressionUtility.NormalizeRebirthCount(fallbackRebirthCount);
            return new ChainlingSnapshot
            {
                speciesId = fallbackSpeciesId,
                displayName = "",
                stars = stars,
                rebirthCount = rebirthCount,
                level = 0,
                evolutionStage = 1,
                moveIds = new List<string>(defaultMoveIds),
                knownMoveId = defaultMoveIds != null && defaultMoveIds.Count > 0 ? defaultMoveIds[0] : "",
                legacyBaseCaptureRate = 45,
                // null ivs → RosterSlotUtility rolls seeded debug IVs (do not pass all-zeros expecting a roll).
                ivs = null,
                visualIdentity = ChainlingVisualIdentity.CreateDefault(),
            };
        }

        private static Chainbound.Combat.Core.AuthoritativeBattleKind snapshotDifficultyToKind(EncounterDifficulty difficulty)
        {
            return difficulty == EncounterDifficulty.Boss
                ? Chainbound.Combat.Core.AuthoritativeBattleKind.Boss
                : Chainbound.Combat.Core.AuthoritativeBattleKind.Wild;
        }
    }
}
