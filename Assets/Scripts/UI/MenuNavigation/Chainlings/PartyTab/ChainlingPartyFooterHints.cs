using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public class ChainlingPartyFooterHints : MonoBehaviour
    {
        public enum FocusLayer
        {
            ViewSection,
            ViewEditing,
            PartySection,
            ColumnEditing,
        }

        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text hintLabel;

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            SetLayer(FocusLayer.PartySection, false);
        }

        public void SetPartyTitle(int filledCount, int totalSlots)
        {
            EnsureBuilt();
            if (titleLabel == null)
                return;

            titleLabel.text = filledCount >= totalSlots
                ? "Active Party"
                : $"Active Party · {filledCount}/{totalSlots}";
        }

        public void SetLayer(FocusLayer layer, bool hasFocusedEntry)
        {
            EnsureBuilt();
            if (hintLabel == null)
                return;

            switch (layer)
            {
                case FocusLayer.ViewEditing:
                    hintLabel.text = "↑↓ View   A Select   B Cancel";
                    break;
                case FocusLayer.PartySection:
                    hintLabel.text = hasFocusedEntry
                        ? "← Change View   A Select Column   B Back"
                        : "← Change View   A Select Column   B Back   Assign at PC";
                    break;
                case FocusLayer.ColumnEditing:
                    hintLabel.text = hasFocusedEntry
                        ? "←→ Columns   A Details   B Back"
                        : "←→ Columns   B Back   Assign at PC";
                    break;
                default:
                    hintLabel.text = hasFocusedEntry
                        ? "→ Party   A Change View   B Back"
                        : "→ Party   A Change View   B Back   Assign at PC";
                    break;
            }
        }

        private void EnsureBuilt()
        {
            if (titleLabel == null)
            {
                titleLabel = PlayerMenuUiFactory.CreateText("Title", transform, "Active Party", 28f, TextAlignmentOptions.TopLeft, false);
                PlayerMenuUiFactory.Top(titleLabel.rectTransform, 36f, 0f, 0f, 0f);
            }

            if (hintLabel == null)
            {
                hintLabel = PlayerMenuUiFactory.CreateText("Hint", transform, "", 22f, TextAlignmentOptions.TopRight, false);
                hintLabel.color = PlayerMenuUiFactory.Muted;
                PlayerMenuUiFactory.Top(hintLabel.rectTransform, 36f, 0f, 0f, 0f);
            }
        }
    }
}
