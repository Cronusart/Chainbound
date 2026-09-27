using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public sealed class EncounterVictoryKnownGrantService
    {
        private readonly IEncounterVictoryKnownGrantPersistence _persistence;

        public EncounterVictoryKnownGrantService(IEncounterVictoryKnownGrantPersistence persistence)
        {
            _persistence = persistence;
        }

        public async Task<List<EncounterVictoryKnownGrantResult>> ApplyGrantsAsync(
            BattleResult result,
            string playerProfileId)
        {
            var grants = new List<EncounterVictoryKnownGrantResult>();
            if (result == null ||
                result.battleType != BattleType.PvE ||
                !result.PlayerWon ||
                result.wasFled ||
                result.pendingVictoryKnownGrants == null ||
                result.pendingVictoryKnownGrants.Count == 0)
            {
                return grants;
            }

            if (_persistence == null || !_persistence.IsAvailable)
            {
                grants.Add(new EncounterVictoryKnownGrantResult
                {
                    success = false,
                    error = "victory_known_grant_persistence_unavailable",
                });
                return grants;
            }

            string encounterId = result.narrativeEncounterId ?? "";
            for (int i = 0; i < result.pendingVictoryKnownGrants.Count; i++)
            {
                EncounterVictoryKnownGrant grant = result.pendingVictoryKnownGrants[i];
                if (string.IsNullOrWhiteSpace(grant.moveId))
                    continue;

                var request = new EncounterVictoryKnownGrantRequest
                {
                    playerProfileId = playerProfileId,
                    narrativeEncounterId = encounterId,
                    moveId = grant.moveId.Trim(),
                    idempotencyKey = grant.ResolveIdempotencyKey(encounterId),
                    battleSeed = result.seed,
                };

                EncounterVictoryKnownGrantResult persisted = await _persistence.GrantAsync(request);
                grants.Add(persisted);
            }

            return grants;
        }
    }
}
