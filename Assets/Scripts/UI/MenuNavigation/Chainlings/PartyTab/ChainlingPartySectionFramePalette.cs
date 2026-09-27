using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public enum ChainlingPartyColumnOpacityState
    {
        PartySectionInactive,
        PartySectionActive,
        ColumnFocused,
    }

    /// <summary>
    /// Authoring palette for party-tab section frame colors. Lives on ColumnsRoot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingPartySectionFramePalette : MonoBehaviour
    {
        [Header("View Rail Section Frame")]
        [SerializeField] private Color viewRailActiveColor = new Color(0.9764706f, 0.7607843f, 0.16862746f, 1f);
        [SerializeField] private Color viewRailInactiveColor = new Color(0.9764706f, 0.7607843f, 0.16862746f, 0f);

        [Header("Party Section Frame")]
        [SerializeField] private Color partyActiveColor = new Color(0.9764706f, 0.7607843f, 0.16862746f, 1f);
        [SerializeField] private Color partyInactiveColor = new Color(0.9764706f, 0.7607843f, 0.16862746f, 0f);

        [Header("Legacy Column Opacity")]
        [Tooltip("Retained for prefab compatibility. Current column focus uses scale, not opacity.")]
        [Range(0f, 1f)]
        [SerializeField] private float partySectionInactiveColumnAlpha = 1f;
        [Tooltip("Retained for prefab compatibility. Current column focus uses scale, not opacity.")]
        [Range(0f, 1f)]
        [SerializeField] private float partySectionActiveColumnAlpha = 1f;
        [Tooltip("Retained for prefab compatibility. Current column focus uses scale, not opacity.")]
        [Range(0f, 1f)]
        [SerializeField] private float columnFocusedAlpha = 1f;

        [Header("Legacy Visual Stage Opacity")]
        [Tooltip("Retained for prefab compatibility. Current column focus uses scale, not opacity.")]
        [Range(0f, 1f)]
        [SerializeField] private float partySectionActiveVisualStageAlpha = 1f;

        public Color GetActiveColor(ChainlingPartySectionFocusTargetKind target)
        {
            return target == ChainlingPartySectionFocusTargetKind.ViewRail
                ? viewRailActiveColor
                : partyActiveColor;
        }

        public Color GetInactiveColor(ChainlingPartySectionFocusTargetKind target)
        {
            return target == ChainlingPartySectionFocusTargetKind.ViewRail
                ? viewRailInactiveColor
                : partyInactiveColor;
        }

        public float GetColumnOpacity(ChainlingPartyColumnOpacityState state)
        {
            return 1f;
        }

        public float GetVisualStageOpacity(ChainlingPartyColumnOpacityState state)
        {
            return 1f;
        }
    }
}
