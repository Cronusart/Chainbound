using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public readonly struct ChainlingPartyBadgeDisplay
    {
        public ChainlingPartyBadgeDisplay(
            string badgeId,
            string displayName,
            string description,
            Sprite icon,
            bool unlocked,
            float progress01,
            long currentValue,
            long threshold)
        {
            BadgeId = badgeId ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            Icon = icon;
            Unlocked = unlocked;
            Progress01 = progress01;
            CurrentValue = currentValue;
            Threshold = threshold;
        }

        public string BadgeId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public bool Unlocked { get; }
        public float Progress01 { get; }
        public long CurrentValue { get; }
        public long Threshold { get; }
    }
}
