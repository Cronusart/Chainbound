using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Container for a sprite animation sequence (multiple frames for one state).
    /// Combat-local copy so the Runtime assembly doesn't depend on Assembly-CSharp.
    /// Functionally identical to the exploration-side SpriteAnimationSequence.
    /// </summary>
    [System.Serializable]
    public class SpriteAnimationSequence
    {
        [Tooltip("Sprites for this animation sequence.")]
        public Sprite[] sprites = new Sprite[0];

        public SpriteAnimationSequence() { }

        public SpriteAnimationSequence(Sprite[] sprites)
        {
            this.sprites = sprites;
        }

        public int Length => sprites != null ? sprites.Length : 0;

        public Sprite this[int index]
        {
            get
            {
                if (sprites == null || index < 0 || index >= sprites.Length)
                    return null;
                return sprites[index];
            }
            set
            {
                if (sprites == null || index < 0 || index >= sprites.Length)
                    return;
                sprites[index] = value;
            }
        }
    }
}
