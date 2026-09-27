using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEngine;
using UnityEngine.Serialization;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Prefab references for Chainlings tab content. Kept separate from
    /// <see cref="PlayerMenuRoot"/> so layout can be authored without touching the menu shell.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ChainlingsMenuViews",
        menuName = "Chainbound/UI/Chainlings Menu Views")]
    public sealed class ChainlingsMenuViewsSO : ScriptableObject
    {
        [Header("View Prefabs")]
        [SerializeField, FormerlySerializedAs("rosterViewPrefab")] private ChainlingsPartyScreen partyViewPrefab;
        [SerializeField] private ChainlingPartyColumnShell partyColumnShellPrefab;
        [SerializeField] private ChainlingPartySlotWidget partySlotViewPrefab;
        [SerializeField] private ChainlingDetailScreen detailViewPrefab;
        [SerializeField] private ChainlingCardWidget cardViewPrefab;

        public ChainlingsPartyScreen PartyViewPrefab => partyViewPrefab;
        public ChainlingPartyColumnShell PartyColumnShellPrefab => partyColumnShellPrefab;
        public ChainlingPartySlotWidget PartySlotViewPrefab => partySlotViewPrefab;
        public ChainlingDetailScreen DetailViewPrefab => detailViewPrefab;
        public ChainlingCardWidget CardViewPrefab => cardViewPrefab;

        public bool HasPartyViewPrefab => partyViewPrefab != null;
    }
}
