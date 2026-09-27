using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Compact static star art for dense UI (Inventory Use picker, combat HUD).
    /// No shimmer frames — filled/empty are single sprites. Layout is right-to-left by default.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CompactStarRatingArt",
        menuName = "Chainbound/Combat/UI/Compact Star Rating Art",
        order = 45)]
    public sealed class CompactStarRatingArtSO : ScriptableObject
    {
        [Header("Sprites")]
        [Tooltip("Filled star (e.g. smol_star).")]
        [SerializeField] private Sprite filledStar;

        [Tooltip("Optional empty/dim star. If null, filledStar is shown tinted by EmptyTint.")]
        [SerializeField] private Sprite emptyStar;

        [Header("Tints")]
        [SerializeField] private Color filledTint = Color.white;
        [SerializeField] private Color emptyTint = new Color(1f, 1f, 1f, 0.35f);

        [Header("Layout")]
        [SerializeField] private float cellSize = 18f;
        [SerializeField] private float spacing = 2f;
        [Tooltip("When true, stars fill from the right edge leftward.")]
        [SerializeField] private bool fillRightToLeft = true;

        [Header("Presentation")]
        [SerializeField] private bool animateFilledPresentation = true;
        [SerializeField, Min(0f)] private float filledStaggerSeconds = 0.045f;
        [SerializeField, Min(0f)] private float filledScaleInDuration = 0.12f;
        [SerializeField, Min(0f)] private float filledSettleDuration = 0.11f;
        [SerializeField] private float filledStartScale = 0.72f;
        [SerializeField] private float filledOvershootScale = 1.12f;

        public Sprite FilledStar => filledStar;
        public Sprite EmptyStar => emptyStar != null ? emptyStar : filledStar;
        public Color FilledTint => filledTint;
        public Color EmptyTint => emptyTint;
        public float CellSize => cellSize;
        public float Spacing => spacing;
        public bool FillRightToLeft => fillRightToLeft;
        public bool AnimateFilledPresentation => animateFilledPresentation;
        public float FilledStaggerSeconds => filledStaggerSeconds;
        public float FilledScaleInDuration => filledScaleInDuration;
        public float FilledSettleDuration => filledSettleDuration;
        public float FilledStartScale => filledStartScale;
        public float FilledOvershootScale => filledOvershootScale;

        public void WarmPresentation()
        {
            TouchSprite(filledStar);
            TouchSprite(emptyStar);
        }

        private static void TouchSprite(Sprite sprite)
        {
            if (sprite == null)
                return;
            Texture2D texture = sprite.texture;
            if (texture != null)
                _ = texture.width;
        }
    }
}
