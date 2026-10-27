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
        public List<string> recordedKoIds = new List<string>();
        public int koSequence;
        public int lastKoSequenceTeam0 = -1;
        public int lastKoSequenceTeam1 = -1;
        public List<TurnActorIntEntry> bossTellStates = new List<TurnActorIntEntry>();
        public int actionSerial;
        public int raiseLowestStageActionSerial = -1;
        public List<string> pendingUplinkAllies = new List<string>();
        public List<string> uplinkPaidKeys = new List<string>();
        public List<SetupFeedArm> pendingSetupFeeds = new List<SetupFeedArm>();
        public float activeSetupFeedFraction;
        public string activeSetupFeedActorId;

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
            if (uplinkPaidKeys != null)
                uplinkPaidKeys.Clear();
            else
                uplinkPaidKeys = new List<string>();
            // Setup Feed waits for the next ally action, so a new turn must not drop it.
        }

        public void ClearPendingUplink()
        {
            if (pendingUplinkAllies == null)
                pendingUplinkAllies = new List<string>();
            else
                pendingUplinkAllies.Clear();
        }

        public void NoteUplinkAlly(string sourceId, string allyId)
        {
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(allyId) ||
                string.Equals(sourceId, allyId, StringComparison.Ordinal))
                return;

            if (pendingUplinkAllies == null)
                pendingUplinkAllies = new List<string>();
            string key = sourceId + "|" + allyId;
            for (int i = 0; i < pendingUplinkAllies.Count; i++)
            {
                if (string.Equals(pendingUplinkAllies[i], key, StringComparison.Ordinal))
                    return;
            }
            pendingUplinkAllies.Add(key);
        }

        public bool TryClaimUplinkPay(string sourceId, string allyId)
        {
            if (uplinkPaidKeys == null)
                uplinkPaidKeys = new List<string>();
            string key = sourceId + "|" + allyId;
            for (int i = 0; i < uplinkPaidKeys.Count; i++)
            {
                if (string.Equals(uplinkPaidKeys[i], key, StringComparison.Ordinal))
                    return false;
            }
            uplinkPaidKeys.Add(key);
            return true;
        }

        public void EndTurn(int turn)
        {
            currentTurn = turn;
        }

        public void RecordActorAction(string actorId)
        {
            actionSerial++;
            AddUnique(actorsThatActedThisTurn, actorId);
        }

        public void ArmSetupFeed(int teamIndex, string sourceActorId, float powerFraction)
        {
            if (teamIndex < 0 || string.IsNullOrWhiteSpace(sourceActorId) || powerFraction <= 0f)
                return;

            EnsurePendingSetupFeeds();
            for (int i = pendingSetupFeeds.Count - 1; i >= 0; i--)
            {
                SetupFeedArm existing = pendingSetupFeeds[i];
                if (existing != null &&
                    existing.teamIndex == teamIndex &&
                    string.Equals(existing.sourceActorId, sourceActorId, StringComparison.Ordinal))
                    pendingSetupFeeds.RemoveAt(i);
            }

            pendingSetupFeeds.Add(new SetupFeedArm
            {
                teamIndex = teamIndex,
                sourceActorId = sourceActorId,
                powerFraction = powerFraction,
                armedActionSerial = actionSerial,
            });
        }

        public bool TryConsumeSetupFeeds(int teamIndex, string actorId, bool powerMove, List<float> consumedFractions)
        {
            if (consumedFractions != null)
                consumedFractions.Clear();
            if (teamIndex < 0 || string.IsNullOrWhiteSpace(actorId))
                return false;

            EnsurePendingSetupFeeds();
            float total = 0f;
            bool consumed = false;
            for (int i = pendingSetupFeeds.Count - 1; i >= 0; i--)
            {
                SetupFeedArm arm = pendingSetupFeeds[i];
                if (arm == null)
                {
                    pendingSetupFeeds.RemoveAt(i);
                    continue;
                }

                if (arm.teamIndex != teamIndex ||
                    string.Equals(arm.sourceActorId, actorId, StringComparison.Ordinal) ||
                    arm.armedActionSerial >= actionSerial ||
                    arm.powerFraction <= 0f)
                    continue;

                pendingSetupFeeds.RemoveAt(i);
                consumed = true;
                total += arm.powerFraction;
                if (consumedFractions != null)
                    consumedFractions.Add(arm.powerFraction);
            }

            if (consumed && powerMove)
            {
                activeSetupFeedFraction = total;
                activeSetupFeedActorId = actorId;
            }

            return consumed;
        }

        public bool TryPeekActiveSetupFeed(string actorId, out float powerFraction)
        {
            powerFraction = 0f;
            if (activeSetupFeedFraction <= 0f ||
                string.IsNullOrWhiteSpace(actorId) ||
                !string.Equals(activeSetupFeedActorId, actorId, StringComparison.Ordinal))
                return false;

            powerFraction = activeSetupFeedFraction;
            return true;
        }

        public void ClearActiveSetupFeed()
        {
            activeSetupFeedFraction = 0f;
            activeSetupFeedActorId = null;
        }

        public bool HasPendingSetupFeed(int teamIndex, string sourceActorId)
        {
            EnsurePendingSetupFeeds();
            for (int i = 0; i < pendingSetupFeeds.Count; i++)
            {
                SetupFeedArm arm = pendingSetupFeeds[i];
                if (arm != null &&
                    arm.teamIndex == teamIndex &&
                    string.Equals(arm.sourceActorId, sourceActorId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void EnsurePendingSetupFeeds()
        {
            if (pendingSetupFeeds == null)
                pendingSetupFeeds = new List<SetupFeedArm>();
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
            RecordHit(-1, actorId, targetId);
        }

        public void RecordHit(int teamIndex, string actorId, string targetId)
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
                teamIndex = teamIndex,
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
            if (string.IsNullOrWhiteSpace(defeatedId) || Contains(recordedKoIds, defeatedId))
                return;

            recordedKoIds.Add(defeatedId);
            koSequence++;
            if (defeatedTeamIndex == 0)
                lastKoSequenceTeam0 = koSequence;
            else if (defeatedTeamIndex == 1)
                lastKoSequenceTeam1 = koSequence;

            AddUnique(targetsKoThisTurn, defeatedId);
            if (defeatedTeamIndex >= 0)
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

        public int CountDistinctTargetsHitThisTurnByTeam(int teamIndex)
        {
            if (teamIndex < 0)
                return 0;
            var targets = new List<string>();
            for (int i = 0; i < actorTargetsHitThisTurn.Count; i++)
            {
                TurnActorTargetEntry entry = actorTargetsHitThisTurn[i];
                if (entry.teamIndex == teamIndex)
                    AddUnique(targets, entry.targetId);
            }
            return targets.Count;
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

        public int GetBossTellState(string actorId)
        {
            if (bossTellStates == null || string.IsNullOrWhiteSpace(actorId))
                return 0;
            for (int i = 0; i < bossTellStates.Count; i++)
                if (string.Equals(bossTellStates[i].actorId, actorId, StringComparison.Ordinal))
                    return bossTellStates[i].value;
            return 0;
        }

        public void SetBossTellState(string actorId, int value)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return;
            if (bossTellStates == null)
                bossTellStates = new List<TurnActorIntEntry>();
            for (int i = 0; i < bossTellStates.Count; i++)
            {
                if (!string.Equals(bossTellStates[i].actorId, actorId, StringComparison.Ordinal))
                    continue;
                bossTellStates[i] = new TurnActorIntEntry { actorId = actorId, value = value };
                return;
            }
            bossTellStates.Add(new TurnActorIntEntry { actorId = actorId, value = value });
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
        public int teamIndex;
        public string actorId;
        public string targetId;
    }

    [Serializable]
    public struct TurnActorIntEntry
    {
        public string actorId;
        public int value;
    }

    [Serializable]
    public sealed class SetupFeedArm
    {
        public int teamIndex = -1;
        public string sourceActorId;
        public float powerFraction;
        public int armedActionSerial;
    }
}
