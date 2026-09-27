using System;

namespace Chainbound.Combat
{
    [Serializable]
    public class ChainlingVisualIdentity
    {
        public const string BaseVariantId = "base";
        public const string EntangledVariantId = "entangled";
        public const string NoneId = "none";
        public const string DefaultId = "default";

        public string visualVariantId = BaseVariantId;
        public string auraId = NoneId;
        public string hudCardVariantId = DefaultId;
        public string partyColumnVariantId = DefaultId;
        public string swapInFxId = DefaultId;
        public string swapOutFxId = DefaultId;
        public string koFxId = NoneId;
        public string victoryPoseId = DefaultId;
        public string idleStyleId = NoneId;
        public string idleFlourishId = NoneId;
        public string meleeAttackStyleId = NoneId;
        public string rangedAttackStyleId = NoneId;
        public string hitReactionStyleId = NoneId;
        public string attackerKoStyleId = NoneId;

        public static ChainlingVisualIdentity CreateDefault()
        {
            return new ChainlingVisualIdentity();
        }

        public static ChainlingVisualIdentity CloneOrDefault(ChainlingVisualIdentity source)
        {
            var copy = new ChainlingVisualIdentity();
            if (source == null)
                return copy;

            copy.visualVariantId = source.visualVariantId;
            copy.auraId = source.auraId;
            copy.hudCardVariantId = source.hudCardVariantId;
            copy.partyColumnVariantId = source.partyColumnVariantId;
            copy.swapInFxId = source.swapInFxId;
            copy.swapOutFxId = source.swapOutFxId;
            copy.koFxId = source.koFxId;
            copy.victoryPoseId = source.victoryPoseId;
            copy.idleStyleId = source.idleStyleId;
            copy.idleFlourishId = source.idleFlourishId;
            copy.meleeAttackStyleId = source.meleeAttackStyleId;
            copy.rangedAttackStyleId = source.rangedAttackStyleId;
            copy.hitReactionStyleId = source.hitReactionStyleId;
            copy.attackerKoStyleId = source.attackerKoStyleId;
            copy.Normalize();
            return copy;
        }

        public static ChainlingVisualIdentity CloneForPlayerSave(ChainlingVisualIdentity source)
        {
            var copy = CloneOrDefault(source);
            copy.Normalize();
            return copy;
        }

        /// <summary>
        /// Normalizes cosmetic identity ids. Entangled state is tracked via
        /// <see cref="ChainlingSnapshot.isEntangled"/> / <see cref="WildTraits.isEntangled"/>,
        /// not by rewriting <see cref="visualVariantId"/>.
        /// </summary>
        /// <param name="entangledFlag">Ignored; retained for call-site compatibility.</param>
        public void Normalize(bool entangledFlag = false)
        {
            _ = entangledFlag;
            visualVariantId = NormalizeBodySkinVariantId(visualVariantId);

            auraId = NormalizeId(auraId, NoneId);
            hudCardVariantId = NormalizeHudCardVariantId(hudCardVariantId);
            partyColumnVariantId = NormalizePartyColumnVariantId(partyColumnVariantId);
            swapInFxId = NormalizeId(swapInFxId, DefaultId);
            swapOutFxId = NormalizeId(swapOutFxId, DefaultId);
            koFxId = NormalizeId(koFxId, NoneId);
            victoryPoseId = NormalizeId(victoryPoseId, DefaultId);
            idleStyleId = NormalizeId(idleStyleId, NoneId);
            idleFlourishId = NormalizeId(idleFlourishId, NoneId);
            meleeAttackStyleId = NormalizeId(meleeAttackStyleId, NoneId);
            rangedAttackStyleId = NormalizeId(rangedAttackStyleId, NoneId);
            hitReactionStyleId = NormalizeId(hitReactionStyleId, NoneId);
            attackerKoStyleId = NormalizeId(attackerKoStyleId, NoneId);
        }

        public static string NormalizeId(string id, string fallback)
        {
            return string.IsNullOrWhiteSpace(id) ? fallback : id.Trim();
        }

        /// <summary>
        /// Body-skin ids only. HUD/FX sentinels (<see cref="DefaultId"/>, <see cref="NoneId"/>)
        /// and the legacy entangled skin id are not catalog variants.
        /// </summary>
        public static bool IsBodySkinVariantId(string id)
        {
            string normalized = NormalizeId(id, BaseVariantId);
            if (string.Equals(normalized, EntangledVariantId, StringComparison.Ordinal))
                return false;
            if (string.Equals(normalized, NoneId, StringComparison.Ordinal))
                return false;
            if (string.Equals(normalized, DefaultId, StringComparison.Ordinal))
                return false;
            return true;
        }

        public static string NormalizeBodySkinVariantId(string id)
        {
            string normalized = NormalizeId(id, BaseVariantId);
            return IsBodySkinVariantId(normalized) ? normalized : BaseVariantId;
        }

        public static string NormalizeHudCardVariantId(string id)
        {
            string normalized = NormalizeId(id, DefaultId);
            return string.Equals(normalized, NoneId, StringComparison.Ordinal)
                ? DefaultId
                : normalized;
        }

        public static string NormalizePartyColumnVariantId(string id)
        {
            string normalized = NormalizeId(id, DefaultId);
            return string.Equals(normalized, NoneId, StringComparison.Ordinal)
                ? DefaultId
                : normalized;
        }

    }
}
