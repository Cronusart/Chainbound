using TMPro;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Pixeloid + uppercase helpers for candy-machine copy.
    /// Titles are bold; body/meta stay regular weight.
    /// </summary>
    public static class CandyMachineTextStyle
    {
        public static void ApplyTitle(TMP_Text text)
        {
            if (text == null)
                return;

            text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (!string.IsNullOrEmpty(text.text))
                text.text = text.text.ToUpperInvariant();
        }

        public static void ApplyLabel(TMP_Text text)
        {
            if (text == null)
                return;

            text.fontStyle |= FontStyles.UpperCase;
            if (!string.IsNullOrEmpty(text.text))
                text.text = text.text.ToUpperInvariant();
        }
    }
}
