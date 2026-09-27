using System.Globalization;
using System.Text.RegularExpressions;
using Chainbound.Combat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class CandyMachineResultView : MonoBehaviour
    {
        private static readonly Regex NumericRange = new Regex(
            @"(?<![\d.])[+-]?\d+(?:\.\d+)?\s*(?:-|–|—)\s*[+-]?\d+(?:\.\d+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private LibraryCardResultPresenter presenter;
        [SerializeField] private Button continueButton;

        public Button ContinueButton => continueButton;
        public float ContentDelay { get; private set; }

        public static string FormatModifierEffect(
            string modifierId,
            ModifierDefinitionSO definition,
            float lockedValue,
            bool hasLockedValue)
        {
            if (string.IsNullOrWhiteSpace(modifierId))
                return string.Empty;

            string label = definition != null ? definition.GetDisplayName() : modifierId;
            if (string.IsNullOrWhiteSpace(label))
                label = modifierId;

            string description = definition != null && !string.IsNullOrWhiteSpace(definition.effectDescription)
                ? definition.effectDescription.Trim()
                : string.Empty;
            if (hasLockedValue && description.Length > 0)
            {
                string formatted = lockedValue.ToString("0.####", CultureInfo.InvariantCulture);
                description = NumericRange.Replace(description, formatted, 1);
                description = description.Replace("{value}", formatted);
                description = description.Replace("{n}", formatted);
            }

            return description.Length > 0 ? $"{label}: {description}" : label;
        }

        public static string FormatModifierDescription(
            string modifierId,
            ModifierDefinitionSO definition,
            float lockedValue,
            bool hasLockedValue)
        {
            string labeled = FormatModifierEffect(modifierId, definition, lockedValue, hasLockedValue);
            if (string.IsNullOrWhiteSpace(labeled))
                return string.Empty;
            string name = FormatModifierLabel(modifierId, definition);
            string prefix = name + ": ";
            if (!string.IsNullOrEmpty(name) && labeled.StartsWith(prefix, System.StringComparison.Ordinal))
                return labeled.Substring(prefix.Length);
            if (string.Equals(labeled, name, System.StringComparison.Ordinal))
                return string.Empty;
            return labeled;
        }

        public static string FormatModifierLabel(string modifierId, ModifierDefinitionSO definition)
        {
            if (string.IsNullOrWhiteSpace(modifierId))
                return string.Empty;
            if (definition != null)
            {
                string name = definition.GetDisplayName();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
            return modifierId;
        }

        public void Bind(CanvasGroup group, LibraryCardResultPresenter cardPresenter, Button continueControl)
        {
            canvasGroup = group;
            presenter = cardPresenter;
            continueButton = continueControl;
        }

        public void Prepare(CandyMachineRevealData data)
        {
            if (data == null)
                return;

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
                continueButton.interactable = false;
            }

            presenter?.Prepare(data);
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public float SetStage(int stage)
        {
            if (stage <= 0)
            {
                if (continueButton != null)
                {
                    continueButton.gameObject.SetActive(false);
                    continueButton.interactable = false;
                }

                return 0f;
            }

            float duration = presenter != null ? presenter.SetStage(stage) : 1.2f;
            if (presenter != null)
                ContentDelay = presenter.ContentDelay;
            if (stage == 3 && presenter != null)
            {
                DOVirtual.DelayedCall(duration, EnableContinue, false)
                    .SetUpdate(true)
                    .SetTarget(this);
            }
            else if (stage == 3)
            {
                EnableContinue();
            }

            return duration;
        }

        public void EnableContinue()
        {
            if (continueButton == null)
                return;

            gameObject.SetActive(true);
            if (continueButton.transform.parent != null)
                continueButton.transform.parent.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;
            continueButton.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            PlayerMenuChromeButtonVisual chrome = continueButton.GetComponent<PlayerMenuChromeButtonVisual>();
            chrome?.SetSelected(true);
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            Image graphic = continueButton.targetGraphic as Image;
            if (graphic != null)
                graphic.raycastTarget = true;
        }

        public void Hide()
        {
            DOTween.Kill(this);
            presenter?.Clear();
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
                continueButton.interactable = false;
            }

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
        }
    }
}
