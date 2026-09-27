using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public enum BattleIntroTraitSlot
    {
        Mutated = 0,
        Entangled = 1,
        Name = 2,
        Variant = 3,
    }

    public enum BattleIntroTraitColorSource
    {
        None = 0,
        Solid = 1,
        MutatedPalette = 2,
        EntangledTypeDisplay = 3,
        VariantProfile = 4,
        VfxAccent = 5,
    }

    public enum BattleIntroCopySpanKind
    {
        Mutated = 0,
        Entangled = 1,
        Name = 2,
        Variant = 3,
    }

    [Serializable]
    public struct BattleIntroCopySpan
    {
        public int visibleStart;
        public int visibleLength;
        public BattleIntroCopySpanKind kind;
        public ChainlingType entangledType;
    }

    [Serializable]
    public struct BattleIntroCopySubject
    {
        public string displayName;
        public bool isMutated;
        public bool isEntangled;
        public ChainlingType entangledType;
        public bool isVariant;

        public static BattleIntroCopySubject FromChainling(ChainlingInstance chainling)
        {
            string name = chainling != null && !string.IsNullOrWhiteSpace(chainling.displayName)
                ? chainling.displayName.Trim()
                : !string.IsNullOrWhiteSpace(chainling?.speciesId)
                    ? chainling.speciesId.Trim()
                    : "Unknown";

            return new BattleIntroCopySubject
            {
                displayName = name,
                isMutated = IsMutated(chainling),
                isEntangled = chainling != null && chainling.isEntangled,
                entangledType = chainling != null && chainling.isEntangled
                    ? chainling.entangledType
                    : ChainlingType.None,
                isVariant = IsVisualVariant(chainling),
            };
        }

        public static bool IsMutated(ChainlingInstance chainling)
        {
            if (chainling == null)
                return false;

            if (chainling.isMutated)
                return true;

            string auraId = ChainlingVisualIdentity.NormalizeId(
                chainling.visualIdentity != null ? chainling.visualIdentity.auraId : null,
                ChainlingVisualIdentity.NoneId);
            return !string.Equals(auraId, ChainlingVisualIdentity.NoneId, StringComparison.Ordinal);
        }

        public static bool IsVisualVariant(ChainlingInstance chainling)
        {
            string variantId = ChainlingVisualIdentity.NormalizeId(
                chainling?.visualIdentity != null ? chainling.visualIdentity.visualVariantId : null,
                ChainlingVisualIdentity.BaseVariantId);
            return !string.Equals(variantId, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal) &&
                !string.Equals(variantId, ChainlingVisualIdentity.DefaultId, StringComparison.Ordinal);
        }
    }

    public sealed class BattleIntroCopyResult
    {
        public string richText = string.Empty;
        public int visibleLength;
        public List<BattleIntroCopySpan> spans = new List<BattleIntroCopySpan>();
    }
}
