using TMPro;
using UnityEngine;

/// <summary>
/// Simple reusable text display used by name-entry style overlays.
/// </summary>
[DisallowMultipleComponent]
public class NameTagDisplayPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private string emptyPlaceholder = "...";

    private void Awake()
    {
        if (displayText == null)
            displayText = GetComponentInChildren<TMP_Text>(true);

        Clear();
    }

    public void SetDisplayName(string name)
    {
        if (displayText == null)
            return;

        string trimmedName = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        displayText.text = string.IsNullOrEmpty(trimmedName) ? emptyPlaceholder : trimmedName;
    }

    public void Clear()
    {
        if (displayText == null)
            return;

        displayText.text = emptyPlaceholder;
    }
}
