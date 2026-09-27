using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    public sealed class CandyMachineRevealData
    {
        public CandyMachineMode Mode { get; }
        public string StepOneCaption { get; }
        public string StepOneValue { get; }
        public string StepTwoCaption { get; }
        public string StepTwoValue { get; }
        public string StepThreeCaption { get; }
        public string StepThreeValue { get; }
        public string Details { get; }
        public Sprite TypeIcon { get; }
        public string PowerText { get; }
        public string AccuracyText { get; }
        public string ApText { get; }
        public string MoveName { get; }
        public string StatsInk { get; }
        public string Mod1Name { get; }
        public string Mod2Name { get; }
        public string Mod1Effect { get; }
        public string Mod2Effect { get; }
        public RarityTier MoveRarity { get; }
        public bool HasMod1Rarity { get; }
        public RarityTier Mod1Rarity { get; }
        public bool HasMod2Rarity { get; }
        public RarityTier Mod2Rarity { get; }

        public bool HasMoveStats =>
            TypeIcon != null
            || !string.IsNullOrWhiteSpace(PowerText)
            || !string.IsNullOrWhiteSpace(AccuracyText)
            || !string.IsNullOrWhiteSpace(ApText);

        public bool IsStamp => Mode == CandyMachineMode.Stamp;

        public CandyMachineRevealData(
            CandyMachineMode mode,
            string stepOneCaption,
            string stepOneValue,
            string stepTwoCaption,
            string stepTwoValue,
            string stepThreeCaption,
            string stepThreeValue,
            string details,
            Sprite typeIcon = null,
            string powerText = null,
            string accuracyText = null,
            string apText = null,
            string moveName = null,
            string statsInk = null,
            string mod1Name = null,
            string mod2Name = null,
            string mod1Effect = null,
            string mod2Effect = null,
            RarityTier moveRarity = RarityTier.Common,
            bool hasMod1Rarity = false,
            RarityTier mod1Rarity = RarityTier.Common,
            bool hasMod2Rarity = false,
            RarityTier mod2Rarity = RarityTier.Common)
        {
            Mode = mode;
            StepOneCaption = stepOneCaption ?? string.Empty;
            StepOneValue = stepOneValue ?? string.Empty;
            StepTwoCaption = stepTwoCaption ?? string.Empty;
            StepTwoValue = stepTwoValue ?? string.Empty;
            StepThreeCaption = stepThreeCaption ?? string.Empty;
            StepThreeValue = stepThreeValue ?? string.Empty;
            Details = details ?? string.Empty;
            TypeIcon = typeIcon;
            PowerText = powerText ?? string.Empty;
            AccuracyText = accuracyText ?? string.Empty;
            ApText = apText ?? string.Empty;
            MoveName = string.IsNullOrWhiteSpace(moveName) ? StepThreeValue : moveName;
            StatsInk = statsInk ?? string.Empty;
            Mod1Name = mod1Name ?? string.Empty;
            Mod2Name = mod2Name ?? string.Empty;
            Mod1Effect = mod1Effect ?? string.Empty;
            Mod2Effect = mod2Effect ?? string.Empty;
            MoveRarity = moveRarity;
            HasMod1Rarity = hasMod1Rarity && !string.IsNullOrWhiteSpace(Mod1Name);
            Mod1Rarity = mod1Rarity;
            HasMod2Rarity = hasMod2Rarity && !string.IsNullOrWhiteSpace(Mod2Name);
            Mod2Rarity = mod2Rarity;
        }
    }
}
