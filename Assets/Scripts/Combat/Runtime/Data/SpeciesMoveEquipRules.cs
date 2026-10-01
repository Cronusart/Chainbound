using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared equip gate for species pools.
    /// Typeless moves equip on anyone. Typed moves equip when they match the specimen's
    /// primary type or its entangled type. Each species may list 1-2 off-type exceptions.
    /// A move listed in another species' exclusiveMoves equips only on that species.
    /// </summary>
    public static class SpeciesMoveEquipRules
    {
        public static LoadoutValidationResult Evaluate(
            SpeciesDefinitionSO species,
            bool isEntangled,
            ChainlingType entangledType,
            string moveId,
            ChainlingType moveType,
            SpeciesDatabaseSO speciesDatabase)
        {
            if (species == null)
                return LoadoutValidationResult.Fail(LoadoutValidationError.TypeIncompatible, "Species is missing.");

            string normalizedMoveId = moveId != null ? moveId.Trim() : "";
            if (string.IsNullOrEmpty(normalizedMoveId))
                return LoadoutValidationResult.Fail(LoadoutValidationError.TypeIncompatible, "Move is missing.");

            string exclusiveOwner = FindExclusiveOwner(species, normalizedMoveId, speciesDatabase);
            if (!string.IsNullOrEmpty(exclusiveOwner) &&
                !string.Equals(exclusiveOwner, species.speciesId, StringComparison.Ordinal))
            {
                return LoadoutValidationResult.Fail(
                    LoadoutValidationError.ExclusiveSpeciesMismatch,
                    "This move can only be equipped on its source species.");
            }

            if (!string.IsNullOrEmpty(exclusiveOwner))
                return LoadoutValidationResult.Valid;

            if (moveType == ChainlingType.None)
                return LoadoutValidationResult.Valid;

            if (moveType == species.primaryType)
                return LoadoutValidationResult.Valid;

            if (isEntangled && entangledType != ChainlingType.None && moveType == entangledType)
                return LoadoutValidationResult.Valid;

            if (species.primaryType == ChainlingType.Meme)
                return LoadoutValidationResult.Valid;

            if (IsOffTypeException(species, normalizedMoveId))
                return LoadoutValidationResult.Valid;

            return LoadoutValidationResult.Fail(
                LoadoutValidationError.TypeIncompatible,
                "This move's type does not match this Chainling.");
        }

        public static string DescribeFit(
            SpeciesDefinitionSO species,
            bool isEntangled,
            ChainlingType entangledType,
            string moveId,
            ChainlingType moveType,
            SpeciesDatabaseSO speciesDatabase)
        {
            LoadoutValidationResult result = Evaluate(
                species, isEntangled, entangledType, moveId, moveType, speciesDatabase);
            if (!result.isValid)
                return "Cannot equip";

            string normalizedMoveId = moveId != null ? moveId.Trim() : "";
            string exclusiveOwner = FindExclusiveOwner(species, normalizedMoveId, speciesDatabase);
            if (!string.IsNullOrEmpty(exclusiveOwner))
                return "Species exclusive";

            if (moveType == ChainlingType.None)
                return "Typeless";

            if (IsOffTypeException(species, normalizedMoveId) &&
                moveType != species.primaryType &&
                !(isEntangled && moveType == entangledType))
                return "Off-type exception";

            return "Type match";
        }

        private static bool IsOffTypeException(SpeciesDefinitionSO species, string moveId)
        {
            if (species?.offTypeMoveExceptions == null)
                return false;

            for (int i = 0; i < species.offTypeMoveExceptions.Count; i++)
            {
                MoveDefinitionSO move = species.offTypeMoveExceptions[i];
                if (move != null && string.Equals(move.moveId, moveId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string FindExclusiveOwner(
            SpeciesDefinitionSO species,
            string moveId,
            SpeciesDatabaseSO speciesDatabase)
        {
            if (speciesDatabase?.species != null)
            {
                for (int i = 0; i < speciesDatabase.species.Count; i++)
                {
                    SpeciesDefinitionSO candidate = speciesDatabase.species[i];
                    if (candidate != null && ContainsExclusive(candidate, moveId))
                        return candidate.speciesId ?? "";
                }
            }

            if (ContainsExclusive(species, moveId))
                return species.speciesId ?? "";

            return "";
        }

        private static bool ContainsExclusive(SpeciesDefinitionSO species, string moveId)
        {
            if (species?.exclusiveMoves == null)
                return false;

            for (int i = 0; i < species.exclusiveMoves.Count; i++)
            {
                MoveDefinitionSO move = species.exclusiveMoves[i];
                if (move != null && string.Equals(move.moveId, moveId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
