using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Serialized button chrome (sliced idle sprite + shimmer frames) that a screen can hand to
    /// buttons it authors up front and to rows it spawns at runtime.
    /// </summary>
    [Serializable]
    public sealed class PlayerMenuChromePalette
    {
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite focusSprite;
        [SerializeField] private List<Sprite> shimmerFrames = new List<Sprite>();
        [SerializeField, Min(0.01f)] private float pixelsPerUnitMultiplier = 0.2f;
        [SerializeField, Min(1f)] private float shimmerFps = 12f;
        [SerializeField] private bool shimmerFollowsFocus = true;

        public Sprite IdleSprite => idleSprite;

        public Sprite FocusSprite => focusSprite;

        public float PixelsPerUnitMultiplier => pixelsPerUnitMultiplier;

        public bool ShimmerFollowsFocus => shimmerFollowsFocus;

        public IReadOnlyList<Sprite> ShimmerFrames => shimmerFrames;

        public void Configure(
            Sprite idle,
            Sprite focus,
            IReadOnlyList<Sprite> frames,
            float pixelsPerUnit,
            float fps,
            bool followFocus)
        {
            idleSprite = idle;
            focusSprite = focus;
            pixelsPerUnitMultiplier = Mathf.Max(0.01f, pixelsPerUnit);
            shimmerFps = Mathf.Max(1f, fps);
            shimmerFollowsFocus = followFocus;

            shimmerFrames.Clear();
            if (frames == null)
                return;

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] != null)
                    shimmerFrames.Add(frames[i]);
            }
        }

        /// <summary>
        /// Applies the palette and removes the legacy focus ring so navigation reads as shimmer only.
        /// </summary>
        public PlayerMenuChromeButtonVisual Apply(Selectable selectable)
        {
            if (selectable == null)
                return null;

            StripFocusRing(selectable.gameObject);

            PlayerMenuChromeButtonVisual visual =
                PlayerMenuUiFactory.GetOrAdd<PlayerMenuChromeButtonVisual>(selectable.gameObject);
            visual.Configure(
                idleSprite,
                focusSprite,
                Color.white,
                Color.white,
                shimmerFrames,
                shimmerFps,
                true,
                pixelsPerUnitMultiplier,
                shimmerFollowsFocus);
            visual.SetSelected(false);
            return visual;
        }

        public static void StripFocusRing(GameObject host)
        {
            if (host == null)
                return;

            MenuSelectableFocusVisual focus = host.GetComponent<MenuSelectableFocusVisual>();
            if (focus != null)
                DestroyComponent(focus);

            Transform frame = host.transform.Find("FocusFrame");
            if (frame != null)
                DestroyComponent(frame.gameObject);
        }

        private static void DestroyComponent(UnityEngine.Object target)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
