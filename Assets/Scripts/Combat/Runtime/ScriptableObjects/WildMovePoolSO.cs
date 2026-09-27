using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Reusable weighted wild move pool shared across area/zone entries.
    /// </summary>
    [CreateAssetMenu(fileName = "WildMovePool_", menuName = "Chainbound/Combat/Encounters/Wild Move Pool")]
    public class WildMovePoolSO : ScriptableObject
    {
        public List<WeightedMoveEntry> entries = new List<WeightedMoveEntry>();
    }
}
