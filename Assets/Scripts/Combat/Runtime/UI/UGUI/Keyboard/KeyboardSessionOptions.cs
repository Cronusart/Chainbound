using System;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum KeyboardCharacterPolicy
    {
        None = 0,
        NameEntry = 1,
        Custom = 2,
    }

    /// <summary>
    /// Lightweight session config passed into the shared on-screen keyboard.
    /// Callers provide the target, validation, and completion callbacks while
    /// the keyboard service stays reusable across onboarding and future flows.
    /// </summary>
    public sealed class KeyboardSessionOptions
    {
        public string Title { get; set; } = "";
        public string Prompt { get; set; } = "";
        /// <summary>
        /// When set, replaces the authored prompt color for this session.
        /// Null keeps the prefab color.
        /// </summary>
        public Color? PromptColor { get; set; }
        public string SubmitLabel { get; set; } = "Confirm";
        public string EmptyDisplayText { get; set; } = "...";
        /// <summary>
        /// When true, shows the keyboard-owned text echo strip. Prefer false when the caller
        /// already displays typed text (name tag, field, etc.).
        /// </summary>
        public bool ShowLivePreviewStrip { get; set; } = false;
        public int MaxLength { get; set; } = 0;
        public bool StartShifted { get; set; } = true;
        public KeyboardCharacterPolicy CharacterPolicy { get; set; } = KeyboardCharacterPolicy.None;
        public Func<char, bool> CharacterValidator { get; set; }
        public Func<string, bool> SubmitValidator { get; set; }
        public Action<string> OnSubmitted { get; set; }
        public Action OnCancelled { get; set; }
        /// <summary>
        /// Fired after the backing field changes. Callers that mirror the text
        /// outside the keyboard should update from here so a later session
        /// still echoes keystrokes if the TMP callback does not.
        /// </summary>
        public Action<string> OnTextChanged { get; set; }

        public bool AllowsCharacter(char character)
        {
            if (CharacterValidator != null)
                return CharacterValidator(character);

            switch (CharacterPolicy)
            {
                case KeyboardCharacterPolicy.NameEntry:
                    return ChainlingDisplayNameRules.IsAllowedCharacter(character);
                case KeyboardCharacterPolicy.Custom:
                    return false;
                default:
                    return true;
            }
        }

        public bool CanSubmit(string text)
        {
            return SubmitValidator == null || SubmitValidator(text ?? "");
        }
    }
}
