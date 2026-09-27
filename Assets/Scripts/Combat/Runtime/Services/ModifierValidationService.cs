using System;
using System.Collections.Generic;
using System.Linq;

namespace Chainbound.Combat
{
    public struct ModifierValidationResult
    {
        public bool isValid;
        public string reason;

        public static ModifierValidationResult Valid => new ModifierValidationResult
        {
            isValid = true,
            reason = string.Empty,
        };

        public static ModifierValidationResult Invalid(string reason) => new ModifierValidationResult
        {
            isValid = false,
            reason = reason ?? "Invalid modifier combination.",
        };
    }

    /// <summary>
    /// Central validation rules for modifier assignment and loadout checks.
    /// </summary>
    public static class ModifierValidationService
    {
        public static ModifierValidationResult CanApply(
            ModifierDefinitionSO modifierDef,
            MoveDefinitionSO moveDef,
            ModifierCatalogSO catalog = null,
            IReadOnlyCollection<string> existingModifierSectionIds = null)
        {
            if (modifierDef == null)
                return ModifierValidationResult.Invalid("Modifier definition is null.");
            if (moveDef == null)
                return ModifierValidationResult.Invalid("Move definition is null.");

            var ctx = new MoveAttributeContext
            {
                category = moveDef.category,
                power = moveDef.power,
                apCost = moveDef.apCost,
                tags = moveDef.tags,
                label = string.IsNullOrWhiteSpace(moveDef.displayName) ? moveDef.moveId : moveDef.displayName,
            };

            return CanApplyInternal(modifierDef, ctx, catalog, existingModifierSectionIds);
        }

        public static ModifierValidationResult CanApply(
            ModifierDefinitionSO modifierDef,
            MoveInstance move,
            ModifierCatalogSO catalog = null,
            IReadOnlyCollection<string> existingModifierSectionIds = null)
        {
            if (modifierDef == null)
                return ModifierValidationResult.Invalid("Modifier definition is null.");
            if (move == null)
                return ModifierValidationResult.Invalid("Move instance is null.");

            var ctx = new MoveAttributeContext
            {
                category = move.category,
                power = move.basePower,
                apCost = move.apCost,
                tags = move.tags,
                label = string.IsNullOrWhiteSpace(move.displayName) ? move.definitionId : move.displayName,
            };

            return CanApplyInternal(modifierDef, ctx, catalog, existingModifierSectionIds);
        }

        public static ModifierValidationResult ValidateLoadout(ChainlingLoadout loadout, ModifierCatalogSO catalog)
        {
            if (loadout == null)
                return ModifierValidationResult.Invalid("Loadout is null.");
            if (catalog == null)
                return ModifierValidationResult.Invalid("Modifier catalog is required for loadout validation.");

            var chainlingDefs = new List<ModifierDefinitionSO>(16);

            for (int i = 0; i < loadout.equipped.Count; i++)
            {
                var move = loadout.equipped[i];
                if (move == null)
                    continue;

                var moveSectionIds = CollectModifierSectionIds(move);
                var moveAssigned = new List<string>(moveSectionIds.Count);

                for (int m = 0; m < moveSectionIds.Count; m++)
                {
                    string sectionId = moveSectionIds[m];
                    var def = catalog.GetBySectionId(sectionId);
                    if (def == null)
                    {
                        return ModifierValidationResult.Invalid(
                            $"Move '{move.displayName}' references unknown modifier '{sectionId}'.");
                    }

                    var canApply = CanApply(def, move, catalog, moveAssigned);
                    if (!canApply.isValid)
                    {
                        return ModifierValidationResult.Invalid(
                            $"Move '{move.displayName}' modifier '{sectionId}' invalid: {canApply.reason}");
                    }

                    moveAssigned.Add(sectionId);
                    chainlingDefs.Add(def);
                }
            }

            int swapIn = CountByTrigger(chainlingDefs, ModifierTriggerCondition.OnSwapIn);
            if (swapIn > 1)
                return ModifierValidationResult.Invalid("Only one OnSwapIn modifier is allowed per Chainling loadout.");

            int swapOut = CountByTrigger(chainlingDefs, ModifierTriggerCondition.OnSwapOut);
            if (swapOut > 1)
                return ModifierValidationResult.Invalid("Only one OnSwapOut modifier is allowed per Chainling loadout.");

            return ModifierValidationResult.Valid;
        }

