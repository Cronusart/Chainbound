using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    [CreateAssetMenu(
        fileName = "CandyMachineDevConfig",
        menuName = "Chainbound/UI/Candy Machine Dev Config")]
    public sealed class CandyMachineDevConfigSO : ScriptableObject
    {
        [Header("Development Surface")]
        [Tooltip("When disabled, hide the dev grant strip and block its grant actions. This does not affect authoritative battle rewards.")]
        [SerializeField] private bool showDevGrantStrip = true;

        [SerializeField, Min(1)] private int defaultRamGrant = 500;
        [SerializeField, Min(1)] private int seed = 1776;
        [SerializeField] private string defaultGrantMoveId;

        public bool ShowDevGrantStrip => showDevGrantStrip;
        public int DefaultRamGrant => Mathf.Max(1, defaultRamGrant);
        public int Seed => seed;
        public string DefaultGrantMoveId => defaultGrantMoveId;
    }
}
