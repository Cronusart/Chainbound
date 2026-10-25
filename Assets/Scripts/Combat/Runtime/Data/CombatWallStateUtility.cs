using System;

namespace Chainbound.Combat
{
    public static class CombatWallStateUtility
    {
        public static bool HasActiveWallState(ChainlingInstance chainling)
        {
            if (chainling == null)
                return false;

            if (CountActiveBarriers(chainling) > 0)
                return true;

            if (chainling.mitigationSources == null)
                return false;

            for (int i = 0; i < chainling.mitigationSources.Count; i++)
            {
                MitigationSource source = chainling.mitigationSources[i];
                if (!IsActiveMitigation(source))
                    continue;
                if (source.blockKind != BlockKind.None || IsWallTag(source.sourceTag) || IsWallTag(source.stackKey))
                    return true;
            }

            return false;
        }

        public static int CountActiveBarriers(ChainlingInstance chainling)
        {
            if (chainling == null || chainling.barriers == null)
                return 0;

            int count = 0;
            for (int i = 0; i < chainling.barriers.Count; i++)
            {
                if (IsActiveBarrier(chainling.barriers[i]))
                    count++;
            }

            return count;
        }

        public static string ResolveLatestWallSourceTag(ChainlingInstance chainling)
        {
            if (chainling == null)
                return string.Empty;

            if (chainling.mitigationSources != null)
            {
                for (int i = chainling.mitigationSources.Count - 1; i >= 0; i--)
                {
                    MitigationSource source = chainling.mitigationSources[i];
                    if (!IsActiveMitigation(source))
                        continue;
                    if (source.blockKind == BlockKind.None && !IsWallTag(source.sourceTag) && !IsWallTag(source.stackKey))
                        continue;

                    if (!string.IsNullOrWhiteSpace(source.sourceTag))
                        return source.sourceTag.Trim();
                    if (!string.IsNullOrWhiteSpace(source.stackKey))
                        return source.stackKey.Trim();
                }
            }

            if (chainling.barriers != null)
            {
                for (int i = chainling.barriers.Count - 1; i >= 0; i--)
                {
                    BarrierInstance barrier = chainling.barriers[i];
                    if (!IsActiveBarrier(barrier))
                        continue;
                    if (!string.IsNullOrWhiteSpace(barrier.sourceTag))
                        return barrier.sourceTag.Trim();
                }
            }

            return string.Empty;
        }

        public static bool TryResolveLatestBlockProfile(
            ChainlingInstance chainling,
            out BlockKind kind,
            out BlockTier tier,
            out string sourceTag)
        {
            kind = BlockKind.None;
            tier = BlockTier.None;
            sourceTag = string.Empty;
            if (chainling == null)
                return false;

            for (int i = chainling.mitigationSources != null ? chainling.mitigationSources.Count - 1 : -1; i >= 0; i--)
            {
                MitigationSource source = chainling.mitigationSources[i];
                if (!IsActiveMitigation(source) || source.blockKind == BlockKind.None)
                    continue;

                kind = source.blockKind;
                tier = source.blockTier != BlockTier.None
                    ? source.blockTier
                    : ResolveBlockTier(source.mitigation);
                sourceTag = !string.IsNullOrWhiteSpace(source.sourceTag)
                    ? source.sourceTag.Trim()
                    : source.stackKey?.Trim() ?? string.Empty;
                return true;
            }


            if (chainling.barriers != null)
            {
                for (int i = chainling.barriers.Count - 1; i >= 0; i--)
                {
                    BarrierInstance barrier = chainling.barriers[i];
                    if (!IsActiveBarrierPublic(barrier))
                        continue;
                    kind = BlockKind.Barrier;
                    tier = BlockTier.Lower;
                    sourceTag = barrier.sourceTag?.Trim() ?? string.Empty;
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveMoveBlockProfile(MoveInstance move, out BlockKind kind, out BlockTier tier)
        {
            kind = BlockKind.None;
            tier = BlockTier.None;
            if (move?.utilityEffects == null)
                return false;

            for (int i = 0; i < move.utilityEffects.Count; i++)
            {
                UtilityEffectPayload effect = move.utilityEffects[i];
                if (effect == null)
                    continue;

                if (effect.effectType == UtilityEffectType.FullAbsorbBarrier ||
                    effect.effectType == UtilityEffectType.DamageThresholdBarrier)
                {
                    kind = BlockKind.Barrier;
                    tier = effect.magnitude > 0f ? ResolveBlockTier(effect.magnitude) : BlockTier.Lower;
                    return true;
                }

                if (effect.effectType != UtilityEffectType.BlockWindow)
                    continue;

                kind = effect.contactFilter == UtilityContactFilter.Contact
                    ? BlockKind.Wall
                    : effect.contactFilter == UtilityContactFilter.Projectile
                        ? BlockKind.Barrier
                        : BlockKind.None;
                if (kind == BlockKind.None)
                    return false;

                tier = ResolveBlockTier(effect.magnitude);
                return true;
            }

            return false;
        }

        public static BlockTier ResolveBlockTier(float mitigation)
        {
            float percent = mitigation <= 1f ? mitigation * 100f : mitigation;
            if (percent <= 30f)
                return BlockTier.Lower;
            if (percent <= 50f)
                return BlockTier.Mid;
            return BlockTier.Greater;
        }

        public static bool MoveDefinitionPlacesWall(MoveDefinitionSO definition)
        {
            if (definition == null)
                return false;

            if (definition.utilityEffects != null)
            {
                for (int i = 0; i < definition.utilityEffects.Count; i++)
                {
                    UtilityEffectPayload effect = definition.utilityEffects[i];
                    if (effect == null)
                        continue;
                    if (effect.effectType == UtilityEffectType.BlockWindow ||
                        effect.effectType == UtilityEffectType.FullAbsorbBarrier ||
                        effect.effectType == UtilityEffectType.DamageThresholdBarrier)
                        return true;
                }
            }

            if (definition.tags != null)
            {
                for (int i = 0; i < definition.tags.Count; i++)
                {
                    string tag = definition.tags[i];
                    if (string.Equals(tag, "WALL", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "FULL_ABSORB_WALL", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public static bool IsWallTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string normalized = value.Trim();
            return normalized.StartsWith("wall_", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("barrier_", StringComparison.OrdinalIgnoreCase);
        }

        public static bool MovePlacesBlock(MoveInstance move)
        {
            if (move?.utilityEffects == null)
                return false;
            for (int i = 0; i < move.utilityEffects.Count; i++)
            {
                UtilityEffectPayload effect = move.utilityEffects[i];
                if (effect != null && effect.effectType == UtilityEffectType.BlockWindow)
                    return true;
            }
            return false;
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

        private static bool IsActiveBarrier(BarrierInstance barrier)
        {
            return IsActiveBarrierPublic(barrier);
        }

        public static bool IsActiveBarrierPublic(BarrierInstance barrier)
        {
            if (barrier == null)
                return false;
            if (barrier.turnsRemaining == 0)
                return false;
            if (barrier.barrierType == BarrierType.SingleHit)
                return barrier.hitsRemaining != 0;
            if (barrier.barrierType == BarrierType.DamageThreshold)
                return barrier.damageThreshold <= 0 || barrier.damageAbsorbed < barrier.damageThreshold;
            return true;
        }
    }
}
