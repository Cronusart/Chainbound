using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class ChainlingsMenuTestHarness : MonoBehaviour,
        IPlayerRosterProvider,
        IPlayerPartyProvider,
        IMenuPartyLineupService,
        IMoveInventoryProvider,
        IMenuMoveEquipService,
        ICosmeticInventoryService,
        IInventoryService,
        IInventoryItemProvenanceService,
        IPlayerStatsProvider,
        IChainlingRebirthService,
        IMenuVisualIdentityService,
        IChainlingsMenuTestHarness
    {
        [Header("Data Sources")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private ChainlingVisualIdentityDatabaseSO identityDatabase;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [FormerlySerializedAs("rewardConfig")]
        [SerializeField] private ProgressionConfigSO progressionConfig;

        [Header("Generation")]
        [SerializeField] private int seed = 1776;
        [SerializeField, Range(1, CombatConstants.MAX_CHAINLINGS)] private int generatedRosterSize = 9;
        [SerializeField, Range(1, CombatConstants.ROSTER_SIZE)] private int generatedPartySize = CombatConstants.ROSTER_SIZE;
        [SerializeField, Range(1, CombatConstants.MAX_KNOWN_MOVES)] private int generatedMoveInventorySize = 48;
        [SerializeField] private bool regenerateOnEnable = true;
        [SerializeField] private bool unlockAllCosmetics = true;
        [FormerlySerializedAs("includeRebirthReadyChainling")]
        [SerializeField] private bool includeCycleReadyChainling = true;
        [SerializeField] private bool allowEntangledVariants = true;
        [SerializeField] private bool seedBagItems = true;

        [Header("Debug Profile")]
        [SerializeField] private string debugProfileId = "debug-profile-chainlings-menu";
        [SerializeField] private string debugWalletAddress = "debug-wallet-chainlings-menu";
        [SerializeField] private string debugTeamName = "Debug Chainlings";
        [SerializeField] private long debugRamBalance = 999999;

        private PlayerRosterSnapshot roster;
        private MoveInventorySnapshot moveInventory;
        private InventorySnapshot bagInventory;
        private ProgressionConfigSO runtimeProgressionConfig;
        private readonly List<string> ownedCosmeticIds = new List<string>();
        private readonly Dictionary<string, string> bagAssetIdsByItemId =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private int generation;

        public string HarnessLabel => "Chainlings Sandbox";
        public SpeciesDatabaseSO SpeciesDatabase => speciesDatabase;

        /// <summary>Live roster cache (mutations like field heals apply here).</summary>
        public PlayerRosterSnapshot CachedRoster
        {
            get
            {
                EnsureGenerated();
                return roster;
            }
        }

        public void Configure(
            SpeciesDatabaseSO speciesDb,
            CombatVisualDatabaseSO combatVisualDb,
            ChainlingVisualIdentityDatabaseSO chainlingIdentityDb,
            MoveDatabaseSO moveDb,
            ProgressionConfigSO progression = null)
        {
            if (speciesDb != null)
                speciesDatabase = speciesDb;
            if (combatVisualDb != null)
                visualDatabase = combatVisualDb;
            if (chainlingIdentityDb != null)
                identityDatabase = chainlingIdentityDb;
            if (moveDb != null)
                moveDatabase = moveDb;
            if (progression != null)
                progressionConfig = progression;
        }

        public void ApplyGenerationSettings(
            int newSeed,
            int rosterSize,
            int partySize,
            int moveInventorySize,
            bool regenerateOnEnableFlag,
            bool unlockCosmetics,
            bool includeRebirthReady,
            bool allowEntangled)
        {
            seed = newSeed;
            generatedRosterSize = rosterSize;
            generatedPartySize = partySize;
            generatedMoveInventorySize = moveInventorySize;
            regenerateOnEnable = regenerateOnEnableFlag;
            unlockAllCosmetics = unlockCosmetics;
            includeCycleReadyChainling = includeRebirthReady;
            allowEntangledVariants = allowEntangled;
        }

        private void OnEnable()
        {
            if (regenerateOnEnable && roster == null)
                RegenerateInternal();
        }

        [ContextMenu("Regenerate Test Roster")]
        public void RegenerateTestRoster()
        {
            RegenerateInternal();
        }

        [ContextMenu("Randomize Party")]
        public void RandomizeParty()
        {
            EnsureGenerated();
            RandomizePartyInternal(new System.Random(seed + generation * 7919 + 31));
        }

        [ContextMenu("Clear Runtime State")]
        public void ClearRuntimeState()
        {
            roster = null;
            moveInventory = null;
            ownedCosmeticIds.Clear();
        }

        public Task<PlayerRosterSnapshot> FetchRosterAsync(string playerProfileId, string walletAddress)
        {
            EnsureGenerated();
            return Task.FromResult(CloneRoster(roster, ResolveProfileId(playerProfileId)));
        }

        public PartySnapshot GetCurrentParty()
        {
            EnsureGenerated();

            var party = new List<PlayerRosterEntry>();
            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                if (entry != null && entry.isPartyMember)
                    party.Add(entry);
            }

            party.Sort((a, b) => a.partySlot.CompareTo(b.partySlot));

            var snapshot = new PartySnapshot { teamName = debugTeamName };
            for (int i = 0; i < party.Count && i < CombatConstants.ROSTER_SIZE; i++)
            {
                if (party[i] != null && party[i].snapshot != null)
                    snapshot.chainlings.Add(CloneSnapshot(party[i].snapshot));
            }

            return snapshot;
        }

        public Task<MenuMutationResult> SavePartyLineupAsync(
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string[] orderedPartyChainlingBackendIds)
        {
            EnsureGenerated();
            return Task.FromResult(ApplyPartyLineup(orderedPartyChainlingBackendIds));
        }

        public Task<MenuMutationResult> ReleaseChainlingAsync(
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string chainlingBackendId)
        {
            EnsureGenerated();
            if (roster == null || roster.chainlings == null || string.IsNullOrWhiteSpace(chainlingBackendId))
                return Task.FromResult(MenuMutationResult.Failed("chainling_id_required"));

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                if (entry?.snapshot == null ||
                    !string.Equals(entry.snapshot.backendChainlingId, chainlingBackendId, StringComparison.Ordinal))
                    continue;

                if (entry.isPartyMember)
                    return Task.FromResult(MenuMutationResult.Failed("still_in_party"));

                roster.chainlings.RemoveAt(i);
                return Task.FromResult(MenuMutationResult.Ok());
            }

            return Task.FromResult(MenuMutationResult.Failed("chainling_not_found"));
        }

        public Task<PlayerRosterSnapshot> RegenerateRosterAsync(string playerProfileId, string walletAddress)
        {
            RegenerateInternal();
            return Task.FromResult(CloneRoster(roster, ResolveProfileId(playerProfileId)));
        }

        public Task<MenuMutationResult> RandomizeLineupAsync(string playerProfileId, string walletAddress)
        {
            EnsureGenerated();
            RandomizePartyInternal(new System.Random(seed + generation * 7919 + 97));
            return Task.FromResult(MenuMutationResult.Ok());
        }

        public Task<MoveInventorySnapshot> FetchMoveInventoryAsync(string playerProfileId, string walletAddress)
        {
            EnsureGenerated();
            return Task.FromResult(CloneMoveInventory(moveInventory, ResolveProfileId(playerProfileId)));
        }

        /// <summary>
        /// Supplies party-slot data for party-tab read-model debug (see <c>ChainlingPartyReadModelDebug</c>).
        /// </summary>
        public bool TryCreatePartyReadModelInputs(
            int slotIndex,
            out PlayerRosterEntry entry,
            out SpeciesDefinitionSO species,
            out IReadOnlyList<MenuMoveItem> moveInventoryItems)
        {
            entry = null;
            species = null;
            moveInventoryItems = null;
            EnsureGenerated();

            if (roster?.chainlings == null)
                return false;

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry candidate = roster.chainlings[i];
                if (candidate == null || !candidate.isPartyMember || candidate.partySlot != slotIndex)
                    continue;

                entry = candidate;
                species = speciesDatabase != null && candidate.snapshot != null
                    ? speciesDatabase.GetById(candidate.snapshot.speciesId)
                    : null;
                moveInventoryItems = moveInventory?.moves;
                return true;
            }

            return false;
        }

        public Task<PlayerStatsSnapshot> FetchStatsAsync(string playerProfileId, string walletAddress)
        {
            EnsureGenerated();

            return Task.FromResult(new PlayerStatsSnapshot
            {
                playerProfileId = ResolveProfileId(playerProfileId),
                displayName = debugTeamName,
                walletAddress = string.IsNullOrWhiteSpace(walletAddress) ? debugWalletAddress : walletAddress.Trim(),
                ramBalance = debugRamBalance,
                handbookUnlocked = true,
                uniqueSpeciesCaptured = roster != null && roster.chainlings != null ? roster.chainlings.Count : 0,
            });
        }

        public Task<MenuMutationResult> EquipMoveAsync(
            string playerProfileId,
            string walletAddress,
            string chainlingId,
            int slot,
            string moveItemId)
        {
            EnsureGenerated();
            return Task.FromResult(EquipMoveInternal(chainlingId, slot, moveItemId));
        }

        public Task<IReadOnlyList<string>> FetchOwnedCosmeticIdsAsync(string playerProfileId, string walletAddress)
        {
            EnsureGenerated();
            return Task.FromResult((IReadOnlyList<string>)new List<string>(ownedCosmeticIds));
        }

        public Task<InventorySnapshot> FetchInventoryAsync(string playerProfileId, string walletAddress)
        {
            EnsureGenerated();
            return Task.FromResult(CloneBagInventory(bagInventory));
        }

        public IReadOnlyDictionary<string, string> GetAssetIdsByItemId()
        {
            EnsureGenerated();
            return new Dictionary<string, string>(bagAssetIdsByItemId, StringComparer.Ordinal);
        }

        public bool TryGetAssetIdForItemId(string itemId, out string assetId)
        {
            EnsureGenerated();
            assetId = null;
            string normalized = NormalizeId(itemId);
            return normalized.Length > 0 && bagAssetIdsByItemId.TryGetValue(normalized, out assetId);
        }

        public void ClearItemInventoryCache()
        {
            // Harness keeps live bag state; no remote cache.
        }

        public Task<VisualIdentityMutationResult> SaveVisualIdentityAsync(
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string chainlingBackendId,
            ChainlingVisualIdentity visualIdentity,
            IReadOnlyCollection<string> ownedIds,
            PlayerRosterSnapshot rosterCacheToPatch,
            ChainlingVisualIdentityDatabaseSO identityDb = null,
            string speciesId = null,
            IReadOnlyDictionary<string, string> cosmeticAssetIdsByCosmeticId = null)
        {
            EnsureGenerated();

            PlayerRosterEntry entry = FindEntry(chainlingBackendId);
            if (entry == null || entry.snapshot == null)
                return Task.FromResult(VisualIdentityMutationResult.Failed("debug_chainling_not_found"));

            ChainlingVisualIdentity normalized = ChainlingVisualIdentity.CloneForPlayerSave(visualIdentity);
            string ownershipError = ValidateOwnedCosmetics(normalized, ownedIds);
            if (!string.IsNullOrEmpty(ownershipError))
                return Task.FromResult(VisualIdentityMutationResult.Failed(ownershipError));

            entry.snapshot.visualIdentity = ChainlingVisualIdentity.CloneForPlayerSave(normalized);
            SupabaseVisualIdentityMutation.PatchRosterCache(rosterCacheToPatch, chainlingBackendId, normalized);
            return Task.FromResult(VisualIdentityMutationResult.Ok(normalized));
        }

        public Task<ChainlingRebirthResult> RebirthAsync(
            string playerProfileId,
            string walletAddress,
            string chainlingId,
            CoreStat chosenStat)
        {
            EnsureGenerated();

            PlayerRosterEntry entry = FindEntry(chainlingId);
            if (entry == null || entry.snapshot == null)
                return Task.FromResult(FailedRebirth("debug_chainling_not_found"));

            ChainlingSnapshot snapshot = entry.snapshot;
            int stars = ChainlingProgressionUtility.NormalizeStars(snapshot.stars);
            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount);
            if (stars < CombatConstants.STAR_MAX)
                return Task.FromResult(FailedRebirth($"debug_rebirth_requires_{CombatConstants.STAR_MAX}_stars"));
            if (rebirth >= CombatConstants.REBIRTH_MAX)
                return Task.FromResult(FailedRebirth("debug_rebirth_maxed"));

            snapshot.stars = 0;
            snapshot.masteryXp = 0;
            snapshot.rebirthCount = rebirth + 1;
            snapshot.ivs = snapshot.ivs != null && snapshot.ivs.Length == CombatConstants.STAT_COUNT
                ? snapshot.ivs
                : new int[CombatConstants.STAT_COUNT];
            snapshot.rebirthIvBonuses = snapshot.rebirthIvBonuses != null &&
                                       snapshot.rebirthIvBonuses.Length == CombatConstants.STAT_COUNT
                ? snapshot.rebirthIvBonuses
                : new int[CombatConstants.STAT_COUNT];
            int statIndex = StatIndex(chosenStat);
            if (statIndex >= 0 && statIndex < snapshot.rebirthIvBonuses.Length)
                snapshot.rebirthIvBonuses[statIndex] += CombatConstants.REBIRTH_IV_BONUS_PER_REBIRTH;

            entry.masteryXp = 0;
            return Task.FromResult(new ChainlingRebirthResult
            {
                success = true,
                error = "",
                ramBalance = debugRamBalance,
                updatedChainling = CloneSnapshot(snapshot),
            });
        }

        private void EnsureGenerated()
        {
            if (roster == null || moveInventory == null || bagInventory == null)
                RegenerateInternal();
        }

        private void RegenerateInternal()
        {
            generation++;
            var rng = new System.Random(seed + generation * 7919);
            BuildOwnedCosmeticIds();
            BuildRoster(rng);
            BuildMoveInventory(rng);
            RandomizePartyInternal(rng);
            SeedInitialLoadouts(rng);
            SeedBagInventory(rng);
        }

        private void BuildRoster(System.Random rng)
        {
            List<SpeciesDefinitionSO> species = GetSpeciesCandidates();
            int count = Mathf.Clamp(generatedRosterSize, 1, CombatConstants.MAX_CHAINLINGS);
            roster = new PlayerRosterSnapshot
            {
                playerProfileId = debugProfileId,
                chainlings = new List<PlayerRosterEntry>(count),
            };

            for (int i = 0; i < count; i++)
            {
                SpeciesDefinitionSO definition = species.Count > 0 ? species[rng.Next(species.Count)] : null;
                string speciesId = definition != null && !string.IsNullOrWhiteSpace(definition.speciesId)
                    ? definition.speciesId
                    : $"debug_species_{i % 4:D2}";
                string displayName = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName
                    : speciesId;

                int rebirth = rng.Next(0, Math.Min(2, CombatConstants.REBIRTH_MAX) + 1);
                int masteryXp = BuildSandboxMasteryXp(i, rebirth, rng);
                int stars = ChainlingProgressionUtility.NormalizeStars(
                    StarProgressionUtility.GetStarCount(masteryXp, rebirth, ResolveProgressionConfig()));
                if (includeCycleReadyChainling && i == 0)
                {
                    rebirth = 0;
                    masteryXp = BuildRebirthReadyMasteryXp();
                    stars = CombatConstants.STAR_MAX;
                }
                else if (i == 2)
                {
                    rebirth = CombatConstants.REBIRTH_MAX;
                    masteryXp = BuildSandboxMasteryXp(i, rebirth, rng);
                    stars = ChainlingProgressionUtility.NormalizeStars(
                        StarProgressionUtility.GetStarCount(masteryXp, rebirth, ResolveProgressionConfig()));
                }

                var pveRecord = new ChainlingPveServiceRecord
                {
                    totalVictories = rng.Next(0, 40),
                    xpEarnedLifetime = rng.Next(0, 12000),
                    progressionMovesGranted = rng.Next(0, 8),
                    damageDealt = rng.Next(0, 30000),
                    damageTaken = rng.Next(0, 20000),
                    damageMitigated = rng.Next(0, 7000),
                    healingTotal = rng.Next(0, 9000),
                    enemyKos = rng.Next(0, 60),
                };

                roster.chainlings.Add(new PlayerRosterEntry
                {
                    isPartyMember = false,
                    partySlot = -1,
                    masteryXp = masteryXp,
                    pveServiceRecord = pveRecord,
                    battleStats = new ChainlingBattleStats
                    {
                        battlesWon = pveRecord.totalVictories,
                        battlesLost = rng.Next(0, 25),
                        kosScored = pveRecord.enemyKos,
                        timesKod = rng.Next(0, 30),
                        timesCaptured = rng.Next(0, 4),
                    },
                    snapshot = BuildSandboxSnapshot(
                        i,
                        definition,
                        speciesId,
                        displayName,
                        stars,
                        masteryXp,
                        rebirth,
                        rng),
                });
            }
        }

        private ChainlingSnapshot BuildSandboxSnapshot(
            int index,
            SpeciesDefinitionSO definition,
            string speciesId,
            string displayName,
            int stars,
            int masteryXp,
            int rebirth,
            System.Random rng)
        {
            var snapshot = new ChainlingSnapshot
            {
                backendChainlingId = $"debug-chainling-{generation:D3}-{index:D3}",
                speciesId = speciesId,
                displayName = $"{displayName} {index + 1}",
                level = 50,
                stars = stars,
                masteryXp = masteryXp,
                rebirthCount = rebirth,
                evolutionStage = ResolveEvolutionStage(definition, rng),
                visualIdentity = BuildRandomVisualIdentity(speciesId, rng),
                ivs = BuildIvs(rng),
                moveIds = new List<string>(),
                modifier1SectionIds = new List<string>(),
                modifier2SectionIds = new List<string>(),
            };

            if (index == 1)
            {
                snapshot.nftMintAddress = $"debug-anchored-core-asset-{generation:D3}-{index:D3}";
                snapshot.archivedAtUnix = 1782560000L + generation * 1000L + index;
            }

            if (index == 2 && allowEntangledVariants)
            {
                snapshot.visualIdentity.visualVariantId = ChainlingVisualIdentity.EntangledVariantId;
                snapshot.visualIdentity.Normalize();
            }

            snapshot.currentHp = BuildSandboxCurrentHp(index, snapshot, definition, rng);
            return snapshot;
        }

        private int BuildSandboxMasteryXp(int index, int rebirth, System.Random rng)
        {
            ProgressionConfigSO config = ResolveProgressionConfig();
            int segmentCount = config.starXpThresholds != null && config.starXpThresholds.Length > 0
                ? config.starXpThresholds.Length + 1
                : CombatConstants.STAR_MAX + 1;
            int targetSegment = Mathf.Abs(index) % Mathf.Max(1, segmentCount);
            int xp = 0;
            for (int i = 0; i < targetSegment && config.starXpThresholds != null && i < config.starXpThresholds.Length; i++)
                xp += StarProgressionUtility.GetXpForStar(i, rebirth, config);

            int segmentSize = StarProgressionUtility.GetXpForStar(targetSegment, rebirth, config);
            float[] progressFractions = { 0.08f, 0.24f, 0.47f, 0.68f, 0.83f, 0.96f };
            float fraction = progressFractions[index % progressFractions.Length];
            if (rng != null)
                fraction = Mathf.Clamp01(fraction + (float)(rng.NextDouble() * 0.06d - 0.03d));

            return xp + Mathf.Clamp(Mathf.RoundToInt(segmentSize * fraction), 0, Mathf.Max(0, segmentSize - 1));
        }

        private int BuildRebirthReadyMasteryXp()
        {
            ProgressionConfigSO config = ResolveProgressionConfig();
            int maxStarXp = StarProgressionUtility.GetTotalXpForMaxStar(0, config);
            int masterySegment = StarProgressionUtility.GetXpForStar(config.starXpThresholds.Length, 0, config);
            return maxStarXp + Mathf.Max(1, Mathf.RoundToInt(masterySegment * 0.35f));
        }

        private int BuildSandboxCurrentHp(
            int index,
            ChainlingSnapshot snapshot,
            SpeciesDefinitionSO definition,
            System.Random rng)
        {
            int maxHp = definition != null ? ChainlingVitalsUtility.BuildMaxHp(snapshot, definition) : 100;
            if (maxHp <= 0)
                maxHp = 100;

            float[] fillSamples = { 1f, 0.84f, 0.67f, 0.49f, 0.31f, 0.72f };
            float fill = fillSamples[index % fillSamples.Length];
            if (rng != null && fill < 0.99f)
                fill = Mathf.Clamp(fill + (float)(rng.NextDouble() * 0.08d - 0.04d), 0.1f, 1f);

            return Mathf.Clamp(Mathf.RoundToInt(maxHp * fill), 1, maxHp);
        }

        private ProgressionConfigSO ResolveProgressionConfig()
        {
            if (progressionConfig != null)
                return progressionConfig;

            if (runtimeProgressionConfig == null)
                runtimeProgressionConfig = BalanceConfigRuntime.Progression;

            return runtimeProgressionConfig;
        }

        private void BuildMoveInventory(System.Random rng)
        {
            List<MoveDefinitionSO> moves = GetMoveCandidates();
            int count = Mathf.Clamp(generatedMoveInventorySize, 1, CombatConstants.MAX_KNOWN_MOVES);
            moveInventory = new MoveInventorySnapshot
            {
                playerProfileId = debugProfileId,
                moves = new List<MenuMoveItem>(count),
                loadouts = new List<MenuMoveLoadoutSlot>(),
            };

            for (int i = 0; i < count; i++)
            {
                MoveDefinitionSO definition = moves.Count > 0 ? moves[rng.Next(moves.Count)] : null;
                string moveId = definition != null && !string.IsNullOrWhiteSpace(definition.moveId)
                    ? definition.moveId
                    : $"debug_move_{i % 8:D2}";

                moveInventory.moves.Add(new MenuMoveItem
                {
                    moveItemId = $"debug-move-item-{generation:D3}-{i:D3}",
                    moveId = moveId,
                    tier = (MoveTier)rng.Next(0, 3),
                    modifier1SectionId = "",
                    modifier2SectionId = "",
                    locked = rng.NextDouble() < 0.05d,
                    exportedAssetId = $"debug-asset-{generation:D3}-{i:D3}",
                    equippedChainlingId = "",
                    equippedSlot = -1,
                });
            }
        }

        private void RandomizePartyInternal(System.Random rng)
        {
            if (roster == null || roster.chainlings == null)
                return;

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                if (roster.chainlings[i] == null)
                    continue;

                roster.chainlings[i].isPartyMember = false;
                roster.chainlings[i].partySlot = -1;
            }

            int count = Mathf.Clamp(generatedPartySize, 1, Math.Min(CombatConstants.ROSTER_SIZE, roster.chainlings.Count));
            int seededPartyCount = Mathf.Min(count, Math.Min(3, roster.chainlings.Count));
            for (int i = 0; i < seededPartyCount; i++)
            {
                if (roster.chainlings[i] == null)
                    continue;

                roster.chainlings[i].isPartyMember = true;
                roster.chainlings[i].partySlot = i;
            }

            if (seededPartyCount >= count)
                return;

            var candidates = new List<PlayerRosterEntry>();
            for (int i = seededPartyCount; i < roster.chainlings.Count; i++)
            {
                if (roster.chainlings[i] != null)
                    candidates.Add(roster.chainlings[i]);
            }
            Shuffle(candidates, rng);
            for (int i = seededPartyCount; i < count; i++)
            {
                int candidateIndex = i - seededPartyCount;
                if (candidateIndex < 0 || candidateIndex >= candidates.Count || candidates[candidateIndex] == null)
                    continue;

                candidates[candidateIndex].isPartyMember = true;
                candidates[candidateIndex].partySlot = i;
            }
        }

        private void SeedInitialLoadouts(System.Random rng)
        {
            if (roster == null || moveInventory == null)
                return;

            var moveIndices = new List<int>();
            for (int i = 0; i < moveInventory.moves.Count; i++)
            {
                if (moveInventory.moves[i] != null && !moveInventory.moves[i].locked)
                    moveIndices.Add(i);
            }

            Shuffle(moveIndices, rng);
            int cursor = 0;

            // Party first so scroll-overflow loadouts are not starved by bench fills.
            var partyEntries = new List<PlayerRosterEntry>();
            var benchEntries = new List<PlayerRosterEntry>();
            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                if (entry == null || entry.snapshot == null)
                    continue;

                if (entry.isPartyMember)
                    partyEntries.Add(entry);
                else
                    benchEntries.Add(entry);
            }

            partyEntries.Sort((a, b) => a.partySlot.CompareTo(b.partySlot));
            bool assignedOverflow = false;
            bool assignedUnderOrEqual = false;
            for (int i = 0; i < partyEntries.Count && cursor < moveIndices.Count; i++)
            {
                PlayerRosterEntry entry = partyEntries[i];
                int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(entry.snapshot.rebirthCount);
                bool forceOverflow = !assignedOverflow &&
                                     maxSlots > MoveLoadoutSlotRules.VisibleRowsBeforeScroll &&
                                     (i == partyEntries.Count - 1 || rng == null || rng.NextDouble() < 0.7d);
                bool forceUnder = !assignedUnderOrEqual &&
                                  !forceOverflow &&
                                  (i == partyEntries.Count - 1 || rng == null || rng.NextDouble() < 0.55d);

                int slotsToFill = ResolveSandboxEquippedMoveCount(
                    entry,
                    maxSlots,
                    rng,
                    forceOverflow: forceOverflow,
                    forceUnderOrEqual: forceUnder);
                if (slotsToFill > MoveLoadoutSlotRules.VisibleRowsBeforeScroll)
                    assignedOverflow = true;
                else
                    assignedUnderOrEqual = true;

                cursor = EquipSandboxMoves(entry, slotsToFill, moveIndices, cursor);
            }

            for (int i = 0; i < benchEntries.Count && cursor < moveIndices.Count; i++)
            {
                PlayerRosterEntry entry = benchEntries[i];
                int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(entry.snapshot.rebirthCount);
                int slotsToFill = ResolveSandboxEquippedMoveCount(
                    entry,
                    maxSlots,
                    rng,
                    forceOverflow: false,
                    forceUnderOrEqual: false);
                cursor = EquipSandboxMoves(entry, slotsToFill, moveIndices, cursor);
            }
        }

        private int EquipSandboxMoves(
            PlayerRosterEntry entry,
            int slotsToFill,
            List<int> moveIndices,
            int cursor)
        {
            if (entry == null || entry.snapshot == null || moveIndices == null)
                return cursor;

            var usedMoveIds = new HashSet<string>(StringComparer.Ordinal);
            for (int slot = 0; slot < slotsToFill && cursor < moveIndices.Count; slot++)
            {
                MenuMoveItem move = moveInventory.moves[moveIndices[cursor++]];
                if (move == null || !usedMoveIds.Add(move.moveId ?? ""))
                {
                    slot--;
                    continue;
                }

                EquipMoveInternal(entry.snapshot.backendChainlingId, slot, move.moveItemId);
            }

            return cursor;
        }

        /// <summary>
        /// Party members get a random equipped count from 2..maxSlots so Moves view can
        /// exercise both the authored 4-row viewport and vertical auto-scroll overflow.
        /// Bench/storage chainlings stay lighter (1–2).
        /// </summary>
        private static int ResolveSandboxEquippedMoveCount(
            PlayerRosterEntry entry,
            int maxSlots,
            System.Random rng,
            bool forceOverflow,
            bool forceUnderOrEqual)
        {
            int clampedMax = Mathf.Max(1, maxSlots);
            if (entry == null || !entry.isPartyMember)
                return rng != null ? rng.Next(1, Mathf.Min(2, clampedMax) + 1) : 1;

            int underOrEqualMax = Mathf.Min(clampedMax, MoveLoadoutSlotRules.VisibleRowsBeforeScroll);
            if (forceOverflow && clampedMax > MoveLoadoutSlotRules.VisibleRowsBeforeScroll)
            {
                int overflowMin = MoveLoadoutSlotRules.VisibleRowsBeforeScroll + 1;
                return rng != null
                    ? rng.Next(overflowMin, clampedMax + 1)
                    : clampedMax;
            }

            if (forceUnderOrEqual || clampedMax <= MoveLoadoutSlotRules.VisibleRowsBeforeScroll)
            {
                if (rng == null)
                    return underOrEqualMax;
                return rng.Next(2, underOrEqualMax + 1);
            }

            // Remaining party members: bias toward overflow when rebirth allows >4 slots.
            if (rng != null && rng.NextDouble() < 0.55d)
            {
                int overflowMin = MoveLoadoutSlotRules.VisibleRowsBeforeScroll + 1;
                return rng.Next(overflowMin, clampedMax + 1);
            }

            if (rng == null)
                return underOrEqualMax;

            return rng.Next(2, underOrEqualMax + 1);
        }

        private MenuMutationResult ApplyPartyLineup(string[] orderedIds)
        {
            if (orderedIds == null || orderedIds.Length == 0 || orderedIds.Length > CombatConstants.ROSTER_SIZE)
                return MenuMutationResult.Failed($"debug_party_must_have_between_1_and_{CombatConstants.ROSTER_SIZE}_entries");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < orderedIds.Length; i++)
            {
                string id = NormalizeId(orderedIds[i]);
                if (string.IsNullOrEmpty(id))
                    return MenuMutationResult.Failed("debug_party_contains_empty_chainling_id");
                if (!seen.Add(id))
                    return MenuMutationResult.Failed($"debug_party_contains_duplicate:{id}");
                if (FindEntry(id) == null)
                    return MenuMutationResult.Failed($"debug_chainling_not_found:{id}");
            }

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                if (roster.chainlings[i] == null)
                    continue;

                roster.chainlings[i].isPartyMember = false;
                roster.chainlings[i].partySlot = -1;
            }

            for (int i = 0; i < orderedIds.Length; i++)
            {
                PlayerRosterEntry entry = FindEntry(orderedIds[i]);
                entry.isPartyMember = true;
                entry.partySlot = i;
            }

            return MenuMutationResult.Ok();
        }

        private MenuMutationResult EquipMoveInternal(string chainlingId, int slot, string moveItemId)
        {
            string normalizedChainlingId = NormalizeId(chainlingId);
            if (string.IsNullOrEmpty(normalizedChainlingId))
                return MenuMutationResult.Failed("debug_chainling_id_required");

            PlayerRosterEntry entry = FindEntry(normalizedChainlingId);
            if (entry == null || entry.snapshot == null)
                return MenuMutationResult.Failed($"debug_chainling_not_found:{normalizedChainlingId}");
            if (slot < 0 || slot >= CombatConstants.EQUIPPED_SLOTS_ON_CHAIN)
                return MenuMutationResult.Failed($"debug_slot_out_of_range:{slot}");
            int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(entry.snapshot.rebirthCount);
            if (slot >= maxSlots)
                return MenuMutationResult.Failed($"debug_slot_exceeds_rebirth_cap:{slot}");

            ClearMoveSlot(normalizedChainlingId, slot);

            string normalizedMoveItemId = NormalizeId(moveItemId);
            if (string.IsNullOrEmpty(normalizedMoveItemId))
                return MenuMutationResult.Ok();

            MenuMoveItem move = FindMove(normalizedMoveItemId);
            if (move == null)
                return MenuMutationResult.Failed($"debug_move_item_not_found:{normalizedMoveItemId}");
            if (move.locked)
                return MenuMutationResult.Failed("debug_move_item_locked");

            if (!string.IsNullOrWhiteSpace(move.equippedChainlingId) && move.equippedSlot >= 0)
                ClearMoveSlot(move.equippedChainlingId, move.equippedSlot);

            move.equippedChainlingId = normalizedChainlingId;
            move.equippedSlot = slot;
            moveInventory.loadouts.Add(new MenuMoveLoadoutSlot
            {
                chainlingId = normalizedChainlingId,
                slot = slot,
                moveItemId = normalizedMoveItemId,
            });
            RefreshSnapshotLoadoutFromInventory(normalizedChainlingId);

            return MenuMutationResult.Ok();
        }

        private void ClearMoveSlot(string chainlingId, int slot)
        {
            if (moveInventory == null || moveInventory.loadouts == null)
                return;

            bool changed = false;
            for (int i = moveInventory.loadouts.Count - 1; i >= 0; i--)
            {
                MenuMoveLoadoutSlot loadout = moveInventory.loadouts[i];
                if (loadout == null ||
                    !string.Equals(loadout.chainlingId, chainlingId, StringComparison.Ordinal) ||
                    loadout.slot != slot)
                {
                    continue;
                }

                MenuMoveItem move = FindMove(loadout.moveItemId);
                if (move != null)
                {
                    move.equippedChainlingId = "";
                    move.equippedSlot = -1;
                }

                moveInventory.loadouts.RemoveAt(i);
                changed = true;
            }

            if (changed)
                RefreshSnapshotLoadoutFromInventory(chainlingId);
        }

        private void RefreshSnapshotLoadoutFromInventory(string chainlingId)
        {
            PlayerRosterEntry entry = FindEntry(chainlingId);
            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            if (snapshot == null)
                return;

            if (snapshot.moveIds == null)
                snapshot.moveIds = new List<string>();
            if (snapshot.modifier1SectionIds == null)
                snapshot.modifier1SectionIds = new List<string>();
            if (snapshot.modifier2SectionIds == null)
                snapshot.modifier2SectionIds = new List<string>();

            snapshot.moveIds.Clear();
            snapshot.modifier1SectionIds.Clear();
            snapshot.modifier2SectionIds.Clear();

            if (moveInventory == null || moveInventory.loadouts == null)
                return;

            int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(snapshot.rebirthCount);
            var loadouts = new List<MenuMoveLoadoutSlot>();
            for (int i = 0; i < moveInventory.loadouts.Count; i++)
            {
                MenuMoveLoadoutSlot loadout = moveInventory.loadouts[i];
                if (loadout == null ||
                    !string.Equals(loadout.chainlingId, chainlingId, StringComparison.Ordinal) ||
                    loadout.slot < 0 ||
                    loadout.slot >= maxSlots)
                {
                    continue;
                }

                loadouts.Add(loadout);
            }

            loadouts.Sort((a, b) => a.slot.CompareTo(b.slot));
            for (int i = 0; i < loadouts.Count && snapshot.moveIds.Count < maxSlots; i++)
            {
                MenuMoveItem move = FindMove(loadouts[i].moveItemId);
                if (move == null || string.IsNullOrWhiteSpace(move.moveId))
                    continue;

                snapshot.moveIds.Add(move.moveId);
                snapshot.modifier1SectionIds.Add(move.modifier1SectionId ?? "");
                snapshot.modifier2SectionIds.Add(move.modifier2SectionId ?? "");
            }
        }

        private ChainlingVisualIdentity BuildRandomVisualIdentity(string speciesId, System.Random rng)
        {
            var identity = ChainlingVisualIdentity.CreateDefault();
            identity.visualVariantId = PickVisualVariantId(speciesId, rng);
            identity.auraId = PickDefinitionId(identityDatabase != null ? identityDatabase.Auras : null, ChainlingVisualIdentity.NoneId, rng, 0.45d);
            identity.hudCardVariantId = PickDefinitionId(identityDatabase != null ? identityDatabase.HudCardVariants : null, ChainlingVisualIdentity.DefaultId, rng, 0.45d);
            identity.partyColumnVariantId = PickDefinitionId(identityDatabase != null ? identityDatabase.PartyColumnVariants : null, ChainlingVisualIdentity.DefaultId, rng, 0.45d);
            identity.swapInFxId = PickDefinitionId(identityDatabase != null ? identityDatabase.SwapInFx : null, ChainlingVisualIdentity.DefaultId, rng, 0.45d);
            identity.swapOutFxId = PickDefinitionId(identityDatabase != null ? identityDatabase.SwapOutFx : null, ChainlingVisualIdentity.DefaultId, rng, 0.45d);
            identity.koFxId = PickDefinitionId(identityDatabase != null ? identityDatabase.KoFx : null, ChainlingVisualIdentity.NoneId, rng, 0.25d);
            identity.victoryPoseId = PickDefinitionId(identityDatabase != null ? identityDatabase.VictoryPoses : null, ChainlingVisualIdentity.DefaultId, rng, 0.45d);
            identity.idleStyleId = PickDefinitionId(identityDatabase != null ? identityDatabase.IdleStyles : null, ChainlingVisualIdentity.NoneId, rng, 0.25d);
            identity.idleFlourishId = PickDefinitionId(identityDatabase != null ? identityDatabase.IdleFlourishes : null, ChainlingVisualIdentity.NoneId, rng, 0.45d);
            identity.meleeAttackStyleId = PickDefinitionId(identityDatabase != null ? identityDatabase.MeleeAttackStyles : null, ChainlingVisualIdentity.NoneId, rng, 0.45d);
            identity.rangedAttackStyleId = PickDefinitionId(identityDatabase != null ? identityDatabase.RangedAttackStyles : null, ChainlingVisualIdentity.NoneId, rng, 0.45d);
            identity.hitReactionStyleId = PickDefinitionId(identityDatabase != null ? identityDatabase.HitReactionStyles : null, ChainlingVisualIdentity.NoneId, rng, 0.25d);
            identity.Normalize();
            return identity;
        }

        private string PickVisualVariantId(string speciesId, System.Random rng)
        {
            var ids = new List<string> { ChainlingVisualIdentity.BaseVariantId };
            if (allowEntangledVariants)
                ids.Add(ChainlingVisualIdentity.EntangledVariantId);

            if (visualDatabase != null)
            {
                List<CombatVisualDatabaseSO.Entry> entries = visualDatabase.GetEntriesForSpecies(speciesId);
                for (int i = 0; i < entries.Count; i++)
                {
                    CombatVisualDatabaseSO.Entry entry = entries[i];
                    if (entry == null)
                        continue;

                    string id = ChainlingVisualIdentity.NormalizeId(entry.visualVariantId, ChainlingVisualIdentity.BaseVariantId);
                    if (!ids.Contains(id))
                        ids.Add(id);
                }
            }

            return ids[rng.Next(ids.Count)];
        }

        private static string PickDefinitionId<T>(
            IReadOnlyList<T> definitions,
            string fallbackId,
            System.Random rng,
            double equipChance)
            where T : ChainlingVisualDefinitionSO
        {
            if (definitions == null || definitions.Count == 0 || rng.NextDouble() > equipChance)
                return fallbackId;

            var ids = new List<string>();
            for (int i = 0; i < definitions.Count; i++)
            {
                T definition = definitions[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Id))
                    ids.Add(definition.Id);
            }

            return ids.Count > 0 ? ids[rng.Next(ids.Count)] : fallbackId;
        }

        private void BuildOwnedCosmeticIds()
        {
            ownedCosmeticIds.Clear();
            if (!unlockAllCosmetics)
                return;

            AddOwned(ChainlingVisualIdentity.BaseVariantId);
            AddOwned(ChainlingVisualIdentity.EntangledVariantId);

            if (visualDatabase != null)
            {
                IReadOnlyList<CombatVisualDatabaseSO.Entry> entries = visualDatabase.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    CombatVisualDatabaseSO.Entry entry = entries[i];
                    if (entry != null)
                        AddOwned(ChainlingVisualIdentity.NormalizeId(entry.visualVariantId, ChainlingVisualIdentity.BaseVariantId));
                }
            }

            if (identityDatabase == null)
                return;

            AddOwnedDefinitions(identityDatabase.Auras);
            AddOwnedDefinitions(identityDatabase.HudCardVariants);
            AddOwnedDefinitions(identityDatabase.PartyColumnVariants);
            AddOwnedDefinitions(identityDatabase.SwapInFx);
            AddOwnedDefinitions(identityDatabase.SwapOutFx);
            AddOwnedDefinitions(identityDatabase.KoFx);
            AddOwnedDefinitions(identityDatabase.VictoryPoses);
            AddOwnedDefinitions(identityDatabase.IdleStyles);
            AddOwnedDefinitions(identityDatabase.IdleFlourishes);
            AddOwnedDefinitions(identityDatabase.MeleeAttackStyles);
            AddOwnedDefinitions(identityDatabase.RangedAttackStyles);
            AddOwnedDefinitions(identityDatabase.HitReactionStyles);
            AddOwnedDefinitions(identityDatabase.AttackerKoStyles);
        }

        private void SeedBagInventory(System.Random rng)
        {
            bagAssetIdsByItemId.Clear();
            var stacks = new List<MenuInventoryItemStack>();
            if (seedBagItems)
            {
                stacks.Add(PlayerItemCatalogSeed.CreateStack("chainling_patch", rng != null ? rng.Next(2, 8) : 4));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("chainling_update", rng != null ? rng.Next(1, 5) : 3));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("chainling_reboot", rng != null ? rng.Next(1, 3) : 1));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("party_patch", 2));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("signal_orb", rng != null ? rng.Next(5, 16) : 10));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("lattice_orb", rng != null ? rng.Next(1, 5) : 2));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("favor_boost_s", rng != null ? rng.Next(1, 4) : 2));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("favor_boost_m", 1));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("system_flush", rng != null ? rng.Next(1, 3) : 1));
                // Sandbox bag only. Quest sequences do not grant this item.
                stacks.Add(PlayerItemCatalogSeed.CreateStack(PlayerMovementSpeedConsumable.ItemId, 2));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("chainling_reformat", 1));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("stamp_solvent", 1));
                stacks.Add(PlayerItemCatalogSeed.CreateStack("seal_breaker", 1));

                AddCoreBackedStack(stacks, "case_binding_standard", $"debug-case-binding-{generation:D3}");
                AddCoreBackedStack(stacks, "archive_key", $"debug-archive-key-{generation:D3}");
                AddCoreBackedStack(stacks, "signature_extract", $"debug-sig-extract-{generation:D3}");
                AddCoreBackedStack(stacks, "archive_severance", $"debug-severance-{generation:D3}");
            }

            bagInventory = new InventorySnapshot
            {
                connectivity = MenuConnectivityState.Ready,
                ramBalance = debugRamBalance,
                stacks = stacks,
                consumableIds = FlattenStackIds(stacks),
                cosmeticAssetIds = new List<string>(),
                walletPublicKey = debugWalletAddress,
                cluster = "devnet",
            };
        }

        private void AddCoreBackedStack(List<MenuInventoryItemStack> stacks, string itemId, string assetId)
        {
            MenuInventoryItemStack stack = PlayerItemCatalogSeed.CreateStack(itemId, 1, assetId);
            stacks.Add(stack);
            if (!string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrWhiteSpace(assetId))
                bagAssetIdsByItemId[itemId] = assetId;
        }

        private static List<string> FlattenStackIds(List<MenuInventoryItemStack> stacks)
        {
            var ids = new List<string>();
            if (stacks == null)
                return ids;

            for (int i = 0; i < stacks.Count; i++)
            {
                MenuInventoryItemStack stack = stacks[i];
                if (stack == null || string.IsNullOrWhiteSpace(stack.itemId))
                    continue;
                for (int q = 0; q < Math.Max(0, stack.quantity); q++)
                    ids.Add(stack.itemId);
            }

            return ids;
        }

        private static InventorySnapshot CloneBagInventory(InventorySnapshot source)
        {
            if (source == null)
            {
                return new InventorySnapshot
                {
                    stacks = new List<MenuInventoryItemStack>(),
                    consumableIds = new List<string>(),
                    cosmeticAssetIds = new List<string>(),
                };
            }

            var clone = new InventorySnapshot
            {
                connectivity = source.connectivity,
                ramBalance = source.ramBalance,
                cluster = source.cluster,
                walletPublicKey = source.walletPublicKey,
                consumableIds = source.consumableIds != null
                    ? new List<string>(source.consumableIds)
                    : new List<string>(),
                cosmeticAssetIds = source.cosmeticAssetIds != null
                    ? new List<string>(source.cosmeticAssetIds)
                    : new List<string>(),
                stacks = new List<MenuInventoryItemStack>(),
            };

            if (source.stacks != null)
            {
                for (int i = 0; i < source.stacks.Count; i++)
                {
                    MenuInventoryItemStack s = source.stacks[i];
                    if (s == null)
                        continue;
                    clone.stacks.Add(new MenuInventoryItemStack
                    {
                        itemId = s.itemId,
                        quantity = s.quantity,
                        coreAssetId = s.coreAssetId,
                        ownershipKind = s.ownershipKind,
                        categoryKey = s.categoryKey,
                        displayName = s.displayName,
                        effectFamilyKey = s.effectFamilyKey,
                    });
                }
            }

            return clone;
        }

        private void AddOwnedDefinitions<T>(IReadOnlyList<T> definitions)
            where T : ChainlingVisualDefinitionSO
        {
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Count; i++)
            {
                T definition = definitions[i];
                if (definition != null)
                    AddOwned(definition.Id);
            }
        }

        private void AddOwned(string id)
        {
            string normalized = NormalizeId(id);
            if (string.IsNullOrEmpty(normalized) || IsFreeId(normalized) || ownedCosmeticIds.Contains(normalized))
                return;

            ownedCosmeticIds.Add(normalized);
        }

        private string ValidateOwnedCosmetics(ChainlingVisualIdentity identity, IReadOnlyCollection<string> ownedIds)
        {
            if (unlockAllCosmetics || ownedIds == null)
                return null;

            var owned = new HashSet<string>(ownedIds, StringComparer.Ordinal);
            return FirstMissingOwnedId(owned,
                identity.visualVariantId,
                identity.auraId,
                identity.hudCardVariantId,
                identity.partyColumnVariantId,
                identity.swapInFxId,
                identity.swapOutFxId,
                identity.koFxId,
                identity.victoryPoseId,
                identity.idleStyleId,
                identity.idleFlourishId,
                identity.meleeAttackStyleId,
                identity.rangedAttackStyleId,
                identity.hitReactionStyleId,
                identity.attackerKoStyleId);
        }

        private static string FirstMissingOwnedId(HashSet<string> owned, params string[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                string id = NormalizeId(ids[i]);
                if (string.IsNullOrEmpty(id) || IsFreeId(id))
                    continue;
                if (!owned.Contains(id))
                    return $"debug_cosmetic_not_owned:{id}";
            }

            return null;
        }

        private List<SpeciesDefinitionSO> GetSpeciesCandidates()
        {
            var result = new List<SpeciesDefinitionSO>();
            if (speciesDatabase == null || speciesDatabase.species == null)
                return result;

            for (int i = 0; i < speciesDatabase.species.Count; i++)
            {
                SpeciesDefinitionSO definition = speciesDatabase.species[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.speciesId))
                    result.Add(definition);
            }

            return result;
        }

        private List<MoveDefinitionSO> GetMoveCandidates()
        {
            var result = new List<MoveDefinitionSO>();
            if (moveDatabase == null || moveDatabase.moves == null)
                return result;

            for (int i = 0; i < moveDatabase.moves.Count; i++)
            {
                MoveDefinitionSO definition = moveDatabase.moves[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.moveId))
                    result.Add(definition);
            }

            return result;
        }

        private int ResolveEvolutionStage(SpeciesDefinitionSO definition, System.Random rng)
        {
            int max = definition != null ? Mathf.Max(1, definition.maxEvolutionStages) : 1;
            return rng.Next(1, max + 1);
        }

        private static int[] BuildIvs(System.Random rng)
        {
            var ivs = new int[CombatConstants.STAT_COUNT];
            for (int i = 0; i < ivs.Length; i++)
                ivs[i] = rng.Next(CombatConstants.IV_MIN, CombatConstants.IV_MAX + 1);
            return ivs;
        }

        private PlayerRosterEntry FindEntry(string chainlingId)
        {
            if (roster == null || roster.chainlings == null)
                return null;

            string normalized = NormalizeId(chainlingId);
            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
                if (snapshot != null &&
                    string.Equals(snapshot.backendChainlingId, normalized, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private MenuMoveItem FindMove(string moveItemId)
        {
            if (moveInventory == null || moveInventory.moves == null)
                return null;

            string normalized = NormalizeId(moveItemId);
            for (int i = 0; i < moveInventory.moves.Count; i++)
            {
                MenuMoveItem move = moveInventory.moves[i];
                if (move != null && string.Equals(move.moveItemId, normalized, StringComparison.Ordinal))
                    return move;
            }

            return null;
        }

        private string ResolveProfileId(string playerProfileId)
        {
            return string.IsNullOrWhiteSpace(playerProfileId) ? debugProfileId : playerProfileId.Trim();
        }

        private static PlayerRosterSnapshot CloneRoster(PlayerRosterSnapshot source, string profileId)
        {
            var clone = new PlayerRosterSnapshot
            {
                playerProfileId = string.IsNullOrWhiteSpace(profileId) ? source != null ? source.playerProfileId : "" : profileId,
            };

            if (source == null || source.chainlings == null)
                return clone;

            for (int i = 0; i < source.chainlings.Count; i++)
                clone.chainlings.Add(CloneEntry(source.chainlings[i]));

            return clone;
        }

        private static PlayerRosterEntry CloneEntry(PlayerRosterEntry source)
        {
            if (source == null)
                return null;

            return new PlayerRosterEntry
            {
                snapshot = CloneSnapshot(source.snapshot),
                isPartyMember = source.isPartyMember,
                partySlot = source.partySlot,
                masteryXp = source.masteryXp,
                pveServiceRecord = ChainlingPveServiceRecord.CloneOrDefault(source.pveServiceRecord),
                battleStats = source.battleStats != null
                    ? new ChainlingBattleStats
                    {
                        battlesWon = source.battleStats.battlesWon,
                        battlesLost = source.battleStats.battlesLost,
                        kosScored = source.battleStats.kosScored,
                        timesKod = source.battleStats.timesKod,
                        timesCaptured = source.battleStats.timesCaptured,
                    }
                    : new ChainlingBattleStats(),
            };
        }

        private static ChainlingSnapshot CloneSnapshot(ChainlingSnapshot source)
        {
            if (source == null)
                return null;

            return new ChainlingSnapshot
            {
                speciesId = source.speciesId,
                backendChainlingId = source.backendChainlingId,
                displayName = source.displayName,
                level = source.level,
                stars = source.stars,
                masteryXp = source.masteryXp,
                rebirthCount = source.rebirthCount,
                evolutionStage = source.evolutionStage,
                visualIdentity = ChainlingVisualIdentity.CloneForPlayerSave(source.visualIdentity),
                moveIds = source.moveIds != null ? new List<string>(source.moveIds) : new List<string>(),
                modifier1SectionIds = source.modifier1SectionIds != null ? new List<string>(source.modifier1SectionIds) : new List<string>(),
                modifier2SectionIds = source.modifier2SectionIds != null ? new List<string>(source.modifier2SectionIds) : new List<string>(),
                ivs = source.ivs != null ? (int[])source.ivs.Clone() : new int[CombatConstants.STAT_COUNT],
                rebirthIvBonuses = source.rebirthIvBonuses != null
                    ? (int[])source.rebirthIvBonuses.Clone()
                    : new int[CombatConstants.STAT_COUNT],
                currentHp = source.currentHp,
                nftMintAddress = source.nftMintAddress,
                archivedAtUnix = source.archivedAtUnix,
            };
        }

        private static MoveInventorySnapshot CloneMoveInventory(MoveInventorySnapshot source, string profileId)
        {
            var clone = new MoveInventorySnapshot
            {
                playerProfileId = string.IsNullOrWhiteSpace(profileId) ? source != null ? source.playerProfileId : "" : profileId,
            };

            if (source == null)
                return clone;

            if (source.moves != null)
            {
                for (int i = 0; i < source.moves.Count; i++)
                {
                    MenuMoveItem move = source.moves[i];
                    if (move == null)
                    {
                        clone.moves.Add(null);
                        continue;
                    }

                    clone.moves.Add(new MenuMoveItem
                    {
                        moveItemId = move.moveItemId,
                        moveId = move.moveId,
                        tier = move.tier,
                        modifier1SectionId = move.modifier1SectionId,
                        modifier1EffectValue = move.modifier1EffectValue,
                        modifier1EffectValueLocked = move.modifier1EffectValueLocked,
                        modifier2SectionId = move.modifier2SectionId,
                        modifier2EffectValue = move.modifier2EffectValue,
                        modifier2EffectValueLocked = move.modifier2EffectValueLocked,
                        locked = move.locked,
                        exportedAssetId = move.exportedAssetId,
                        equippedChainlingId = move.equippedChainlingId,
                        equippedSlot = move.equippedSlot,
                    });
                }
            }

            if (source.loadouts != null)
            {
                for (int i = 0; i < source.loadouts.Count; i++)
                {
                    MenuMoveLoadoutSlot slot = source.loadouts[i];
                    if (slot == null)
                    {
                        clone.loadouts.Add(null);
                        continue;
                    }

                    clone.loadouts.Add(new MenuMoveLoadoutSlot
                    {
                        chainlingId = slot.chainlingId,
                        slot = slot.slot,
                        moveItemId = slot.moveItemId,
                    });
                }
            }

            return clone;
        }

        private static ChainlingRebirthResult FailedRebirth(string error)
        {
            return new ChainlingRebirthResult
            {
                success = false,
                error = error ?? "",
            };
        }

        private static int StatIndex(CoreStat stat)
        {
            switch (stat)
            {
                case CoreStat.MaxHP: return 0;
                case CoreStat.MeleeAtk: return 1;
                case CoreStat.MeleeDef: return 2;
                case CoreStat.RangedAtk: return 3;
                case CoreStat.RangedDef: return 4;
                case CoreStat.Speed: return 5;
                default: return -1;
            }
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            if (list == null)
                return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        private static bool IsFreeId(string id)
        {
            return string.Equals(id, ChainlingVisualIdentity.NoneId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.DefaultId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.EntangledVariantId, StringComparison.Ordinal);
        }
    }
}
