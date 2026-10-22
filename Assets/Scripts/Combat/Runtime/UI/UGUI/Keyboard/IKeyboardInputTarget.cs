using TMPro;

namespace Chainbound.Combat
{
    public interface IKeyboardInputTarget
    {
        string Text { get; set; }
        int CharacterLimit { get; }
        bool IsValid { get; }
        void Focus();
    }

    public sealed class TMPInputFieldKeyboardTarget : IKeyboardInputTarget
    {
        private readonly TMP_InputField inputField;

        public TMPInputFieldKeyboardTarget(TMP_InputField inputField)
        {
            this.inputField = inputField;
        }

        public string Text
        {
            get => inputField != null ? inputField.text ?? "" : "";
            set
            {
                if (inputField == null)
                    return;

                inputField.text = value ?? "";
                MoveCaretToEnd();
            }
        }

        public int CharacterLimit => inputField != null ? inputField.characterLimit : 0;
        public bool IsValid => inputField != null;

        public void Focus()
        {
            if (inputField == null)
                return;

            // Keep caret in sync only. Do not steal EventSystem selection from virtual keys
            // (D-Pad / Submit navigation owns selection while the shared keyboard is open).
            MoveCaretToEnd();
        }

        private void MoveCaretToEnd()
        {
            if (inputField == null)
                return;

            int length = inputField.text != null ? inputField.text.Length : 0;
            inputField.caretPosition = length;
            inputField.selectionAnchorPosition = length;
            inputField.selectionFocusPosition = length;
        }
    }
}
