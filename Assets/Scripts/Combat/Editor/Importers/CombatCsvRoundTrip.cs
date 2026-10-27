using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Shared CSV export helpers for Moves/Modifiers round-trip with Combat2DataImporter.
    /// </summary>
    public static class CombatCsvRoundTrip
    {
        public const string DefaultDataFolder = "Docs/Specs/Combat/Data";
        public const string DefaultMovesFolder = "Assets/Data/Combat/Moves";
        public const string DefaultMovesDatabasePath = "Assets/Data/Combat/Moves/Database/MoveDatabase.asset";
        public const string DefaultModifiersFolder = "Assets/Data/Combat/Modifiers";
        public const string DefaultModifiersCatalogPath = "Assets/Data/Combat/Modifiers/Catalog/ModifierCatalog.asset";

        public static readonly string MovesHeader =
            "MoveId,DisplayName,Category,BaseType,Rarity,Targeting,ContactType,Power,APCost,Accuracy,HitMin,HitMax,RechargeTurns,StatusEffects,StageChanges,Tags,TriggerCondition,HPThreshold,TriggerOnce,HealPercentMaxHP,Description,SelectionPolicy,ActiveConditionType,ActiveConditionThreshold,UtilityEffects,HpCostPercent,ResolvePrimaryTiming,ResolveUtilityTiming,ResolveConditionalPolicy,ResolveConditions,ResolveRequireAll,DamagePowerMultiplier,DamagePreMitigationMultiplier,DamageFinalMultiplier,DamageOverrideAttackStat,DamageAttackStat,ConditionalDamageMultiplier,ConditionalDamageConditions,ConditionalDamageRequireAll,ConditionalDamageScalingCondition,ConditionalDamageBonusPerCount,ConditionalDamageMaxMultiplier,RandomDamageMultipliers,AccuracyTuning,MoveEffectsOncePerTarget";

        public static readonly string ModifiersHeader =
            "SectionId,DisplayName,Category,Scope,MinRarity,EffectMin,EffectMax,AllowedCategories,ForbiddenCombos,SlotCost,IsTypeInfusion,IsStatSub,SubStat,TriggerCondition,TriggerChance,EffectDescription,PowerCap,APCostCap,ForbiddenTags,SlotType,LowHpThreshold,Weight";

        public static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!needsQuotes)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static string JoinCsv(params string[] fields)
        {
            if (fields == null || fields.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsvField(fields[i] ?? string.Empty));
            }
            return sb.ToString();
        }

        public static string ExportMovesToCSV(
            string csvPath = null,
            string movesFolder = null,
            string moveDatabasePath = null)
        {
            csvPath = string.IsNullOrWhiteSpace(csvPath)
                ? Path.Combine(DefaultDataFolder, "Moves.csv").Replace('\\', '/')
                : csvPath.Replace('\\', '/');
            movesFolder = string.IsNullOrWhiteSpace(movesFolder) ? DefaultMovesFolder : movesFolder;
            moveDatabasePath = string.IsNullOrWhiteSpace(moveDatabasePath)
                ? DefaultMovesDatabasePath
                : moveDatabasePath;

            var moves = LoadMoves(movesFolder, moveDatabasePath);
            moves.Sort((a, b) => string.Compare(a.moveId, b.moveId, StringComparison.OrdinalIgnoreCase));

            var prefix = ReadLeadingComments(csvPath, "MoveId");
            var sb = new StringBuilder();
            for (int i = 0; i < prefix.Count; i++)
                sb.AppendLine(prefix[i]);
            sb.AppendLine(MovesHeader);

            for (int i = 0; i < moves.Count; i++)
            {
                MoveDefinitionSO m = moves[i];
                if (m == null || string.IsNullOrWhiteSpace(m.moveId))
                    continue;
                MoveResolveProfile profile = m.resolveProfile ?? new MoveResolveProfile();
                MoveResolveProfileUtility.EnsureNestedDefaults(profile);

                var fields = new List<string>
                {
                    m.moveId ?? string.Empty,
                    m.displayName ?? string.Empty,
                    m.category.ToString(),
                    m.baseType.ToString(),
                    m.rarity.ToString(),
                    m.targeting.ToString(),
                    m.contactType.ToString(),
                    m.power.ToString(CultureInfo.InvariantCulture),
                    m.apCost.ToString(CultureInfo.InvariantCulture),
                    m.accuracy.ToString(CultureInfo.InvariantCulture),
                    m.hitCountMin.ToString(CultureInfo.InvariantCulture),
                    m.hitCountMax.ToString(CultureInfo.InvariantCulture),
                    m.rechargeTurns.ToString(CultureInfo.InvariantCulture),
                    FormatStatusEffects(m.statusEffects),
                    FormatStageChanges(m.stageChanges),
                    FormatPipeList(m.tags),
                    m.triggerCondition.ToString(),
                    m.hpThresholdPercent.ToString(CultureInfo.InvariantCulture),
                    m.triggerOncePerBattle ? "true" : "false",
                    m.healPercentMaxHP > 0 ? m.healPercentMaxHP.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    m.description ?? string.Empty,
                    m.selectionPolicy.ToString(),
                    m.activeConditionType.ToString(),
                    m.activeConditionThresholdPercent.ToString(CultureInfo.InvariantCulture),
                    FormatUtilityEffects(m.utilityEffects),
                    m.hpCostPercent.ToString(CultureInfo.InvariantCulture),
                    profile.primaryTiming.ToString(),
                    profile.utilityExecutionTiming.ToString(),
                    profile.conditionalEffectPolicy.ToString(),
                    FormatResolveConditions(profile.resolveConditions),
                    profile.requireAllResolveConditions ? "true" : "false",
                    profile.damage.powerMultiplier.ToString(CultureInfo.InvariantCulture),
                    profile.damage.preMitigationDamageMultiplier.ToString(CultureInfo.InvariantCulture),
                    profile.damage.finalDamageMultiplier.ToString(CultureInfo.InvariantCulture),
                    profile.damage.overrideAttackStat ? "true" : "false",
                    profile.damage.attackStatOverride.ToString(),
                    profile.conditionalDamage.multiplier.ToString(CultureInfo.InvariantCulture),
                    FormatResolveConditions(profile.conditionalDamage.conditions),
                    profile.conditionalDamage.requireAllConditions ? "true" : "false",
                    profile.conditionalDamage.useScalingCondition
                        ? FormatResolveCondition(profile.conditionalDamage.scalingCondition)
                        : string.Empty,
                    profile.conditionalDamage.bonusMultiplierPerCount.ToString(CultureInfo.InvariantCulture),
                    profile.conditionalDamage.maxMultiplier.ToString(CultureInfo.InvariantCulture),
                    FormatFloatList(profile.conditionalDamage.randomMultipliers),
                    FormatAccuracyTuning(profile.accuracy),
                    profile.moveEffectsOncePerTarget ? "true" : string.Empty,
                };

                sb.AppendLine(JoinCsv(fields.ToArray()));
            }

            EnsureParentDirectory(csvPath);
            File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return $"Exported {moves.Count} moves → {csvPath}";
        }

        public static string FormatAccuracyTuning(MoveAccuracyTuning tuning)
        {
            if (tuning == null)
                return string.Empty;

            var tokens = new List<string>();
            bool sureHit = tuning.ignoreEvasionStages == 6 && Mathf.Abs(tuning.minHitChance - 0.95f) < 0.0001f;
            if (sureHit)
                tokens.Add("SureHit");
            else
            {
                if (tuning.ignoreEvasionStages != 0)
                    tokens.Add($"IgnoreEva:{tuning.ignoreEvasionStages}");
                if (tuning.minHitChance > 0f)
                    tokens.Add($"MinHit:{tuning.minHitChance.ToString(CultureInfo.InvariantCulture)}");
            }
            if (tuning.forceHit)
                tokens.Add("ForceHit");
            if (Mathf.Abs(tuning.accuracyBonusPercent) > 0.0001f)
                tokens.Add($"Bonus:{tuning.accuracyBonusPercent.ToString(CultureInfo.InvariantCulture)}");
            if (tuning.maxHitChance < 0.9999f)
                tokens.Add($"MaxHit:{tuning.maxHitChance.ToString(CultureInfo.InvariantCulture)}");
            return string.Join("|", tokens);
        }

        public static string ExportModifiersToCSV(
            string csvPath = null,
            string modifiersFolder = null,
            string catalogPath = null)
        {
            csvPath = string.IsNullOrWhiteSpace(csvPath)
                ? Path.Combine(DefaultDataFolder, "Modifiers.csv").Replace('\\', '/')
                : csvPath.Replace('\\', '/');
            modifiersFolder = string.IsNullOrWhiteSpace(modifiersFolder)
                ? DefaultModifiersFolder
                : modifiersFolder;
            catalogPath = string.IsNullOrWhiteSpace(catalogPath)
                ? DefaultModifiersCatalogPath
                : catalogPath;

            var modifiers = LoadModifiers(modifiersFolder, catalogPath);
            modifiers.Sort((a, b) => string.Compare(a.sectionId, b.sectionId, StringComparison.OrdinalIgnoreCase));

            var prefix = ReadLeadingComments(csvPath, "SectionId");
            var sb = new StringBuilder();
            for (int i = 0; i < prefix.Count; i++)
                sb.AppendLine(prefix[i]);
            sb.AppendLine(ModifiersHeader);

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDefinitionSO m = modifiers[i];
                if (m == null || string.IsNullOrWhiteSpace(m.sectionId))
                    continue;

                sb.AppendLine(JoinCsv(
                    m.sectionId ?? string.Empty,
                    m.GetDisplayName() ?? string.Empty,
                    m.category.ToString(),
                    m.scope.ToString(),
                    m.minimumRarity.ToString(),
                    m.effectMin.ToString(CultureInfo.InvariantCulture),
                    m.effectMax.ToString(CultureInfo.InvariantCulture),
                    FormatEnumPipe(m.allowedCategories),
                    FormatPipeArray(m.forbiddenCombinations),
                    m.slotCostIncrease.ToString(CultureInfo.InvariantCulture),
                    m.isTypeInfusion ? "true" : "false",
                    m.isStatSubstitution ? "true" : "false",
                    m.substitutionStat.ToString(),
                    m.triggerCondition.ToString(),
                    m.triggerChance.ToString(CultureInfo.InvariantCulture),
                    m.effectDescription ?? string.Empty,
                    m.powerCap.ToString(CultureInfo.InvariantCulture),
                    m.apCostCap.ToString(CultureInfo.InvariantCulture),
                    FormatPipeArray(m.forbiddenTags),
                    m.slotType.ToString(),
                    m.lowHpThresholdPercent.ToString(CultureInfo.InvariantCulture),
                    Math.Max(0, m.weight).ToString(CultureInfo.InvariantCulture)));
            }

            EnsureParentDirectory(csvPath);
            File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return $"Exported {modifiers.Count} modifiers → {csvPath}";
        }

        public static int FindHeaderColumnIndex(string[] headerFields, string columnName)
        {
            if (headerFields == null || string.IsNullOrWhiteSpace(columnName))
                return -1;

            for (int i = 0; i < headerFields.Length; i++)
            {
                if (string.Equals(headerFields[i].Trim(), columnName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static List<MoveDefinitionSO> LoadMoves(string movesFolder, string moveDatabasePath)
        {
            var db = AssetDatabase.LoadAssetAtPath<MoveDatabaseSO>(moveDatabasePath);
            if (db != null && db.moves != null && db.moves.Count > 0)
            {
                return db.moves.Where(m => m != null).ToList();
            }

            var list = new List<MoveDefinitionSO>();
            string[] guids = AssetDatabase.FindAssets("t:MoveDefinitionSO", new[] { movesFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var move = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(path);
                if (move != null)
                    list.Add(move);
            }
            return list;
        }

        private static List<ModifierDefinitionSO> LoadModifiers(string modifiersFolder, string catalogPath)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModifierCatalogSO>(catalogPath);
            if (catalog != null && catalog.modifiers != null && catalog.modifiers.Count > 0)
            {
                return catalog.modifiers.Where(m => m != null).ToList();
            }

            var list = new List<ModifierDefinitionSO>();
            string[] guids = AssetDatabase.FindAssets("t:ModifierDefinitionSO", new[] { modifiersFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var mod = AssetDatabase.LoadAssetAtPath<ModifierDefinitionSO>(path);
                if (mod != null)
                    list.Add(mod);
            }
            return list;
        }

        /// <summary>
        /// Preserves leading # comment / legend lines before the first data row.
        /// Skips the old header row (replaced on export).
        /// </summary>
        private static List<string> ReadLeadingComments(string csvPath, string headerKey)
        {
            var prefix = new List<string>();
            if (!File.Exists(csvPath))
                return prefix;

            string[] lines = File.ReadAllLines(csvPath);
            bool seenHeader = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (!seenHeader)
                {
                    if (trimmed.StartsWith("#", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(trimmed))
                    {
                        if (!string.IsNullOrWhiteSpace(trimmed))
                            prefix.Add(trimmed);
                        continue;
                    }

                    if (trimmed.StartsWith(headerKey, StringComparison.OrdinalIgnoreCase))
                    {
                        seenHeader = true;
                        continue;
                    }

                    // Unexpected content before header — stop preserving.
                    break;
                }

                // Preserve consecutive legend comments immediately after header.
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    prefix.Add(trimmed);
                    continue;
                }

                break;
            }

            return prefix;
        }

        private static void EnsureParentDirectory(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static string FormatPipeList(IList<string> values)
        {
            if (values == null || values.Count == 0)
                return string.Empty;
            return string.Join("|", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
        }

        private static string FormatPipeArray(string[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;
            return string.Join("|", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
        }

        private static string FormatEnumPipe<T>(T[] values) where T : Enum
        {
            if (values == null || values.Length == 0)
                return string.Empty;
            return string.Join("|", values.Select(v => v.ToString()));
        }

        public static string FormatStatusEffectsPublic(List<MoveStatusEffectDef> effects) => FormatStatusEffects(effects);
        public static string FormatStageChangesPublic(List<MoveStageChangeDef> changes) => FormatStageChanges(changes);
        public static string FormatResolveConditionsPublic(List<MoveResolveCondition> conditions) => FormatResolveConditions(conditions);
        public static string FormatResolveConditionPublic(MoveResolveCondition condition) => FormatResolveCondition(condition);
        public static string FormatFloatListPublic(List<float> values) => FormatFloatList(values);
        public static string FormatUtilityEffectsPublic(List<UtilityEffectPayload> effects) => FormatUtilityEffects(effects);

        private static string FormatStatusEffects(List<MoveStatusEffectDef> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                MoveStatusEffectDef e = effects[i];
                if (e.statusId == StatusId.None)
                    continue;
                string target = e.overrideTargeting ? e.targetOverride.ToString() : string.Empty;
                parts.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1}:{2}:{3}",
                    e.statusId,
                    e.duration,
                    e.chance,
                    target));
            }
            return string.Join("|", parts);
        }

        private static string FormatStageChanges(List<MoveStageChangeDef> changes)
        {
            if (changes == null || changes.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < changes.Count; i++)
            {
                MoveStageChangeDef c = changes[i];
                parts.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1}:{2}:{3}",
                    c.stat,
                    c.stages,
                    c.chance,
                    c.targetsSelf ? "true" : "false"));
            }
            return string.Join("|", parts);
        }

        private static string FormatResolveConditions(List<MoveResolveCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return string.Empty;
            return string.Join("|", conditions.Select(FormatResolveCondition));
        }

        private static string FormatResolveCondition(MoveResolveCondition condition)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}:{3}:{4}:{5}",
                condition.subject,
                condition.conditionType,
                condition.threshold,
                condition.statusId,
                condition.stat,
                condition.comparisonStat);
        }

        private static string FormatFloatList(List<float> values)
        {
            if (values == null || values.Count == 0)
                return string.Empty;
            return string.Join("|", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
        }

        private static string FormatUtilityEffects(List<UtilityEffectPayload> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                UtilityEffectPayload e = effects[i];
                if (e == null) continue;
                string formatted = FormatUtilityEffect(e);
                if (!string.IsNullOrEmpty(formatted))
                    parts.Add(formatted);
            }
            return string.Join("|", parts);
        }

        private static string FormatUtilityEffect(UtilityEffectPayload e)
        {
            switch (e.effectType)
            {
                case UtilityEffectType.MitigationWindow:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "MitigationWindow:{0}:{1}:{2}:{3}",
                        FormatPercentToken(e.magnitude),
                        e.turnBudget,
                        e.hitBudget,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.GainApOnDamageIfSpeedHigher:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "GainApOnDamageIfSpeedHigher:{0}:{1}:{2}:{3}",
                        e.apGain,
                        e.turnBudget,
                        e.hitBudget,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.FullAbsorbBarrier:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "FullAbsorbBarrier:{0}:{1}:{2}",
                        e.turnBudget,
                        e.hitBudget,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.DamageThresholdBarrier:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "DamageThresholdBarrier:{0}:{1}:{2}:{3}:{4}",
                        e.magnitude.ToString(CultureInfo.InvariantCulture),
                        e.turnBudget,
                        e.hitBudget,
                        FormatPercentToken(e.reflectPercent),
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.WallHitRetaliation:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "WallHitRetaliation:{0}:{1}",
                        e.apGain,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.GainAP:
                case UtilityEffectType.DrainAP:
                case UtilityEffectType.StealAP:
                case UtilityEffectType.TransferAP:
                case UtilityEffectType.RefundAP:
                case UtilityEffectType.NextMoveAPSurcharge:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}:{3}:{4}",
                        e.effectType,
                        e.apGain,
                        FormatPercentToken(e.chance),
                        e.targetOverride,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.TurnStartPendingStatus:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "TurnStartPendingStatus:{0}:{1}:{2}:{3}",
                        e.statusId,
                        e.statusDuration,
                        e.targetOverride,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.Cleanse:
                {
                    string cleanse = string.Format(
                        CultureInfo.InvariantCulture,
                        "Cleanse:{0}:{1}:{2}",
                        e.apGain,
                        e.chance.ToString(CultureInfo.InvariantCulture),
                        e.targetOverride);
                    if (!string.IsNullOrEmpty(e.stackKey))
                        cleanse += ":" + e.stackKey;
                    return cleanse;
                }

                case UtilityEffectType.ApplyChemistryStatus:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "ApplyChemistryStatus:{0}:{1}:{2}:{3}:{4}",
                        e.statusId,
                        e.statusDuration,
                        FormatPercentToken(e.chance),
                        e.targetOverride,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.BlockWindow:
                {
                    string category = e.contactFilter == UtilityContactFilter.Contact
                        ? "Melee"
                        : e.contactFilter == UtilityContactFilter.Projectile ? "Ranged" : "Any";
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "BlockWindow:{0}:{1}:{2}:{3}",
                        FormatPercentToken(e.magnitude),
                        category,
                        e.turnBudget,
                        e.stackKey ?? string.Empty);
                }

                case UtilityEffectType.BlockReflect:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "BlockReflect:{0}:{1}",
                        FormatPercentToken(e.reflectPercent),
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.StripBuffs:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "StripBuffs:{0}:{1}:{2}:{3}",
                        (int)Math.Round(e.magnitude),
                        FormatPercentToken(e.chance),
                        e.targetOverride,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.StripAllStatuses:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "StripAllStatuses:{0}:{1}:{2}",
                        FormatPercentToken(e.chance),
                        e.targetOverride,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.StripBuffsThenStage:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "StripBuffsThenStage:{0}:{1}:{2}:{3}:{4}:{5}",
                        (int)Math.Round(e.magnitude),
                        FormatPercentToken(e.chance),
                        e.targetOverride,
                        e.stageStat,
                        e.stageAmount,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.HealSelfPerTargetDebuff:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "HealSelfPerTargetDebuff:{0}:{1}:{2}",
                        FormatRawPercent(e.magnitude),
                        FormatPercentToken(e.chance),
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.BonusHealPercentIfSpeedHigher:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "BonusHealPercentIfSpeedHigher:{0}:{1}:{2}",
                        FormatRawPercent(e.magnitude),
                        e.targetOverride,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.IncomingDamageMitigation:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "IncomingDamageMitigation:{0}:{1}:{2}",
                        FormatPercentToken(e.magnitude),
                        e.contactFilter,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.ChangeStage:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "ChangeStage:{0}:{1}:{2}:{3}:{4}:{5}:{6}",
                        e.stageStat,
                        e.stageAmount,
                        FormatPercentToken(e.chance),
                        e.targetOverride,
                        e.stageCap,
                        e.affectAllAllies ? "true" : "false",
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.RandomStageChange:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "RandomStageChange:{0}:{1}:{2}:{3}:{4}:{5}",
                        e.stageAmount,
                        e.stageAmountMax,
                        FormatPercentToken(e.chance),
                        e.targetOverride,
                        e.stageCap,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.ArmUtilityWindow:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "ArmUtilityWindow:{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}:{8}:{9}:{10}:{11}",
                        e.armedTrigger,
                        e.turnBudget,
                        e.hitBudget,
                        e.apGain,
                        FormatPercentToken(e.healPercentMaxHP),
                        e.stageStat,
                        e.stageAmount,
                        e.targetOverride,
                        FormatPercentToken(e.counterDamagePercentMaxHP),
                        e.affectAllAllies ? "true" : "false",
                        e.stageCap,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.BankAPUntilNextTurn:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "BankAPUntilNextTurn:{0}:{1}:{2}",
                        e.threshold,
                        e.apGain,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.GainApIfDistinctTargetsHitAtLeast:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "GainApIfDistinctTargetsHitAtLeast:{0}:{1}:{2}",
                        e.threshold,
                        e.apGain,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.FieldEntryStageChange:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "FieldEntryStageChange:{0}:{1}:{2}:{3}:{4}",
                        e.stageStat,
                        e.stageAmount,
                        e.turnBudget,
                        FormatPercentToken(e.chance),
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.MoveAPDiscountWhileHPAbove:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "MoveAPDiscountWhileHPAbove:{0}:{1}:{2}",
                        e.apGain,
                        e.threshold,
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.LifestealPercent:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "LifestealPercent:{0}:{1}",
                        FormatPercentToken(e.magnitude),
                        e.stackKey ?? string.Empty);

                case UtilityEffectType.ExtendBuff:
                    return string.IsNullOrEmpty(e.stackKey) ? "ExtendBuff" : "ExtendBuff:" + e.stackKey;

                case UtilityEffectType.RaiseLowestStage:
                    return string.IsNullOrEmpty(e.stackKey) ? "RaiseLowestStage" : "RaiseLowestStage:" + e.stackKey;

                case UtilityEffectType.HealPercentIfStageAtLeast:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "HealPercentIfStageAtLeast:{0}:{1}:{2}:{3}:{4}",
                        FormatPercentToken(e.healPercentMaxHP),
                        e.stageStat,
                        e.threshold,
                        e.targetOverride,
                        e.stackKey ?? string.Empty);

                default:
                    return e.effectType.ToString();
            }
        }

        private static string FormatPercentToken(float value)
        {
            if (value > 1f)
                return value.ToString(CultureInfo.InvariantCulture) + "%";
            return (value * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatRawPercent(float value)
        {
            // BonusHeal stores fraction (0.10) or already-percent depending on authoring;
            // CSV examples use whole percents (10).
            if (value > 0f && value <= 1f)
                return (value * 100f).ToString("0.##", CultureInfo.InvariantCulture);
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
