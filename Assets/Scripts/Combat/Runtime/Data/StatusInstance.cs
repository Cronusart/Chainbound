using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// A live status effect on a Chainling during combat.
    /// Tracks remaining duration, counter progress, and source info.
    /// </summary>
    [Serializable]
    public class StatusInstance
    {
        public StatusId statusId;
        public StatusClass statusClass;
        public StatusOrigin origin;

        /// <summary>Remaining turns for Duration/Consumed statuses. -1 for Instant (already resolved).</summary>
        public int turnsRemaining;

        /// <summary>Current counter value for CounterStatus (e.g., Cascade Failure tracks hits).</summary>
        public int counterValue;

        /// <summary>Threshold for CounterStatus trigger.</summary>
        public int counterThreshold;

        /// <summary>For Consumed statuses: has it been consumed?</summary>
        public bool isConsumed;

        /// <summary>Parameterized type for TypeCoating / HypedTypeCoating / BacklashType.</summary>
        public ChainlingType parameterType;

        /// <summary>Source move instance ID (for logging/replay).</summary>
        public string sourceId;

        /// <summary>
        /// Exact temporary stage delta owned by this status. This is separate
        /// from chemistry counters and prevents cap-clamped effects from
        /// over-restoring when they expire.
        /// </summary>
        public bool hasTemporaryStageDelta;
        public CoreStat temporaryStageStat;
        public int temporaryStageDelta;

        /// <summary>
        /// For Recharging status: the move instance ID being recharged.
        /// Only relevant when statusId == StatusId.Recharging.
        /// </summary>
        public string rechargingMoveId;

        public StatusInstance() { }

        public StatusInstance(StatusId id, StatusClass cls, StatusOrigin origin, int duration, string sourceId = "")
        {
            this.statusId = id;
            this.statusClass = cls;
            this.origin = origin;
            this.turnsRemaining = duration;
            this.counterValue = 0;
            this.counterThreshold = 0;
            this.isConsumed = false;
            this.parameterType = ChainlingType.None;
            this.sourceId = sourceId;
            this.rechargingMoveId = "";
            this.hasTemporaryStageDelta = false;
            this.temporaryStageStat = CoreStat.MeleeAtk;
            this.temporaryStageDelta = 0;
        }

        /// <summary>Returns true if the status has expired (duration-based) or been consumed.</summary>
        public bool IsExpired =>
            (statusClass == StatusClass.Duration && turnsRemaining <= 0) ||
            (statusClass == StatusClass.Consumed && (isConsumed || turnsRemaining <= 0)) ||
            (statusClass == StatusClass.Instant); // Instants resolve immediately

        /// <summary>Decrement duration at End of Turn. Returns true if still active.</summary>
        public bool Tick()
        {
            if (statusClass == StatusClass.Duration || statusClass == StatusClass.Consumed)
            {
                turnsRemaining--;
            }
            return !IsExpired;
        }

        /// <summary>Consume the status (for Consumed class). Marks it as used.</summary>
        public void Consume()
        {
            if (statusClass == StatusClass.Consumed)
            {
                isConsumed = true;
            }
        }

        /// <summary>Increment counter. Returns true if threshold is reached.</summary>
        public bool IncrementCounter(int amount = 1)
        {
            if (statusClass != StatusClass.Counter) return false;
            counterValue += amount;
            return counterValue >= counterThreshold;
        }
    }
}
