using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Base behaviour for UGUI menu screens. Layout is owned by the prefab; this
    /// component owns visibility, focus, and a stable screen id.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuScreen : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string screenId = "";
        [SerializeField] private string displayName = "";

        [Header("View")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private MenuInputContextController inputContext;
        [SerializeField] private Selectable defaultSelection;
        [SerializeField] private TMP_Text titleLabel;

        [Header("Back")]
        [SerializeField] private bool closeWithBack = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onShown = new UnityEvent();
        [SerializeField] private UnityEvent onHidden = new UnityEvent();

        public string ScreenId => screenId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? screenId : displayName;
        public CanvasGroup CanvasGroup => canvasGroup;
        public bool IsVisible { get; private set; }
        public bool CloseWithBack => closeWithBack;
        public Selectable DefaultSelection => defaultSelection;
        public MenuInputContextController InputContext => inputContext;

        protected virtual void Awake()
        {
            ResolveReferences();
            if (titleLabel != null && !string.IsNullOrWhiteSpace(DisplayName))
                titleLabel.text = DisplayName;
        }

        public virtual void Show()
        {
            ResolveReferences();
            gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            IsVisible = true;

            if (inputContext != null)
                inputContext.SelectDefaultDeferred();
            else if (defaultSelection != null)
                defaultSelection.Select();

            onShown?.Invoke();
        }

        public virtual void Hide()
        {
            ResolveReferences();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            IsVisible = false;
            gameObject.SetActive(false);
            onHidden?.Invoke();
        }

        public virtual void Refresh()
        {
        }

        public virtual bool TryHandleCancel()
        {
            return false;
        }

        public virtual void ConfigureInputContext(MenuInputContextController context)
        {
            if (context == null)
                return;

            List<Selectable> order = new List<Selectable>();
            CollectNavigationSelectables(order);
            Selectable defaultSelect = ResolveDefaultNavigationSelection(order);
            context.SetContentNavigation(order, defaultSelect, transform);
        }

        protected virtual void CollectNavigationSelectables(List<Selectable> order)
        {
            Selectable[] selectables = GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (MenuSelectableNavigation.IsUsable(selectable) && !order.Contains(selectable))
                    order.Add(selectable);
            }
        }

        protected virtual Selectable ResolveDefaultNavigationSelection(List<Selectable> order)
        {
            if (defaultSelection != null && MenuSelectableNavigation.IsUsable(defaultSelection))
                return defaultSelection;

            for (int i = 0; i < order.Count; i++)
            {
                if (MenuSelectableNavigation.IsUsable(order[i]))
                    return order[i];
            }

            return null;
        }

        public void SetScreenIdentity(string id, string name, CanvasGroup group)
        {
            if (!string.IsNullOrWhiteSpace(id))
                screenId = id;
            if (!string.IsNullOrWhiteSpace(name))
                displayName = name;
            if (group != null)
                canvasGroup = group;
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (inputContext == null)
                inputContext = GetComponent<MenuInputContextController>();
            if (defaultSelection == null)
                defaultSelection = GetComponentInChildren<Selectable>(true);
        }
    }
}
