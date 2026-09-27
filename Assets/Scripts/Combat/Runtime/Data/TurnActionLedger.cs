using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Serializable, list-backed turn memory for modifier resolution.
    /// Keep this free of dictionaries/sets so battle snapshots remain Unity-serializer friendly.
    /// </summary>
    [Serializable]
    public sealed class TurnActionLedger
    {
        public int currentTurn = -1;
        public List<string> actorsThatActedThisTurn = new List<string>();
        public List<string> actorsThatMissedThisTurn = new List<string>();
        public List<string> actorsThatMissedLastTurn = new List<string>();
        public List<string> targetsHitThisTurn = new List<string>();
        public List<string> targetsHitLastTurn = new List<string>();
        public List<string> targetsDamagedThisTurn = new List<string>();
        public List<string> targetsDamagedLastTurn = new List<string>();
        public List<string> targetsKoThisTurn = new List<string>();
        public List<TurnTeamActorEntry> damagingMoveActorsThisTurn = new List<TurnTeamActorEntry>();
        public List<TurnTeamStringEntry> allyKosThisTurn = new List<TurnTeamStringEntry>();
        public List<TurnActorTargetEntry> actorTargetsHitThisTurn = new List<TurnActorTargetEntry>();

        public void BeginTurn(int turn)
        {
            if (currentTurn == turn)
                return;

            actorsThatMissedLastTurn.Clear();
            AddRangeUnique(actorsThatMissedLastTurn, actorsThatMissedThisTurn);
            targetsHitLastTurn.Clear();
            AddRangeUnique(targetsHitLastTurn, targetsHitThisTurn);
            targetsDamagedLastTurn.Clear();
            AddRangeUnique(targetsDamagedLastTurn, targetsDamagedThisTurn);

            actorsThatActedThisTurn.Clear();
            actorsThatMissedThisTurn.Clear();
            targetsHitThisTurn.Clear();
            targetsDamagedThisTurn.Clear();
            targetsKoThisTurn.Clear();
            damagingMoveActorsThisTurn.Clear();
            allyKosThisTurn.Clear();
            actorTargetsHitThisTurn.Clear();
            currentTurn = turn;
        }

        public void EndTurn(int turn)
        {
            currentTurn = turn;
        }

        public void RecordActorAction(string actorId)
        {
            AddUnique(actorsThatActedThisTurn, actorId);
        }

        public void RecordMiss(string attackerId)
        {
            AddUnique(actorsThatMissedThisTurn, attackerId);
        }

        public void RecordHit(string targetId)
        {
            AddUnique(targetsHitThisTurn, targetId);
        }

        public void RecordHit(string actorId, string targetId)
        {
            RecordHit(targetId);
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(targetId))
                return;

            for (int i = 0; i < actorTargetsHitThisTurn.Count; i++)
            {
                TurnActorTargetEntry entry = actorTargetsHitThisTurn[i];
                if (string.Equals(entry.actorId, actorId, StringComparison.Ordinal) &&
                    string.Equals(entry.targetId, targetId, StringComparison.Ordinal))
                    return;
            }

            actorTargetsHitThisTurn.Add(new TurnActorTargetEntry
            {
                actorId = actorId,
                targetId = targetId,
            });
        }

        public void RecordHpDamage(string targetId, int damage)
        {
            if (damage > 0)
                AddUnique(targetsDamagedThisTurn, targetId);
        }

        public void RecordDamagingMove(int teamIndex, string actorId)
        {
            if (teamIndex < 0 || string.IsNullOrWhiteSpace(actorId))
                return;

            for (int i = 0; i < damagingMoveActorsThisTurn.Count; i++)
            {
                TurnTeamActorEntry entry = damagingMoveActorsThisTurn[i];
                if (entry.teamIndex == teamIndex && string.Equals(entry.actorId, actorId, StringComparison.Ordinal))
                    return;
            }

            damagingMoveActorsThisTurn.Add(new TurnTeamActorEntry
            {
                teamIndex = teamIndex,
                actorId = actorId
            });
        }

        public void RecordKo(string defeatedId, int defeatedTeamIndex)
        {
            AddUnique(targetsKoThisTurn, defeatedId);
            if (defeatedTeamIndex >= 0 && !string.IsNullOrWhiteSpace(defeatedId))
            {
                allyKosThisTurn.Add(new TurnTeamStringEntry
                {
                    teamIndex = defeatedTeamIndex,
                    value = defeatedId
                });
            }
        }

        public bool DidActorActThisTurn(string actorId)
        {
            return Contains(actorsThatActedThisTurn, actorId);
        }

        public bool DidChainlingMissLastTurn(string chainlingId)
        {
            return Contains(actorsThatMissedLastTurn, chainlingId);
        }

        public bool DidActorMissThisTurn(string actorId)
        {
            return Contains(actorsThatMissedThisTurn, actorId);
        }

        public bool DidTargetMissLastTurn(string targetId)
        {
            return DidChainlingMissLastTurn(targetId);
        }

        public bool WasTargetHitThisTurn(string targetId)
        {
            return Contains(targetsHitThisTurn, targetId);
        }

        public bool WasTargetHitLastTurn(string targetId)
        {
            return Contains(targetsHitLastTurn, targetId);
        }

        public bool DidTargetTakeDamageThisTurn(string targetId)
        {
            return Contains(targetsDamagedThisTurn, targetId);
        }

        public bool DidTargetTakeDamageLastTurn(string targetId)
        {
            return Contains(targetsDamagedLastTurn, targetId);
        }

        public bool DidActorUseDamagingMoveThisTurn(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return false;
            for (int i = 0; i < damagingMoveActorsThisTurn.Count; i++)
            {
                if (string.Equals(damagingMoveActorsThisTurn[i].actorId, actorId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public int CountDistinctTargetsHitThisTurn(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return 0;
            int count = 0;
            for (int i = 0; i < actorTargetsHitThisTurn.Count; i++)
            {
                if (string.Equals(actorTargetsHitThisTurn[i].actorId, actorId, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        public bool DidTeamSufferKoThisTurn(int teamIndex)
        {
            for (int i = 0; i < allyKosThisTurn.Count; i++)
            {
                if (allyKosThisTurn[i].teamIndex == teamIndex)
                    return true;
            }
            return false;
        }

        public bool DidTeamUseDamagingMoveThisTurn(int teamIndex)
        {
            for (int i = 0; i < damagingMoveActorsThisTurn.Count; i++)
            {
                if (damagingMoveActorsThisTurn[i].teamIndex == teamIndex)
                    return true;
            }

            return false;
        }

        public bool DidAllyUseDamagingMoveThisTurn(int teamIndex, string actorId)
        {
            for (int i = 0; i < damagingMoveActorsThisTurn.Count; i++)
            {
                TurnTeamActorEntry entry = damagingMoveActorsThisTurn[i];
                if (entry.teamIndex == teamIndex &&
                    !string.Equals(entry.actorId, actorId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public bool DidAllyKoOccurThisTurn(int teamIndex)
        {
            for (int i = 0; i < allyKosThisTurn.Count; i++)
            {
                if (allyKosThisTurn[i].teamIndex == teamIndex)
                    return true;
            }

            return false;
        }

        private static void AddRangeUnique(List<string> target, List<string> source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
                AddUnique(target, source[i]);
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (list == null || string.IsNullOrWhiteSpace(value) || Contains(list, value))
                return;
            list.Add(value);
        }

        private static bool Contains(List<string> list, string value)
        {
            if (list == null || string.IsNullOrWhiteSpace(value))
                return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }

    [Serializable]
    public struct TurnTeamActorEntry
    {
        public int teamIndex;
        public string actorId;
    }

    [Serializable]
    public struct TurnTeamStringEntry
    {
        public int teamIndex;
        public string value;
    }

    [Serializable]
    public struct TurnActorTargetEntry
    {
        public string actorId;
        public string targetId;
    }
}