        private static ModifierValidationResult CanApplyInternal(
            ModifierDefinitionSO modifierDef,
            MoveAttributeContext move,
            ModifierCatalogSO catalog,
            IReadOnlyCollection<string> existingModifierSectionIds)
        {
            var existingIds = NormalizeIds(existingModifierSectionIds);
            if (!string.IsNullOrWhiteSpace(modifierDef.sectionId) && existingIds.Contains(modifierDef.sectionId))
            {
                return ModifierValidationResult.Invalid(
                    $"Modifier '{modifierDef.sectionId}' is already attached.");
            }

            if (modifierDef.allowedCategories != null && modifierDef.allowedCategories.Length > 0)
            {
                bool categoryAllowed = false;
                for (int i = 0; i < modifierDef.allowedCategories.Length; i++)
                {
                    if (modifierDef.allowedCategories[i] == move.category)
                    {
                        categoryAllowed = true;
                        break;
                    }
                }

                if (!categoryAllowed)
                {
                    return ModifierValidationResult.Invalid(
                        $"Move '{move.label}' category '{move.category}' is not allowed for '{modifierDef.sectionId}'.");
                }
            }

            if (modifierDef.powerCap > 0 && move.power > modifierDef.powerCap)
            {
                return ModifierValidationResult.Invalid(
                    $"Move '{move.label}' power {move.power} exceeds cap {modifierDef.powerCap} for '{modifierDef.sectionId}'.");
            }

            if (modifierDef.apCostCap > 0 && move.apCost > modifierDef.apCostCap)
            {
                return ModifierValidationResult.Invalid(
                    $"Move '{move.label}' AP {move.apCost} exceeds cap {modifierDef.apCostCap} for '{modifierDef.sectionId}'.");
            }

            if (HasForbiddenTag(move.tags, modifierDef.forbiddenTags, out string blockedTag))
            {
                return ModifierValidationResult.Invalid(
                    $"Move '{move.label}' has forbidden tag '{blockedTag}' for '{modifierDef.sectionId}'.");
            }

            if (HasForbiddenCombo(modifierDef.forbiddenCombinations, existingIds, out string blockedByCandidate))
            {
                return ModifierValidationResult.Invalid(
                    $"Modifier '{modifierDef.sectionId}' forbids combination with '{blockedByCandidate}'.");
            }

            var existingDefs = ResolveDefinitions(catalog, existingIds);
            for (int i = 0; i < existingDefs.Count; i++)
            {
                var existing = existingDefs[i];
                if (existing == null)
                    continue;

                if (HasForbiddenCombo(existing.forbiddenCombinations, modifierDef.sectionId, out string _))
                {
                    return ModifierValidationResult.Invalid(
                        $"Existing modifier '{existing.sectionId}' forbids '{modifierDef.sectionId}'.");
                }
            }

            if (modifierDef.slotType != ModifierSlotType.None)
            {
                for (int i = 0; i < existingDefs.Count; i++)
                {
                    var existing = existingDefs[i];
                    if (existing == null)
                        continue;
                    if (existing.slotType == modifierDef.slotType)
                    {
                        return ModifierValidationResult.Invalid(
                            $"Only one '{modifierDef.slotType}' slot-type modifier is allowed per Stamped.");
                    }
                }
            }

            if (modifierDef.triggerCondition == ModifierTriggerCondition.OnSwapIn &&
                CountByTrigger(existingDefs, ModifierTriggerCondition.OnSwapIn) >= 1)
            {
                return ModifierValidationResult.Invalid("Only one OnSwapIn modifier is allowed per Chainling.");
            }

            if (modifierDef.triggerCondition == ModifierTriggerCondition.OnSwapOut &&
                CountByTrigger(existingDefs, ModifierTriggerCondition.OnSwapOut) >= 1)
            {
                return ModifierValidationResult.Invalid("Only one OnSwapOut modifier is allowed per Chainling.");
            }

            return ModifierValidationResult.Valid;
        }

