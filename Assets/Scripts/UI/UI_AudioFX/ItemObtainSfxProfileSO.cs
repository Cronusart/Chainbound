using System;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.Audio
{
    [Serializable]
    public sealed class ItemObtainCategorySfx
    {
        [Tooltip("Inventory category that selects this obtain sound.")]
        public PlayerItemCategory category = PlayerItemCategory.Field;

        [Tooltip("Sound played when an item in this category is obtained.")]
        public SoundEventSO soundEvent;
    }

    /// <summary>
    /// Data-driven sound routing for the item-obtain panel. Categories without
    /// an assigned event resolve to the shared fallback event.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ItemObtainSfxProfile",
        menuName = "Chainbound/UI/Audio/Item Obtain SFX Profile",
        order = 70)]
    public sealed class ItemObtainSfxProfileSO : ScriptableObject
    {
        [Tooltip("Used for unknown items and categories without an assigned sound.")]
        [SerializeField] private SoundEventSO fallbackEvent;

        [Tooltip("Optional category-specific overrides. The first matching non-null event wins.")]
        [SerializeField] private List<ItemObtainCategorySfx> categoryEvents =
            new List<ItemObtainCategorySfx>();

        public SoundEventSO FallbackEvent => fallbackEvent;

        public SoundEventSO Resolve(PlayerItemCategory category)
        {
            if (categoryEvents != null)
            {
                for (int i = 0; i < categoryEvents.Count; i++)
                {
                    ItemObtainCategorySfx entry = categoryEvents[i];
                    if (entry != null && entry.category == category && entry.soundEvent != null)
                        return entry.soundEvent;
                }
            }

            return fallbackEvent;
        }
    }
}
