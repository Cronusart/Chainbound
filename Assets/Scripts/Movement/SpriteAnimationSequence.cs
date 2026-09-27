using UnityEngine;

/// <summary>
/// Container for a sprite animation sequence (multiple frames for one direction).
/// Used by MeshRendererPlayerVisual to support multi-frame animations.
/// </summary>
[System.Serializable]
public class SpriteAnimationSequence
{
    [Tooltip("Sprites for this animation sequence (e.g., 7-8 frames for walk animation)")]
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

