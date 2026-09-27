using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "CombatWallVisualProfile", menuName = "Chainbound/Combat/Presentation/Visual Profiles/Wall Visual Profile")]
    public class CombatWallVisualProfileSO : ScriptableObject
    {
        [Header("Art")]
        [Tooltip("Fallback sprite when a wall move omits wallPresentation.wallSprite.")]
        public Sprite defaultWallSprite;

        [Tooltip("Depth-writing material (e.g. DepthWriteUnlit). Matches CombatChainlingVisual.")]
        public Material defaultWallMaterial;

        public Color defaultTint = Color.white;

        [Tooltip("World-space bounding box for wall art. Default 5x5 matches CombatVisualProfileSO / chainlings.")]
        public Vector2 spriteSize = new Vector2(5f, 5f);

        [Tooltip("When enabled, the wall quad keeps the sprite pixel aspect ratio and fits inside spriteSize.")]
        public bool preserveSpriteAspect = true;

        [Tooltip("Multiplier applied to spriteSize when a move omits wallPresentation.wallSpriteScale.")]
        public Vector2 defaultScale = Vector2.one;

        [Header("Raise")]
        public float raiseDuration = 0.22f;
        public Ease raiseEase = Ease.OutBack;

        [Header("Hit")]
        public float hitShoveDistance = 0.08f;
        public float hitStretchY = 1.08f;
        public float hitOutDuration = 0.06f;
        public float hitRecoilDuration = 0.07f;
        public float hitSettleDuration = 0.08f;
        public Ease hitEase = Ease.OutQuad;

        [Header("Break")]
        public float breakDuration = 0.2f;
        public Ease breakEase = Ease.InBack;
    }
}
