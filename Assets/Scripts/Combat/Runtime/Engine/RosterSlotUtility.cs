using System;
using System.Collections.Generic;
using Chainbound.Combat.Core;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Builds <see cref="TeamState"/> rosters from battle snapshots for <see cref="CombatResolver.InitializeBattle"/>.
    /// </summary>
    public static class RosterSlotUtility
    {
        public static TeamState BuildTeamFromParty(
            int teamIndex,
            PartySnapshot party,
            SpeciesDatabaseSO speciesDB,
            MoveDatabaseSO moveDB,
            int battleSeed,
            CombatRng rng,
            ModifierCatalogSO modifierCatalog = null,
            CombatRulesetSO ruleset = null)
        {
            string teamName = party != null && !string.IsNullOrWhiteSpace(party.teamName)
                ? party.teamName
                : "Player";
            return BuildTeam(teamIndex, teamName, party?.chainlings, speciesDB, moveDB, battleSeed, rng, modifierCatalog, ruleset);
        }

        public static TeamState BuildTeamFromEncounter(
            int teamIndex,
            EncounterSnapshot encounter,
            SpeciesDatabaseSO speciesDB,
            MoveDatabaseSO moveDB,
            int battleSeed,
            CombatRng rng,
            ModifierCatalogSO modifierCatalog = null,
            CombatRulesetSO ruleset = null)
        {
            string teamName = encounter != null && !string.IsNullOrWhiteSpace(encounter.teamName)
                ? encounter.teamName
                : "Wild";
            return BuildTeam(teamIndex, teamName, encounter?.chainlings, speciesDB, moveDB, battleSeed, rng, modifierCatalog, ruleset);
        }

        private static TeamState BuildTeam(
            int teamIndex,
            string teamName,
            List<ChainlingSnapshot> snapshots,
            SpeciesDatabaseSO speciesDB,
            MoveDatabaseSO moveDB,
            int battleSeed,
            CombatRng rng,
            ModifierCatalogSO modifierCatalog,
            CombatRulesetSO ruleset)
        {
            var team = new TeamState(teamIndex, teamName);
            if (snapshots == null || snapshots.Count == 0)
                return team;

            int count = Math.Min(snapshots.Count, CombatConstants.ROSTER_SIZE);
            for (int rosterIndex = 0; rosterIndex < count; rosterIndex++)
            {
                ChainlingSnapshot snapshot = snapshots[rosterIndex];
                if (snapshot == null)
                    continue;

                ChainlingInstance instance = CreateInstance(
                    snapshot,
                    speciesDB,
                    moveDB,
                    teamIndex,
                    rosterIndex,
                    battleSeed,
                    rng,
                    modifierCatalog,
                    ruleset);

                if (instance != null)
                    team.chainlings.Add(instance);
            }

            return team;
        }

        private static ChainlingInstance CreateInstance(
            ChainlingSnapshot snapshot,
            SpeciesDatabaseSO speciesDB,
            MoveDatabaseSO moveDB,
            int teamIndex,
            int rosterIndex,
            int battleSeed,
            CombatRng rng,
            ModifierCatalogSO modifierCatalog,
            CombatRulesetSO ruleset)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.speciesId))
                return null;

            SpeciesDefinitionSO speciesDef = speciesDB != null
                ? speciesDB.GetById(snapshot.speciesId)
                : null;
            if (speciesDef == null)
                return null;

            ChainlingProgressionUtility.ResolveSnapshotProgression(
                snapshot.stars,
                snapshot.rebirthCount,
                snapshot.level,
                out int stars,
                out int rebirthCount);

            int maxMoveSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(rebirthCount);
            List<string> eligibilityMoveIds = ResolveEligibilityMoveIds(snapshot, moveDB, maxMoveSlots);
            if (ruleset != null &&
                !ruleset.IsChainlingEligible(
                    speciesDef.primaryType,
                    speciesDef.secondaryType,
                    eligibilityMoveIds,
                    out string reason))
            {
                Debug.LogWarning(
                    $"[RosterSlotUtility] Skipping ineligible Chainling '{snapshot.displayName ?? snapshot.speciesId}' " +
                    $"for ruleset '{ruleset.rulesetId}' ({reason}).");
                return null;
            }

            int evolutionStage = snapshot.evolutionStage > 0 ? snapshot.evolutionStage : 1;
            int[] ivs = ResolveIvs(snapshot, snapshot.speciesId, teamIndex, rosterIndex, battleSeed);

            ChainlingInstance instance = CreateFromSpecies(
                speciesDef,
                snapshot,
                moveDB,
                stars,
                rebirthCount,
                evolutionStage,
                ivs,
                rng,
                modifierCatalog,
                ruleset);

            if (instance == null)
                return null;

            instance.backendChainlingId = snapshot.backendChainlingId ?? string.Empty;

            instance.visualIdentity = ChainlingVisualIdentity.CloneOrDefault(snapshot.visualIdentity);
            instance.isMutated = snapshot.isMutated || BattleIntroCopySubject.IsMutated(instance);
            instance.isEntangled = snapshot.isEntangled;
            instance.entangledType = snapshot.isEntangled ? snapshot.entangledType : ChainlingType.None;
            instance.legacyBaseCaptureRate = Math.Max(0, Math.Min(100, snapshot.legacyBaseCaptureRate));
            if (snapshot.hasCaptureProfile)
            {
                instance.hasCaptureProfile = true;
                instance.captureProfile = snapshot.captureProfile;
            }
            instance.captureMutationRarityRank = Math.Max(0, Math.Min(5, snapshot.captureMutationRarityRank));
            if (instance.isEntangled && instance.entangledType != ChainlingType.None)
            {
                instance.primaryType = instance.entangledType;
                instance.secondaryType = ChainlingType.None;
            }

            return instance;
        }

        private static ChainlingInstance CreateFromSpecies(
            SpeciesDefinitionSO speciesDef,
            ChainlingSnapshot snapshot,
            MoveDatabaseSO moveDB,
            int stars,
            int rebirthCount,
            int evolutionStage,
            int[] ivs,
            CombatRng rng,
            ModifierCatalogSO modifierCatalog,
            CombatRulesetSO ruleset)
        {
            if (speciesDef == null)
                return null;

            int maxMoveSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(rebirthCount);
            List<MoveDefinitionSO> equipped = ResolveEquippedMoves(snapshot, moveDB, maxMoveSlots);
            ChainlingInstance instance = CombatFactory.CreateChainling(
                speciesDef,
                stars,
                rebirthCount,
                evolutionStage,
                equipped,
                ivs,
                rng,
                snapshot.rebirthIvBonuses);

            if (instance == null)
                return null;

            ApplySnapshotModifierSectionIds(instance, snapshot);
            PassiveMaxHpUtility.ApplyToInstance(instance, modifierCatalog);
            instance.battleRuleset = ruleset;

            if (!string.IsNullOrWhiteSpace(snapshot.displayName))
                instance.displayName = snapshot.displayName.Trim();

            // Refresh full HP after ruleset mutators change EffectiveMaxHP (persisted HP still wins).
            if (!ChainlingVitalsUtility.HasPersistedCurrentHp(snapshot))
            {
                instance.currentHP = instance.EffectiveMaxHP;
                instance.displayHP = instance.currentHP;
            }

            ApplyPersistedHp(instance, snapshot, speciesDef);

            return instance;
        }

        private static void ApplyPersistedHp(
            ChainlingInstance instance,
            ChainlingSnapshot snapshot,
            SpeciesDefinitionSO speciesDef)
        {
            if (instance == null || snapshot == null || speciesDef == null || !ChainlingVitalsUtility.HasPersistedCurrentHp(snapshot))
                return;

            int currentHp = ChainlingVitalsUtility.ResolveEffectiveCurrentHp(snapshot, speciesDef);
            instance.currentHP = currentHp;
            instance.displayHP = currentHp;
        }

        /// <summary>
        /// The first roster entry that has a species but no move id the move database can resolve.
        /// Missing species stay on the existing skip path and are not reported here.
        /// </summary>
        public static ChainlingSnapshot FindFirstWithoutResolvableMove(
            IList<ChainlingSnapshot> snapshots,
            SpeciesDatabaseSO speciesDB,
            MoveDatabaseSO moveDB)
        {
            if (snapshots == null)
                return null;

            int count = Math.Min(snapshots.Count, CombatConstants.ROSTER_SIZE);
            for (int i = 0; i < count; i++)
            {
                ChainlingSnapshot snapshot = snapshots[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.speciesId))
                    continue;
                if (speciesDB == null || speciesDB.GetById(snapshot.speciesId) == null)
                    continue;
                if (!HasResolvableMove(snapshot, moveDB))
                    return snapshot;
            }

            return null;
        }

        public static bool HasResolvableMove(ChainlingSnapshot snapshot, MoveDatabaseSO moveDB)
        {
            if (snapshot == null)
                return false;

            ChainlingProgressionUtility.ResolveSnapshotProgression(
                snapshot.stars,
                snapshot.rebirthCount,
                snapshot.level,
                out _,
                out int rebirthCount);
            int maxMoveSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(rebirthCount);
            return ResolveEquippedMoves(snapshot, moveDB, maxMoveSlots).Count > 0;
        }

        public static string DescribeChainling(ChainlingSnapshot snapshot)
        {
            if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.displayName))
                return snapshot.displayName.Trim();
            if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.speciesId))
                return snapshot.speciesId.Trim();
            return "Chainling";
        }

        /// <summary>
        /// Copies saved move and modifier ids onto roster rows that currently have no resolvable move.
        /// Rows that already resolve a move keep their loadout.
        /// </summary>
        public static void ApplyRefreshedLoadouts(
            IList<ChainlingSnapshot> current,
            PartySnapshot refreshed,
            SpeciesDatabaseSO speciesDB,
            MoveDatabaseSO moveDB)
        {
            if (current == null || refreshed?.chainlings == null)
                return;

            int count = Math.Min(current.Count, CombatConstants.ROSTER_SIZE);
            for (int i = 0; i < count; i++)
            {
                ChainlingSnapshot target = current[i];
                if (target == null || HasResolvableMove(target, moveDB))
                    continue;
                if (speciesDB != null &&
                    !string.IsNullOrWhiteSpace(target.speciesId) &&
                    speciesDB.GetById(target.speciesId) == null)
                    continue;

                ChainlingSnapshot source = FindRefreshedRow(refreshed, target, i);
                if (source == null)
                    continue;

                target.moveIds = CopyIds(source.moveIds);
                target.modifier1SectionIds = CopyIds(source.modifier1SectionIds);
                target.modifier2SectionIds = CopyIds(source.modifier2SectionIds);
            }
        }

        private static ChainlingSnapshot FindRefreshedRow(
            PartySnapshot refreshed,
            ChainlingSnapshot original,
            int index)
        {
            if (refreshed?.chainlings == null || original == null)
                return null;

            if (!string.IsNullOrWhiteSpace(original.backendChainlingId))
            {
                for (int i = 0; i < refreshed.chainlings.Count; i++)
                {
                    ChainlingSnapshot candidate = refreshed.chainlings[i];
                    if (candidate != null &&
                        string.Equals(candidate.backendChainlingId, original.backendChainlingId, StringComparison.Ordinal))
                        return candidate;
                }

                return null;
            }

            if (index >= 0 && index < refreshed.chainlings.Count)
                return refreshed.chainlings[index];
            return null;
        }

        private static List<string> CopyIds(List<string> source)
        {
            return source != null ? new List<string>(source) : new List<string>();
        }

        private static List<string> ResolveEligibilityMoveIds(
            ChainlingSnapshot snapshot,
            MoveDatabaseSO moveDB,
            int maxMoveSlots)
        {
            List<MoveDefinitionSO> equipped = ResolveEquippedMoves(snapshot, moveDB, maxMoveSlots);
            var ids = new List<string>(equipped.Count);
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i] != null && !string.IsNullOrWhiteSpace(equipped[i].moveId))
                    ids.Add(equipped[i].moveId);
            }

            return ids;
        }

        private static List<MoveDefinitionSO> ResolveEquippedMoves(
            ChainlingSnapshot snapshot,
            MoveDatabaseSO moveDB,
            int maxMoveSlots)
        {
            int capacity = Math.Max(0, maxMoveSlots);
            var equipped = new List<MoveDefinitionSO>(capacity);
            if (snapshot?.moveIds == null || moveDB == null)
                return equipped;

            for (int i = 0; i < snapshot.moveIds.Count && equipped.Count < capacity; i++)
            {
                string moveId = snapshot.moveIds[i];
                if (string.IsNullOrWhiteSpace(moveId))
                    continue;

                MoveDefinitionSO def = moveDB.GetById(moveId);
                AddUniqueMove(equipped, def);
            }

            return equipped;
        }

        private static void ApplySnapshotModifierSectionIds(ChainlingInstance chainling, ChainlingSnapshot snapshot)
        {
            if (chainling?.moves == null || chainling.moves.Count == 0 || snapshot == null)
                return;

            for (int i = 0; i < chainling.moves.Count; i++)
            {
                MoveInstance move = chainling.moves[i];
                if (move == null)
                    continue;

                string mod1 = GetAlignedSectionId(snapshot.modifier1SectionIds, i);
                string mod2 = GetAlignedSectionId(snapshot.modifier2SectionIds, i);

                if (!string.IsNullOrEmpty(mod1))
                    move.modifier1SectionId = mod1;
                if (!string.IsNullOrEmpty(mod2))
                    move.modifier2SectionId = mod2;

                TryAttachSwapModifierRuntimeStub(move, mod1);
                TryAttachSwapModifierRuntimeStub(move, mod2);
            }
        }

        private static void TryAttachSwapModifierRuntimeStub(MoveInstance move, string sectionId)
        {
            if (move == null || string.IsNullOrWhiteSpace(sectionId))
                return;
            if (!sectionId.StartsWith("6.4-", StringComparison.Ordinal))
                return;

            if (move.modifiers == null)
                move.modifiers = new List<ModifierInstance>();

            for (int i = 0; i < move.modifiers.Count; i++)
            {
                ModifierInstance existing = move.modifiers[i];
                if (existing != null &&
                    string.Equals(existing.definitionSectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            move.modifiers.Add(new ModifierInstance
            {
                definitionSectionId = sectionId,
                category = ModifierCategory.TRG,
                scope = ModifierScope.Passive,
                rarity = RarityTier.Common,
                triggerCondition = ResolveSwapTrigger(sectionId),
            });
        }

        private static ModifierTriggerCondition ResolveSwapTrigger(string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return ModifierTriggerCondition.None;

            switch (sectionId.Trim().ToLowerInvariant())
            {
                case "6.4-f":
                case "6.4-g":
                case "6.4-h":
                case "6.4-i":
                    return ModifierTriggerCondition.OnSwapOut;
                default:
                    return ModifierTriggerCondition.OnSwapIn;
            }
        }

        private static int[] ResolveIvs(
            ChainlingSnapshot snapshot,
            string speciesId,
            int teamIndex,
            int rosterIndex,
            int battleSeed)
        {
            // Authored length-6 IV arrays are always honored, including all zeros (deterministic tests).
            // Seeded debug IVs only when ivs is null or too short.
            if (snapshot?.ivs != null && snapshot.ivs.Length >= CombatConstants.STAT_COUNT)
                return ChainlingSnapshotStatUtility.CopyIvs(snapshot.ivs);

            int seedTier = snapshot != null
                ? ChainlingProgressionUtility.GetProgressionTieBreakScore(snapshot.stars, snapshot.rebirthCount)
                : 0;
            var provider = new SeededDebugIvProvider(battleSeed);
            return provider.GetIvs(speciesId, seedTier, teamIndex, rosterIndex);
        }

        private static string GetAlignedSectionId(List<string> sectionIds, int moveIndex)
        {
            if (sectionIds == null || moveIndex < 0 || moveIndex >= sectionIds.Count)
                return null;

            string id = sectionIds[moveIndex];
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }

        private static void AddUniqueMove(List<MoveDefinitionSO> list, MoveDefinitionSO def)
        {
            if (def == null || list == null)
                return;
            if (!list.Contains(def))
                list.Add(def);
        }

        /// <summary>
        /// Seat and roster slot ids (t0r0..t1r5). Move ids append m0.. Call after both teams exist and before the resolver logs anything.
        /// </summary>
        public static void StampStablePvpIds(TeamState team)
        {
            if (team?.chainlings == null)
                return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                if (chainling == null)
                    continue;

                chainling.instanceId = $"t{team.teamIndex}r{i}";
                if (chainling.moves == null)
                    continue;

                for (int move = 0; move < chainling.moves.Count; move++)
                {
                    if (chainling.moves[move] != null)
                        chainling.moves[move].instanceId = $"t{team.teamIndex}r{i}m{move}";
                }
            }
        }

        public static void ApplyFullHeal(TeamState team)
        {
            if (team?.chainlings == null)
                return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                if (chainling == null)
                    continue;

                chainling.currentHP = chainling.EffectiveMaxHP;
                chainling.displayHP = chainling.currentHP;
                chainling.isAlive = chainling.currentHP > 0;
            }
        }
    }
}
