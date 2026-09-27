namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Player-facing labels for party view modes. Enum may lag (Detail → Entry).
    /// </summary>
    public static class ChainlingPartyViewModeLabels
    {
        public static string GetDisplayName(ChainlingPartyViewMode mode)
        {
            switch (mode)
            {
                case ChainlingPartyViewMode.Detail:
                    return "Entry";
                default:
                    return mode.ToString();
            }
        }
    }
}
