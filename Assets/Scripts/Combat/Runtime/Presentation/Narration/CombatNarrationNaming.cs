using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Builds combat-narration display names for Chainlings.
    /// Local player team uses bare names; opponents keep Wild/Entangled (or Enemy in PvP).
    /// </summary>
    public static class CombatNarrationNaming
    {
        public const string EnemyPrefix = "Enemy";
        public const string EntangledPrefix = "Entangled";

        public static string FormatChainlingName(TeamState team, ChainlingInstance chainling)
        {
            string displayName = chainling != null && !string.IsNullOrWhiteSpace(chainling.displayName)
                ? chainling.displayName.Trim()
                : "Chainling";

            string prefix = ResolvePrefix(team, chainling);
            return string.IsNullOrEmpty(prefix) ? displayName : $"{prefix} {displayName}";
        }

        public static string ResolvePrefix(TeamState team, ChainlingInstance chainling = null)
        {
            if (team == null || team.teamIndex == 0)
                return null;

            if (chainling != null && chainling.isEntangled)
                return EntangledPrefix;

            return ResolveOpponentTeamPrefix(team.teamName);
        }

        /// <summary>
        /// Resolves a team subject for forensic/text-feed lines (e.g. "Wild swapped in …").
        /// Null means omit the team subject (local player).
        /// </summary>
        public static string ResolveTeamSubject(string teamName, int teamIndex)
        {
            if (teamIndex == 0)
                return null;

            return ResolveOpponentTeamPrefix(teamName);
        }

        private static string ResolveOpponentTeamPrefix(string teamName)
        {
            string trimmed = teamName != null ? teamName.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmed) ||
                string.Equals(trimmed, "Player", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Opponent", StringComparison.OrdinalIgnoreCase))
            {
                return EnemyPrefix;
            }

            return trimmed;
        }
    }
}
