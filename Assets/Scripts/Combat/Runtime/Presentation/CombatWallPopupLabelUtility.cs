using System;

namespace Chainbound.Combat
{
    public readonly struct CombatWallRaiseLabels
    {
        public readonly string mitigationLabel;
        public readonly string reflectLabel;
        public readonly string debuffLabel;

        public CombatWallRaiseLabels(string mitigationLabel, string reflectLabel, string debuffLabel)
        {
            this.mitigationLabel = mitigationLabel ?? string.Empty;
            this.reflectLabel = reflectLabel ?? string.Empty;
            this.debuffLabel = debuffLabel ?? string.Empty;
        }

        public bool HasMitigationLabel => !string.IsNullOrWhiteSpace(mitigationLabel);
        public bool HasReflectLabel => !string.IsNullOrWhiteSpace(reflectLabel);
        public bool HasDebuffLabel => !string.IsNullOrWhiteSpace(debuffLabel);
        public bool HasFollowUpLabels => HasReflectLabel || HasDebuffLabel;
    }

    /// <summary>
    /// Derives wall raise and on-hit popup labels from move utilities and active wall state.
    /// </summary>
    public static class CombatWallPopupLabelUtility
    {
        public static CombatWallRaiseLabels ResolveRaiseLabels(
            ChainlingInstance chainling,
            MoveDefinitionSO moveDefinition)
        {
            string mitigationLabel = string.Empty;
            string reflectLabel = string.Empty;
            string debuffLabel = string.Empty;

            if (moveDefinition?.utilityEffects != null)
            {
                for (int i = 0; i < moveDefinition.utilityEffects.Count; i++)
                {
                    UtilityEffectPayload effect = moveDefinition.utilityEffects[i];
                    if (effect == null)
                        continue;

                    switch (effect.effectType)
                    {
                        case UtilityEffectType.BlockWindow:
                        case UtilityEffectType.MitigationWindow:
                            mitigationLabel = FormatPercentMitigation(effect.magnitude);
                            break;

                        case UtilityEffectType.DamageThresholdBarrier:
                            mitigationLabel = FormatFlatMitigation(Math.Max(0, (int)Math.Round(effect.magnitude)));
                            if (effect.reflectPercent > 0f)
                                reflectLabel = FormatReflectPercent(effect.reflectPercent);
                            break;

                        case UtilityEffectType.FullAbsorbBarrier:
                            mitigationLabel = FormatBlockHits(effect.hitBudget);
                            break;
                    }
                }

                int apDrain = CombatWallEffectUtility.ResolveWallHitApDrainFromDefinition(moveDefinition);
                if (apDrain > 0)
                    debuffLabel = FormatApDebuff(apDrain);
            }

            if (string.IsNullOrWhiteSpace(mitigationLabel))
                mitigationLabel = ResolveMitigationLabelFromActiveState(chainling);
            if (string.IsNullOrWhiteSpace(reflectLabel))
                reflectLabel = ResolveReflectLabelFromActiveState(chainling);
            if (string.IsNullOrWhiteSpace(debuffLabel))
                debuffLabel = ResolveDebuffLabelFromActiveState(chainling);

            return new CombatWallRaiseLabels(mitigationLabel, reflectLabel, debuffLabel);
        }

        public static string FormatPercentMitigation(float magnitude)
        {
            float normalized = magnitude > 1f ? magnitude / 100f : magnitude;
            int percent = Math.Max(0, (int)Math.Round(normalized * 100f));
            if (percent <= 0)
                return string.Empty;
            return $"-{percent}% DMG";
        }

        public static string FormatFlatMitigation(int pool)
        {
            if (pool <= 0)
                return string.Empty;
            return $"-{pool} DMG";
        }

        public static string FormatBlockHits(int hitBudget)
        {
            if (hitBudget < 0)
                return "BLOCK · ∞";
            if (hitBudget == 1)
                return "BLOCK";
            if (hitBudget > 1)
                return $"BLOCK · {hitBudget}H";
            return "BLOCK";
        }

        public static string FormatReflectPercent(float reflectPercent)
        {
            float normalized = reflectPercent > 1f ? reflectPercent / 100f : reflectPercent;
            int percent = Math.Max(0, (int)Math.Round(normalized * 100f));
            if (percent <= 0)
                return string.Empty;
            return $"REFLECT {percent}%";
        }

        public static string FormatApDebuff(int apDrain)
        {
            if (apDrain <= 0)
                return string.Empty;
            return apDrain == 1 ? "-1 AP" : $"-{apDrain} AP";
        }

        public static string FormatReflectHitLabel(int reflectedDamage, float reflectPercent)
        {
            if (reflectedDamage > 0)
                return reflectedDamage.ToString();
            return FormatReflectPercent(reflectPercent);
        }

