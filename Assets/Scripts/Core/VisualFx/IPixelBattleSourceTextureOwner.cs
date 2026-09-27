using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Identifies a component that owns the current source texture of a Pixel Battle renderer.
    /// </summary>
    public interface IPixelBattleSourceTextureOwner
    {
        bool OwnsPixelBattleSourceTexture(Renderer renderer, int materialSlotIndex);
    }
}
