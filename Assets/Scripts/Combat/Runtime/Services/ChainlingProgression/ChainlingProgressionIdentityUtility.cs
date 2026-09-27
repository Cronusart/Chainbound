namespace Chainbound.Combat
{
    public static class ChainlingProgressionIdentityUtility
    {
        public static bool IsEntangled(ChainlingSnapshot snapshot)
        {
            return snapshot != null && snapshot.isEntangled;
        }

        /// <summary>
        /// Legacy helper: visual identity alone no longer encodes entangled.
        /// Prefer <see cref="IsEntangled(ChainlingSnapshot)"/> or the battle flag.
        /// </summary>
        public static bool IsEntangled(ChainlingVisualIdentity identity)
        {
            return false;
        }

        public static bool IsEntangled(BattleParticipant participant)
        {
            return participant != null && participant.isEntangled;
        }
    }
}
