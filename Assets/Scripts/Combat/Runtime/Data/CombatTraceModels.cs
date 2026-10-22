using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    [Serializable]
    public struct StatBreakdown
    {
        public CoreStat stat;
        public int baseValue;
        public int ivValue;
        public int basePlusIv;
        public int stars;
        public int rebirthCount;
        public int cycleAutoFlat;
        public int cyclePickFlat;
        /// <summary>Flat MaxHP from equipped passive DUR MaxHP% modifiers (MaxHP only).</summary>
        public int passiveMaxHpBonus;
        /// <summary>Legacy field; always 1 under Cycle flat progression.</summary>
        public float progressionMultiplier;
        public float stageMultiplier;
        public int stageValue;
        public float statusMultiplier;
        public int finalInt;
    }

    [Serializable]
    public struct StatsSnapshot
    {
        public StatBreakdown maxHP;
        public StatBreakdown meleeAtk;
        public StatBreakdown meleeDef;
        public StatBreakdown rangedAtk;
        public StatBreakdown rangedDef;
        public StatBreakdown speed;
    }

    [Serializable]
    public struct DamageComputationTrace
    {
        public CoreStat atkStatUsed;
        public CoreStat defStatUsed;
        public float attackerStatFinal;
        public float defenderStatFinal;
        public float atkDefRatio;
        public int basePower;
        public float levelFactor;
        public float randomFactor;
        public float stab;
        public float chemistry;
        public float crit;
        public float mitigation;
        public float wallMitigation;
        public float mitigationFloor;
        public float tuningMultiplier;
        public int preMitigationDamage;
        public int finalDamage;
    }

    [Serializable]
    public class DamageTracePayload
    {
        public string attackerId;
        public string defenderId;
        public string moveId;
        public CoreStat atkStatUsed;
        public CoreStat defStatUsed;
        public StatBreakdown atkBreakdown;
        public StatBreakdown defBreakdown;
        public float atkDefRatio;
        public int basePower;
        public float randomFactor;
        public float stab;
        public float chemistry;
        public float crit;
        public float mitigation;
        public float mitigationFloor;
        public float tuningMultiplier;
        public int finalDamage;
        public int hpBefore;
        public int hpAfter;
        public int expectedDelta;
        public int actualDelta;
        public bool damageApplicationMismatch;
        public string mismatchReason;
    }

    [Serializable]
    public class InitiativeActorTrace
    {
        public string actorId;
        public int teamIndex;
        public int slotIndex;
        public int stars;
        public int rebirthCount;
        public int speedFinal;
        public StatBreakdown speedBreakdown;
    }

    [Serializable]
    public class TieBreakTrace
    {
        public List<string> actorIds;
        public List<float> rolls;
        public List<string> resolvedOrder;

        public TieBreakTrace()
        {
            actorIds = new List<string>();
            rolls = new List<float>();
            resolvedOrder = new List<string>();
        }
    }

    [Serializable]
    public class InitiativeTracePayload
    {
        public int turn;
        public List<InitiativeActorTrace> candidates;
        public List<string> finalOrder;
        public List<TieBreakTrace> tieBreaks;

        public InitiativeTracePayload()
        {
            candidates = new List<InitiativeActorTrace>();
            finalOrder = new List<string>();
            tieBreaks = new List<TieBreakTrace>();
        }
    }
}
