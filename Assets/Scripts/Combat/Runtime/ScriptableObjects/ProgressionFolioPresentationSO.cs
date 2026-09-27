using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public struct FolioRarityColor
    {
        public RarityTier rarity;
        public Color color;
    }

    /// <summary>Reusable tactile presentation and copy for post-battle progression folios.</summary>
    [CreateAssetMenu(fileName = "ProgressionFolioPresentation", menuName = "Chainbound/Combat/UI/Progression Folio Presentation")]
    public sealed class ProgressionFolioPresentationSO : ScriptableObject
    {
        [Header("Timing")]
        [Min(0.1f)] public float materializeSeconds = 0.34f;
        [Min(0f)] public float anticipationSeconds = 0.32f;
        [Min(0.1f)] public float masterySweepSeconds = 0.8f;
        [Min(0.1f)] public float minimumUnskippableSeconds = 0.45f;
        [Min(0f)] public float resolvedHoldSeconds = 0.65f;
        [Min(0.05f)] public float dismissSeconds = 0.18f;

        [Header("Materialization")]
        [Tooltip("Optional UI-compatible glitch material. It is used only during the initial digital-to-analog materialization.")]
        public Material uiGlitchMaterial;
        public string glitchStrengthProperty = "_GlitchIntensity";

        [Header("Audio")]
        public AudioClip folioIssuedClip;
        [Tooltip("Warm, neutral paper-settle cue. Never use an error buzz for a normal missed roll.")]
        public AudioClip noFolioClip;
        public AudioClip issuanceInterruptedClip;

        [Header("Palette")]
        public Color backdropColor = new Color(0.035f, 0.04f, 0.045f, 0.92f);
        public Color paperColor = new Color(0.91f, 0.84f, 0.68f, 1f);
        public Color inkColor = new Color(0.13f, 0.105f, 0.075f, 1f);
        public Color neutralSealColor = new Color(0.39f, 0.32f, 0.22f, 1f);
        public Color interruptedColor = new Color(0.55f, 0.18f, 0.14f, 1f);
        public List<FolioRarityColor> rarityColors = new List<FolioRarityColor>();

        [Header("Copy")]
        public string starRecordedTemplate = "STAR {0} RECORDED";
        public string masteryRecorded = "FIVE-STAR MASTERY RECORDED";
        public string issuedHeading = "FOLIO ISSUED";
        public string noFolioHeading = "NO FOLIO ISSUED";
        public string interruptedHeading = "ISSUANCE INTERRUPTED";
        public string pendingHeading = "ISSUANCE PENDING";
        public string noFolioBody = "No Known Move was learned during this milestone.";
        public string recordEnteredTemplate = "The milestone has been entered into {0}’s record.";
        public string firstCopyTemplate = "Fresh copy added to Known Moves.";
        public string duplicateCopyTemplate = "Additional copy added to Known Moves · Owned ×{0}";

        [Header("Known card print")]
        public SoundEventSO cardSlideSound;
        public SoundEventSO cardRiserSound;
        public SoundEventSO cardPrintSound;
        public Sprite libraryCardFront;
        public Sprite libraryCardOutline;
        [Min(0f)] public float riserHoldSeconds = 1.7f;
        [Min(1f)] public float settleOvershoot = 1.12f;

        public Color GetRarityColor(RarityTier rarity)
        {
            for (int i = 0; i < rarityColors.Count; i++)
            {
                if (rarityColors[i].rarity == rarity)
                    return rarityColors[i].color;
            }
            return neutralSealColor;
        }

        public static ProgressionFolioPresentationSO CreateRuntimeDefault()
        {
            var profile = CreateInstance<ProgressionFolioPresentationSO>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }
    }
}
