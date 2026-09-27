using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Central catalog of all modifier definitions.
    /// </summary>
    [CreateAssetMenu(fileName = "ModifierCatalog", menuName = "Chainbound/Combat/Modifiers/Modifier Catalog")]
    public class ModifierCatalogSO : ScriptableObject
    {
        public List<ModifierDefinitionSO> modifiers = new List<ModifierDefinitionSO>();

        private Dictionary<string, ModifierDefinitionSO> _lookup;

        /// <summary>Get a modifier definition by section ID.</summary>
        public ModifierDefinitionSO GetBySectionId(string sectionId)
        {
            if (_lookup == null) BuildLookup();
            if (string.IsNullOrWhiteSpace(sectionId))
                return null;

            _lookup.TryGetValue(sectionId.Trim(), out var def);
            return def;
        }

        /// <summary>Get all modifiers of a given category.</summary>
        public List<ModifierDefinitionSO> GetByCategory(ModifierCategory category)
        {
            var result = new List<ModifierDefinitionSO>();
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].category == category)
                    result.Add(modifiers[i]);
            }
            return result;
        }

        /// <summary>Get all modifier definitions in this catalog.</summary>
        public IReadOnlyList<ModifierDefinitionSO> GetAll() => modifiers;

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, ModifierDefinitionSO>(modifiers.Count, System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i] != null && !string.IsNullOrEmpty(modifiers[i].sectionId))
                    _lookup[modifiers[i].sectionId] = modifiers[i];
            }
        }

        private void OnEnable() => BuildLookup();
    }
}
