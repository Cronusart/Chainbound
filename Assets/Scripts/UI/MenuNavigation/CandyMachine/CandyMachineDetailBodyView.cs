using TMPro;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    public enum CandyMachineDetailBodyMode
    {
        Idle,
        Confirm,
        Reveal,
        Error,
    }

    [DisallowMultipleComponent]
    public sealed class CandyMachineDetailBodyView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text body;
        [SerializeField] private TMP_Text hint;

        public CandyMachineDetailBodyMode Mode { get; private set; }

        public void Bind(CanvasGroup group, TMP_Text titleText, TMP_Text subtitleText, TMP_Text bodyText, TMP_Text hintText)
        {
            canvasGroup = group;
            title = titleText;
            subtitle = subtitleText;
            body = bodyText;
            hint = hintText;
            if (body != null)
            {
                body.textWrappingMode = TextWrappingModes.Normal;
                body.overflowMode = TextOverflowModes.Overflow;
            }
        }

        public void Show(CandyMachineDetailBodyMode mode, string headline, string copy, string hintCopy, string subtitleCopy = null)
        {
            Mode = mode;
            if (title != null) title.text = (headline ?? string.Empty).ToUpperInvariant();
            if (subtitle != null) subtitle.text = (subtitleCopy ?? string.Empty).ToUpperInvariant();
            if (body != null) body.text = (copy ?? string.Empty).ToUpperInvariant();
            if (hint != null) hint.text = (hintCopy ?? string.Empty).ToUpperInvariant();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                bool consumePointer = mode != CandyMachineDetailBodyMode.Reveal;
                canvasGroup.interactable = consumePointer;
                canvasGroup.blocksRaycasts = consumePointer;
            }
        }

        public void SetBody(string copy)
        {
            if (body != null)
                body.text = (copy ?? string.Empty).ToUpperInvariant();
        }
    }
}
