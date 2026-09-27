namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public readonly struct ChainlingPartyDisplayLine
    {
        public ChainlingPartyDisplayLine(string label, string value, bool isMuted = false)
        {
            Label = label ?? "";
            Value = value ?? "";
            IsMuted = isMuted;
        }

        public string Label { get; }
        public string Value { get; }
        public bool IsMuted { get; }
    }
}
