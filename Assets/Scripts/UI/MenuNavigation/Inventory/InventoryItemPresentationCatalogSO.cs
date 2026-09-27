using System;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    [Serializable]
    public sealed class InventoryItemPresentationEntry
    {
        public string itemId;
        public Sprite rowIcon;
        public Sprite detailPortrait;
        public Color fallbackTint = Color.white;
        public Color accentColor = Color.white;
        public string displayNameOverride;
    }

    [CreateAssetMenu(
        fileName = "InventoryItemPresentationCatalog",
        menuName = "Chainbound/UI/Inventory/Item Presentation Catalog",
        order = 60)]
    public sealed class InventoryItemPresentationCatalogSO : ScriptableObject, IItemPresentationSprites
    {
        [SerializeField] private Sprite fallbackRowIcon;
        [SerializeField] private Sprite fallbackDetailPortrait;
        [SerializeField] private Color fallbackTint = new Color(0.2f, 0.85f, 0.75f, 0.92f);
        [SerializeField] private List<InventoryItemPresentationEntry> entries = new List<InventoryItemPresentationEntry>();

        private Dictionary<string, InventoryItemPresentationEntry> lookup;

        public Sprite FallbackRowIcon => fallbackRowIcon;
        public Sprite FallbackDetailPortrait => fallbackDetailPortrait;
        public Color FallbackTint => fallbackTint;

        public bool TryResolve(string itemId, out InventoryItemPresentationEntry presentation)
        {
            presentation = null;
            EnsureLookup();
            string id = (itemId ?? "").Trim();
            return id.Length > 0 && lookup.TryGetValue(id, out presentation) && presentation != null;
        }

        public bool TryGetItemSprites(string itemId, out Sprite rowIcon, out Sprite detailPortrait)
        {
            rowIcon = fallbackRowIcon;
            detailPortrait = fallbackDetailPortrait;
            if (TryResolve(itemId, out InventoryItemPresentationEntry presentation) && presentation != null)
            {
                if (presentation.rowIcon != null)
                    rowIcon = presentation.rowIcon;
                if (presentation.detailPortrait != null)
                    detailPortrait = presentation.detailPortrait;
            }

            return rowIcon != null || detailPortrait != null;
        }

        public bool TryResolve(
            MenuInventoryItemStack stack,
            PlayerItemDefinition definition,
            out InventoryItemPresentationEntry presentation)
        {
            string id = !string.IsNullOrWhiteSpace(stack?.itemId)
                ? stack.itemId
                : definition != null
                    ? definition.itemId
                    : "";
            return TryResolve(id, out presentation);
        }

        private void OnValidate()
        {
            lookup = null;
        }

        private void EnsureLookup()
        {
            if (lookup != null)
                return;

            lookup = new Dictionary<string, InventoryItemPresentationEntry>(StringComparer.Ordinal);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                InventoryItemPresentationEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.itemId))
                    continue;

                lookup[entry.itemId.Trim()] = entry;
            }
        }
    }
}
