using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Inspector-wired UGUI tab bar. The prefab owns layout; code only updates
    /// labels, icons, selected state, and lock badges.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuTabBar : MonoBehaviour
    {
        [Serializable]
        public class TabBinding
        {
            public Button button;
            public TMP_Text label;
            public Image icon;
            public Image selectionFrame;
            public GameObject lockedBadge;
            public TMP_Text lockedLabel;
        }

        [SerializeField] private MenuTabDefinitionSO[] tabs;
        [SerializeField] private TabBinding[] bindings;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color unselectedColor = new Color(0.75f, 0.78f, 0.85f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.45f, 0.48f, 0.55f, 1f);
        [SerializeField] private bool preferButtonVisualComponents = true;
        [SerializeField] private bool configureVerticalNavigation = true;
        [SerializeField] private UnityEvent<int> onTabFocused = new UnityEvent<int>();
        [SerializeField] private UnityEvent<int> onTabSelected = new UnityEvent<int>();

        private UnityAction[] clickHandlers;
        private bool[] lockedStates;
        private int selectedIndex = -1;

        public event Action<int> TabFocused;
        public event Action<int> TabSubmitted;
        public event Action<int> TabSelected;
        public IReadOnlyList<MenuTabDefinitionSO> Tabs => tabs;

        private void OnEnable()
        {
            InstallButtonHandlers();
            RefreshAll();
        }

        private void OnDisable()
        {
            RemoveButtonHandlers();
        }

        public void SetTabs(MenuTabDefinitionSO[] definitions)
        {
            tabs = definitions;
            bool grew = EnsureBindingCoverage();
            lockedStates = null;
            if (grew && isActiveAndEnabled)
                InstallButtonHandlers();
            RefreshAll();
            ConfigureVerticalNavigation();
        }

        private bool EnsureBindingCoverage()
        {
            if (tabs == null || bindings == null || bindings.Length == 0 || tabs.Length <= bindings.Length)
                return false;

            var grown = new System.Collections.Generic.List<TabBinding>(bindings);
            while (grown.Count < tabs.Length)
            {
                TabBinding source = grown[grown.Count - 1];
                if (source == null || source.button == null)
                    break;

                GameObject clone = Instantiate(source.button.gameObject, source.button.transform.parent);
                clone.name = tabs[grown.Count] != null ? tabs[grown.Count].TabId + "TabButton" : "ExtraTabButton";
                var binding = new TabBinding
                {
                    button = clone.GetComponent<Button>(),
                    label = Remap(source.label, source.button.transform, clone.transform),
                    icon = Remap(source.icon, source.button.transform, clone.transform),
                    selectionFrame = Remap(source.selectionFrame, source.button.transform, clone.transform),
                    lockedBadge = RemapObject(source.lockedBadge, source.button.transform, clone.transform),
                    lockedLabel = Remap(source.lockedLabel, source.button.transform, clone.transform),
                };
                grown.Add(binding);
            }

            if (grown.Count == bindings.Length)
                return false;

            bindings = grown.ToArray();
            return true;
        }

        private static T Remap<T>(T original, Transform sourceRoot, Transform cloneRoot) where T : Component
        {
            if (original == null || sourceRoot == null || cloneRoot == null)
                return null;
            if (original.transform == sourceRoot)
                return cloneRoot.GetComponent<T>();

            string path = RelativePath(original.transform, sourceRoot);
            Transform mapped = string.IsNullOrEmpty(path) ? null : cloneRoot.Find(path);
            return mapped != null ? mapped.GetComponent<T>() : null;
        }

        private static GameObject RemapObject(GameObject original, Transform sourceRoot, Transform cloneRoot)
        {
            if (original == null)
                return null;
            Component mapped = Remap<Transform>(original.transform, sourceRoot, cloneRoot);
            return mapped != null ? mapped.gameObject : null;
        }

        private static string RelativePath(Transform child, Transform root)
        {
            if (child == null || root == null || child == root)
                return string.Empty;

            var names = new System.Collections.Generic.List<string>();
            Transform cursor = child;
            while (cursor != null && cursor != root)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }

            if (cursor != root)
                return string.Empty;

            names.Reverse();
            return string.Join("/", names);
        }

        public Button GetButton(int index)
        {
            if (bindings == null || index < 0 || index >= bindings.Length)
                return null;

            return bindings[index] != null ? bindings[index].button : null;
        }

        public void SetLocked(int index, bool locked)
        {
            EnsureLockState();
            if (index < 0 || index >= lockedStates.Length) return;
            lockedStates[index] = locked;
            Refresh(index);
        }

        public void SetSelectedIndex(int index)
        {
            if (selectedIndex == index) return;

            int previous = selectedIndex;
            selectedIndex = index;
            Refresh(previous);
            Refresh(selectedIndex);
        }

        public bool IsLocked(int index)
        {
            EnsureLockState();
            return index >= 0 && index < lockedStates.Length && lockedStates[index];
        }

        private void HandleTabClicked(int index)
        {
            FocusTab(index);
            if (IsLocked(index)) return;

            onTabSelected?.Invoke(index);
            TabSubmitted?.Invoke(index);
            TabSelected?.Invoke(index);

            Button button = GetButton(index);
            PlayerMenuTabButtonVisual visual = button != null
                ? button.GetComponent<PlayerMenuTabButtonVisual>()
                : null;
            visual?.PlayPressed();
        }

        internal void FocusTab(int index)
        {
            if (!HasTab(index)) return;

            SetSelectedIndex(index);
            onTabFocused?.Invoke(index);
            TabFocused?.Invoke(index);
        }

        private void InstallButtonHandlers()
        {
            RemoveButtonHandlers();
            if (bindings == null) return;

            clickHandlers = new UnityAction[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                int capturedIndex = i;
                clickHandlers[i] = () => HandleTabClicked(capturedIndex);

                Button button = bindings[i] != null ? bindings[i].button : null;
                if (button != null)
                {
                    button.onClick.AddListener(clickHandlers[i]);
                    MenuTabButtonFocusRelay relay = button.GetComponent<MenuTabButtonFocusRelay>();
                    if (relay == null)
                        relay = button.gameObject.AddComponent<MenuTabButtonFocusRelay>();
                    relay.Initialize(this, capturedIndex);
                }
            }

            ConfigureVerticalNavigation();
        }

        private void RemoveButtonHandlers()
        {
            if (bindings == null || clickHandlers == null) return;

            int count = Mathf.Min(bindings.Length, clickHandlers.Length);
            for (int i = 0; i < count; i++)
            {
                Button button = bindings[i] != null ? bindings[i].button : null;
                if (button != null && clickHandlers[i] != null)
                    button.onClick.RemoveListener(clickHandlers[i]);
            }

            clickHandlers = null;
        }

        private void RefreshAll()
        {
            EnsureLockState();
            if (bindings == null) return;

            for (int i = 0; i < bindings.Length; i++)
                Refresh(i);
        }

        private void Refresh(int index)
        {
            if (bindings == null || index < 0 || index >= bindings.Length)
                return;

            TabBinding binding = bindings[index];
            if (binding == null) return;

            MenuTabDefinitionSO tab = tabs != null && index < tabs.Length ? tabs[index] : null;
            bool hasTab = tab != null;
            bool locked = IsLocked(index);
            bool selected = index == selectedIndex;
            PlayerMenuTabButtonVisual visual = binding.button != null
                ? binding.button.GetComponent<PlayerMenuTabButtonVisual>()
                : null;
            bool usingButtonVisual = preferButtonVisualComponents && visual != null;

            if (binding.button != null)
            {
                binding.button.gameObject.SetActive(hasTab);
                binding.button.interactable = hasTab;
            }

            if (binding.label != null)
            {
                binding.label.text = hasTab ? tab.DisplayName : "";
                if (!usingButtonVisual)
                    binding.label.color = locked ? lockedColor : (selected ? selectedColor : unselectedColor);
            }

            if (binding.icon != null)
            {
                binding.icon.enabled = hasTab && tab.Icon != null;
                binding.icon.sprite = hasTab ? tab.Icon : null;
                binding.icon.color = locked ? lockedColor : (selected ? selectedColor : unselectedColor);
            }

            if (usingButtonVisual)
                visual.ApplyState(hasTab, selected, locked, Application.isPlaying);

            if (binding.selectionFrame != null)
                binding.selectionFrame.enabled = !usingButtonVisual && selected;

            if (binding.lockedBadge != null)
                binding.lockedBadge.SetActive(locked);

            if (binding.lockedLabel != null)
                binding.lockedLabel.text = hasTab ? tab.LockedLabel : "";
        }

        private void EnsureLockState()
        {
            int count = tabs != null ? tabs.Length : 0;
            if (lockedStates != null && lockedStates.Length == count) return;

            lockedStates = new bool[count];
        }

        private bool HasTab(int index)
        {
            return tabs != null &&
                index >= 0 &&
                index < tabs.Length &&
                tabs[index] != null;
        }

        private void ConfigureVerticalNavigation()
        {
            if (!configureVerticalNavigation || bindings == null)
                return;

            for (int i = 0; i < bindings.Length; i++)
            {
                Button button = GetButton(i);
                if (button == null) continue;

                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = FindButton(i, -1);
                navigation.selectOnDown = FindButton(i, 1);
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                button.navigation = navigation;
            }
        }

        private Button FindButton(int startIndex, int direction)
        {
            if (bindings == null || direction == 0)
                return null;

            int index = startIndex + direction;
            while (index >= 0 && index < bindings.Length)
            {
                Button button = GetButton(index);
                if (button != null && HasTab(index) && button.gameObject.activeInHierarchy)
                    return button;

                index += direction;
            }

            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MenuTabButtonFocusRelay : MonoBehaviour, ISelectHandler
    {
        private MenuTabBar owner;
        private int tabIndex = -1;

        public void Initialize(MenuTabBar tabBar, int index)
        {
            owner = tabBar;
            tabIndex = index;
        }

        public void OnSelect(BaseEventData eventData)
        {
            owner?.FocusTab(tabIndex);
        }
    }
}
