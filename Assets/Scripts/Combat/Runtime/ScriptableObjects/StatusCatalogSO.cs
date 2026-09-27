using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Single source of truth for all status definitions in the game.
    /// Holds references to all StatusDefinitionSO assets.
    /// </summary>
    [CreateAssetMenu(fileName = "StatusCatalog", menuName = "Chainbound/Combat/Statuses/Status Catalog")]
    public class StatusCatalogSO : ScriptableObject
    {
        [Tooltip("All status definitions. Must cover every StatusId in the enum.")]
        public List<StatusDefinitionSO> definitions = new List<StatusDefinitionSO>();

        private Dictionary<StatusId, StatusDefinitionSO> _lookup;

        /// <summary>
        /// Get a status definition by ID. Returns null if not found.
        /// </summary>
        public StatusDefinitionSO Get(StatusId id)
        {
            if (_lookup == null) BuildLookup();
            _lookup.TryGetValue(id, out var def);
            return def;
        }

        /// <summary>
        /// Returns true if the given status is ChemistryOnly.
        /// </summary>
        public bool IsChemistryOnly(StatusId id)
        {
            var def = Get(id);
            return def != null && def.origin == StatusOrigin.ChemistryOnly;
        }

        /// <summary>Rebuild the lookup dictionary. Call after modifying the list at edit-time.</summary>
        public void BuildLookup()
        {
            _lookup = new Dictionary<StatusId, StatusDefinitionSO>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && !_lookup.ContainsKey(definitions[i].statusId))
                    _lookup[definitions[i].statusId] = definitions[i];
            }
        }

        private void OnEnable()
        {
            // Lazy rebuild so CreateInstance + definitions.Add + Get works even when
            // OnEnable ran before the list was populated.
            _lookup = null;
        }
    }
}
