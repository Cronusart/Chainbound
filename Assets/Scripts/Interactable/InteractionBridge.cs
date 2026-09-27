using UnityEngine;

/// <summary>
/// Deprecated migration sentinel retained only so stale content can be detected safely.
/// New content must use QuestProgressInteractionBridge plus QuestInteractionProfileSO assets.
/// </summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
[System.Obsolete("InteractionBridge is deprecated. Use QuestProgressInteractionBridge with QuestInteractionProfileSO assets.", false)]
public class InteractionBridge : MonoBehaviour
{
    [SerializeField, HideInInspector] private string questId = "";
    [SerializeField, HideInInspector] private DialogueSequenceSO duringQuestDialogue;
    [SerializeField, HideInInspector] private string duringQuestTriggerId = "";
    [SerializeField, HideInInspector] private DialogueSequenceSO afterQuestDialogue;
    [SerializeField, HideInInspector] private string afterQuestTriggerId = "";
    [SerializeField, HideInInspector] private bool enableDebugLogs = false;

    public bool HasLegacyConfiguration =>
        !string.IsNullOrWhiteSpace(questId) ||
        duringQuestDialogue != null ||
        !string.IsNullOrWhiteSpace(duringQuestTriggerId) ||
        afterQuestDialogue != null ||
        !string.IsNullOrWhiteSpace(afterQuestTriggerId);

    private void OnValidate()
    {
        Debug.LogError(
            "[InteractionBridge] InteractionBridge is deprecated. Replace it with QuestProgressInteractionBridge plus QuestInteractionProfileSO assets.",
            this);
    }

    /// <summary>
    /// Parameterless entry point retained only so old UnityEvents fail loudly instead of silently.
    /// </summary>
    public void OnInteract()
    {
        Debug.LogError(
            "[InteractionBridge] InteractionBridge is no longer supported at runtime. Replace it with QuestProgressInteractionBridge plus QuestInteractionProfileSO assets.",
            this);
    }
}
