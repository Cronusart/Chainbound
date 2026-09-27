using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingPartyVitalsDisplayUtility
    {
        public static ChainlingPartyColumnVitals Build(ChainlingPartyColumnContext ctx)
        {
            if (ctx == null || !ctx.HasFilledChainling || ctx.entry?.snapshot == null || ctx.species == null)
                return null;

            ChainlingVitalsDisplay display = ChainlingVitalsUtility.BuildDisplay(ctx.entry.snapshot, ctx.species);
            return new ChainlingPartyColumnVitals
            {
                currentHp = display.CurrentHp,
                maxHp = display.MaxHp,
                fill01 = display.Fill01,
                isFainted = display.IsFainted,
                label = display.Label,
            };
        }
    }
}
