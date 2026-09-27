using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    [CreateAssetMenu(
        fileName = "CandyMachineDevConfig",
        menuName = "Chainbound/UI/Candy Machine Dev Config")]
    public sealed class CandyMachineDevConfigSO : ScriptableObject
    {
        [SerializeField, Min(1)] private int defaultRamGrant = 500;
        [SerializeField, Min(1)] private int seed = 1776;
        [SerializeField] private string defaultGrantMoveId;

        public int DefaultRamGrant => Mathf.Max(1, defaultRamGrant);
        public int Seed => seed;
        public string DefaultGrantMoveId => defaultGrantMoveId;
    }
}
