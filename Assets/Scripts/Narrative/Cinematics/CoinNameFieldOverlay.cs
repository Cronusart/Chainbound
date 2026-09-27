using TMPro;
using UnityEngine;

/// <summary>
/// Authorable TMP overlay shown over a held sprite-cinematic monitor frame
/// while the player enters their coin/player name. Edit the prefab's RectTransform
/// and TMP settings in the Project window — do not drive layout from the cinematic SO.
/// The '$' prefix is display-only and is never part of the saved player name.
/// </summary>
public sealed class CoinNameFieldOverlay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private string displayPrefix = "$";

    public void SetText(string text)
    {
        if (label == null)
            return;

        label.text = displayPrefix + (text ?? string.Empty);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (label != null)
        {
            label.gameObject.SetActive(true);
            if (string.IsNullOrEmpty(label.text))
                label.text = displayPrefix;
        }
    }

    public void Hide()
    {
        if (label != null)
            label.text = string.Empty;
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);
    }
#endif
}
