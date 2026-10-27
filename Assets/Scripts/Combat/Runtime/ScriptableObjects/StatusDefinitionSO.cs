using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// ScriptableObject defining a single status effect.
    /// Matches COMBAT_02A_StatusFrameworkAndCatalog_v1.2 Section 6.
    /// </summary>
    [CreateAssetMenu(fileName = "StatusDef_", menuName = "Chainbound/Combat/Statuses/Status Definition")]
    public class StatusDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public StatusId statusId;
        public string displayName;

        [TextArea(2, 4)]
        public string effectSummary;

        [Header("Classification")]
        public StatusClass statusClass;
        public StatusOrigin origin;

        [Header("Timing")]
        [Tooltip("Default duration in turns (for Duration/Consumed). 0 for Instant, -1 for Counter.")]
        public int defaultDuration;

        [Tooltip("For CounterStatus: threshold before triggering effect.")]
        public int counterThreshold;

        [Header("Stage Effects")]
        [Tooltip("Stage changes applied when this status is active or triggers.")]
        public StatusStageEffect[] stageEffects;

        [Header("Flags")]
        [Tooltip("Does this status need a parameterized ChainlingType? (e.g., TypeCoating, BacklashType)")]
        public bool isParameterized;

        [Tooltip("Is this a debuff that can be cleansed?")]
        public bool isDebuff;

        [Tooltip("Is this a buff?")]
        public bool isBuff;

        [Tooltip("Does this status prevent the Chainling from acting? (e.g., Offline, Rekt)")]
        public bool preventsAction;

        [Tooltip("Does this status have End-of-Turn damage? (e.g., Rugged, FOMO DoT)")]
        public bool hasEndOfTurnDamage;

        [Tooltip("End-of-Turn damage as fraction of MaxHP (e.g., 0.1 = 1/10 MaxHP).")]
        public float endOfTurnDamagePercent;

        [Tooltip("Does this status affect AP at End of Turn?")]
        public bool hasEndOfTurnAP;

        [Tooltip("AP change at End of Turn (+1 for Pumped/Yield, -1 for Drained).")]
        public int endOfTurnAPChange;
    }

    /// <summary>
    /// A stage change applied by a status effect.
    /// </summary>
    [System.Serializable]
    public struct StatusStageEffect
    {
        public CoreStat stat;
        public int stages;

        [Tooltip("Applied once on status application (true) or per turn end (false).")]
        public bool applyOnce;

        [Tooltip("Accuracy or Evasion. CoreStat cannot store those stages.")]
        public bool usesCombatStage;
        public CombatStageStat combatStage;
    }
}
