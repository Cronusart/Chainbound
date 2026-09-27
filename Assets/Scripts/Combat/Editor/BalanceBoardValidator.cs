using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Scans lever-board assets and reports OnValidate-style invariant violations.
    /// Menu: Tools/Chainbound/Combat/Validate Balance Boards
    /// </summary>
    public static class BalanceBoardValidator
    {
        [MenuItem("Tools/Chainbound/Combat/Validate Balance Boards")]
        public static void ValidateAll()
        {
            var report = new StringBuilder();
            int issues = 0;

            issues += ValidateAssets<CombatResolverConfigSO>("t:CombatResolverConfigSO", report, ValidateResolver);
            issues += ValidateAssets<ProgressionConfigSO>("t:ProgressionConfigSO", report, ValidateProgression);
            issues += ValidateAssets<PostBattleRewardsConfigSO>("t:PostBattleRewardsConfigSO", report, ValidateRewards);
            issues += ValidateAssets<VendingConfigSO>("t:VendingConfigSO", report, ValidateVending);
            issues += ValidateAssets<CombatRulesetSO>("t:CombatRulesetSO", report, ValidateRuleset);
            issues += ValidateAssets<ModifierBalanceOverlaySO>("t:ModifierBalanceOverlaySO", report, ValidateOverlay);
            issues += ValidateAssets<BattleIntroCopyConfigSO>("t:BattleIntroCopyConfigSO", report, ValidateIntroCopy);

            if (issues == 0)
            {
                Debug.Log("[BalanceBoardValidator] All balance boards passed validation.");
                EditorUtility.DisplayDialog("Balance Boards", "All balance boards passed validation.", "OK");
            }
            else
            {
                Debug.LogWarning($"[BalanceBoardValidator] {issues} issue(s):\n{report}");
                EditorUtility.DisplayDialog("Balance Boards", $"{issues} issue(s) found. See Console.", "OK");
            }
        }

        private delegate void Validator<T>(T asset, string path, StringBuilder report, ref int issues);

        private static int ValidateAssets<T>(string filter, StringBuilder report, Validator<T> validator)
            where T : Object
        {
            int issues = 0;
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                    continue;
                validator(asset, path, report, ref issues);
            }

            return issues;
        }

        private static void ValidateResolver(CombatResolverConfigSO c, string path, StringBuilder report, ref int issues)
        {
            if (c.randomRollMax < c.randomRollMin)
                Issue(report, ref issues, path, "randomRollMax < randomRollMin");
            if (c.stageMax < c.stageMin)
                Issue(report, ref issues, path, "stageMax < stageMin");
            if (c.apCap < 1)
                Issue(report, ref issues, path, "apCap < 1");
        }

        private static void ValidateProgression(ProgressionConfigSO c, string path, StringBuilder report, ref int issues)
        {
            if (c.starXpThresholds == null || c.starXpThresholds.Length != CombatConstants.STAR_MAX)
                Issue(report, ref issues, path, $"starXpThresholds length != {CombatConstants.STAR_MAX}");
            if (c.rebirthRamCosts == null || c.rebirthRamCosts.Length != CombatConstants.REBIRTH_MAX)
                Issue(report, ref issues, path, $"rebirthRamCosts length != {CombatConstants.REBIRTH_MAX}");
            if (c.moveLearnRarityWeights == null || c.moveLearnRarityWeights.Length != 6)
                Issue(report, ref issues, path, "moveLearnRarityWeights length != 6");
        }

        private static void ValidateRewards(PostBattleRewardsConfigSO c, string path, StringBuilder report, ref int issues)
        {
            if (c.activeXpShare < 0f || c.activeXpShare > 1f)
                Issue(report, ref issues, path, "activeXpShare out of range");
            if (c.benchXpShare < 0f || c.benchXpShare > 1f)
                Issue(report, ref issues, path, "benchXpShare out of range");
        }

        private static void ValidateVending(VendingConfigSO c, string path, StringBuilder report, ref int issues)
        {
            if (c.rarityTierWeightsPercent == null || c.rarityTierWeightsPercent.Length != 6)
            {
                Issue(report, ref issues, path, "rarityTierWeightsPercent length != 6");
                return;
            }

            float sum = 0f;
            for (int i = 0; i < c.rarityTierWeightsPercent.Length; i++)
                sum += c.rarityTierWeightsPercent[i];
            if (sum <= 0f)
                Issue(report, ref issues, path, "rarity tier weights sum to 0");
        }

        private static void ValidateRuleset(CombatRulesetSO c, string path, StringBuilder report, ref int issues)
        {
            if (string.IsNullOrWhiteSpace(c.rulesetId))
                Issue(report, ref issues, path, "rulesetId empty");
            if (c.resolverConfig == null)
                Issue(report, ref issues, path, "resolverConfig is null (runtime default will be used)");
        }

        private static void ValidateOverlay(ModifierBalanceOverlaySO c, string path, StringBuilder report, ref int issues)
        {
            if (string.IsNullOrWhiteSpace(c.overlayId))
                Issue(report, ref issues, path, "overlayId empty");
            if (c.entries == null)
                return;

            var seen = new HashSet<string>();
            for (int i = 0; i < c.entries.Count; i++)
            {
                ModifierBalanceOverlayEntry e = c.entries[i];
                if (e == null || string.IsNullOrEmpty(e.sectionId))
                    continue;
                if (!seen.Add(e.sectionId.ToLowerInvariant()))
                    Issue(report, ref issues, path, $"duplicate overlay sectionId '{e.sectionId}'");
                if (e.overrideEffectRange && e.effectMax < e.effectMin)
                    Issue(report, ref issues, path, $"overlay '{e.sectionId}' effectMax < effectMin");
            }
        }

        private static void ValidateIntroCopy(BattleIntroCopyConfigSO c, string path, StringBuilder report, ref int issues)
        {
            if (string.IsNullOrWhiteSpace(c.wildSingleTemplate) ||
                !c.wildSingleTemplate.Contains(BattleIntroCopyConfigSO.SubjectToken))
            {
                Issue(report, ref issues, path, "wildSingleTemplate missing {subject}");
            }

            if (string.IsNullOrWhiteSpace(c.wildDualTemplate) ||
                !c.wildDualTemplate.Contains(BattleIntroCopyConfigSO.Subject0Token) ||
                !c.wildDualTemplate.Contains(BattleIntroCopyConfigSO.Subject1Token))
            {
                Issue(report, ref issues, path, "wildDualTemplate missing {subject0}/{subject1}");
            }

            if (string.IsNullOrWhiteSpace(c.trainerTemplate) ||
                !c.trainerTemplate.Contains(BattleIntroCopyConfigSO.TeamToken))
            {
                Issue(report, ref issues, path, "trainerTemplate missing {team}");
            }

            if (string.IsNullOrWhiteSpace(c.pvpTemplate))
                Issue(report, ref issues, path, "pvpTemplate empty");

            if (c.traitOrder == null || c.traitOrder.Count == 0)
                Issue(report, ref issues, path, "traitOrder empty");

            if (c.mutatedStyle == null || string.IsNullOrWhiteSpace(c.mutatedStyle.label))
                Issue(report, ref issues, path, "mutatedStyle.label empty");
            if (c.entangledStyle == null || string.IsNullOrWhiteSpace(c.entangledStyle.label))
                Issue(report, ref issues, path, "entangledStyle.label empty");
            if (c.variantStyle == null || string.IsNullOrWhiteSpace(c.variantStyle.label))
                Issue(report, ref issues, path, "variantStyle.label empty");
        }

        private static void Issue(StringBuilder report, ref int issues, string path, string message)
        {
            issues++;
            report.Append("- ").Append(path).Append(": ").Append(message).Append('\n');
        }
    }
}
