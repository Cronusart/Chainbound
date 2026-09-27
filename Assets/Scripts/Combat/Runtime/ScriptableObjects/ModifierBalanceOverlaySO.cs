using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class ModifierBalanceOverlayEntry
    {
        public string sectionId;
        public bool overrideEffectRange;
        public float effectMin;
        public float effectMax;
        public bool overrideMinimumRarity;
        public RarityTier minimumRarity = RarityTier.Common;
        public bool overrideWeight;
        [Min(0)] public int weight = 1;
    }

    /// <summary>
    /// Event/experiment overlay applied on top of CSV-imported modifier catalog values.
    /// Never persisted back to Modifiers.csv. Overlay id is folded into config stamps.
    /// </summary>
    [CreateAssetMenu(fileName = "ModifierBalanceOverlay", menuName = "Chainbound/Combat/Config/Modifier Balance Overlay")]
    public class ModifierBalanceOverlaySO : ScriptableObject
    {
        [Header("Identity")]
        public string overlayId = "event_overlay";
        [Min(1)] public int configVersion = 1;

        [Header("Per-Modifier Overrides")]
        public List<ModifierBalanceOverlayEntry> entries = new List<ModifierBalanceOverlayEntry>();

        [Header("Optional Tier Weight Override")]
        [Tooltip("When enabled, replaces VendingConfigSO rarity tier weights for the event window.")]
        public bool overrideTierWeights;
        public float[] rarityTierWeightsPercent = { 45f, 30f, 17.5f, 5f, 2f, 0.5f };

        public bool TryGetEntry(string sectionId, out ModifierBalanceOverlayEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(sectionId) || entries == null)
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                ModifierBalanceOverlayEntry candidate = entries[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.sectionId))
                    continue;

                if (string.Equals(candidate.sectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public void ApplyToDefinition(ModifierDefinitionSO definition)
        {
            if (definition == null || !TryGetEntry(definition.sectionId, out ModifierBalanceOverlayEntry entry))
                return;

            if (entry.overrideEffectRange)
            {
                definition.effectMin = entry.effectMin;
                definition.effectMax = Mathf.Max(entry.effectMin, entry.effectMax);
            }

            if (entry.overrideMinimumRarity)
                definition.minimumRarity = entry.minimumRarity;

            if (entry.overrideWeight)
                definition.weight = Mathf.Max(0, entry.weight);
        }

        public float ResolveEffectMin(ModifierDefinitionSO definition)
        {
            if (definition == null)
                return 0f;
            if (TryGetEntry(definition.sectionId, out ModifierBalanceOverlayEntry entry) && entry.overrideEffectRange)
                return entry.effectMin;
            return definition.effectMin;
        }

        public float ResolveEffectMax(ModifierDefinitionSO definition)
        {
            if (definition == null)
                return 0f;
            if (TryGetEntry(definition.sectionId, out ModifierBalanceOverlayEntry entry) && entry.overrideEffectRange)
                return Mathf.Max(entry.effectMin, entry.effectMax);
            return definition.effectMax;
        }

        public int ResolveWeight(ModifierDefinitionSO definition)
        {
            if (definition == null)
                return 1;
            if (TryGetEntry(definition.sectionId, out ModifierBalanceOverlayEntry entry) && entry.overrideWeight)
                return Mathf.Max(0, entry.weight);
            return Mathf.Max(0, definition.weight);
        }

        public RarityTier ResolveMinimumRarity(ModifierDefinitionSO definition)
        {
            if (definition == null)
                return RarityTier.Common;
            if (TryGetEntry(definition.sectionId, out ModifierBalanceOverlayEntry entry) && entry.overrideMinimumRarity)
                return entry.minimumRarity;
            return definition.minimumRarity;
        }

        public string ComputeConfigHash() => BalanceConfigHashUtility.HashModifierOverlay(this);

        public static ModifierBalanceOverlaySO CreateRuntimeDefault()
        {
            var overlay = CreateInstance<ModifierBalanceOverlaySO>();
            overlay.hideFlags = HideFlags.HideAndDontSave;
            overlay.overlayId = "none";
            overlay.configVersion = 1;
            return overlay;
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            if (string.IsNullOrWhiteSpace(overlayId))
                overlayId = "unnamed_overlay";
            if (entries == null)
                entries = new List<ModifierBalanceOverlayEntry>();
            if (rarityTierWeightsPercent == null || rarityTierWeightsPercent.Length != 6)
                rarityTierWeightsPercent = new float[] { 45f, 30f, 17.5f, 5f, 2f, 0.5f };
        }
    }
}
