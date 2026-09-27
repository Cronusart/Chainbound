using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Resolves swap modifier effects (OnSwapIn, OnSwapOut) using modifier trigger metadata.
    /// </summary>
    public static class SwapModifierResolver
    {
        private static readonly CoreStat[] StageStats =
        {
            CoreStat.MeleeAtk,
            CoreStat.MeleeDef,
            CoreStat.RangedAtk,
            CoreStat.RangedDef,
            CoreStat.Speed
        };

        private const string S64A = "6.4-a";
        private const string S64B = "6.4-b";
        private const string S64C = "6.4-c";
        private const string S64D = "6.4-d";
        private const string S64E = "6.4-e";
        private const string S64F = "6.4-f";
        private const string S64G = "6.4-g";
        private const string S64H = "6.4-h";
        private const string S64I = "6.4-i";
        private const int MITIGATION_DURATION_TURNS = 1;
        private const int ACCURACY_DURATION_TURNS = 1;

        private struct SwapModifierRef
        {
            public string sectionId;
            public ModifierDefinitionSO definition;
            public ModifierInstance runtime;
            public MoveInstance sourceMove;
        }

        /// <summary>
        /// Process OnSwapOut modifiers on the outgoing chainling, then OnSwapIn modifiers on the incoming.
        /// </summary>
        public static void ProcessSwapModifiers(
            CombatState state,
            TeamState team,
            ChainlingInstance outgoing,
            ChainlingInstance incoming,
            int turn,
            CombatEventLog log,
            CombatRng rng,
            ModifierCatalogSO modifierCatalog = null,
            CombatApService apService = null)
        {
            if (team == null || outgoing == null || incoming == null || log == null || rng == null)
                return;

            ChainlingInstance ally = FindAlly(team, incoming);

            ProcessModifiersForChainling(
                outgoing,
                outgoing,
                ally,
                ModifierTriggerCondition.OnSwapOut,
                turn,
                log,
                rng,
                modifierCatalog,
                apService);

            ProcessModifiersForChainling(
                incoming,
                incoming,
                ally,
                ModifierTriggerCondition.OnSwapIn,
                turn,
                log,
                rng,
                modifierCatalog,
                apService);
        }

        private static ChainlingInstance FindAlly(TeamState team, ChainlingInstance exclude)
        {
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                var c = team.GetActiveInSlot(s);
                if (c != null && c.isAlive && c != exclude)
                    return c;
            }
            return null;
        }

        private static void ProcessModifiersForChainling(
            ChainlingInstance source,
            ChainlingInstance selfTarget,
            ChainlingInstance allyTarget,
            ModifierTriggerCondition trigger,
            int turn,
            CombatEventLog log,
            CombatRng rng,
            ModifierCatalogSO modifierCatalog,
            CombatApService apService)
        {
            if (source == null || source.moves == null)
                return;

            bool appliedForTrigger = false;
            var processedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int m = 0; m < source.moves.Count && !appliedForTrigger; m++)
            {
                var move = source.moves[m];
                if (move == null)
                    continue;

                var refs = BuildModifierRefs(move, modifierCatalog);
                for (int i = 0; i < refs.Count && !appliedForTrigger; i++)
                {
                    var modRef = refs[i];
                    if (string.IsNullOrWhiteSpace(modRef.sectionId))
                        continue;
                    if (processedSections.Contains(modRef.sectionId))
                        continue;

                    ModifierTriggerCondition resolvedTrigger = ResolveTrigger(modRef);
                    if (resolvedTrigger != trigger)
                        continue;

                    processedSections.Add(modRef.sectionId);
                    appliedForTrigger = ApplySwapModifierEffect(
                        modRef,
                        source,
                        selfTarget,
                        allyTarget,
                        turn,
                        log,
                        rng,
                        apService);
                }
            }
        }

        private static List<SwapModifierRef> BuildModifierRefs(MoveInstance move, ModifierCatalogSO catalog)
        {
            var list = new List<SwapModifierRef>(4);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (move.modifiers != null)
            {
                for (int i = 0; i < move.modifiers.Count; i++)
                {
                    var runtime = move.modifiers[i];
                    string sectionId = runtime != null ? NormalizeId(runtime.definitionSectionId) : string.Empty;
                    TryAddRef(list, seen, sectionId, catalog, runtime, move);
                }
            }

            TryAddRef(list, seen, NormalizeId(move.modifier1SectionId), catalog, null, move);
            TryAddRef(list, seen, NormalizeId(move.modifier2SectionId), catalog, null, move);

            if (move.modifierIds != null)
            {
                for (int i = 0; i < move.modifierIds.Length; i++)
                    TryAddRef(list, seen, NormalizeId(move.modifierIds[i]), catalog, null, move);
            }

            return list;
        }

        private static void TryAddRef(
            List<SwapModifierRef> list,
            HashSet<string> seen,
            string sectionId,
            ModifierCatalogSO catalog,
            ModifierInstance runtime,
            MoveInstance sourceMove)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return;
            if (!seen.Add(sectionId))
                return;

            list.Add(new SwapModifierRef
            {
                sectionId = sectionId,
                definition = catalog != null ? catalog.GetBySectionId(sectionId) : null,
                runtime = runtime,
                sourceMove = sourceMove,
            });
        }

        private static ModifierTriggerCondition ResolveTrigger(SwapModifierRef modRef)
        {
            if (modRef.runtime != null && modRef.runtime.triggerCondition != ModifierTriggerCondition.None)
                return NormalizeLegacyTrigger(modRef.runtime.triggerCondition);

            if (modRef.definition != null && modRef.definition.triggerCondition != ModifierTriggerCondition.None)
                return NormalizeLegacyTrigger(modRef.definition.triggerCondition);

            return GetLegacySwapTrigger(modRef.sectionId);
        }

        private static ModifierTriggerCondition NormalizeLegacyTrigger(ModifierTriggerCondition trigger)
        {
            if (trigger == ModifierTriggerCondition.OnHPBelow30)
                return ModifierTriggerCondition.OnLowHP;
            return trigger;
        }

        private static bool ApplySwapModifierEffect(
            SwapModifierRef modRef,
            ChainlingInstance source,
            ChainlingInstance selfTarget,
            ChainlingInstance allyTarget,
            int turn,
            CombatEventLog log,
            CombatRng rng,
            CombatApService apService)
        {
            bool targetsAlly = TargetsAlly(modRef);
            if (targetsAlly && (modRef.sourceMove == null || modRef.sourceMove.category != MoveCategory.Support))
            {
                string moveName = modRef.sourceMove != null && !string.IsNullOrEmpty(modRef.sourceMove.displayName)
                    ? modRef.sourceMove.displayName
                    : "unknown move";
                string sourceName = source != null && !string.IsNullOrEmpty(source.displayName)
                    ? source.displayName
                    : "Chainling";
                log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{sourceName} has {modRef.sectionId} on {moveName}, but ally-target swap modifiers require Support moves.",
                    turn);
                return false;
            }

            ChainlingInstance target = targetsAlly ? allyTarget : selfTarget;
            if (target == null || !target.isAlive)
                return false;

            bool applied = false;

            if (IsApGain(modRef))
            {
                int apGain = ResolveApGain(modRef);
                CombatApService ap = apService ?? new CombatApService(log);
                applied = ap.Gain(target, apGain, turn, $"SwapModifier.{modRef.sectionId}") > 0;
            }
            else if (IsRandomStage(modRef))
            {
                float roll;
                int idx = rng.NextInt(0, StageStats.Length - 1, out roll);
                CoreStat stat = StageStats[idx];
                int oldStage = target.GetStage(stat);
                target.SetStage(stat, oldStage + 1);
                int newStage = target.GetStage(stat);
                int delta = newStage - oldStage;
                if (delta != 0)
                    log.LogStageChange(turn, target.instanceId, stat, delta, newStage);

                log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"SwapModifier.{modRef.sectionId}.RandomStat",
                        chance = 1f,
                        roll = roll,
                        triggered = true,
                    },
                    source != null ? source.instanceId : string.Empty,
                    new List<string> { target.instanceId },
                    turn);
                applied = true;
            }
            else if (IsAccuracyBoost(modRef))
            {
                float accuracyBonus = ResolvePercentValue(modRef, rng, 0.05f, 0.10f);
                target.AddAccuracySource(accuracyBonus, ACCURACY_DURATION_TURNS, $"SwapModifier.{modRef.sectionId}");

                log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{target.displayName} gained +{accuracyBonus * 100f:F1}% accuracy for {ACCURACY_DURATION_TURNS} turn ({modRef.sectionId})",
                    turn);
                applied = true;
            }
            else if (IsMitigation(modRef))
            {
                float mitigationValue = ResolvePercentValue(
                    modRef,
                    rng,
                    ResolveLegacyMitigation(modRef.sectionId),
                    ResolveLegacyMitigation(modRef.sectionId));
                if (mitigationValue > 0f)
                {
                    target.AddMitigationSource(mitigationValue, MITIGATION_DURATION_TURNS, $"SwapModifier.{modRef.sectionId}");
                    log.LogMessage(
                        CombatEventType.SystemMessage,
                        $"{target.displayName} gained {mitigationValue * 100f:F1}% mitigation for {MITIGATION_DURATION_TURNS} turn ({modRef.sectionId})",
                        turn);
                    applied = true;
                }
            }

            if (applied)
            {
                string sourceId = source != null ? source.instanceId : string.Empty;
                string targetId = target.instanceId;
                log.Log(ModifierOutcomePayload.CreateEvent(
                    turn,
                    modRef.sectionId,
                    ResolveCategory(modRef),
                    ResolveDisplayText(modRef),
                    targetId,
                    ModifierPopupMoment.OnSwapTrigger,
                    true,
                    $"Swap modifier triggered for {target.displayName}",
                    sourceId,
                    targetId,
                    modRef.sourceMove != null ? modRef.sourceMove.instanceId : string.Empty));
            }

            return applied;
        }

        private static bool TargetsAlly(SwapModifierRef modRef)
        {
            if (modRef.definition != null && modRef.definition.allowedCategories != null)
            {
                for (int i = 0; i < modRef.definition.allowedCategories.Length; i++)
                {
                    if (modRef.definition.allowedCategories[i] == MoveCategory.Support)
                        return true;
                }
            }

            string desc = modRef.definition != null ? modRef.definition.effectDescription : string.Empty;
            if (Contains(desc, "ally"))
                return true;

            return string.Equals(modRef.sectionId, S64D, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modRef.sectionId, S64E, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modRef.sectionId, S64H, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modRef.sectionId, S64I, StringComparison.OrdinalIgnoreCase);
        }

        private static ModifierCategory ResolveCategory(SwapModifierRef modRef)
        {
            if (modRef.definition != null)
                return modRef.definition.category;
            if (modRef.runtime != null)
                return modRef.runtime.category;
            return ModifierCategory.TRG;
        }

        private static string ResolveDisplayText(SwapModifierRef modRef)
        {
            if (IsApGain(modRef))
                return "+1 AP";
            if (IsRandomStage(modRef))
                return "STAT +1";
            if (IsAccuracyBoost(modRef))
                return "ACCURACY";
            if (IsMitigation(modRef))
                return "MITIGATE";
            return "SWAP";
        }

        private static bool IsApGain(SwapModifierRef modRef)
        {
            string desc = modRef.definition != null ? modRef.definition.effectDescription : string.Empty;
            if (Contains(desc, " ap"))
                return true;

            return string.Equals(modRef.sectionId, S64B, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modRef.sectionId, S64E, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modRef.sectionId, S64F, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRandomStage(SwapModifierRef modRef)
        {
            string desc = modRef.definition != null ? modRef.definition.effectDescription : string.Empty;
            if (Contains(desc, "random stat stage"))
                return true;

            return string.Equals(modRef.sectionId, S64C, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modRef.sectionId, S64I, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAccuracyBoost(SwapModifierRef modRef)
        {
            string desc = modRef.definition != null ? modRef.definition.effectDescription : string.Empty;
            if (Contains(desc, "accuracy"))
                return true;

            return string.Equals(modRef.sectionId, S64H, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMitigation(SwapModifierRef modRef)
        {
            string desc = modRef.definition != null ? modRef.definition.effectDescription : string.Empty;
            if (Contains(desc, "mitigation"))
                return true;

            return string.Equals(modRef.sectionId, S64A, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modRef.sectionId, S64D, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modRef.sectionId, S64G, StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveApGain(SwapModifierRef modRef)
        {
            float min = 1f;
            float max = 1f;
            if (HasLockedValue(modRef.runtime))
            {
                min = modRef.runtime.effectValue;
                max = min;
            }
            else if (modRef.definition != null)
            {
                if (modRef.definition.effectMin > 0f) min = modRef.definition.effectMin;
                if (modRef.definition.effectMax > 0f) max = modRef.definition.effectMax;
                if (max < min) max = min;
            }

            int rounded = (int)Math.Round((min + max) * 0.5f);
            return Math.Max(1, rounded);
        }

        private static float ResolvePercentValue(SwapModifierRef modRef, CombatRng rng, float fallbackMin, float fallbackMax)
        {
            float min = fallbackMin;
            float max = fallbackMax;

            if (HasLockedValue(modRef.runtime))
            {
                min = NormalizePercent(modRef.runtime.effectValue);
                max = min;
            }
            else if (modRef.definition != null)
            {
                if (modRef.definition.effectMin > 0f) min = NormalizePercent(modRef.definition.effectMin);
                if (modRef.definition.effectMax > 0f) max = NormalizePercent(modRef.definition.effectMax);
            }

            if (max < min) max = min;
            if (Math.Abs(max - min) < 0.0001f) return min;
            return min + (max - min) * rng.NextFloat();
        }

        private static bool HasLockedValue(ModifierInstance runtime)
        {
            return runtime != null && runtime.effectValueLocked;
        }

        private static float NormalizePercent(float raw)
        {
            return raw > 1f ? raw / 100f : raw;
        }

        private static float ResolveLegacyMitigation(string sectionId)
        {
            if (string.Equals(sectionId, S64A, StringComparison.OrdinalIgnoreCase)) return 0.125f;
            if (string.Equals(sectionId, S64D, StringComparison.OrdinalIgnoreCase)) return 0.10f;
            if (string.Equals(sectionId, S64G, StringComparison.OrdinalIgnoreCase)) return 0.075f;
            return 0f;
        }

        private static string NormalizeId(string sectionId)
        {
            return string.IsNullOrWhiteSpace(sectionId) ? string.Empty : sectionId.Trim();
        }

        private static bool Contains(string text, string value)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(value))
                return false;
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ModifierTriggerCondition GetLegacySwapTrigger(string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return ModifierTriggerCondition.None;

            if (string.Equals(sectionId, S64A, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sectionId, S64B, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sectionId, S64C, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sectionId, S64D, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sectionId, S64E, StringComparison.OrdinalIgnoreCase))
            {
                return ModifierTriggerCondition.OnSwapIn;
            }

            if (string.Equals(sectionId, S64F, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sectionId, S64G, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sectionId, S64H, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sectionId, S64I, StringComparison.OrdinalIgnoreCase))
            {
                return ModifierTriggerCondition.OnSwapOut;
            }

            return ModifierTriggerCondition.None;
        }
    }
}
