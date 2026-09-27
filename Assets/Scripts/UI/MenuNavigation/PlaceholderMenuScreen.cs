using TMPro;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Simple reusable tab body for early menu scaffolds and empty states.
    /// Replace the prefab body later without changing navigation code.
    /// </summary>
    public class PlaceholderMenuScreen : MenuScreen
    {
        [Header("Placeholder Copy")]
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string heading = "";
        [TextArea(2, 5)]
        [SerializeField] private string body = "";
        [SerializeField] private string status = "";

        public override void Refresh()
        {
            base.Refresh();
            if (headingLabel != null)
                headingLabel.text = string.IsNullOrWhiteSpace(heading) ? DisplayName : heading;
            if (bodyLabel != null)
                bodyLabel.text = body;
            if (statusLabel != null)
                statusLabel.text = status;
        }

        public void SetStatus(string value)
        {
            status = value ?? "";
            if (statusLabel != null)
                statusLabel.text = status;
        }
    }
}
