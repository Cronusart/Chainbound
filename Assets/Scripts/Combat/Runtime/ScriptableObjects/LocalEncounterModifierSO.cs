using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Designer-authored local encounter / reward modifier (events, tests, staging).
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterModifier_", menuName = "Chainbound/Combat/Encounters/Encounter Modifier")]
    public class LocalEncounterModifierSO : ScriptableObject
    {
        public EncounterModifier modifier = new EncounterModifier();

        private void OnValidate()
        {
            if (modifier == null)
                modifier = new EncounterModifier();
            if (string.IsNullOrWhiteSpace(modifier.modifierId))
                modifier.modifierId = name;
            if (string.IsNullOrWhiteSpace(modifier.displayName))
                modifier.displayName = name;
        }
    }
}
