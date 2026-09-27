using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "ChainlingProgressionEventCatalog", menuName = "Chainbound/Combat/Progression/Event Catalog")]
    public class ChainlingProgressionEventCatalogSO : ScriptableObject
    {
        public List<ChainlingProgressionEventSO> events = new List<ChainlingProgressionEventSO>();

        public IReadOnlyList<ChainlingProgressionEventSO> Events => events;

        public ChainlingProgressionEventSO GetById(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || events == null)
                return null;

            for (int i = 0; i < events.Count; i++)
            {
                ChainlingProgressionEventSO entry = events[i];
                if (entry != null && string.Equals(entry.eventId, eventId, System.StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private void OnValidate()
        {
            if (events == null)
                events = new List<ChainlingProgressionEventSO>();
        }
    }
}
