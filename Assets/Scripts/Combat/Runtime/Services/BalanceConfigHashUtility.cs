using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Deterministic SHA256 hashes over lever-board values for config stamping and drift tests.
    /// </summary>
    public static class BalanceConfigHashUtility
    {
        public static string HashResolverConfig(CombatResolverConfigSO config)
        {
            if (config == null)
                return HashUtf8("resolver:null");

            var sb = new StringBuilder(256);
            sb.Append("resolver|v=").Append(config.configVersion);
            Append(sb, "apPerTurn", config.apPerTurn);
            Append(sb, "apCap", config.apCap);
            Append(sb, "rngMin", config.randomRollMin);
            Append(sb, "rngMax", config.randomRollMax);
            Append(sb, "crit", config.critMultiplierDefault);
            Append(sb, "progDmg", config.progressionDamageFactor);
            Append(sb, "chemAdv", config.chemistryAdvantageFactor);
            Append(sb, "chemDis", config.chemistryDisadvantageFactor);
            Append(sb, "stabOn", config.enableStab ? 1 : 0);
            Append(sb, "stab", config.stabMultiplier);
            Append(sb, "stageMin", config.stageMin);
            Append(sb, "stageMax", config.stageMax);
            Append(sb, "stageCap", config.stageActiveChangeCap);
            Append(sb, "mitCap", config.mitigationSingleSourceCap);
            Append(sb, "mitFloor", config.mitigationFloor);
            Append(sb, "exclBar", config.exclusiveBarriersAndWalls ? 1 : 0);
            Append(sb, "maxHpBonus", config.maxPassiveMaxHpBonusPercent);
            Append(sb, "rekt", config.rektLoseActionChance);
            Append(sb, "glitch", config.glitchedMindMisactChance);
            Append(sb, "slowed", config.slowedLoseActionChance);
            Append(sb, "firewall", config.firewallProjectileMitigation);
            Append(sb, "slowedStage", config.slowedSpeedStages);
            Append(sb, "shatteredStage", config.shatteredDefenseStages);
            Append(sb, "shatteredChip", config.shatteredCurrentHpDamage);
            Append(sb, "drip", config.dripHealPercentMaxHp);
            Append(sb, "cascade", config.cascadeFailureMaxHpDamage);
            Append(sb, "splinterSpread", config.splinterDamageMultiplier);
            Append(sb, "splinterSingle", config.splinterSingleTargetDamageMultiplier);
            Append(sb, "ruggedCap", config.ruggedTickCap);
            Append(sb, "fomoDot", config.fomoSelfDotPercent);
            Append(sb, "turnLimit", config.turnActionTimeLimitSeconds);
            return HashUtf8(sb.ToString());
        }

        public static string HashPostBattleRewards(PostBattleRewardsConfigSO config)
        {
            if (config == null)
                return HashUtf8("rewards:null");

            var sb = new StringBuilder(256);
            sb.Append("rewards|v=").Append(config.configVersion);
            Append(sb, "koS", config.koXpStandard);
            Append(sb, "koE", config.koXpElite);
            Append(sb, "koB", config.koXpBoss);
            Append(sb, "cmpS", config.completionBonusStandard);
            Append(sb, "cmpE", config.completionBonusElite);
            Append(sb, "cmpB", config.completionBonusBoss);
            Append(sb, "ramS", config.ramStandard);
            Append(sb, "ramE", config.ramElite);
            Append(sb, "ramB", config.ramBoss);
            Append(sb, "activeShare", config.activeXpShare);
            Append(sb, "benchShare", config.benchXpShare);
            Append(sb, "kodXp", config.kodReceivesXp ? 1 : 0);
            Append(sb, "capXp", config.capturedReceivesXp ? 1 : 0);
            Append(sb, "defeat", config.defeatGrantsRewards ? 1 : 0);
            Append(sb, "defeatRamFlat", config.defeatRamPenaltyFlat);
            Append(sb, "defeatRamPerLevel", config.defeatRamPenaltyPerPlayerLevel);
            Append(sb, "defeatReviveKo", config.defeatRevivesKoChainlings ? 1 : 0);
            Append(sb, "defeatRespawnHp", config.defeatKoRespawnHpFraction);
            return HashUtf8(sb.ToString());
        }

        public static string HashProgressionConfig(ProgressionConfigSO config)
        {
            if (config == null)
                return HashUtf8("progression:null");

            var sb = new StringBuilder(384);
            sb.Append("progression|v=").Append(config.configVersion);
            AppendArray(sb, "stars", config.starXpThresholds);
            Append(sb, "mastery", config.masteryBaseXp);
            Append(sb, "rebMul", config.rebirthXpMultiplierBase);
            AppendArray(sb, "rebCost", config.rebirthRamCosts);
            Append(sb, "autoFlat", config.cycleAutoStatFlat);
            Append(sb, "autoPct", config.cycleAutoStatPercent);
            Append(sb, "pickFlat", config.cyclePickStatFlat);
            Append(sb, "pickPct", config.cyclePickStatPercent);
            Append(sb, "hpAutoFlat", config.cycleAutoHpFlat);
            Append(sb, "hpAutoPct", config.cycleAutoHpPercent);
            Append(sb, "hpPickFlat", config.cyclePickHpFlat);
            Append(sb, "hpPickPct", config.cyclePickHpPercent);
            AppendFloatArray(sb, "learnRarity", config.moveLearnRarityWeights);
            return HashUtf8(sb.ToString());
        }

        public static string HashVendingConfig(VendingConfigSO config)
        {
            if (config == null)
                return HashUtf8("vending:null");

            var sb = new StringBuilder(256);
            sb.Append("vending|v=").Append(config.configVersion);
            Append(sb, "gen", config.generatorVersion);
            AppendFloatArray(sb, "tiers", config.rarityTierWeightsPercent);
            Append(sb, "getKnown", config.ramCostGetKnown);
            Append(sb, "stampKnown", config.ramCostStampKnown);
            Append(sb, "cap", config.vendingDailyCap);
            Append(sb, "favor", config.eventFavorBoost);
            return HashUtf8(sb.ToString());
        }

        public static string HashChainlingRecoveryConfig(ChainlingRecoveryConfigSO config)
        {
            if (config == null)
                return HashUtf8("chainlingRecovery:null");

            var sb = new StringBuilder(192);
            sb.Append("chainlingRecovery|v=").Append(config.configVersion);
            Append(sb, "enabled", config.enabled ? 1 : 0);
            Append(sb, "interval", config.intervalSeconds);
            Append(sb, "healBp", config.HealBasisPoints);
            Append(sb, "catchup", config.maxCatchupIntervals);
            Append(sb, "party", config.partyMembersOnly ? 1 : 0);
            return HashUtf8(sb.ToString());
        }

        public static string HashRuleset(CombatRulesetSO ruleset, string resolverHash)
        {
            if (ruleset == null)
                return HashUtf8("ruleset:null");

            var sb = new StringBuilder(256);
            sb.Append("ruleset|id=").Append(ruleset.rulesetId ?? string.Empty);
            sb.Append("|v=").Append(ruleset.configVersion);
            sb.Append("|resolver=").Append(resolverHash ?? "none");
            sb.Append("|mut=").Append(ruleset.statMutators != null ? ruleset.statMutators.Count : 0);
            sb.Append("|types=").Append(ruleset.allowedTypes != null ? ruleset.allowedTypes.Count : 0);
            sb.Append("|req=").Append(ruleset.requiredMoveIds != null ? ruleset.requiredMoveIds.Count : 0);
            sb.Append("|ban=").Append(ruleset.bannedMoveIds != null ? ruleset.bannedMoveIds.Count : 0);
            return HashUtf8(sb.ToString());
        }

        public static string HashModifierOverlay(ModifierBalanceOverlaySO overlay)
        {
            if (overlay == null)
                return HashUtf8("overlay:null");

            var sb = new StringBuilder(256);
            sb.Append("overlay|id=").Append(overlay.overlayId ?? string.Empty);
            sb.Append("|v=").Append(overlay.configVersion);
            sb.Append("|entries=").Append(overlay.entries != null ? overlay.entries.Count : 0);
            sb.Append("|tierOverride=").Append(overlay.overrideTierWeights ? 1 : 0);
            if (overlay.entries != null)
            {
                for (int i = 0; i < overlay.entries.Count; i++)
                {
                    ModifierBalanceOverlayEntry e = overlay.entries[i];
                    if (e == null)
                        continue;
                    sb.Append('|').Append(e.sectionId ?? string.Empty);
                    Append(sb, "emin", e.effectMin);
                    Append(sb, "emax", e.effectMax);
                    Append(sb, "w", e.weight);
                }
            }

            return HashUtf8(sb.ToString());
        }

        public static string HashBattleIntroCopy(BattleIntroCopyConfigSO config)
        {
            if (config == null)
                return HashUtf8("introCopy:null");

            var sb = new StringBuilder(256);
            sb.Append("introCopy|v=").Append(config.configVersion);
            sb.Append("|wild1=").Append(config.wildSingleTemplate ?? string.Empty);
            sb.Append("|wild2=").Append(config.wildDualTemplate ?? string.Empty);
            sb.Append("|trainer=").Append(config.trainerTemplate ?? string.Empty);
            sb.Append("|pvp=").Append(config.pvpTemplate ?? string.Empty);
            sb.Append("|sep=").Append(config.traitSeparator ?? string.Empty);
            sb.Append("|order=");
            if (config.traitOrder != null)
            {
                for (int i = 0; i < config.traitOrder.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append((int)config.traitOrder[i]);
                }
            }

            AppendTrait(sb, "mut", config.mutatedStyle);
            AppendTrait(sb, "ent", config.entangledStyle);
            AppendTrait(sb, "name", config.nameStyle);
            AppendTrait(sb, "var", config.variantStyle);
            Append(sb, "varGrad", config.applyVariantVertexGradient ? 1 : 0);
            return HashUtf8(sb.ToString());
        }

        public static string HashUtf8(string payload)
        {
            if (payload == null)
                payload = string.Empty;

            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        private static void AppendTrait(StringBuilder sb, string key, BattleIntroTraitStyle style)
        {
            if (style == null)
            {
                sb.Append('|').Append(key).Append("=null");
                return;
            }

            sb.Append('|').Append(key)
                .Append("=on").Append(style.includeWhenActive ? 1 : 0)
                .Append(",lbl=").Append(style.label ?? string.Empty)
                .Append(",col=").Append(style.colorize ? 1 : 0)
                .Append(",src=").Append((int)style.colorSource)
                .Append(",grad=").Append(style.applyVertexGradient ? 1 : 0);
        }

        private static void Append(StringBuilder sb, string key, int value)
        {
            sb.Append('|').Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Append(StringBuilder sb, string key, float value)
        {
            sb.Append('|').Append(key).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendArray(StringBuilder sb, string key, int[] values)
        {
            sb.Append('|').Append(key).Append('=');
            if (values == null)
            {
                sb.Append("null");
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AppendFloatArray(StringBuilder sb, string key, float[] values)
        {
            sb.Append('|').Append(key).Append('=');
            if (values == null)
            {
                sb.Append("null");
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(values[i].ToString("R", CultureInfo.InvariantCulture));
            }
        }
    }
}
