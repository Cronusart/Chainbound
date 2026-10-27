using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// In-memory Known / Stamp / RAM for editor and development-build candy-machine presentation.
    /// Does not touch ProfileService, ClosedBeta spend_ram, or Metaplex mint paths.
    /// </summary>
    public sealed class DevCandyMachineInventory : IKnownMoveService, IStampedMoveService
    {
        public readonly struct CatalogMove
        {
            public readonly string moveId;
            public readonly string displayName;
            public readonly RarityTier rarity;
            public readonly bool bossOnly;

            public CatalogMove(string moveId, string displayName, RarityTier rarity, bool bossOnly = false)
            {
                this.moveId = moveId;
                this.displayName = displayName;
                this.rarity = rarity;
                this.bossOnly = bossOnly;
            }
        }

        private readonly VendingConfigSO config;
        private readonly List<CatalogMove> moves;
        private readonly MoveDatabaseSO moveDatabase;
        private readonly ModifierCatalogSO modifierCatalog;
        private readonly IVrfService stampEntropy;
        private readonly System.Random knownMoveRng;
        private readonly List<KnownMoveRecord> known = new List<KnownMoveRecord>();
        private readonly List<StampedMoveRecord> stamped = new List<StampedMoveRecord>();
        private int nextKnownId;

        public DevCandyMachineInventory(
            VendingConfigSO config,
            IReadOnlyList<CatalogMove> catalogMoves,
            IReadOnlyList<string> modifierIds,
            int seed = 1776)
            : this(config, catalogMoves, null, null, seed)
        {
        }

        public DevCandyMachineInventory(
            VendingConfigSO config,
            MoveDatabaseSO moveDatabase,
            ModifierCatalogSO modifierCatalog,
            int seed = 1776)
            : this(config, BuildCatalogMoves(moveDatabase), moveDatabase, modifierCatalog, seed)
        {
        }

        private DevCandyMachineInventory(
            VendingConfigSO config,
            IReadOnlyList<CatalogMove> catalogMoves,
            MoveDatabaseSO moveDatabase,
            ModifierCatalogSO modifierCatalog,
            int seed)
        {
            this.config = config != null ? config : VendingConfigSO.CreateRuntimeDefault();
            this.moveDatabase = moveDatabase;
            this.modifierCatalog = modifierCatalog;
            stampEntropy = new DeterministicVrfService(seed);
            moves = new List<CatalogMove>();
            if (catalogMoves != null)
            {
                for (int i = 0; i < catalogMoves.Count; i++)
                {
                    CatalogMove move = catalogMoves[i];
                    if (!string.IsNullOrWhiteSpace(move.moveId) && !move.bossOnly)
                        moves.Add(move);
                }
            }

            knownMoveRng = new System.Random(seed);
        }

        /// <summary>
        /// Development builds define DEVELOPMENT_BUILD at compile time. Keep that
        /// explicit here instead of relying only on Debug.isDebugBuild, because the
        /// Android player build pipeline can set the development option independently
        /// of the editor's PlayerSettings flag.
        /// </summary>
        public static bool IsDevSurfaceAllowed
        {
            get
            {
#if DEVELOPMENT_BUILD
                return true;
#else
                return Debug.isDebugBuild;
#endif
            }
        }

        public long RamBalance { get; private set; }

        public int RamCostGetKnown => Mathf.Max(0, config.ramCostGetKnown);

        public int RamCostStamp => Mathf.Max(0, config.ramCostStampKnown);

        public IReadOnlyList<KnownMoveRecord> CachedKnown => known;

        public IReadOnlyList<StampedMoveRecord> CachedStamped => stamped;

        public IReadOnlyList<CatalogMove> CatalogMoves => moves;

        public static DevCandyMachineInventory FromCatalogs(
            VendingConfigSO vendingConfig,
            MoveDatabaseSO moveDatabase,
            ModifierCatalogSO modifierCatalog,
            int seed = 1776)
        {
            return new DevCandyMachineInventory(vendingConfig, moveDatabase, modifierCatalog, seed);
        }

        public void GrantRam(long amount)
        {
            if (amount <= 0)
                return;
            RamBalance += amount;
        }

        public KnownMoveRecord GrantKnown(string moveId)
        {
            if (string.IsNullOrWhiteSpace(moveId))
                throw new ArgumentException("A move id is required.", nameof(moveId));

            CatalogMove catalog = FindMove(moveId.Trim());
            KnownMoveRecord record = CreateKnown(catalog.moveId);
            known.Add(record);
            return record;
        }

        public void Clear()
        {
            known.Clear();
            stamped.Clear();
        }

        public void SetRam(long amount)
        {
            RamBalance = Math.Max(0L, amount);
        }

        public Task<KnownMoveRecord> GetKnownAsync(string idempotencyKey)
        {
            if (moves.Count == 0)
                throw new InvalidOperationException("No moves are loaded in the candy catalog.");

            SpendRam(RamCostGetKnown, "Get Known");
            CatalogMove pick = PickMove();
            KnownMoveRecord record = CreateKnown(pick.moveId);
            known.Add(record);
            return Task.FromResult(record);
        }

        public Task RefreshKnownAsync() => Task.CompletedTask;

        public Task<StampTransactionReview> ReviewStampAsync(StampMoveRequest request)
        {
            ValidateStamp(request);
            return Task.FromResult(new StampTransactionReview
            {
                cluster = "dev-local",
                knownInstanceId = request.knownInstanceId,
                ramCost = RamCostStamp,
                outputCollection = "dev-moves",
                simulationSucceeded = true,
                simulationMessage = "Local presentation review. No wallet signature.",
            });
        }

        public async Task<StampMoveResult> StampMoveAsync(StampMoveRequest request)
        {
            ValidateStamp(request);
            KnownMoveRecord selected = FindKnown(request.knownInstanceId);
            int modifierCount = Mathf.Clamp(request.requestedModifierCount, 1, 2);
            MoveDefinitionSO moveDefinition = moveDatabase != null
                ? moveDatabase.GetById(selected.moveId)
                : null;
            byte[] owner = Encoding.UTF8.GetBytes("dev-local");
            byte[] context = Encoding.UTF8.GetBytes(
                $"{request.idempotencyKey}|{selected.instanceId}|v{Mathf.Max(1, config.generatorVersion)}");
            ushort hashedMoveId = MoveDatabaseSO.HashMoveId(selected.moveId);
            VrfRequestResult entropy = await stampEntropy.RequestRandomnessAsync(context);
            if (entropy == null
                || entropy.status != VrfRequestStatus.Fulfilled
                || entropy.randomness == null
                || entropy.randomness.Length == 0)
            {
                return new StampMoveResult
                {
                    error = entropy?.errorMessage ?? "Deterministic VRF mock did not fulfill the stamp request.",
                };
            }

            if (!MoveModifierGenerator.TryGenerateStampedModifiersFromVrf(
                    entropy.randomness,
                    2,
                    hashedMoveId,
                    owner,
                    context,
                    modifierCatalog,
                    moveDefinition,
                    modifierCount,
                    out IReadOnlyList<StampedModifierRoll> rolls,
                    out string generationError,
                    maxAttempts: MoveModifierGenerator.DefaultMaxAttempts,
                    vendingConfig: config))
            {
                return new StampMoveResult
                {
                    error = $"Stamp modifier generation failed: {generationError}",
                };
            }

            SpendRam(RamCostStamp, "Stamp");
            StampedModifierRoll first = rolls[0];
            StampedModifierRoll second = modifierCount > 1 ? rolls[1] : default;
            byte[] traceHash = MoveModifierGenerator.ComputeVrfDerivedSeed(
                entropy.randomness, 2, hashedMoveId, owner, context);

            var record = new StampedMoveRecord
            {
                assetAddress = $"dev-stamped-{Guid.NewGuid():N}",
                moveId = selected.moveId,
                knownInstanceId = selected.instanceId,
                acquisition = LibraryCardFaces.Normalize(selected.acquisition),
                modifier1Id = first.sectionId,
                modifier1EffectValue = first.effectValue,
                modifier1EffectValueLocked = true,
                modifier2Id = modifierCount > 1 ? second.sectionId : null,
                modifier2EffectValue = modifierCount > 1 ? second.effectValue : 0f,
                modifier2EffectValueLocked = modifierCount > 1,
                modifierCount = modifierCount,
                schemaVersion = 1,
                generatorVersion = Mathf.Max(1, config.generatorVersion),
                isCoreAsset = false,
                rarity = MoveModifierGenerator.GenerateRarityFromVrf(
                    entropy.randomness, 2, hashedMoveId, owner, context, config).ToString(),
                entropyTrace = ToHex(traceHash),
            };

            known.RemoveAll(item => string.Equals(item.instanceId, request.knownInstanceId, StringComparison.Ordinal));
            stamped.Add(record);
            return new StampMoveResult
            {
                success = true,
                confirmed = true,
                acquisition = record.acquisition,
                verifiedAsset = record,
            };
        }

        public Task RefreshStampedAsync() => Task.CompletedTask;

        public string FormatOdds()
        {
            float[] weights = config.rarityTierWeightsPercent;
            if (weights == null || weights.Length != 6)
                return "Odds unavailable.";

            return
                $"Common {weights[0]:0.#}% | Uncommon {weights[1]:0.#}% | Rare {weights[2]:0.#}% | " +
                $"Epic {weights[3]:0.#}% | Legendary {weights[4]:0.#}% | Mythic {weights[5]:0.#}%";
        }

        public CatalogMove ResolveMove(string moveId)
        {
            return FindMove(moveId);
        }

        private KnownMoveRecord CreateKnown(string moveId)
        {
            nextKnownId++;
            return new KnownMoveRecord
            {
                instanceId = $"dev-known-{nextKnownId:0000}",
                moveId = moveId,
                modifierCount = 0,
                generatorVersion = Mathf.Max(1, config.generatorVersion),
                poolHash = config.ComputeConfigHash(),
                acquisition = LibraryCardFaces.CandyRoll,
            };
        }

        private void SpendRam(int cost, string action)
        {
            if (cost <= 0)
                return;
            if (RamBalance < cost)
                throw new InvalidOperationException($"Not enough RAM to {action}. Need {cost}, have {RamBalance}.");
            RamBalance -= cost;
        }

        private void ValidateStamp(StampMoveRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.knownInstanceId)
                || string.IsNullOrWhiteSpace(request.idempotencyKey))
                throw new ArgumentException("Stamp requires an exact Known instance and idempotency key.");
            if (request.requestedModifierCount < 1 || request.requestedModifierCount > 2)
                throw new ArgumentOutOfRangeException(nameof(request.requestedModifierCount));
            FindKnown(request.knownInstanceId);
        }

        private KnownMoveRecord FindKnown(string instanceId)
        {
            for (int i = 0; i < known.Count; i++)
            {
                if (string.Equals(known[i].instanceId, instanceId, StringComparison.Ordinal))
                    return known[i];
            }

            throw new InvalidOperationException("That Known move is not in the candy inventory.");
        }

        private CatalogMove FindMove(string moveId)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                if (string.Equals(moves[i].moveId, moveId, StringComparison.Ordinal))
                    return moves[i];
            }

            throw new InvalidOperationException($"Move '{moveId}' is not available in the candy catalog.");
        }

        private CatalogMove PickMove()
        {
            RarityTier tier = PickRarityTier();
            var matches = new List<CatalogMove>();
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].rarity == tier)
                    matches.Add(moves[i]);
            }

            IReadOnlyList<CatalogMove> pool = matches.Count > 0 ? matches : moves;
            return pool[knownMoveRng.Next(pool.Count)];
        }

        private RarityTier PickRarityTier()
        {
            int[] thresholds = config.BuildCumulativeThresholds();
            int roll = knownMoveRng.Next(1, VendingConfigSO.RollBasisPoints + 1);
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (roll <= thresholds[i])
                    return (RarityTier)i;
            }

            return RarityTier.Common;
        }

        private static List<CatalogMove> BuildCatalogMoves(MoveDatabaseSO database)
        {
            var result = new List<CatalogMove>();
            if (database == null || database.moves == null)
                return result;
            for (int i = 0; i < database.moves.Count; i++)
            {
                MoveDefinitionSO definition = database.moves[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.moveId))
                    continue;
                if (IsExcludedFromCandyCatalog(definition))
                    continue;
                result.Add(new CatalogMove(
                    definition.moveId,
                    string.IsNullOrWhiteSpace(definition.displayName)
                        ? definition.moveId
                        : definition.displayName,
                    definition.rarity,
                    MoveAvailability.IsBossOnly(definition)));
            }
            return result;
        }

        private static bool IsExcludedFromCandyCatalog(MoveDefinitionSO definition)
        {
            if (definition == null)
                return false;
            if (MoveAvailability.IsBossOnly(definition))
                return true;
            if (definition.tags == null)
                return false;

            for (int i = 0; i < definition.tags.Count; i++)
            {
                if (string.Equals(definition.tags[i], "NO_CANDY", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string ToHex(byte[] value)
        {
            if (value == null || value.Length == 0)
                return string.Empty;
            return BitConverter.ToString(value).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
