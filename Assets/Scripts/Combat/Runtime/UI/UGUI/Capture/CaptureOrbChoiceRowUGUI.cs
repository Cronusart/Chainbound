using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>Move-row chrome plus quantity, feeding orb details on gamepad/pointer focus.</summary>
    [DisallowMultipleComponent]
    public sealed class CaptureOrbChoiceRowUGUI : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        [SerializeField] private CombatMoveRowItemUGUI moveRow;
        [SerializeField] private TMP_Text quantityLabel;

        private Action focused;
        private MoveRowSelectionTuning tuning = MoveRowSelectionTuning.CreateDefaults();

        public void Configure(Action onFocused) => focused = onFocused;

        public void SetSelectionTuning(MoveRowSelectionTuning selectionTuning) => tuning = selectionTuning;

        public void Bind(string displayName, int quantity, Sprite rowIcon, bool selected, bool animate)
        {
            EnsureReferences();
            if (quantityLabel != null)
                quantityLabel.text = $"x{Mathf.Max(0, quantity)}";

            if (moveRow != null)
            {
                moveRow.SetData(
                    string.IsNullOrWhiteSpace(displayName) ? "Orb" : displayName,
                    rowIcon,
                    isUsable: true,
                    selected,
                    hideTypeIcon: false,
                    hasPriority: false);
                moveRow.ApplySelectionNudge(selected, animate, tuning);
                if (selected && animate)
                    moveRow.PlaySelectBounce(tuning);
            }
        }

        public void SetSelected(bool selected, bool animate)
        {
            EnsureReferences();
            if (moveRow == null)
                return;

            moveRow.ApplySelectionNudge(selected, animate, tuning);
            if (selected && animate)
                moveRow.PlaySelectBounce(tuning);
        }

        public void PlaySelectBounce()
        {
            EnsureReferences();
            moveRow?.PlaySelectBounce(tuning);
        }

        public void OnSelect(BaseEventData eventData) => focused?.Invoke();

        public void OnPointerEnter(PointerEventData eventData)
        {
            EventSystem.current?.SetSelectedGameObject(gameObject);
        }

        private void Awake() => EnsureReferences();

        private void EnsureReferences()
        {
            if (moveRow == null)
                moveRow = GetComponent<CombatMoveRowItemUGUI>();
            if (quantityLabel == null)
            {
                Transform quantity = transform.Find("RowContent/Quantity") ?? transform.Find("Quantity");
                if (quantity != null)
                    quantityLabel = quantity.GetComponent<TMP_Text>();
            }
        }
    }
}
