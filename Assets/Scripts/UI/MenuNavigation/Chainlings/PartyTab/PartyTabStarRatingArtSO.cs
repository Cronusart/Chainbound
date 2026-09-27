using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Baked composite star shimmer frames exported from Aseprite (full cell per frame).
    /// Empty star is a single static sprite at the same canvas size as each shimmer frame.
    /// </summary>
    [CreateAssetMenu(fileName = "PartyTabStarRatingArt", menuName = "Chainbound/UI/Party Tab Star Rating Art")]
    public sealed class PartyTabStarRatingArtSO : ScriptableObject
    {
        [Header("Sprites")]
        [Tooltip("Dim outline star for unfilled slots (same pixel size as shimmer frames).")]
        [SerializeField] private Sprite emptyStar;

        [Tooltip("Baked filled-star shimmer loop; each frame is a full cell composite.")]
        [SerializeField] private Sprite[] filledShimmerFrames;

        [Header("Layout")]
        [SerializeField] private float cellSize = 40f;
        [SerializeField] private float spacing = 8f;

        [Header("Animation")]
        [SerializeField] private float fps = 10f;
        [Tooltip("Delay between starting shimmer on each filled slot (Lead → Bench wave).")]
        [SerializeField] private float staggerSeconds = 0.04f;

        public Sprite EmptyStar => emptyStar;
        public Sprite[] FilledShimmerFrames => filledShimmerFrames;
        public float CellSize => cellSize;
        public float Spacing => spacing;
        public float Fps => fps;
        public float StaggerSeconds => staggerSeconds;

        public bool HasFilledShimmer => filledShimmerFrames != null && filledShimmerFrames.Length > 0;
    }
}
