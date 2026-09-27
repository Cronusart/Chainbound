using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Binds a cycle numeral sprite from <see cref="CycleIconPaletteSO"/> onto a UI Image.
    /// Safe for Inventory Use rows and future combat HUD cards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CycleIconDisplay : MonoBehaviour
    {
        [SerializeField] private CycleIconPaletteSO palette;
        [SerializeField] private Image iconImage;
        [SerializeField] private bool hideWhenMissing = true;

        public Image IconImage => iconImage;

        public void Configure(CycleIconPaletteSO cyclePalette)
        {
            if (cyclePalette != null)
                palette = cyclePalette;
        }

        public void EnsureDefaultLayoutForEditor()
        {
            if (iconImage == null)
                iconImage = GetComponent<Image>();
            if (iconImage == null)
                iconImage = gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
        }

        public void SetCycleFromRebirthCount(int rebirthCount, bool visible = true)
        {
            EnsureDefaultLayoutForEditor();
            if (!visible)
            {
                if (iconImage != null)
                    iconImage.enabled = false;
                return;
            }

            Sprite sprite = palette != null ? palette.GetIconForRebirthCount(rebirthCount) : null;
            if (iconImage == null)
                return;

            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null || !hideWhenMissing;
            if (sprite == null && hideWhenMissing)
                iconImage.enabled = false;
            else if (sprite != null)
                iconImage.color = Color.white;
        }
    }
}
