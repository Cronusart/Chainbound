using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Parses wall-related utility metadata and resolves on-hit retaliation from active wall state.
    /// </summary>
    public static class CombatWallEffectUtility
    {
        public static int ResolveWallHitApDrainFromMove(MoveInstance move)
        {
            if (move?.utilityEffects == null)
                return 0;

            for (int i = 0; i < move.utilityEffects.Count; i++)
            {
                UtilityEffectPayload effect = move.utilityEffects[i];
                if (effect == null || effect.effectType != UtilityEffectType.WallHitRetaliation)
                    continue;

                if (effect.apGain > 0)
                    return effect.apGain;
                if (effect.magnitude > 0f)
                    return Math.Max(0, (int)Math.Round(effect.magnitude));
            }

            return 0;
        }

        public static int ResolveWallHitApDrainFromDefinition(MoveDefinitionSO definition)
        {
            if (definition?.utilityEffects == null)
                return 0;

            for (int i = 0; i < definition.utilityEffects.Count; i++)
            {
                UtilityEffectPayload effect = definition.utilityEffects[i];
                if (effect == null || effect.effectType != UtilityEffectType.WallHitRetaliation)
                    continue;

                if (effect.apGain > 0)
                    return effect.apGain;
                if (effect.magnitude > 0f)
                    return Math.Max(0, (int)Math.Round(effect.magnitude));
            }

            return 0;
        }

        public static int ResolveMitigationWallApDrainOnHit(ChainlingInstance chainling, string preferredSourceTag)
        {
            if (chainling?.mitigationSources == null)
                return 0;

            for (int i = chainling.mitigationSources.Count - 1; i >= 0; i--)
            {
                MitigationSource source = chainling.mitigationSources[i];
                if (!IsActiveMitigation(source))
                    continue;
                if (!IsWallSource(source.sourceTag, source.stackKey))
                    continue;
                if (!MatchesPreferredSource(preferredSourceTag, source.sourceTag, source.stackKey))
                    continue;
                if (source.attackerApDrainOnHit <= 0)
                    continue;

                return source.attackerApDrainOnHit;
            }

            return 0;
        }

        public static float ResolveWallReflectPercent(ChainlingInstance chainling, string preferredSourceTag)
        {
            if (chainling?.mitigationSources != null)
            {
                for (int i = chainling.mitigationSources.Count - 1; i >= 0; i--)
                {
                    MitigationSource source = chainling.mitigationSources[i];
                    if (!IsActiveMitigation(source) || source.blockKind == BlockKind.None)
                        continue;
                    if (!MatchesPreferredSource(preferredSourceTag, source.sourceTag, source.stackKey))
                        continue;
                    if (source.reflectPercent > 0f)
                        return source.reflectPercent;
                }
            }

            if (chainling?.barriers == null)
                return 0f;

            for (int i = chainling.barriers.Count - 1; i >= 0; i--)
            {
                BarrierInstance barrier = chainling.barriers[i];
                if (!CombatWallStateUtility.IsActiveBarrierPublic(barrier))
                    continue;
                if (!MatchesPreferredSource(preferredSourceTag, barrier.sourceTag, string.Empty))
                    continue;
                if (barrier.reflectPercent <= 0f)
                    continue;

                return barrier.reflectPercent;
            }

            return 0f;
        }

        public static float ResolveBlockReflectPercentFromMove(MoveInstance move)
        {
            if (move?.utilityEffects == null)
                return 0f;
            for (int i = 0; i < move.utilityEffects.Count; i++)
            {
                UtilityEffectPayload effect = move.utilityEffects[i];
                if (effect?.effectType == UtilityEffectType.BlockReflect)
                    return Math.Max(0f, Math.Min(1f, effect.reflectPercent));
            }
            return 0f;
        }

        public static int ResolveBlockApGainIfFasterFromMove(MoveInstance move)
        {
            if (move?.utilityEffects == null)
                return 0;
            for (int i = 0; i < move.utilityEffects.Count; i++)
            {
                UtilityEffectPayload effect = move.utilityEffects[i];
                if (effect?.effectType == UtilityEffectType.GainApOnDamageIfSpeedHigher)
                    return Math.Max(0, effect.apGain > 0 ? effect.apGain : 1);
            }
            return 0;
        }

        private static bool IsActiveMitigation(MitigationSource source)
        {
            if (source == null)
                return false;
            if (source.turnsRemaining <= 0)
                return false;
            if (source.hitsRemaining == 0)
                return false;
            return source.mitigation > 0f;
        }

        private static bool IsWallSource(string sourceTag, string stackKey)
        {
            return CombatWallStateUtility.IsWallTag(sourceTag) ||
                   CombatWallStateUtility.IsWallTag(stackKey);
        }

        private static bool MatchesPreferredSource(string preferredSourceTag, string sourceTag, string stackKey)
        {
            if (string.IsNullOrWhiteSpace(preferredSourceTag))
                return true;

            string preferred = preferredSourceTag.Trim();
            if (string.Equals(preferred, sourceTag, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(preferred, stackKey, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
