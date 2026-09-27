using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public sealed class FieldItemHpPatch
    {
        public string chainlingId;
        public int currentHp;
        public int healedAmount;
    }

    /// <summary>
    /// Pure heal/revive math for menu field items against persisted HP.
    /// </summary>
    public static class FieldItemHealUtility
    {
        public static bool NeedsPartyTarget(PlayerItemEffectFamily family)
        {
            return family == PlayerItemEffectFamily.HealSingle
                || family == PlayerItemEffectFamily.StatusClear
                || family == PlayerItemEffectFamily.Revive;
        }

        public static bool TryApplySingleHeal(
            ChainlingSnapshot snapshot,
            SpeciesDefinitionSO species,
            int healAmount,
            out FieldItemHpPatch patch,
            out string error)
        {
            patch = null;
            error = "";
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.backendChainlingId))
            {
                error = "target_required";
                return false;
            }

            if (species == null)
            {
                error = "species_missing";
                return false;
            }

            int maxHp = ChainlingVitalsUtility.BuildMaxHp(snapshot, species);
            int current = ChainlingVitalsUtility.ResolveEffectiveCurrentHp(snapshot, species);
            if (current <= 0)
            {
                error = "target_fainted_use_revive";
                return false;
            }

            int room = Mathf.Max(0, maxHp - current);
            int healed = Mathf.Clamp(healAmount, 0, room);
            if (healed <= 0)
            {
                error = "already_full_hp";
                return false;
            }

            int next = current + healed;
            snapshot.currentHp = next;
            patch = new FieldItemHpPatch
            {
                chainlingId = snapshot.backendChainlingId,
                currentHp = next,
                healedAmount = healed,
            };
            return true;
        }

        public static bool TryApplyRevive(
            ChainlingSnapshot snapshot,
            SpeciesDefinitionSO species,
            FieldItemConfigSO config,
            out FieldItemHpPatch patch,
            out string error)
        {
            patch = null;
            error = "";
            config ??= FieldItemConfigSO.CreateRuntimeDefault();

            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.backendChainlingId))
            {
                error = "target_required";
                return false;
            }

            if (species == null)
            {
                error = "species_missing";
                return false;
            }

            int maxHp = ChainlingVitalsUtility.BuildMaxHp(snapshot, species);
            int current = ChainlingVitalsUtility.ResolveEffectiveCurrentHp(snapshot, species);
            if (current > 0)
            {
                error = "target_not_fainted";
                return false;
            }

            int next = config.ResolveReviveHp(maxHp);
            if (next <= 0)
            {
                error = "revive_failed";
                return false;
            }

            snapshot.currentHp = next;
            patch = new FieldItemHpPatch
            {
                chainlingId = snapshot.backendChainlingId,
                currentHp = next,
                healedAmount = next - current,
            };
            return true;
        }

        public static bool TryApplyPartyHeal(
            IReadOnlyList<ChainlingSnapshot> party,
            Func<ChainlingSnapshot, SpeciesDefinitionSO> resolveSpecies,
            int healAmount,
            out List<FieldItemHpPatch> patches,
            out string error)
        {
            patches = new List<FieldItemHpPatch>();
            error = "";
            if (party == null || party.Count == 0)
            {
                error = "party_empty";
                return false;
            }

            if (resolveSpecies == null)
            {
                error = "species_resolver_missing";
                return false;
            }

            int totalHealed = 0;
            for (int i = 0; i < party.Count; i++)
            {
                ChainlingSnapshot snapshot = party[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.backendChainlingId))
                    continue;

                SpeciesDefinitionSO species = resolveSpecies(snapshot);
                if (species == null)
                    continue;

                int maxHp = ChainlingVitalsUtility.BuildMaxHp(snapshot, species);
                int current = ChainlingVitalsUtility.ResolveEffectiveCurrentHp(snapshot, species);
                if (current <= 0)
                    continue;

                // Unsynced (-1) resolves as full — skip no-ops.
                if (!ChainlingVitalsUtility.HasPersistedCurrentHp(snapshot) && current >= maxHp)
                    continue;

                int room = Mathf.Max(0, maxHp - current);
                int healed = Mathf.Clamp(healAmount, 0, room);
                if (healed <= 0)
                    continue;

                int next = current + healed;
                snapshot.currentHp = next;
                patches.Add(new FieldItemHpPatch
                {
                    chainlingId = snapshot.backendChainlingId,
                    currentHp = next,
                    healedAmount = healed,
                });
                totalHealed += healed;
            }

            if (totalHealed <= 0)
            {
                error = "party_already_full_hp";
                return false;
            }

            return true;
        }
    }
}
