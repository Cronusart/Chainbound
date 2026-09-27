using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Patches cached roster/party snapshots with end-of-battle HP without a full refetch.
    /// </summary>
    public static class ChainlingRosterHpMutation
    {
        public static int PatchRosterFromParticipants(
            PlayerRosterSnapshot roster,
            IReadOnlyList<BattleParticipant> participants)
        {
            if (roster?.chainlings == null || participants == null || participants.Count == 0)
                return 0;

            int patched = 0;
            for (int i = 0; i < participants.Count; i++)
            {
                BattleParticipant participant = participants[i];
                if (participant == null || string.IsNullOrWhiteSpace(participant.backendChainlingId))
                    continue;

                if (PatchEntryHp(roster, participant.backendChainlingId, participant.endCurrentHp))
                    patched++;
            }

            return patched;
        }

        public static int PatchPartyFromParticipants(
            PartySnapshot party,
            IReadOnlyList<BattleParticipant> participants)
        {
            if (party?.chainlings == null || participants == null || participants.Count == 0)
                return 0;

            int patched = 0;
            for (int i = 0; i < participants.Count; i++)
            {
                BattleParticipant participant = participants[i];
                if (participant == null || string.IsNullOrWhiteSpace(participant.backendChainlingId))
                    continue;

                for (int j = 0; j < party.chainlings.Count; j++)
                {
                    ChainlingSnapshot snapshot = party.chainlings[j];
                    if (snapshot == null || snapshot.backendChainlingId != participant.backendChainlingId)
                        continue;

                    snapshot.currentHp = MathMaxZero(participant.endCurrentHp);
                    patched++;
                    break;
                }
            }

            return patched;
        }

        public static bool PatchEntryHp(PlayerRosterSnapshot roster, string backendChainlingId, int endCurrentHp)
        {
            if (roster?.chainlings == null || string.IsNullOrWhiteSpace(backendChainlingId))
                return false;

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                if (entry?.snapshot == null || entry.snapshot.backendChainlingId != backendChainlingId)
                    continue;

                entry.snapshot.currentHp = MathMaxZero(endCurrentHp);
                return true;
            }

            return false;
        }

        private static int MathMaxZero(int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}
