using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "ChainlingHudCardVariant_", menuName = "Chainbound/Combat/Chainlings/Cosmetics/HUD Card Variant")]
    public class ChainlingHudCardVariantDefinitionSO : ChainlingVisualDefinitionSO
    {
        [Header("Card Background")]
        public Sprite normalSprite;
        public Sprite activeSprite;
        public Sprite targetSprite;
        public Sprite activeTargetSprite;

        [Header("HP Frame Overlay")]
        public Sprite hpFrameOverlayNormal;
        public Sprite hpFrameOverlayActive;
        public Sprite hpFrameOverlayTarget;
        public Sprite hpFrameOverlayActiveTarget;
    }
}
