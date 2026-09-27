using UnityEngine;

namespace SignalJam.VisualFx
{
    [CreateAssetMenu(
        fileName = "PixelBattleVfxStyle",
        menuName = "Chainbound/VFX/Pixel Battle VFX Style")]
    public sealed class PixelBattleVfxStyleSO : ScriptableObject
    {
        [Tooltip("The shared UV grid used by combat Pixel Battle VFX before a preset multiplier is applied.")]
        [Range(1, 128)]
        [SerializeField] private int basePixelCells = 16;

        public int BasePixelCells => basePixelCells;

        private void OnValidate()
        {
            basePixelCells = Mathf.Clamp(basePixelCells, 1, 128);
        }
    }
}
