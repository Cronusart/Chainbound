using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class CombatPopupVisualCatalogEntry
    {
        public CombatPopupCategory category;
        public CombatPopupVisualProfileSO profile;
    }

    /// <summary>
    /// Maps combat popup categories to visual profiles with a safe fallback.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatPopupVisualCatalog", menuName = "Chainbound/Combat/Presentation/Popups/Popup Visual Catalog")]
    public class CombatPopupVisualCatalogSO : ScriptableObject
    {
        [SerializeField] private CombatPopupVisualProfileSO defaultProfile;
        [SerializeField] private List<CombatPopupVisualCatalogEntry> entries = new List<CombatPopupVisualCatalogEntry>();

        private Dictionary<CombatPopupCategory, CombatPopupVisualProfileSO> _lookup;

        public CombatPopupVisualProfileSO DefaultProfile => defaultProfile;

        public CombatPopupVisualProfileSO ResolveProfile(CombatPopupCategory category)
        {
            if (_lookup == null)
                BuildLookup();

            if (_lookup.TryGetValue(category, out var profile) && profile != null)
                return profile;

            return defaultProfile;
        }

        public void BuildLookup()
        {
            _lookup = new Dictionary<CombatPopupCategory, CombatPopupVisualProfileSO>();
            for (int i = 0; i < entries.Count; i++)
            {
                CombatPopupVisualCatalogEntry entry = entries[i];
                if (entry == null || entry.profile == null)
                    continue;
                _lookup[entry.category] = entry.profile;
            }
        }

        private void OnEnable()
        {
            BuildLookup();
        }
    }
}
