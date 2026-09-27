using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public static class CombatLogSettings
    {
        public static bool EnableForensicLogging = true;
        public static bool EnableTranscript = true;
        public static int MaxEventsKept = 5000;
    }

    public enum CombatForensicEventType
    {
        TurnOrderComputed,
        ActorSelected,
        ActionChosen,
        TargetsResolved,
        Retargeted,
        APCostPaid,
        APChanged,
        ProcRoll,
        MultiHitRolled,
        HitCheck,
        DamageResolved,
        ChemistryResolved,
        StatusApplied,
        StatusRefreshed,
        StatusCleansed,
        StatusBlocked,
        KOResolved,
        SwapResolved,
        TurnEndStatusTicks,
        TurnEndSnapshot,
        PriorityOverride,
        DamagePrevented,
        ConfigStamp,
        BattleEnded,
    }

    [Serializable]
    public class CombatEntityRef
    {
        public int teamId = -1;
        public int slotId = -1;
        public string instanceId;
    }

    [Serializable]
    public class CombatLogEvent
    {
        public int sequenceIndex;
        public string battleId;
        public int turnIndex;
        public int actionIndex;
        public string timestampUtc;
        public string eventType;
        public CombatEntityRef actorRef;
        public List<CombatEntityRef> targets = new List<CombatEntityRef>();
        public int rngSeed;
        public int rngStepIndex;
        public string payloadJson;
    }

    [Serializable]
    public class ForensicActorInfo
    {
        public CombatEntityRef actorRef;
        public string speciesId;
        public string displayName;
        public int stars;
        public int rebirthCount;
        public int speedFinal;
        public float tieBreakRoll = -1f;
    }

    [Serializable]
    public class TieBreakGroupPayload
    {
        public List<string> actorInstanceIds = new List<string>();
        public List<float> rolls = new List<float>();
        public List<string> resolvedOrder = new List<string>();
    }

    [Serializable]
    public class TurnOrderComputedPayload
    {
        /// <summary>Human-readable: primary rule for action sequence (e.g. Speed).</summary>
        public string orderRule = "Speed";
        public string tieBreakRuleUsed;
        /// <summary>Phase 1: Swaps execute first, before any combat. Order by speed among swappers.</summary>
        public List<string> swapPhaseOrder = new List<string>();
        /// <summary>Phase 2: Priority moves (PRIOR tag) execute after swaps but before normal combat. Speed-ordered.</summary>
        public List<string> priorityBlockOrder = new List<string>();
        /// <summary>Phase 3: Normal combat actions execute last among moves. Order by speed among combatants.</summary>
        public List<string> combatPhaseOrder = new List<string>();
        /// <summary>Phase 4: Capture attempts always resolve after combat, before end-of-turn ticks.</summary>
        public List<string> capturePhaseOrder = new List<string>();
        public List<ForensicActorInfo> candidates = new List<ForensicActorInfo>();
        public List<string> finalOrder = new List<string>();
        public List<TieBreakGroupPayload> tieBreakGroups = new List<TieBreakGroupPayload>();
    }

    [Serializable]
    public class ActorSelectedPayload
    {
        public string reason;
        public int queueIndex;
    }

    [Serializable]
    public class ActionChosenPayload
    {
        public string actorDisplayName;
        public string actionType;
        public string moveId;
        public string moveName;
        public string category;
        public string targetingMode;
        public int apCost;
        public int selectedTargetTeamId = -1;
        public int selectedTargetSlotId = -1;
        public string selectedTargetId;
    }

    [Serializable]
    public class TargetsResolvedPayload
    {
        public List<string> selectedTargetNames = new List<string>();
        public List<string> resolvedTargetNames = new List<string>();
        public List<CombatEntityRef> selectedTargets = new List<CombatEntityRef>();
        public List<CombatEntityRef> resolvedTargets = new List<CombatEntityRef>();
        public bool retargeted;
        public string retargetReason;
    }

    [Serializable]
    public class APCostPaidPayload
    {
        public int apBefore;
        public int cost;
        public int apAfter;
    }

    [Serializable]
    public class APChangedPayload
    {
        public int apBefore;
        public int delta;
        public int apAfter;
        public string source;
    }

    [Serializable]
    public class ProcRollPayload
    {
        public string procId;
        public float chance;
        public float roll;
        public bool triggered;
    }

    [Serializable]
    public class MultiHitRolledPayload
    {
        public int minHits;
        public int maxHits;
        public int rolledHits;
        public bool capped;
        public float roll;
    }

    [Serializable]
    public class HitCheckPayload
    {
        public string attackerDisplayName;
        public string targetDisplayName;
        public string moveId;
        public string moveName;
        public int hitIndex;
        public int totalHits;
        public float hitChance;
        public float roll;
        public bool didHit;
        public string missReason;
        public int attackerAccuracyStage;
        public int defenderEvasionStage;
        public float attackerAccuracyMultiplier;
        public float defenderEvasionMultiplier;
    }

    [Serializable]
    public class DamagePacketPayload
    {
        public int movePower;
        public string damageType;
        public string attackerStatUsed;
        public string defenderStatUsed;
        public int attackerAtkFinal;
        public int defenderDefFinal;
        public float levelFactor;
        public float randomMultiplier;
        public float stabMultiplier;
        public int chemistryAdv;
        public int chemistryDis;
        public int chemistryExponent;
        public float chemistryMultiplier;
        public float critRate;
        public float critRoll;
        public float critMultiplier;
        public bool wasCrit;
        public int finalDamage;
        public string clampReason;
    }

    [Serializable]
    public class DamageResolvedPayload
    {
        public string attackerDisplayName;
        public string targetDisplayName;
        public string moveId;
        public string moveName;
        public int hitIndex;
        public int totalHits;
        public int hpBefore;
        public int hpAfter;
        /// <summary>Max HP of the target for transcript display (current/max).</summary>
        public int maxHp;
        public DamagePacketPayload packet;
    }

    [Serializable]
    public class ChemistryReactionDecisionPayload
    {
        public ChainlingType attackType;
        public ChainlingType targetType;
        public StatusId statusId;
        public ChainlingType parameterType;
        public ChemistryRecipient recipient;
        public bool applied;
        public string blockedReason;
        public int counterBefore;
        public int counterAfter;
    }

    [Serializable]
    public class ChemistryResolvedPayload
    {
        public string moveId;
        public string moveName;
        public ChainlingType moveType;
        public string moveTypeName;
        public ChainlingType coatingType;
        public ChainlingType[] targetTypes;
        public string[] targetTypeNames;
        public ChemistryRelation relation;
        public int chemAdv;
        public int chemDis;
        public int exponent;
        public float chemistryMultiplier;
        public List<ChemistryReactionDecisionPayload> reactions = new List<ChemistryReactionDecisionPayload>();
    }

    [Serializable]
    public class StatusEventPayload
    {
        public string targetDisplayName;
        public string statusId;
        public string origin;
        public int durationBefore;
        public int durationAfter;
        public int counterBefore;
        public int counterAfter;
        public string blockReason;
        public string sourceId;
    }

    [Serializable]
    public class KOResolvedPayload
    {
        public string targetDisplayName;
        public int hpBefore;
        public int hpAfter;
        public string koSource;
    }

    [Serializable]
    public class SwapResolvedPayload
    {
        public string teamName;
        public string outgoingDisplayName;
        public string incomingDisplayName;
        public CombatEntityRef outgoingRef;
        public CombatEntityRef incomingRef;
        public string swapType;
        public bool incomingCanActThisTurn;
        public bool incomingEligibleNextTurn;
        public string note;
    }

    [Serializable]
    public class TurnEndStatusTickEntry
    {
        public string targetDisplayName;
        public CombatEntityRef targetRef;
        public string tickType;
        public string source;
        public int amount;
        public int hpBefore;
        public int hpAfter;
        public int apBefore;
        public int apAfter;
    }

    [Serializable]
    public class TurnEndStatusTicksPayload
    {
        public List<TurnEndStatusTickEntry> ticks = new List<TurnEndStatusTickEntry>();
    }

    [Serializable]
    public class StatusSnapshotPayload
    {
        public string statusId;
        public int turnsRemaining;
        public bool consumed;
    }

    [Serializable]
    public class EntityTurnSnapshotPayload
    {
        public CombatEntityRef entityRef;
        public string speciesId;
        public string displayName;
        public int hp;
        public int maxHp;
        public int ap;
        public int maxAp;
        public bool isAlive;
        public ChainlingType primaryType;
        public ChainlingType secondaryType;
        public ChainlingType coatingType;
        public int stageMeleeAtk;
        public int stageMeleeDef;
        public int stageRangedAtk;
        public int stageRangedDef;
        public int stageSpeed;
        public int stageAccuracy;
        public int stageEvasion;
        public int stageCritRate;
        public float mitigationTotal;
        public int mitigationSourceCount;
        public float accuracyBonusMultiplier = 1f;
        public int accuracySourceCount;
        public List<StatusSnapshotPayload> statuses = new List<StatusSnapshotPayload>();
    }

    [Serializable]
    public class TurnEndSnapshotPayload
    {
        public List<EntityTurnSnapshotPayload> entities = new List<EntityTurnSnapshotPayload>();
    }

    [Serializable]
    public class ConfigStampPayload
    {
        public string rulesetId;
        public int configVersion;
        public string configHash;
        public int resolverConfigVersion;
        public string resolverConfigHash;
        public string overlayId;
    }

    [Serializable]
    public class BattleEndedPayload
    {
        public int winnerTeamIndex;
        public int turnCount;
        public float durationSeconds;
        public string battleType;
        public string endReason;
        public string team0Name;
        public string team1Name;
        public int participantCountPerTeam;
        public string rulesetId;
        public string configHash;
    }

    [Serializable]
    public class CombatForensicLogExport
    {
        public string battleId;
        public int seed;
        public int rngFinalStep;
        public int eventCount;
        public List<CombatLogEvent> events = new List<CombatLogEvent>();
    }

    /// <summary>
    /// Human-readable type names for combat log export (hackathon demo).
    /// </summary>
    public static class CombatLogTypeNames
    {
        public static string GetTypeName(ChainlingType type) => type.ToString();

        public static string GetStatName(CoreStat stat) => stat.ToString();

        public static Dictionary<int, string> BuildTypeIdToNameMap()
        {
            var map = new Dictionary<int, string>();
            foreach (ChainlingType t in System.Enum.GetValues(typeof(ChainlingType)))
                map[(int)t] = t.ToString();
            return map;
        }
    }
}
