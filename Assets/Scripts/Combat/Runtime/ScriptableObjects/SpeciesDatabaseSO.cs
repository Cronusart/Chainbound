using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Central database of all species definitions.
    /// New species are added by creating SpeciesDefinitionSO assets and adding them here.
    /// </summary>
    [CreateAssetMenu(fileName = "SpeciesDatabase", menuName = "Chainbound/Combat/Species/Species Database")]
    public class SpeciesDatabaseSO : ScriptableObject
    {
        public List<SpeciesDefinitionSO> species = new List<SpeciesDefinitionSO>();

        private Dictionary<string, SpeciesDefinitionSO> _lookup;

        /// <summary>Get a species definition by ID.</summary>
        public SpeciesDefinitionSO GetById(string speciesId)
        {
            if (_lookup == null) BuildLookup();
            _lookup.TryGetValue(speciesId, out var def);
            return def;
        }

        /// <summary>Get all species of a given primary type.</summary>
        public List<SpeciesDefinitionSO> GetByType(ChainlingType type)
        {
            var result = new List<SpeciesDefinitionSO>();
            for (int i = 0; i < species.Count; i++)
            {
                if (species[i].primaryType == type || species[i].secondaryType == type)
                    result.Add(species[i]);
            }
            return result;
        }

        /// <summary>Get all starter species.</summary>
        public List<SpeciesDefinitionSO> GetStarters()
        {
            var result = new List<SpeciesDefinitionSO>();
            for (int i = 0; i < species.Count; i++)
            {
                if (species[i].isStarter) result.Add(species[i]);
            }
            return result;
        }

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, SpeciesDefinitionSO>(species.Count);
            for (int i = 0; i < species.Count; i++)
            {
                if (species[i] != null && !string.IsNullOrEmpty(species[i].speciesId))
                    _lookup[species[i].speciesId] = species[i];
            }
        }

        public void WarmLookup()
        {
            BuildLookup();
        }

        private void OnEnable() => BuildLookup();
    }
}
