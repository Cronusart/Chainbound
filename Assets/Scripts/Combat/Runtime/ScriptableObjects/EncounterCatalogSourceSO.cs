using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Points the database and the party Entry lookup at the same encounter zones.
    /// This is not a second species table. Entries still live on the zone assets.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterCatalogSource", menuName = "Chainbound/Combat/Encounters/Encounter Catalog Source")]
    public class EncounterCatalogSourceSO : ScriptableObject
    {
        public SpeciesDatabaseSO speciesDatabase;
        public List<EncounterZoneSO> zones = new List<EncounterZoneSO>();
    }
}
