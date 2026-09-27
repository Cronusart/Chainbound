using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "ChainlingNameplateFrame_", menuName = "Chainbound/Combat/Chainlings/Legacy Cosmetics/Nameplate Frame")]
    public class ChainlingNameplateFrameDefinitionSO : ChainlingVisualDefinitionSO
    {
        public Sprite frameSprite;
        public SpriteAnimationSequence frameAnimation = new SpriteAnimationSequence();
        [Min(0.01f)] public float fps = 8f;
    }
}
