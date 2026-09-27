using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(
        fileName = "ChainlingPartyColumnVariant_",
        menuName = "Chainbound/Combat/Chainlings/Cosmetics/Party Column Variant")]
    public class ChainlingPartyColumnVariantDefinitionSO : ChainlingVisualDefinitionSO
    {
        [Header("Card Chrome")]
        public Sprite staticCardFrameSprite;
        public SpriteAnimationSequence cardFrameSequence = new SpriteAnimationSequence();
        [Min(0.01f)] public float cardFrameFps = 8f;
        public bool cardFrameLoop = true;

        [Header("Background")]
        public Sprite backgroundSprite;
        public bool overrideBackgroundColor;
        public Color backgroundColorTint = new Color(0.07f, 0.095f, 0.14f, 0.92f);

        [Header("Info Panel (Deprecated)")]
        [Tooltip("Deprecated. InfoPanel/Background is authored on the party-column prefab and is no longer changed by party column variants.")]
        public Sprite infoPanelBackgroundSprite;

        [Header("Empty Slot")]
        public bool useEmptyTint = true;
        public Color emptyTintColor = new Color(0.07f, 0.095f, 0.14f, 1f);
        [Range(0f, 1f)] public float emptyTintAlpha = 0.55f;

        [Header("Selection Outline")]
        public Sprite selectionOutlineSprite;
        public SpriteAnimationSequence selectionOutlineSequence = new SpriteAnimationSequence();
        [Min(0.01f)] public float selectionOutlineFps = 10f;
        public bool selectionOutlineLoop = true;
        public Vector4 selectionOutlineInset = new Vector4(-10f, -4f, -10f, -4f);
        public bool overrideSelectionOutlineColor;
        public Color selectionOutlineColor = new Color(0.2f, 0.85f, 0.75f, 0.95f);

        public bool HasCardFrameAnimation => cardFrameSequence != null && cardFrameSequence.Length > 0;
        public bool HasSelectionOutlineAnimation => selectionOutlineSequence != null && selectionOutlineSequence.Length > 0;
    }
}
