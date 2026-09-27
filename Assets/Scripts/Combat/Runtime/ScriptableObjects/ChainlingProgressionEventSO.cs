using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class WeightMultiplierPatch
    {
        public string moveId;
        [Min(0f)] public float multiplier = 1f;
    }

    [Serializable]
    public class SpeciesProgressionEventPatch
    {
        [Tooltip("Empty applies to all species.")]
        public string speciesId;

        [Tooltip("Additive percent bonus to the gate roll for this species/global patch.")]
        public int starGateBonusPercent;

        public List<SpeciesProgressionMoveEntry> addedMoves = new List<SpeciesProgressionMoveEntry>();
        public List<WeightMultiplierPatch> weightMultipliers = new List<WeightMultiplierPatch>();
    }

    [CreateAssetMenu(fileName = "ChainlingProgressionEvent", menuName = "Chainbound/Combat/Progression/Event")]
    public class ChainlingProgressionEventSO : ScriptableObject
    {
        public string eventId;
        public bool enabled;
        public string displayName;
        public List<SpeciesProgressionEventPatch> patches = new List<SpeciesProgressionEventPatch>();

        private void OnValidate()
        {
            if (patches == null)
                patches = new List<SpeciesProgressionEventPatch>();

            for (int i = 0; i < patches.Count; i++)
            {
                SpeciesProgressionEventPatch patch = patches[i];
                if (patch == null)
                    continue;

                patch.starGateBonusPercent = Mathf.Clamp(patch.starGateBonusPercent, -100, 100);
                if (patch.addedMoves == null)
                    patch.addedMoves = new List<SpeciesProgressionMoveEntry>();
                if (patch.weightMultipliers == null)
                    patch.weightMultipliers = new List<WeightMultiplierPatch>();

                for (int j = 0; j < patch.addedMoves.Count; j++)
                {
                    SpeciesProgressionMoveEntry entry = patch.addedMoves[j];
                    if (entry == null)
                        continue;

                    entry.poolTag = ProgressionPoolTag.EventOnly;
                    if (entry.weight < 0)
                        entry.weight = 0;
                    if (entry.maxStar == 0)
                        entry.maxStar = CombatConstants.STAR_MAX;
                    if (entry.maxStar < entry.minStar)
                        entry.maxStar = entry.minStar;
                }

                for (int j = 0; j < patch.weightMultipliers.Count; j++)
                {
                    WeightMultiplierPatch multiplier = patch.weightMultipliers[j];
                    if (multiplier != null && multiplier.multiplier < 0f)
                        multiplier.multiplier = 0f;
                }
            }
        }
    }
}
