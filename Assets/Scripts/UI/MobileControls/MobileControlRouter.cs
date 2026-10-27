using System;
using UnityEngine;
using UnityEngine.Events;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Subscribes to <see cref="MobileInputManager"/> events and routes them to gameplay
    /// systems via inspector UnityEvents and runtime <c>*Routed</c> subscribers
    /// (e.g. <see cref="SignalJam.UI.MenuNavigation.PersistentPlayerMenuHost"/>).
    /// Does not load scenes; unhandled routes log a warning when debug is enabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileControlRouter : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private MobileInputManager inputManager;

        [Header("Button Routes")]
        [SerializeField] private UnityEvent onBack;
        [SerializeField] private UnityEvent onMenu;
        [SerializeField] private UnityEvent onPrimaryInteract;
        [SerializeField] private UnityEvent onSecondaryInteract;
        [SerializeField] private UnityEvent onStart;
        [SerializeField] private UnityEvent onSelect;
        [SerializeField] private UnityEvent onWallet;

        [SerializeField] private bool enableDebugLogs = false;

        public event Action BackRouted;
        public event Action MenuRouted;
        public event Action StartRouted;
        public event Action SelectRouted;
        public event Action WalletRouted;

        private bool isSubscribed;

        private void OnEnable()
        {
            ResolveInputManager();
            SubscribeInputManager();
        }

        private void Update()
        {
            if (isSubscribed) return;

            ResolveInputManager();
            SubscribeInputManager();
        }

        private void OnDisable()
        {
            UnsubscribeInputManager();
        }

        private void ResolveInputManager()
        {
            if (inputManager != null) return;

            inputManager = GetComponent<MobileInputManager>();
            if (inputManager == null)
                inputManager = GetComponentInChildren<MobileInputManager>(true);
            if (inputManager == null)
                inputManager = FindObjectOfType<MobileInputManager>(true);
        }

        private void SubscribeInputManager()
        {
            if (isSubscribed || inputManager == null) return;

            inputManager.OnBackPressed += HandleBack;
            inputManager.OnMenuPressed += HandleMenu;
            inputManager.OnPrimaryInteract += HandlePrimaryInteract;
            inputManager.OnSecondaryInteract += HandleSecondaryInteract;
            inputManager.OnStartPressed += HandleStart;
            inputManager.OnSelectPressed += HandleSelect;
            inputManager.OnWalletPressed += HandleWallet;
            isSubscribed = true;
        }

        private void UnsubscribeInputManager()
        {
            if (!isSubscribed || inputManager == null) return;

            inputManager.OnBackPressed -= HandleBack;
            inputManager.OnMenuPressed -= HandleMenu;
            inputManager.OnPrimaryInteract -= HandlePrimaryInteract;
            inputManager.OnSecondaryInteract -= HandleSecondaryInteract;
            inputManager.OnStartPressed -= HandleStart;
            inputManager.OnSelectPressed -= HandleSelect;
            inputManager.OnWalletPressed -= HandleWallet;
            isSubscribed = false;
        }

        private void HandleBack()
        {
            bool handledByRuntimeSubscriber = BackRouted != null;
            BackRouted?.Invoke();
            Invoke(onBack, "Back", handledByRuntimeSubscriber);
        }

        private void HandleMenu()
        {
            bool handledByRuntimeSubscriber = MenuRouted != null;
            MenuRouted?.Invoke();
            Invoke(onMenu, "Menu", handledByRuntimeSubscriber);
        }

        private void HandlePrimaryInteract() => Invoke(onPrimaryInteract, "PrimaryInteract");
        private void HandleSecondaryInteract() => Invoke(onSecondaryInteract, "SecondaryInteract");

        private void HandleStart()
        {
            bool handledByRuntimeSubscriber = StartRouted != null;
            StartRouted?.Invoke();
            Invoke(onStart, "Start", handledByRuntimeSubscriber);
        }

        private void HandleSelect()
        {
            bool handledByRuntimeSubscriber = SelectRouted != null;
            SelectRouted?.Invoke();
            Invoke(onSelect, "Select", handledByRuntimeSubscriber);
        }

        private void HandleWallet()
        {
            bool handledByRuntimeSubscriber = WalletRouted != null;
            WalletRouted?.Invoke();
            Invoke(onWallet, "Wallet", handledByRuntimeSubscriber);
        }

        private void Invoke(UnityEvent evt, string label, bool handledByRuntimeSubscriber = false)
        {
            if (evt != null && evt.GetPersistentEventCount() > 0)
            {
                if (enableDebugLogs)
                    Debug.Log($"[MobileControlRouter] {label} -> UnityEvent");
                evt.Invoke();
                return;
            }

            if (handledByRuntimeSubscriber)
            {
                if (enableDebugLogs)
                    Debug.Log($"[MobileControlRouter] {label} -> runtime subscriber");
                return;
            }

            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[MobileControlRouter] {label} unhandled. Wire a UnityEvent on {nameof(MobileControlRouter)} " +
                    $"or subscribe to {label}Routed (PersistentPlayerMenuHost handles Menu/Start/Back in gameplay).",
                    this);
            }
        }
    }
}
