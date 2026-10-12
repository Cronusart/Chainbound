namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// The Colosseum player-menu tab stays available in the Editor and development
    /// builds so the two-client matrix can open the casual queue. Release player
    /// builds omit it. The hub Colosseum object is the player path.
    /// </summary>
    public static class ColosseumMenuVisibility
    {
        public const string TabId = "colosseum";

        /// <summary>Test seam. Null uses the compile-time player-build rule.</summary>
        public static bool? OverrideForTests;

        public static bool ShowInPlayerMenu
        {
            get
            {
                if (OverrideForTests.HasValue)
                    return OverrideForTests.Value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsMenuTabAllowed(string tabId)
        {
            return ShowInPlayerMenu || !IsColosseum(tabId);
        }

        /// <summary>
        /// Same length as <paramref name="definitions"/> so tab indices stay aligned.
        /// A hidden Colosseum slot is null, which the tab bar does not draw.
        /// </summary>
        public static MenuTabDefinitionSO[] ForMenuBar(MenuTabDefinitionSO[] definitions)
        {
            if (definitions == null || ShowInPlayerMenu)
                return definitions;

            var visible = new MenuTabDefinitionSO[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                MenuTabDefinitionSO definition = definitions[i];
                visible[i] = definition != null && IsColosseum(definition.TabId) ? null : definition;
            }

            return visible;
        }

        private static bool IsColosseum(string tabId)
        {
            return string.Equals(tabId, TabId, System.StringComparison.Ordinal);
        }
    }
}
