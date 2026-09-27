using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    [Serializable]
    public class BattleRewardSummary
    {
        public int ramEarned;
        /// <summary>RAM removed after an eligible PvE defeat. Never applies to a flee or draw.</summary>
        public int ramPenalty;
        public List<ChainlingXpReward> chainlingRewards = new List<ChainlingXpReward>();
        public bool playerWon;
        public EncounterDifficulty difficulty;
        public string rareConsumableItemId = "";
        public bool grantedRareConsumable;

        /// <summary>Extra item ids granted by encounter reward modifiers / events.</summary>
        public List<string> bonusItemIds = new List<string>();

        /// <summary>Effective XP multiplier applied from encounter reward modifiers.</summary>
        public float xpMultiplier = 1f;

        /// <summary>Effective RAM multiplier applied from encounter reward modifiers.</summary>
        public float ramMultiplier = 1f;

        /// <summary>Account-level Known moves granted from encounter victory (boss events).</summary>
        public List<EncounterVictoryKnownGrantResult> victoryKnownGrants = new List<EncounterVictoryKnownGrantResult>();
    }

    [Serializable]
    public class ChainlingXpReward
    {
        public string instanceId;
        public string backendChainlingId;
        public string speciesId;
        public string displayName;
        public int xpEarned;
        public float shareMultiplier;
        public bool wasBenchOnly;
        public bool wasKOd;
        public bool isEntangled;
        public List<string> knownMoveIds = new List<string>();
        public int rebirthCount;
        public int previousMasteryXp;
        public int newMasteryXp;
        public int previousStars;
        public int newStars;
        public bool gainedStar;
        public List<int> starsCrossed = new List<int>();
        public List<int> masteryCyclesCompleted = new List<int>();
        public List<ProgressionGrantResult> progressionGrants = new List<ProgressionGrantResult>();
    }
}
