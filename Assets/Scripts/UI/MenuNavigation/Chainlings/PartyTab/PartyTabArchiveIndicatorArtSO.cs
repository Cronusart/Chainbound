using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [CreateAssetMenu(
        fileName = "PartyTabArchiveIndicatorArt",
        menuName = "Chainbound/UI/Party Tab Archive Indicator Art")]
    public sealed class PartyTabArchiveIndicatorArtSO : ScriptableObject
    {
        [Header("Sprites")]
        [SerializeField] private Sprite dossierSprite;
        [SerializeField] private Sprite anchoredSprite;

        [Header("Fallback Glyphs")]
        [SerializeField] private string dossierFallbackGlyph = "P";
        [SerializeField] private string anchoredFallbackGlyph = "B";

        [Header("Presentation")]
        [SerializeField] private Vector2 indicatorSize = new Vector2(34f, 34f);
        [SerializeField] private Color dossierTint = new Color(0.68f, 0.76f, 0.86f, 1f);
        [SerializeField] private Color anchoredTint = new Color(1f, 0.72f, 0.30f, 1f);

        public Vector2 IndicatorSize => indicatorSize;

        public Sprite ResolveSprite(ArchiveBindingState state)
        {
            return state == ArchiveBindingState.Anchored ? anchoredSprite : dossierSprite;
        }

        public string ResolveFallbackGlyph(ArchiveBindingState state)
        {
            string value = state == ArchiveBindingState.Anchored ? anchoredFallbackGlyph : dossierFallbackGlyph;
            return string.IsNullOrWhiteSpace(value) ? (state == ArchiveBindingState.Anchored ? "B" : "P") : value;
        }

        public Color ResolveTint(ArchiveBindingState state)
        {
            return state == ArchiveBindingState.Anchored ? anchoredTint : dossierTint;
        }

        public static Color FallbackTint(ArchiveBindingState state)
        {
            return state == ArchiveBindingState.Anchored
                ? new Color(1f, 0.72f, 0.30f, 1f)
                : new Color(0.68f, 0.76f, 0.86f, 1f);
        }
    }
}