        public static string FormatDefenseCardBadge(MoveInstance move)
        {
            if (move == null)
                return string.Empty;

            if (TryResolveBlockEffect(move, out UtilityEffectPayload effect))
            {
                float mitigation = effect.magnitude > 1f ? effect.magnitude / 100f : effect.magnitude;
                BlockKind kind = effect.contactFilter == UtilityContactFilter.Contact
                    ? BlockKind.Wall
                    : BlockKind.Barrier;
                BlockTier tier = ResolveTier(mitigation);
                string priority = MoveSelectDisplayUtility.HasPriorityTag(move) ? " · PRIOR" : string.Empty;
                return $"{kind.ToString().ToUpperInvariant()} · {tier.ToString().ToUpperInvariant()}{priority}";
            }

            if (HasTag(move, "ARMOR"))
                return "ARMOR";
            return string.Empty;
        }

        public static string FormatHolderChip(ChainlingInstance chainling)
        {
            if (chainling?.mitigationSources == null)
                return string.Empty;

            for (int i = chainling.mitigationSources.Count - 1; i >= 0; i--)
            {
                MitigationSource source = chainling.mitigationSources[i];
                if (source == null || source.blockKind == BlockKind.None ||
                    source.turnsRemaining <= 0 || source.hitsRemaining == 0)
                {
                    continue;
                }

                int percent = Math.Max(0, (int)Math.Round(source.mitigation * 100f));
                string category = source.blockKind == BlockKind.Wall ? "MELEE" : "RANGED";
                int hits = Math.Max(1, source.hitsRemaining);
                int turns = Math.Max(1, source.turnsRemaining);
                return $"{source.blockKind.ToString().ToUpperInvariant()} {percent}% · {category} · " +
                       $"{hits} hit{(hits == 1 ? string.Empty : "s")} · {turns} turn{(turns == 1 ? string.Empty : "s")}";
            }

            return string.Empty;
        }

        private static bool TryResolveBlockEffect(MoveInstance move, out UtilityEffectPayload block)
        {
            block = null;
            if (move?.utilityEffects == null)
                return false;
            for (int i = 0; i < move.utilityEffects.Count; i++)
            {
                UtilityEffectPayload effect = move.utilityEffects[i];
                if (effect != null && effect.effectType == UtilityEffectType.BlockWindow)
                {
                    block = effect;
                    return true;
                }
            }
            return false;
        }

        private static BlockTier ResolveTier(float mitigation)
        {
            float percent = mitigation <= 1f ? mitigation * 100f : mitigation;
            if (percent <= 30f)
                return BlockTier.Lower;
            return percent <= 50f ? BlockTier.Mid : BlockTier.Greater;
        }

        private static bool HasTag(MoveInstance move, string expected)
        {
            if (move?.tags == null)
                return false;
            for (int i = 0; i < move.tags.Count; i++)
            {
                if (string.Equals(move.tags[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string ResolveMitigationLabelFromActiveState(ChainlingInstance chainling)
        {
            if (chainling?.barriers != null)
            {
                for (int i = chainling.barriers.Count - 1; i >= 0; i--)
                {
                    BarrierInstance barrier = chainling.barriers[i];
                    if (!CombatWallStateUtility.IsActiveBarrierPublic(barrier))
                        continue;

                    if (barrier.barrierType == BarrierType.DamageThreshold && barrier.damageThreshold > 0)
                        return FormatFlatMitigation(barrier.damageThreshold);

                    if (barrier.barrierType == BarrierType.SingleHit)
                        return FormatBlockHits(barrier.hitsRemaining);
                }
            }

            if (chainling?.mitigationSources != null)
            {
                for (int i = chainling.mitigationSources.Count - 1; i >= 0; i--)
                {
                    MitigationSource source = chainling.mitigationSources[i];
                    if (source == null || source.turnsRemaining <= 0 || source.hitsRemaining == 0)
                        continue;
                    if (source.mitigation <= 0f)
                        continue;
                    if (source.blockKind == BlockKind.None &&
                        !CombatWallStateUtility.IsWallTag(source.sourceTag) &&
                        !CombatWallStateUtility.IsWallTag(source.stackKey))
                    {
                        continue;
                    }

                    return FormatPercentMitigation(source.mitigation);
                }
            }

            return string.Empty;
        }

        private static string ResolveReflectLabelFromActiveState(ChainlingInstance chainling)
        {
            if (chainling == null)
                return string.Empty;

            string sourceTag = CombatWallStateUtility.ResolveLatestWallSourceTag(chainling);
            float reflectPercent = CombatWallEffectUtility.ResolveWallReflectPercent(chainling, sourceTag);
            return FormatReflectPercent(reflectPercent);
        }

        private static string ResolveDebuffLabelFromActiveState(ChainlingInstance chainling)
        {
            if (chainling == null)
                return string.Empty;

            string sourceTag = CombatWallStateUtility.ResolveLatestWallSourceTag(chainling);

            if (chainling.barriers != null)
            {
                for (int i = chainling.barriers.Count - 1; i >= 0; i--)
                {
                    BarrierInstance barrier = chainling.barriers[i];
                    if (!CombatWallStateUtility.IsActiveBarrierPublic(barrier))
                        continue;
                    if (barrier.attackerApDrainOnHit > 0)
                        return FormatApDebuff(barrier.attackerApDrainOnHit);
                }
            }

            int apDrain = CombatWallEffectUtility.ResolveMitigationWallApDrainOnHit(chainling, sourceTag);
            return FormatApDebuff(apDrain);
        }
    }
}
