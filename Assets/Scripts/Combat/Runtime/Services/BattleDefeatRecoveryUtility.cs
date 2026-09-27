using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Applies the data-driven post-defeat revival rule to the battle outcome snapshot
    /// before that snapshot is persisted. The KO flag remains intact for rewards and telemetry.
    /// </summary>
    public static class BattleDefeatRecoveryUtility
    {
        public static int ApplyToPveDefeat(BattleResult result, PostBattleRewardsConfigSO config)
        {
            if (!IsEligiblePveDefeat(result) || config == null || result.playerParticipants == null)
                return 0;

            int recoveredCount = 0;
            for (int i = 0; i < result.playerParticipants.Count; i++)
            {
                BattleParticipant participant = result.playerParticipants[i];
                if (participant == null || !participant.wasKOd)
                    continue;

                int recoveredHp = config.ResolveDefeatKoRespawnHp(participant.endMaxHp);
                if (recoveredHp <= 0)
                    continue;

                participant.endCurrentHp = recoveredHp;
                recoveredCount++;
            }

            return recoveredCount;
        }

        public static bool IsEligiblePveDefeat(BattleResult result)
        {
            return result != null
                && result.battleType == BattleType.PvE
                && !result.PlayerWon
                && !result.IsDraw
                && !result.wasFled
                && !result.EndedByCapture;
        }
    }
}
