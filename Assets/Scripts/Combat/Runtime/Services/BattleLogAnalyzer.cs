using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public static class BattleLogAnalyzer
    {
        public static int CountEnemyKOs(IReadOnlyList<CombatEvent> events, TeamState enemyTeam)
        {
            int total = 0;
            var bySpecies = CountEnemyKOsBySpecies(events, enemyTeam);
            foreach (var pair in bySpecies)
                total += pair.Value;
            return total;
        }

        public static Dictionary<string, int> CountEnemyKOsBySpecies(
            IReadOnlyList<CombatEvent> events,
            TeamState enemyTeam)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (events == null || enemyTeam?.chainlings == null)
                return counts;

            var speciesByInstanceId = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < enemyTeam.chainlings.Count; i++)
            {
                var chainling = enemyTeam.chainlings[i];
                if (chainling == null || string.IsNullOrEmpty(chainling.instanceId))
                    continue;

                speciesByInstanceId[chainling.instanceId] = string.IsNullOrEmpty(chainling.speciesId)
                    ? "unknown"
                    : chainling.speciesId;
            }

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt.eventType != CombatEventType.ChainlingKO || string.IsNullOrEmpty(evt.targetId))
                    continue;

                if (!speciesByInstanceId.TryGetValue(evt.targetId, out string speciesId))
                    continue;

                counts.TryGetValue(speciesId, out int current);
                counts[speciesId] = current + 1;
            }

            return counts;
        }

        public static HashSet<string> GetKOInstanceIds(IReadOnlyList<CombatEvent> events, TeamState team)
        {
            var knockedOut = new HashSet<string>(StringComparer.Ordinal);
            if (events == null || team?.chainlings == null)
                return knockedOut;

            var teamInstanceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var chainling = team.chainlings[i];
                if (chainling != null && !string.IsNullOrEmpty(chainling.instanceId))
                    teamInstanceIds.Add(chainling.instanceId);
            }

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt.eventType != CombatEventType.ChainlingKO || string.IsNullOrEmpty(evt.targetId))
                    continue;

                if (teamInstanceIds.Contains(evt.targetId))
                    knockedOut.Add(evt.targetId);
            }

            return knockedOut;
        }
    }
}
