using System;
using System.Collections.Generic;
using Chainbound.Combat.Core;

namespace Chainbound.Combat
{
    /// <summary>
    /// Rebuilds the encounter roster from the durable battle-service snapshot.
    /// This prevents a resumed scene from rolling a different wild encounter.
    /// </summary>
    public sealed class AuthoritativeBattleEncounterProvider : IEncounterProvider
    {
        private readonly AuthoritativeClientBattleState state;

        public AuthoritativeBattleEncounterProvider(AuthoritativeClientBattleState state)
        {
            this.state = state;
        }

        public EncounterSnapshot GenerateEncounter(int seed)
        {
            var encounter = new EncounterSnapshot
            {
                encounterSeed = state != null && state.seed != 0 ? unchecked((int)state.seed) : seed,
                teamName = ResolveTeamName(state),
                aiPersonality = AIPersonality.Aggressive,
                zoneId = state != null ? state.zoneId ?? string.Empty : string.Empty,
                captureEnabled = state != null && state.encounterCaptureEnabled,
                capturePolicy = state != null
                    ? (EncounterCapturePolicy)state.capturePolicy
                    : EncounterCapturePolicy.UseEncounterDefault,
                authoritativeBattleKind = state != null
                    ? (AuthoritativeBattleKind)state.battleKind
                    : AuthoritativeBattleKind.Wild,
                difficulty = ResolveDifficulty(state),
            };

            if (state?.targets == null)
                return encounter;

            for (int i = 0; i < state.targets.Count; i++)
            {
                AuthoritativeClientBattleTarget target = state.targets[i];
                if (target == null || target.isRemoved || string.IsNullOrWhiteSpace(target.speciesId))
                    continue;

                var moves = new List<string>();
                if (!string.IsNullOrWhiteSpace(target.knownMoveId))
                    moves.Add(target.knownMoveId.Trim());

                encounter.chainlings.Add(new ChainlingSnapshot
                {
                    backendChainlingId = target.targetId ?? string.Empty,
                    speciesId = target.speciesId,
                    displayName = target.displayName,
                    stars = Math.Max(0, target.stars),
                    level = Math.Max(1, target.level),
                    evolutionStage = 1,
                    moveIds = moves,
                    knownMoveId = target.knownMoveId ?? string.Empty,
                    ivs = NormalizeIvs(target.rawIvs),
                    currentHp = Math.Max(0, target.currentHp),
                    isMutated = target.mutated,
                    mutationId = string.IsNullOrWhiteSpace(target.mutationId) ? "none" : target.mutationId,
                    isEntangled = target.isEntangled,
                    entangledType = ParseType(target.entangledTypeId),
                    visualIdentity = CreateVisualIdentity(target),
                });
            }
            return encounter;
        }

        private static string ResolveTeamName(AuthoritativeClientBattleState source)
        {
            return source != null && (AuthoritativeBattleKind)source.battleKind == AuthoritativeBattleKind.Boss
                ? "Boss"
                : "Wild";
        }

        private static EncounterDifficulty ResolveDifficulty(AuthoritativeClientBattleState source)
        {
            if (source != null && source.targets != null && source.targets.Count > 0 &&
                Enum.TryParse(source.targets[0]?.difficultyId, true, out EncounterDifficulty parsed))
            {
                return parsed;
            }
            return source != null && (AuthoritativeBattleKind)source.battleKind == AuthoritativeBattleKind.Boss
                ? EncounterDifficulty.Boss
                : EncounterDifficulty.Standard;
        }

        private static ChainlingType ParseType(string value)
        {
            return Enum.TryParse(value, true, out ChainlingType parsed) ? parsed : ChainlingType.None;
        }

        private static ChainlingVisualIdentity CreateVisualIdentity(AuthoritativeClientBattleTarget target)
        {
            ChainlingVisualIdentity identity = ChainlingVisualIdentity.CreateDefault();
            identity.visualVariantId = string.IsNullOrWhiteSpace(target.visualVariantId)
                ? ChainlingVisualIdentity.BaseVariantId
                : target.visualVariantId;
            identity.auraId = target.mutated && !string.IsNullOrWhiteSpace(target.mutationId)
                ? target.mutationId
                : ChainlingVisualIdentity.NoneId;
            identity.Normalize(target.isEntangled);
            return identity;
        }

        private static int[] NormalizeIvs(int[] source)
        {
            var result = new int[6];
            if (source == null)
                return result;
            for (int i = 0; i < Math.Min(source.Length, result.Length); i++)
                result[i] = source[i];
            return result;
        }
    }
}
