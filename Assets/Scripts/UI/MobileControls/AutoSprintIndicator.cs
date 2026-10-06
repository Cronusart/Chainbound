using TMPro;
using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Small lower-center label shown while the on-screen sprint lock is on.
    /// Dialogue presentation suppresses it without clearing the lock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoSprintIndicator : MonoBehaviour
    {
        public const string DefaultLabel = "AUTO SPRINT";

        [SerializeField] private MobileInputManager input;
        [SerializeField] private TMP_Text label;

        private bool dialogueShowing;

        public static AutoSprintIndicator Instance { get; private set; }

        public static void SetDialogueShowing(bool showing)
        {
            if (Instance == null)
                return;

            Instance.dialogueShowing = showing;
            Instance.Refresh();
        }

        private void Awake()
        {
            Instance = this;
            if (label != null)
            {
                if (string.IsNullOrWhiteSpace(label.text))
                    label.text = DefaultLabel;
                label.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (label == null)
                return;

            bool show = !dialogueShowing && input != null && input.SprintLocked;
            if (label.gameObject.activeSelf != show)
                label.gameObject.SetActive(show);
        }
    }
}
