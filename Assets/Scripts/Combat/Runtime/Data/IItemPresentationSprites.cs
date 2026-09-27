using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Combat-safe sprite lookup for inventory/capture items.
    /// Implemented by MenuNavigation's presentation catalog; Combat.Runtime must not reference that assembly.
    /// </summary>
    public interface IItemPresentationSprites
    {
        bool TryGetItemSprites(string itemId, out Sprite rowIcon, out Sprite detailPortrait);
    }
}
