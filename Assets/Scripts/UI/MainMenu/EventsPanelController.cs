using TMPro;
using UnityEngine;

/// <summary>Data-driven event/news destination opened from the Xmas Tree.</summary>
[DisallowMultipleComponent]
public sealed class EventsPanelController : MainMenuPanelControllerBase
{
    [SerializeField] private MainMenuEventsFeedSO feed;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;

    protected override void OnPanelOpening()
    {
        base.OnPanelOpening();
        if (feed == null)
            return;

        if (titleLabel != null)
            titleLabel.text = feed.title;
        if (bodyLabel != null)
            bodyLabel.text = feed.body;
    }
}
