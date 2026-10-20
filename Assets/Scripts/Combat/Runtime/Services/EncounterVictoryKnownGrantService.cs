using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public sealed class EncounterVictoryKnownGrantService
    {
        private readonly IEncounterVictoryKnownGrantPersistence _persistence;
        private readonly MoveDatabaseSO _moveDatabase;

        public EncounterVictoryKnownGrantService(
            IEncounterVictoryKnownGrantPersistence persistence,
            MoveDatabaseSO moveDatabase = null)
        {
            _persistence = persistence;
            _moveDatabase = moveDatabase;
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
                string moveId = grant.moveId.Trim();
                if (MoveAvailability.IsBossOnly(moveId, _moveDatabase))
                {
                    grants.Add(new EncounterVictoryKnownGrantResult
                    {
                        moveId = moveId,
                        success = false,
                        error = "boss_only_move_not_grantable",
                    });
                    continue;
                }

                var request = new EncounterVictoryKnownGrantRequest
                {
                    playerProfileId = playerProfileId,
                    narrativeEncounterId = encounterId,
                    moveId = moveId,
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
