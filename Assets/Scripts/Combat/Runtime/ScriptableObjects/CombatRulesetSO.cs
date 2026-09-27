using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class RulesetStatMutator
    {
        public CoreStat stat = CoreStat.MaxHP;
        [Tooltip("Multiplies the progressed stat after Cycle grants.")]
        [Min(0f)] public float multiplier = 1f;
        [Tooltip("Optional hard cap after multiplier. 0 = no cap.")]
        [Min(0)] public int absoluteCap;
    }

    /// <summary>
    /// Swappable battle ruleset profile: resolver config + event mutators + roster eligibility.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatRuleset", menuName = "Chainbound/Combat/Config/Combat Ruleset")]
    public class CombatRulesetSO : ScriptableObject
    {
        [Header("Identity")]
        public string rulesetId = "pve_default";
        public string displayName = "PvE Default";
        [Min(1)] public int configVersion = 1;

        [Header("Resolver")]
        public CombatResolverConfigSO resolverConfig;

        [Header("Stat Mutators (event)")]
        public List<RulesetStatMutator> statMutators = new List<RulesetStatMutator>();

        [Header("Roster Eligibility")]
        [Tooltip("Empty = all types allowed.")]
        public List<ChainlingType> allowedTypes = new List<ChainlingType>();

        [Tooltip("Chainling must know at least one of these move ids. Empty = no requirement.")]
        public List<string> requiredMoveIds = new List<string>();

        [Tooltip("Chainling must not know any of these move ids.")]
        public List<string> bannedMoveIds = new List<string>();

        public CombatResolverConfigSO ResolveResolverConfig()
        {
            if (resolverConfig != null)
                return resolverConfig;
            return BalanceConfigRuntime.ResolverConfig;
        }

        public int ApplyStatMutator(CoreStat stat, int value)
        {
            if (statMutators == null || statMutators.Count == 0)
                return value;

            float result = value;
            for (int i = 0; i < statMutators.Count; i++)
            {
                RulesetStatMutator mutator = statMutators[i];
                if (mutator == null || mutator.stat != stat)
                    continue;

                result *= Mathf.Max(0f, mutator.multiplier);
                if (mutator.absoluteCap > 0)
                    result = Mathf.Min(result, mutator.absoluteCap);
            }

            return Mathf.Max(1, Mathf.RoundToInt(result));
        }

        public bool IsChainlingEligible(ChainlingType primaryType, ChainlingType secondaryType, IReadOnlyList<string> moveIds, out string reason)
        {
            reason = null;

            if (allowedTypes != null && allowedTypes.Count > 0)
            {
                bool typeOk = false;
                for (int i = 0; i < allowedTypes.Count; i++)
                {
                    ChainlingType allowed = allowedTypes[i];
                    if (allowed == primaryType || allowed == secondaryType)
                    {
                        typeOk = true;
                        break;
                    }
                }

                if (!typeOk)
                {
                    reason = "type_filter";
                    return false;
                }
            }

            if (bannedMoveIds != null && bannedMoveIds.Count > 0 && moveIds != null)
            {
                for (int i = 0; i < bannedMoveIds.Count; i++)
                {
                    string banned = bannedMoveIds[i];
                    if (string.IsNullOrEmpty(banned))
                        continue;

                    for (int m = 0; m < moveIds.Count; m++)
                    {
                        if (string.Equals(moveIds[m], banned, StringComparison.OrdinalIgnoreCase))
                        {
                            reason = "banned_move:" + banned;
                            return false;
                        }
                    }
                }
            }

            if (requiredMoveIds != null && requiredMoveIds.Count > 0)
            {
                if (moveIds == null || moveIds.Count == 0)
                {
                    reason = "missing_required_move";
                    return false;
                }

                bool anyRequired = false;
                for (int i = 0; i < requiredMoveIds.Count; i++)
                {
                    string required = requiredMoveIds[i];
                    if (string.IsNullOrEmpty(required))
                        continue;

                    for (int m = 0; m < moveIds.Count; m++)
                    {
                        if (string.Equals(moveIds[m], required, StringComparison.OrdinalIgnoreCase))
                        {
                            anyRequired = true;
                            break;
                        }
                    }

                    if (anyRequired)
                        break;
                }

                if (!anyRequired)
                {
                    reason = "missing_required_move";
                    return false;
                }
            }

            return true;
        }

        public string ComputeConfigHash()
        {
            CombatResolverConfigSO resolver = ResolveResolverConfig();
            string resolverHash = resolver != null ? resolver.ComputeConfigHash() : "none";
            return BalanceConfigHashUtility.HashRuleset(this, resolverHash);
        }

        public static CombatRulesetSO CreateRuntimeDefault()
        {
            var ruleset = CreateInstance<CombatRulesetSO>();
            ruleset.hideFlags = HideFlags.HideAndDontSave;
            ruleset.rulesetId = "pve_default";
            ruleset.displayName = "PvE Default";
            ruleset.configVersion = 1;
            ruleset.resolverConfig = CombatResolverConfigSO.CreateRuntimeDefault();
            return ruleset;
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            if (string.IsNullOrWhiteSpace(rulesetId))
                rulesetId = "unnamed_ruleset";
            if (statMutators == null)
                statMutators = new List<RulesetStatMutator>();
            if (allowedTypes == null)
                allowedTypes = new List<ChainlingType>();
            if (requiredMoveIds == null)
                requiredMoveIds = new List<string>();
            if (bannedMoveIds == null)
                bannedMoveIds = new List<string>();
        }
    }
}