        private static bool HasForbiddenTag(
            IReadOnlyList<string> moveTags,
            IReadOnlyList<string> forbiddenTags,
            out string blockedTag)
        {
            blockedTag = string.Empty;
            if (moveTags == null || moveTags.Count == 0 || forbiddenTags == null || forbiddenTags.Count == 0)
                return false;

            for (int i = 0; i < forbiddenTags.Count; i++)
            {
                string forbidden = forbiddenTags[i];
                if (string.IsNullOrWhiteSpace(forbidden))
                    continue;

                for (int t = 0; t < moveTags.Count; t++)
                {
                    if (string.Equals(moveTags[t], forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        blockedTag = forbidden.Trim();
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasForbiddenCombo(
            IReadOnlyList<string> combos,
            IReadOnlyCollection<string> existingIds,
            out string blockedId)
        {
            blockedId = string.Empty;
            if (combos == null || combos.Count == 0 || existingIds == null || existingIds.Count == 0)
                return false;

            for (int i = 0; i < combos.Count; i++)
            {
                string combo = combos[i];
                if (string.IsNullOrWhiteSpace(combo))
                    continue;

                string trimmed = combo.Trim();
                if (existingIds.Any(id => string.Equals(id, trimmed, StringComparison.Ordinal)))
                {
                    blockedId = trimmed;
                    return true;
                }
            }

            return false;
        }

        private static bool HasForbiddenCombo(
            IReadOnlyList<string> combos,
            string candidateId,
            out string blockedId)
        {
            blockedId = string.Empty;
            if (combos == null || combos.Count == 0 || string.IsNullOrWhiteSpace(candidateId))
                return false;

            for (int i = 0; i < combos.Count; i++)
            {
                string combo = combos[i];
                if (string.IsNullOrWhiteSpace(combo))
                    continue;
                if (string.Equals(combo.Trim(), candidateId, StringComparison.OrdinalIgnoreCase))
                {
                    blockedId = combo.Trim();
                    return true;
                }
            }

            return false;
        }

        private static List<string> CollectModifierSectionIds(MoveInstance move)
        {
            var ids = new List<string>(4);
            if (move == null)
                return ids;

            if (move.modifiers != null)
            {
                for (int i = 0; i < move.modifiers.Count; i++)
                {
                    string id = move.modifiers[i] != null ? move.modifiers[i].definitionSectionId : null;
                    AddUnique(ids, id);
                }
            }

            AddUnique(ids, move.modifier1SectionId);
            AddUnique(ids, move.modifier2SectionId);

            if (move.modifierIds != null)
            {
                for (int i = 0; i < move.modifierIds.Length; i++)
                    AddUnique(ids, move.modifierIds[i]);
            }

            return ids;
        }

        private static void AddUnique(List<string> ids, string id)
        {
            if (ids == null || string.IsNullOrWhiteSpace(id))
                return;

            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            ids.Add(id.Trim());
        }

        private static HashSet<string> NormalizeIds(IReadOnlyCollection<string> ids)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ids == null)
                return set;

            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    set.Add(id.Trim());
            }

            return set;
        }

        private static List<ModifierDefinitionSO> ResolveDefinitions(ModifierCatalogSO catalog, IReadOnlyCollection<string> ids)
        {
            var defs = new List<ModifierDefinitionSO>();
            if (catalog == null || ids == null || ids.Count == 0)
                return defs;

            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                defs.Add(catalog.GetBySectionId(id.Trim()));
            }

            return defs;
        }

        private static int CountByTrigger(IReadOnlyList<ModifierDefinitionSO> defs, ModifierTriggerCondition trigger)
        {
            if (defs == null || defs.Count == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def != null && def.triggerCondition == trigger)
                    count++;
            }

            return count;
        }

        private struct MoveAttributeContext
        {
            public MoveCategory category;
            public int power;
            public int apCost;
            public IReadOnlyList<string> tags;
            public string label;
        }
    }
}
