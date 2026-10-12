using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class RamShopScreen : MenuScreen
    {
        public const string RamShopScreenId = "ram_shop";
        private const int MaxStatusPolls = 20;
        private const string LastOpenOrderKey = "RamShop.LastOpenOrder.v1";

        [Header("Services")]
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        [Header("Readout")]
        [SerializeField] private TMP_Text balanceLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text unavailableLabel;

        [Header("Tender")]
        [SerializeField] private Button solButton;
        [SerializeField] private Button usdcButton;
        [SerializeField] private Button skrButton;

        [Header("Footer")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;

        [Header("Confirmation")]
        [SerializeField] private GameObject confirmSheet;
        [SerializeField] private TMP_Text confirmSummaryLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelConfirmButton;
        [Tooltip("UiPendingIndicator is kept behind the MonoBehaviour boundary because the legacy visual lives in Assembly-CSharp.")]
        [SerializeField] private MonoBehaviour pendingIndicator;

        private readonly List<Button> bundleButtons = new List<Button>();
        private IRamShopService shop;
        private IChainTxOrchestrator transactions;
        private IWalletService wallet;
        private RamCatalog catalog;
        private RamBundle selectedBundle;
        private RamPurchaseOrder selectedOrder;
        private Button restoreSelection;
        private string selectedTender = "SOL";
        private int refreshVersion;
        private bool busy;
        private bool buttonsWired;
        private bool resumingOrder;

        [Serializable]
        private sealed class LastOpenOrder
        {
            public string orderId;
            public string cluster;
            public string wallet;
            public string reference;
        }

        public bool HasEmbeddedLayout =>
            confirmSheet != null && GetComponentsInChildren<RamBundleCardPresenter>(true).Length == 3;

        protected override void Awake()
        {
            ResolveAuthoredReferences();
            base.Awake();
            WireButtons();
            ApplyExplicitNavigation();
            ResolveServices();
            SetConfirmationVisible(false);
        }

        private void OnEnable()
        {
            ResolveServices();
            if (wallet != null)
            {
                wallet.OnAccountChanged += HandleWalletChanged;
                wallet.OnConnected += HandleWalletChanged;
                wallet.OnDisconnected += HandleWalletDisconnected;
            }
        }

        private void OnDisable()
        {
            if (wallet != null)
            {
                wallet.OnAccountChanged -= HandleWalletChanged;
                wallet.OnConnected -= HandleWalletChanged;
                wallet.OnDisconnected -= HandleWalletDisconnected;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && IsVisible) Refresh();
        }

        public override void Show()
        {
            ResolveAuthoredReferences();
            WireButtons();
            ResolveServices();
            base.Show();
            Refresh();
        }

        public override void Refresh()
        {
            _ = RefreshAsync(++refreshVersion);
        }

        public override bool TryHandleCancel()
        {
            if (confirmSheet == null || !confirmSheet.activeSelf) return false;
            CloseConfirmation();
            return true;
        }

        protected override void CollectNavigationSelectables(List<Selectable> order)
        {
            if (confirmSheet != null && confirmSheet.activeSelf)
            {
                if (confirmButton != null && confirmButton.interactable) order.Add(confirmButton);
                if (cancelConfirmButton != null && cancelConfirmButton.interactable) order.Add(cancelConfirmButton);
                return;
            }

            AddIfUsable(order, solButton);
            AddIfUsable(order, usdcButton);
            AddIfUsable(order, skrButton);
            foreach (Button button in bundleButtons) AddIfUsable(order, button);
            AddIfUsable(order, retryButton);
            AddIfUsable(order, closeButton);
        }

        public void SelectTender(string tender)
        {
            if (busy) return;
            selectedTender = string.IsNullOrWhiteSpace(tender) ? "SOL" : tender.ToUpperInvariant();
            SetStatus($"{selectedTender} selected. Choose a bundle for a fresh server quote.");
        }

        public void Retry()
        {
            if (!busy) Refresh();
        }

        public void CloseShop()
        {
            if (TryHandleCancel()) return;
            GetComponentInParent<PlayerMenuController>()?.Close();
        }

        public void CloseConfirmation()
        {
            SetConfirmationVisible(false);
            if (restoreSelection != null && restoreSelection.gameObject.activeInHierarchy)
                restoreSelection.Select();
        }

        public void ConfirmPurchase()
        {
            if (busy || selectedOrder == null) return;
            if (IsQuoteExpired(selectedOrder))
            {
                _ = RefreshExpiredQuoteAsync();
                return;
            }
            _ = SubmitAndSettleAsync(selectedOrder);
        }

        private async Task RefreshAsync(int version)
        {
            ResolveServices();
            if (shop == null)
            {
                ShowUnavailable("RAM shop service is unavailable.");
                return;
            }

            SetBusy(true, "Refreshing authoritative catalog…");
            try
            {
                RamCatalog loaded = await shop.GetCatalogAsync();
                if (version != refreshVersion) return;
                catalog = loaded;
                if (balanceLabel != null)
                    balanceLabel.text = $"RAM {loaded.authoritativeBalance ?? "0"}";
                if (!loaded.checkoutAvailable)
                {
                    ShowUnavailable(string.IsNullOrWhiteSpace(loaded.maintenanceReason)
                        ? "RAM bundles are not available yet."
                        : loaded.maintenanceReason);
                    await ResumeLastOpenOrderAsync(version);
                    return;
                }
                if (unavailableLabel != null) unavailableLabel.gameObject.SetActive(false);
                SetStatus("Choose SOL. The Seeker wallet signs the payment.");
                await ResumeLastOpenOrderAsync(version);
            }
            catch (Exception ex)
            {
                if (version == refreshVersion) ShowUnavailable(ReadableError(ex));
            }
            finally
            {
                if (version == refreshVersion) SetBusy(false);
            }
        }

        private void SelectBundle(int ramAmount)
        {
            if (busy) return;
            RamBundle bundle = FindOffer(ramAmount);
            Button source = bundleButtons.Find(button =>
            {
                RamBundleCardPresenter card = button != null ? button.GetComponent<RamBundleCardPresenter>() : null;
                return card != null && card.RamAmount == ramAmount;
            });
            if (bundle == null)
            {
                string reason = catalog != null && catalog.checkoutAvailable
                    ? $"{selectedTender} has no verified devnet price. Pay with SOL from the Seeker wallet."
                    : catalog != null && !string.IsNullOrWhiteSpace(catalog.maintenanceReason)
                        ? ShopperMessage(catalog.maintenanceReason)
                        : "RAM bundles are not available yet.";
                SetStatus(reason);
                return;
            }

            BeginQuote(bundle, source);
        }

        private RamBundle FindOffer(int ramAmount)
        {
            if (catalog == null || !catalog.checkoutAvailable || catalog.bundles == null)
                return null;

            foreach (RamBundle bundle in catalog.bundles)
            {
                if (!int.TryParse(bundle.ramAmount, out int amount) || amount != ramAmount)
                    continue;
                RamTender price = bundle.prices?.Find(item =>
                    string.Equals(item.tender, selectedTender, StringComparison.OrdinalIgnoreCase));
                if (price != null)
                    return bundle;
            }

            return null;
        }

        private void BeginQuote(RamBundle bundle, Button source)
        {
            if (!busy) _ = BeginQuoteAsync(bundle, source);
        }

        private async Task BeginQuoteAsync(RamBundle bundle, Button source)
        {
            if (wallet == null || !wallet.IsConnected)
            {
                SetStatus("Connect the linked wallet before checkout.");
                return;
            }
            if (transactions == null)
            {
                SetStatus("Transaction recovery service is unavailable.");
                return;
            }
            if (RequiresDeviceSignAndSend && !wallet.SupportsSignAndSend)
            {
                SetStatus("Open this shop on a Seeker and approve the payment in the wallet.");
                return;
            }

            SetBusy(true, "Requesting a fresh server quote…");
            try
            {
                RamPurchaseOrder order = await shop.PrepareAsync(bundle.sku, selectedTender);
                var request = new ChainTxRequest(
                    new ChainTxScope(order.payment.cluster, wallet.PublicKey, null, order.orderId),
                    order.transactionBytes,
                    RequiresDeviceSignAndSend);
                order.payment.simulation = await transactions.SimulateAsync(request);
                if (!order.payment.simulation.success) throw new InvalidOperationException(order.payment.simulation.errorMessage);
                selectedBundle = bundle;
                selectedOrder = order;
                restoreSelection = source;
                SaveLastOpenOrder(order);
                BindConfirmation(order);
                SetConfirmationVisible(true);
            }
            catch (Exception ex)
            {
                SetStatus(ReadableError(ex));
                PendingIndicatorCall("ShowFailure");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task SubmitAndSettleAsync(RamPurchaseOrder order)
        {
            SetBusy(true, "Waiting for wallet approval…");
            try
            {
                var request = new ChainTxRequest(
                    new ChainTxScope(order.payment.cluster, order.payment.feePayer, null, order.orderId),
                    order.transactionBytes,
                    RequiresDeviceSignAndSend);
                TxResult result = await transactions.SubmitAsync(request);
                order.payment.simulation = result.simulation ?? order.payment.simulation;
                if (result.status == TxStatus.Rejected)
                {
                    transactions.ForgetIfUnsubmitted(request.scope);
                    ClearLastOpenOrder();
                    selectedOrder = null;
                    SetStatus("Payment was rejected in the wallet. Choose the bundle again for a fresh quote.");
                    PendingIndicatorCall("ShowFailure");
                    CloseConfirmation();
                    return;
                }

                RamPurchaseOrder settled;
                try
                {
                    settled = !string.IsNullOrWhiteSpace(result.txSignature)
                        ? await shop.ConfirmAsync(order.orderId, result.txSignature)
                        : await shop.GetStatusAsync(order.orderId, null, order.payment.reference);
                }
                catch (RamShopServiceException ex)
                {
                    SetStatus(ReadableError(ex) + " Recovery will keep checking Devnet.");
                    settled = order;
                }
                selectedOrder = settled;
                if (settled.state != RamPurchaseState.Settled)
                    settled = await PollStatusAsync(settled);

                selectedOrder = settled;
                if (settled.state == RamPurchaseState.Settled)
                {
                    if (balanceLabel != null) balanceLabel.text = $"RAM {settled.balanceAfter}";
                    SetStatus($"{settled.ramAmount} RAM settled. Receipt verified.");
                    PendingIndicatorCall("ShowSuccess");
                    ClearLastOpenOrder();
                    CloseConfirmation();
                }
                else if (IsTerminal(settled.state))
                {
                    SetStatus(TerminalOrderMessage(settled));
                    PendingIndicatorCall("ShowFailure");
                    ClearLastOpenOrder();
                    CloseConfirmation();
                }
                else
                {
                    SetStatus("The Seeker payment was sent. Keep this shop open until devnet finalizes it.");
                }
            }
            catch (Exception ex)
            {
                SetStatus(ReadableError(ex) + " Recovery remains active.");
                PendingIndicatorCall("ShowFailure");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task<RamPurchaseOrder> PollStatusAsync(RamPurchaseOrder order)
        {
            RamPurchaseOrder current = order;
            for (int attempt = 0; attempt < MaxStatusPolls && !IsTerminal(current.state); attempt++)
            {
                await Task.Delay(2000);
                try
                {
                    current = await shop.GetStatusAsync(current.orderId, current.paymentSignature, current.payment.reference);
                }
                catch (Exception ex)
                {
                    SetStatus(ReadableError(ex) + " Retrying automatically.");
                    continue;
                }
                if (IsTerminal(current.state))
                    break;
            }
            return current;
        }

        private async Task RefreshExpiredQuoteAsync()
        {
            RamPurchaseOrder expired = selectedOrder;
            RamBundle bundle = selectedBundle;
            Button source = restoreSelection;
            if (expired != null && transactions != null && expired.payment != null)
            {
                transactions.ForgetIfUnsubmitted(new ChainTxScope(
                    expired.payment.cluster,
                    expired.payment.feePayer,
                    null,
                    expired.orderId));
            }
            selectedOrder = null;
            ClearLastOpenOrder();
            SetConfirmationVisible(false);
            SetStatus("That Devnet quote expired. Requesting a fresh one.");
            if (bundle != null) await BeginQuoteAsync(bundle, source);
        }

        private async Task ResumeLastOpenOrderAsync(int version)
        {
            if (resumingOrder || shop == null || transactions == null || wallet == null || !wallet.IsConnected)
                return;
            LastOpenOrder saved = LoadLastOpenOrder();
            if (saved == null || string.IsNullOrWhiteSpace(saved.orderId)) return;
            if (!string.Equals(saved.wallet, wallet.PublicKey, StringComparison.Ordinal) ||
                (catalog != null && !string.IsNullOrWhiteSpace(catalog.cluster) &&
                 !string.Equals(saved.cluster, catalog.cluster, StringComparison.OrdinalIgnoreCase)))
                return;

            resumingOrder = true;
            try
            {
                var scope = new ChainTxScope(saved.cluster, saved.wallet, null, saved.orderId);
                PendingTxRecord pending = transactions.Query(scope);
                RamPurchaseOrder current = await shop.GetStatusAsync(saved.orderId, pending?.signature, saved.reference);
                if (version != refreshVersion) return;
                if (current.state == RamPurchaseState.Quoted && pending == null)
                {
                    ClearLastOpenOrder();
                    return;
                }
                SetStatus("Resuming the previous Devnet RAM payment.");
                current = await PollStatusAsync(current);
                if (version != refreshVersion) return;
                selectedOrder = current;
                if (current.state == RamPurchaseState.Settled)
                {
                    if (balanceLabel != null) balanceLabel.text = $"RAM {current.balanceAfter}";
                    SetStatus($"{current.ramAmount} RAM settled. Receipt verified.");
                    PendingIndicatorCall("ShowSuccess");
                    ClearLastOpenOrder();
                }
                else if (IsTerminal(current.state))
                {
                    SetStatus(TerminalOrderMessage(current));
                    PendingIndicatorCall("ShowFailure");
                    ClearLastOpenOrder();
                }
                else
                {
                    SetStatus("The Devnet payment is still pending. Recovery remains active.");
                }
            }
            catch (Exception ex)
            {
                if (version == refreshVersion) SetStatus(ReadableError(ex) + " Recovery remains active.");
            }
            finally
            {
                resumingOrder = false;
            }
        }

        private void SaveLastOpenOrder(RamPurchaseOrder order)
        {
            if (order?.payment == null) return;
            PlayerPrefs.SetString(LastOpenOrderKey, JsonUtility.ToJson(new LastOpenOrder
            {
                orderId = order.orderId,
                cluster = order.payment.cluster,
                wallet = order.payment.feePayer,
                reference = order.payment.reference,
            }));
            PlayerPrefs.Save();
        }

        private static LastOpenOrder LoadLastOpenOrder()
        {
            if (!PlayerPrefs.HasKey(LastOpenOrderKey)) return null;
            try
            {
                return JsonUtility.FromJson<LastOpenOrder>(PlayerPrefs.GetString(LastOpenOrderKey));
            }
            catch
            {
                return null;
            }
        }

        private static void ClearLastOpenOrder()
        {
            PlayerPrefs.DeleteKey(LastOpenOrderKey);
            PlayerPrefs.Save();
        }

        private static bool IsQuoteExpired(RamPurchaseOrder order)
        {
            return order == null || !DateTime.TryParse(
                order.quoteExpiresAt,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime expiresAt) || expiresAt.ToUniversalTime() <= DateTime.UtcNow;
        }

        private static bool IsTerminal(RamPurchaseState state)
        {
            return state == RamPurchaseState.Settled || state == RamPurchaseState.Rejected ||
                   state == RamPurchaseState.Failed || state == RamPurchaseState.ExpiredUnpaid ||
                   state == RamPurchaseState.ManualReview;
        }

        private static string TerminalOrderMessage(RamPurchaseOrder order)
        {
            switch (order.state)
            {
                case RamPurchaseState.ExpiredUnpaid:
                    return "That quote expired without a verified payment. Choose the bundle again.";
                case RamPurchaseState.ManualReview:
                    return "The payment details did not match the quote. No RAM was credited; support review is required.";
                case RamPurchaseState.Rejected:
                    return "Payment was rejected in the wallet.";
                default:
                    return "The payment did not complete. No RAM was credited.";
            }
        }

        private static bool RequiresDeviceSignAndSend
        {
            get
            {
#if UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        private void BindConfirmation(RamPurchaseOrder order)
        {
            if (confirmSummaryLabel == null) return;
            string mint = string.IsNullOrWhiteSpace(order.payment.mint) ? "Native SOL" : order.payment.mint;
            string simulation = order.payment.simulation != null && order.payment.simulation.success
                ? $"Simulation passed ({order.payment.simulation.unitsConsumed} CU)"
                : "Simulation unavailable";
            string paymentProgram = string.IsNullOrWhiteSpace(order.payment.paymentProgram)
                ? "System Program"
                : order.payment.paymentProgram;
            string creditProgram = string.IsNullOrWhiteSpace(order.payment.programId)
                ? "Chainbound program"
                : order.payment.programId;
            confirmSummaryLabel.text =
                $"{order.bundleName}  •  {order.ramAmount} RAM\n" +
                $"Pay {RamAtomicAmountFormatter.Format(order.payment.atomicAmount, order.payment.decimals)} {order.payment.tender}\n" +
                $"Cluster: {order.payment.cluster}\n" +
                $"Payment program: {paymentProgram}\nCredit program: {creditProgram}\n" +
                $"Destination: {order.payment.destination}\n" +
                $"Mint: {mint}\nFee payer: {order.payment.feePayer}\n" +
                $"Estimated fee: {order.payment.estimatedFeeLamports} lamports\n{simulation}";
        }

        private void ApplyExplicitNavigation()
        {
            var tenders = new[] { solButton, usdcButton, skrButton };
            for (int i = 0; i < tenders.Length; i++)
            {
                Button button = tenders[i];
                if (button == null) continue;
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = tenders[(i + tenders.Length - 1) % tenders.Length];
                navigation.selectOnRight = tenders[(i + 1) % tenders.Length];
                navigation.selectOnDown = i < bundleButtons.Count ? bundleButtons[i] : retryButton;
                button.navigation = navigation;
            }

            for (int i = 0; i < bundleButtons.Count; i++)
            {
                if (bundleButtons[i] == null) continue;
                Navigation navigation = bundleButtons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = i > 0 ? bundleButtons[i - 1] : bundleButtons[i];
                navigation.selectOnRight = i + 1 < bundleButtons.Count ? bundleButtons[i + 1] : bundleButtons[i];
                navigation.selectOnUp = i < tenders.Length ? tenders[i] : solButton;
                navigation.selectOnDown = retryButton;
                bundleButtons[i].navigation = navigation;
            }
        }

        private void SetConfirmationVisible(bool visible)
        {
            if (confirmSheet != null) confirmSheet.SetActive(visible);
            if (visible) confirmButton?.Select();
        }

        private void SetBusy(bool value, string message = null)
        {
            busy = value;
            PendingIndicatorCall("SetPending", value);
            if (confirmButton != null) confirmButton.interactable = !value;
            if (retryButton != null) retryButton.interactable = !value;
            if (!string.IsNullOrWhiteSpace(message)) SetStatus(message);
        }

        private void ShowUnavailable(string message)
        {
            string readable = ShopperMessage(message);
            if (unavailableLabel != null)
            {
                unavailableLabel.gameObject.SetActive(true);
                unavailableLabel.text = readable;
            }
            SetStatus(readable);
        }

        private static string ShopperMessage(string reason)
        {
            switch ((reason ?? string.Empty).Trim())
            {
                case "linked_wallet_required":
                    return "Link this Seeker wallet to your profile before buying RAM.";
                case "player_profile_not_initialized":
                    return "This wallet needs an on-chain player profile before it can buy RAM.";
                case "ram_reconciliation_required":
                    return "Profile RAM and on-chain RAM do not match yet.";
                case "ram_ledger_audit_pending":
                    return "Checking this wallet against the devnet profile.";
                case "payment_not_finalized":
                    return "Payment was sent and is waiting for Devnet finality.";
                case "solana_rpc_unavailable":
                case "solana_rpc_error":
                case "ram_shop_request_failed":
                    return "Devnet is taking longer than expected to respond.";
                case "catalog_not_configured":
                case "checkout_disabled":
                    return "RAM checkout is temporarily unavailable.";
                case "quote_expired_unpaid":
                case "order_not_confirmable":
                    return "That quote expired. Choose the bundle again for a fresh quote.";
                case "ram_shop_devnet_only":
                case "order_cluster_mismatch":
                case "catalog_cluster_mismatch":
                    return "Switch the connected wallet to Devnet and try again.";
                case "payment_execution_failed":
                    return "The Devnet payment failed. No RAM was credited.";
                case "order_not_found":
                    return "The saved RAM order could not be found. Choose a bundle to start again.";
                default:
                    return string.IsNullOrWhiteSpace(reason)
                        ? "RAM checkout could not complete. Please retry."
                        : reason.Contains("_")
                            ? "RAM checkout could not complete. Please retry."
                            : reason;
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message ?? string.Empty;
        }

        private void WireButtons()
        {
            if (buttonsWired) return;
            Wire(solButton, () => SelectTender("SOL"));
            Wire(usdcButton, () => SelectTender("USDC"));
            Wire(skrButton, () => SelectTender("SKR"));
            Wire(retryButton, Retry);
            Wire(closeButton, CloseShop);
            Wire(confirmButton, ConfirmPurchase);
            Wire(cancelConfirmButton, CloseConfirmation);
            foreach (Button button in bundleButtons)
            {
                RamBundleCardPresenter card = button != null ? button.GetComponent<RamBundleCardPresenter>() : null;
                int amount = card != null ? card.RamAmount : 0;
                Wire(button, () => SelectBundle(amount));
            }
            buttonsWired = true;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void ResolveServices()
        {
            if (supabaseConfig == null) supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
            if (shop == null && supabaseConfig != null) shop = new SupabaseRamShopService(supabaseConfig);
            SolanaServiceBridge bridge = SolanaServiceBridge.Instance;
            transactions = bridge != null ? bridge.TxOrchestrator : null;
            wallet = bridge != null ? bridge.WalletService : null;
        }

        private void ResolveAuthoredReferences()
        {
            balanceLabel ??= Find<TMP_Text>("RamBalanceLabel");
            statusLabel ??= Find<TMP_Text>("StatusLabel");
            unavailableLabel ??= Find<TMP_Text>("UnavailableLabel");
            solButton ??= Find<Button>("Tender_SOL");
            usdcButton ??= Find<Button>("Tender_USDC");
            skrButton ??= Find<Button>("Tender_SKR");
            bundleButtons.Clear();
            foreach (string cardName in new[] { "Bundle_5", "Bundle_20", "Bundle_100" })
            {
                Button card = Find<Button>(cardName);
                if (card != null) bundleButtons.Add(card);
            }
            retryButton ??= Find<Button>("RetryButton");
            closeButton ??= Find<Button>("CloseButton");
            confirmSheet ??= FindTransform("ConfirmSheet")?.gameObject;
            confirmSummaryLabel ??= Find<TMP_Text>("ConfirmSummaryLabel");
            confirmButton ??= Find<Button>("ConfirmButton");
            cancelConfirmButton ??= Find<Button>("CancelConfirmButton");
            if (pendingIndicator == null)
            {
                MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour != null && behaviour.GetType().Name == "UiPendingIndicator")
                    {
                        pendingIndicator = behaviour;
                        break;
                    }
                }
            }
        }

        private T Find<T>(string objectName) where T : Component
        {
            Transform child = FindTransform(objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private Transform FindTransform(string objectName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children) if (child.name == objectName) return child;
            return null;
        }

        private static void AddIfUsable(List<Selectable> order, Selectable selectable)
        {
            if (selectable != null && MenuSelectableNavigation.IsUsable(selectable)) order.Add(selectable);
        }

        private void HandleWalletChanged(string _) => Refresh();
        private void HandleWalletDisconnected() => Refresh();
        private static string ReadableError(Exception error)
        {
            if (error is RamShopServiceException service) return ShopperMessage(service.ErrorCode);
            return string.IsNullOrWhiteSpace(error?.Message)
                ? "RAM checkout could not complete. Please retry."
                : error.Message;
        }

        private void PendingIndicatorCall(string method, object value = null)
        {
            if (pendingIndicator == null) return;
            if (value == null)
                pendingIndicator.SendMessage(method, SendMessageOptions.DontRequireReceiver);
            else
                pendingIndicator.SendMessage(method, value, SendMessageOptions.DontRequireReceiver);
        }
    }
}
