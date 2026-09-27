using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [CreateAssetMenu(
        fileName = "PlayerMenuTabButtonVisualProfile",
        menuName = "Chainbound/UI/Player Menu/Tab Button Visual Profile")]
    public sealed class PlayerMenuTabButtonVisualProfileSO : ScriptableObject
    {
        [Header("Sprites")]
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Image.Type imageType = Image.Type.Sliced;
        [SerializeField] private bool preserveAspect;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color lockedColor = new Color(0.62f, 0.62f, 0.62f, 1f);

        [Header("Motion")]
        [SerializeField, Min(1f)] private float selectedScale = 1.05f;
        [SerializeField, Min(0.01f)] private float focusDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float unfocusDuration = 0.14f;
        [SerializeField] private Ease focusEase = Ease.OutSine;
        [SerializeField] private Ease unfocusEase = Ease.InOutSine;
        [SerializeField, Range(0.01f, 1f)] private float pressScale = 0.94f;
        [SerializeField, Min(0.01f)] private float pressDuration = 0.08f;
        [SerializeField] private Ease pressEase = Ease.OutSine;
        [SerializeField] private Ease pressReturnEase = Ease.OutBack;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Shimmer")]
        [SerializeField] private List<Sprite> shimmerFrames = new List<Sprite>();
        [SerializeField, Min(1f)] private float shimmerFps = 12f;
        [SerializeField, Min(0f)] private float shimmerPauseDuration = 0.6f;

        public Sprite NormalSprite => normalSprite;
        public Sprite SelectedSprite => selectedSprite;
        public Sprite LockedSprite => lockedSprite != null ? lockedSprite : normalSprite;
        public Image.Type ImageType => imageType;
        public bool PreserveAspect => preserveAspect;
        public Color NormalColor => normalColor;
        public Color SelectedColor => selectedColor;
        public Color LockedColor => lockedColor;
        public float SelectedScale => Mathf.Max(1f, selectedScale);
        public float FocusDuration => Mathf.Max(0.01f, focusDuration);
        public float UnfocusDuration => Mathf.Max(0.01f, unfocusDuration);
        public Ease FocusEase => focusEase;
        public Ease UnfocusEase => unfocusEase;
        public float PressScale => Mathf.Clamp(pressScale, 0.01f, 1f);
        public float PressDuration => Mathf.Max(0.01f, pressDuration);
        public Ease PressEase => pressEase;
        public Ease PressReturnEase => pressReturnEase;
        public bool UseUnscaledTime => useUnscaledTime;
        public IReadOnlyList<Sprite> ShimmerFrames => shimmerFrames;
        public float ShimmerFps => Mathf.Max(1f, shimmerFps);
        public float ShimmerPauseDuration => Mathf.Max(0f, shimmerPauseDuration);

        public bool HasShimmerFrames
        {
            get
            {
                if (shimmerFrames == null)
                    return false;

                for (int i = 0; i < shimmerFrames.Count; i++)
                {
                    if (shimmerFrames[i] != null)
                        return true;
                }

                return false;
            }
        }
    }
}
