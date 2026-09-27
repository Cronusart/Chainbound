using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingVariantDisplayUtility
    {
        public static string ResolveFormLabel(ChainlingPartyColumnContext ctx)
        {
            // Legacy alias: Form was overloaded with entangled + skin. Prefer Type + Variant.
            return ResolveVariantLabel(ctx);
        }

        /// <summary>
        /// Primary type for General view. Entangled is a type mutation prefix, not a Form.
        /// </summary>
        public static string ResolveTypeLabel(ChainlingPartyColumnContext ctx)
        {
            bool entangled = ctx?.entry?.snapshot != null &&
                ChainlingProgressionIdentityUtility.IsEntangled(ctx.entry.snapshot);

            ChainlingType primary = ChainlingType.None;
            if (entangled && ctx.entry.snapshot.entangledType != ChainlingType.None)
                primary = ctx.entry.snapshot.entangledType;
            else if (ctx?.species != null)
                primary = ctx.species.primaryType;

            if (primary == ChainlingType.None)
                return "—";

            string typeName = primary.ToString();
            return entangled ? $"Entangled {typeName}" : typeName;
        }

        /// <summary>
        /// Cosmetic / visual variant label aligned with Style + Loadout "Variant" slot.
        /// Entangled mutation is shown on Type; Variant stays Default unless a real skin id is equipped.
        /// </summary>
        public static string ResolveVariantLabel(ChainlingPartyColumnContext ctx)
        {
            ChainlingVisualIdentity identity = ctx?.entry?.snapshot != null
                ? ChainlingVisualIdentity.CloneOrDefault(ctx.entry.snapshot.visualIdentity)
                : ChainlingVisualIdentity.CreateDefault();

            string variantId = ChainlingVisualIdentity.NormalizeId(
                identity != null ? identity.visualVariantId : "",
                ChainlingVisualIdentity.BaseVariantId);

            if (IsDefaultVariant(variantId))
                return "Default";

            if (ctx?.visualDatabase != null && ctx.species != null)
            {
                ChainlingType entangledType = ChainlingType.None;
                if (ctx.entry?.snapshot != null &&
                    ChainlingProgressionIdentityUtility.IsEntangled(ctx.entry.snapshot))
                {
                    entangledType = ctx.entry.snapshot.entangledType;
                }

                CombatVisualProfileSO profile = ctx.visualDatabase.GetProfile(
                    ctx.species.speciesId,
                    variantId,
                    entangledType);
                if (profile != null && !string.IsNullOrWhiteSpace(profile.displayName))
                    return profile.displayName;
            }

            return ToTitle(variantId);
        }

        private static bool IsDefaultVariant(string id)
        {
            return string.IsNullOrWhiteSpace(id) ||
                string.Equals(id, ChainlingVisualIdentity.BaseVariantId, System.StringComparison.Ordinal) ||
                string.Equals(id, ChainlingVisualIdentity.DefaultId, System.StringComparison.Ordinal);
        }

        private static string ToTitle(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "Default";

            string[] pieces = id.Replace('-', '_').Split('_');
            for (int i = 0; i < pieces.Length; i++)
            {
                string piece = pieces[i];
                if (string.IsNullOrWhiteSpace(piece))
                    continue;

                pieces[i] = char.ToUpperInvariant(piece[0]) +
                    (piece.Length > 1 ? piece.Substring(1).ToLowerInvariant() : "");
            }

            return string.Join(" ", pieces);
        }
    }
}
