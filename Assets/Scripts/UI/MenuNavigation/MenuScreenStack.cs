using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Small stack controller for UGUI menu screens. Root tab screens are shown
    /// one at a time; detail panels can be pushed and popped above them.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuScreenStack : MonoBehaviour
    {
        [SerializeField] private RectTransform screenHost;
        [SerializeField] private MenuScreen[] initialScreens;
        [SerializeField] private PlayerMenuPanelTransitionController transitionController;
        [SerializeField] private bool animateStackTransitions = true;
        [SerializeField] private UnityEvent onStackEmptied = new UnityEvent();

        private readonly List<MenuScreen> stack = new List<MenuScreen>();

        public MenuScreen Current => stack.Count > 0 ? stack[stack.Count - 1] : null;
        public int Count => stack.Count;

        public void ConfigureTransitionController(PlayerMenuPanelTransitionController controller)
        {
            if (controller != null)
                transitionController = controller;
        }

        public MenuScreen PeekBelow()
        {
            return stack.Count > 1 ? stack[stack.Count - 2] : null;
        }

        private void Awake()
        {
            if (screenHost == null)
                screenHost = transform as RectTransform;
            if (transitionController == null)
                transitionController = GetComponentInParent<PlayerMenuPanelTransitionController>(true);

            HideInitialScreens();
        }

        public void ShowRoot(MenuScreen screen)
        {
            ShowRoot(screen, animateStackTransitions);
        }

        public void ShowRoot(MenuScreen screen, bool animate)
        {
            if (screen == null) return;

            MenuScreen outgoing = Current;
            if (!ShouldAnimate(animate) || outgoing == null || outgoing == screen)
            {
                HideAllStackedScreens();
                stack.Clear();
                PushInternal(screen);
                return;
            }

            stack.Clear();
            stack.Add(screen);
            transitionController.PlaySwap(
                outgoing.transform,
                () =>
                {
                    outgoing.Hide();
                    ShowScreenInternal(screen);
                },
                screen.transform);
        }

        public void Push(MenuScreen screen)
        {
            if (screen == null) return;

            MenuScreen current = Current;
            if (!ShouldAnimate(animateStackTransitions) || current == null)
            {
                if (current != null)
                    current.Hide();

                PushInternal(screen);
                return;
            }

            ParentToHost(screen);
            stack.Add(screen);
            transitionController.PlaySwap(
                current.transform,
                () =>
                {
                    current.Hide();
                    ShowScreenInternal(screen);
                },
                screen.transform);
        }

        public bool Pop()
        {
            if (stack.Count == 0)
                return false;

            MenuScreen popped = Current;
            stack.RemoveAt(stack.Count - 1);
            MenuScreen next = Current;
            if (next != null)
            {
                if (ShouldAnimate(animateStackTransitions) && popped != null)
                {
                    transitionController.PlaySwap(
                        popped.transform,
                        () =>
                        {
                            popped.Hide();
                            ShowScreenInternal(next);
                        },
                        next.transform);
                }
                else
                {
                    if (popped != null)
                        popped.Hide();
                    ShowScreenInternal(next);
                }

                return true;
            }

            if (popped != null)
                popped.Hide();

            onStackEmptied?.Invoke();
            return false;
        }

        public void Clear()
        {
            ClearInternal(cancelTransitions: true);
        }

        public void ClearWithoutCancellingTransition()
        {
            ClearInternal(cancelTransitions: false);
        }

        private void ClearInternal(bool cancelTransitions)
        {
            if (cancelTransitions)
                transitionController?.Cancel();
            HideAllStackedScreens();
            stack.Clear();
            onStackEmptied?.Invoke();
        }

        private void PushInternal(MenuScreen screen)
        {
            ParentToHost(screen);
            stack.Add(screen);
            ShowScreenInternal(screen);
        }

        private void ParentToHost(MenuScreen screen)
        {
            if (screen == null)
                return;

            if (screenHost != null && screen.transform.parent != screenHost)
                screen.transform.SetParent(screenHost, false);
        }

        private static void ShowScreenInternal(MenuScreen screen)
        {
            if (screen == null)
                return;

            screen.Show();
        }

        private bool ShouldAnimate(bool animate)
        {
            return animate &&
                Application.isPlaying &&
                transitionController != null &&
                transitionController.isActiveAndEnabled &&
                !transitionController.IsPlaying;
        }

        private void HideInitialScreens()
        {
            if (initialScreens == null) return;

            for (int i = 0; i < initialScreens.Length; i++)
            {
                if (initialScreens[i] != null)
                    initialScreens[i].Hide();
            }
        }

        private void HideAllStackedScreens()
        {
            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i] != null)
                    stack[i].Hide();
            }
        }
    }
}
