using System.Collections.Generic;

namespace Chainbound.Combat
{
    public class ChainlingPveNftMetadataContributor : IChainlingNftMetadataContributor
    {
        public IReadOnlyDictionary<string, object> BuildServiceRecordMetadata(ChainlingPveServiceRecord record)
        {
            ChainlingPveServiceRecord source = ChainlingPveServiceRecord.CloneOrDefault(record);
            return new Dictionary<string, object>
            {
                { "service_record_version", source.serviceRecordVersion },
                { "pve_total_victories", source.totalVictories },
                { "pve_xp_earned_lifetime", source.xpEarnedLifetime },
                { "pve_progression_moves_granted", source.progressionMovesGranted },
                { "pve_damage_dealt", source.damageDealt },
                { "pve_damage_taken", source.damageTaken },
                { "pve_damage_mitigated", source.damageMitigated },
                { "pve_healing_total", source.healingTotal },
                { "pve_enemy_kos", source.enemyKos },
            };
        }
    }
}
