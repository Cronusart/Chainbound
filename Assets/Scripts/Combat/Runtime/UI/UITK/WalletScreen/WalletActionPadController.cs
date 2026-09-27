using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chainbound.Combat
{
    /// <summary>
    /// Mirrors CombatActionSelectorController highlight/confirm state onto the wallet action pad UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalletActionPadController : MonoBehaviour
    {
        private const string SelectedClass = "is-selected";
        private const string BlinkOffClass = "blink-off";
        private const string ConfirmClass = "confirm";
        private const string DisabledClass = "is-disabled";

        [SerializeField] private UIDocument walletDocument;
        [SerializeField] private CombatActionSelectorController selectorController;
        [SerializeField] private float blinkInterval = 0.35f;
        [SerializeField] private int confirmFlashMs = 140;

        private readonly List<VisualElement> _actions = new List<VisualElement>(5);

        private VisualElement _actionFlee;
        private VisualElement _actionFight;
        private VisualElement _actionSwap;
        private VisualElement _actionItem;
        private VisualElement _actionMint;
        private VisualElement _selectedAction;
        private Label _fleeLabel;

        private IVisualElementScheduledItem _blinkSchedule;
        private bool _blinkOff;

        private void OnEnable()
        {
            if (walletDocument == null)
                walletDocument = GetComponent<UIDocument>();
            if (selectorController == null)
                selectorController = FindFirstObjectByType<CombatActionSelectorController>();

            if (!CacheElements())
                return;

            if (selectorController != null)
            {
                selectorController.OnOptionHighlighted += HandleHighlighted;
                selectorController.OnOptionConfirmed += HandleConfirmed;
                selectorController.OnOptionAvailabilityChanged += HandleAvailabilityChanged;
                SetSelectedByIndex(selectorController.CurrentIndex);
                HandleAvailabilityChanged(
                    CombatActionSelectorController.SelectorOption.Flee,
                    selectorController.IsOptionAvailable(CombatActionSelectorController.SelectorOption.Flee));
            }
            else
            {
                SetSelectedByIndex(0);
            }

            StartBlink();
        }

        private void OnDisable()
        {
            StopBlink();

            if (selectorController != null)
            {
                selectorController.OnOptionHighlighted -= HandleHighlighted;
                selectorController.OnOptionConfirmed -= HandleConfirmed;
                selectorController.OnOptionAvailabilityChanged -= HandleAvailabilityChanged;
            }
        }

        private void OnValidate()
        {
            blinkInterval = Mathf.Max(0.05f, blinkInterval);
            confirmFlashMs = Mathf.Clamp(confirmFlashMs, 50, 800);
        }

        private bool CacheElements()
        {
            var root = walletDocument != null ? walletDocument.rootVisualElement : null;
            if (root == null)
                return false;

            _actionFlee = root.Q<VisualElement>("Action_Flee");
            _actionFight = root.Q<VisualElement>("Action_Fight");
            _actionSwap = root.Q<VisualElement>("Action_Swap");
            _actionItem = root.Q<VisualElement>("Action_Item");
            _actionMint = root.Q<VisualElement>("Action_Mint");
            _fleeLabel = _actionFlee != null ? _actionFlee.Q<Label>(className: "action-label") : null;

            _actions.Clear();
            AddIfNotNull(_actions, _actionFlee);
            AddIfNotNull(_actions, _actionFight);
            AddIfNotNull(_actions, _actionSwap);
            AddIfNotNull(_actions, _actionItem);
            AddIfNotNull(_actions, _actionMint);

            return _actions.Count > 0;
        }

        private static void AddIfNotNull(List<VisualElement> list, VisualElement element)
        {
            if (element != null)
                list.Add(element);
        }

        private void HandleHighlighted(int optionIndex)
        {
            SetSelectedByIndex(optionIndex);
        }

        private void HandleConfirmed(int optionIndex)
        {
            var selected = ResolveElementForIndex(optionIndex);
            if (selected == null)
                return;

            selected.AddToClassList(ConfirmClass);
            selected.schedule.Execute(() => selected.RemoveFromClassList(ConfirmClass)).StartingIn(confirmFlashMs);
        }

        private void HandleAvailabilityChanged(CombatActionSelectorController.SelectorOption option, bool available)
        {
            if (option != CombatActionSelectorController.SelectorOption.Flee || _actionFlee == null)
                return;

            _actionFlee.EnableInClassList(DisabledClass, !available);
            _actionFlee.tooltip = available ? string.Empty : "Flee is unavailable in this encounter.";
            if (_fleeLabel != null)
                _fleeLabel.text = available ? "FLEE" : "LOCKED";
        }

        private void StartBlink()
        {
            StopBlink();

            var root = walletDocument != null ? walletDocument.rootVisualElement : null;
            if (root == null)
                return;

            int intervalMs = Mathf.Max(50, Mathf.RoundToInt(blinkInterval * 1000f));
            _blinkSchedule = root.schedule.Execute(ToggleBlink).Every(intervalMs);
        }

        private void StopBlink()
        {
            _blinkSchedule?.Pause();
            _blinkSchedule = null;
        }

        private void ToggleBlink()
        {
            if (_selectedAction == null)
                return;

            _blinkOff = !_blinkOff;
            _selectedAction.EnableInClassList(BlinkOffClass, _blinkOff);
        }

        private void SetSelectedByIndex(int optionIndex)
        {
            var target = ResolveElementForIndex(optionIndex);
            if (target == null)
                return;

            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                action.RemoveFromClassList(SelectedClass);
                action.RemoveFromClassList(BlinkOffClass);
            }

            _selectedAction = target;
            _blinkOff = false;
            _selectedAction.AddToClassList(SelectedClass);
        }

        private VisualElement ResolveElementForIndex(int optionIndex)
        {
            switch (optionIndex)
            {
                case 0: // Fight
                    return _actionFight;
                case 1: // Swap
                    return _actionSwap;
                case 2: // Item
                    return _actionItem;
                case 3: // Flee
                    return _actionFlee;
                case 4: // Record/Mint
                    return _actionMint != null ? _actionMint : _actionItem;
                default:
                    return _actionItem;
            }
        }
    }
}
