using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered quest interaction router. First matching slot wins.
/// </summary>
[CreateAssetMenu(
    fileName = "QuestInteractionRouter",
    menuName = "Chainbound/Narrative/Interactions/Quest Interaction Router")]
public class QuestInteractionRouterSO : ScriptableObject
{
    [Serializable]
    public class RouteSlot
    {
        [Tooltip("Optional label for designer readability only.")]
        [SerializeField] private string label = "";

        [Tooltip("Quest interaction profile to use when this slot matches.")]
        [SerializeField] private QuestInteractionProfileSO profile;

        [Tooltip("Which lifecycle states allow this slot to win.")]
        [SerializeField] private NarrativeQuestLifecycleMask allowedLifecycleMask = NarrativeQuestLifecycleMask.Active;

        public string Label => label;
        public QuestInteractionProfileSO Profile => profile;
        public NarrativeQuestLifecycleMask AllowedLifecycleMask => allowedLifecycleMask;
    }

    [Tooltip("Ordered router slots. First slot whose profile quest lifecycle matches wins.")]
    [SerializeField] private List<RouteSlot> slots = new List<RouteSlot>();

    public IReadOnlyList<RouteSlot> Slots => slots;

#if UNITY_EDITOR
    private void OnValidate()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            RouteSlot slot = slots[i];
            if (slot == null || slot.Profile == null)
            {
                Debug.LogWarning($"[QuestInteractionRouterSO] Slot {i} is missing a profile.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(slot.Profile.QuestId))
            {
                Debug.LogWarning(
                    $"[QuestInteractionRouterSO] Slot {i} profile '{slot.Profile.name}' is missing a quest id.",
                    this);
            }
        }
    }
#endif
}
