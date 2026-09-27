using System;

namespace Chainbound.Combat
{
    [Serializable]
    public class ChainlingBattleStatContribution
    {
        public string chainlingBackendId;
        public string instanceId;
        public string displayName;
        public ChainlingPveStatDelta delta = new ChainlingPveStatDelta();

        /// <summary>
        /// Transition-only bridge for existing ChainlingBattleStats.timesKod.
        /// This is not part of the PvE service-record metric catalog.
        /// </summary>
        public int legacyTimesKod;

        public ChainlingBattleStatContribution Clone()
        {
            return new ChainlingBattleStatContribution
            {
                chainlingBackendId = chainlingBackendId,
                instanceId = instanceId,
                displayName = displayName,
                delta = delta != null ? delta.Clone() : new ChainlingPveStatDelta(),
                legacyTimesKod = Math.Max(0, legacyTimesKod),
            };
        }
    }
}
