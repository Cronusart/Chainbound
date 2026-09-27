using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Inspector-configurable player party provider for the real battle scene.
    /// Used as a fallback while inventory / NFT wallet systems are not yet implemented.
    /// Feeds the real <see cref="CombatBattleController"/> pipeline with production-shaped snapshots.
    /// </summary>
    [CreateAssetMenu(fileName = "ManualPartyProvider", menuName = "Chainbound/Combat/Testing/Manual Party Provider")]
    public class ManualPartyProviderSO : ScriptableObject, IPlayerPartyProvider
    {
        [Header("Party (1-3 Chainlings)")]
        [Tooltip("Ordered roster: index 0 = Lead, 1 = Partner, 2 = Bench.")]
        [SerializeField] private List<ManualChainlingEntry> roster = new List<ManualChainlingEntry>();

        [Header("Team")]
        [SerializeField] private string teamName = "Player";

        public PartySnapshot GetCurrentParty()
        {
            var snapshot = new PartySnapshot { teamName = teamName };

            int count = Mathf.Min(roster.Count, CombatConstants.ROSTER_SIZE);
            for (int i = 0; i < count; i++)
            {
                ManualChainlingEntry entry = roster[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.speciesId))
                    continue;

                ChainlingSnapshot built = ManualChainlingSnapshotBuilder.Build(entry);
                if (built != null)
                    snapshot.chainlings.Add(built);
            }

            return snapshot;
        }
    }
}
