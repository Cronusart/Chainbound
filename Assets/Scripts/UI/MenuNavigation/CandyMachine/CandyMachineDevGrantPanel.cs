using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// In-menu RAM / Known grants. Hidden in release player builds via Debug.isDebugBuild.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CandyMachineDevGrantPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button initializeSpineButton;
        [SerializeField] private Button grantRamButton;
        [SerializeField] private Button grantKnownButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private CandyMachineDevConfigSO config;

        private CandyMachineScreen screen;
        private string pendingRamGrantIdempotencyKey;
        private DevnetRamGrantReview pendingRamGrantReview;

        public static bool ShouldShow => DevCandyMachineInventory.IsDevSurfaceAllowed;

        private bool GrantSurfaceEnabled => ShouldShow && (config == null || config.ShowDevGrantStrip);

        /// <summary>
        /// Binds the authored panel when Candy Machine opens at runtime. The prefab already
        /// serializes its controls, so only the host reference needs to be supplied here.
        /// </summary>
        public void Bind(CandyMachineScreen host)
        {
            screen = host;
            EnsureInitializeButton();
            WireButtons();
            RefreshVisibility();
            RefreshForCurrentState();
        }

        public void Bind(
            CandyMachineScreen host,
            CanvasGroup canvasGroup,
            Button ram,
            Button known,
            Button clear,
            TMP_Text status,
            CandyMachineDevConfigSO devConfig)
        {
            screen = host;
            group = canvasGroup;
            grantRamButton = ram;
            grantKnownButton = known;
            clearButton = clear;
            statusLabel = status;
            config = devConfig;
            EnsureInitializeButton();
            WireButtons();
            RefreshVisibility();
            RefreshForCurrentState();
        }

        public void RefreshForCurrentState()
        {
            if (screen == null || !GrantSurfaceEnabled)
                return;

            pendingRamGrantReview = null;
            pendingRamGrantIdempotencyKey = null;
            bool localPreview = screen.DevInventory != null;
            bool freshProfile = screen.DisplayRamBalance <= 0;
            if (grantRamButton != null)
                grantRamButton.interactable = localPreview || freshProfile;

            SetStatus(localPreview
                ? $"LOCAL PREVIEW | RAM {screen.DisplayRamBalance}"
                : freshProfile
                    ? "DEVNET TOOLS READY | RAM GRANT AVAILABLE"
                    : $"DEVNET | RAM {screen.DisplayRamBalance} | FRESH-PROFILE RAM GRANT DISABLED");
        }

        public void RefreshVisibility()
        {
            // OnEnable runs when the candy panel activates, before Open binds the host.
            // Leave the authored strip alone until that bind, or it disappears before testing starts.
            if (screen == null)
                return;

            bool show = GrantSurfaceEnabled && screen.CanShowDevGrantPanel;
            if (show)
                RestoreCollapsedStrip();

            if (group != null)
            {
                group.alpha = show ? 1f : 0f;
                group.interactable = show;
                group.blocksRaycasts = show;
            }

            gameObject.SetActive(show);
        }

        /// <summary>
        /// Keeps long-running dev-strip messages aligned with the authoritative
        /// server mirror without clearing an in-progress two-step grant review.
        /// </summary>
        public void SynchronizeDisplayedRam()
        {
            if (screen == null || statusLabel == null || string.IsNullOrWhiteSpace(statusLabel.text))
                return;
            statusLabel.text = Regex.Replace(
                statusLabel.text,
                @"\bRAM\s+\d+\b",
                $"RAM {screen.DisplayRamBalance}",
                RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// A zero-size strip cannot draw. Restore the dev-bar rect only in that case
        /// so an authored non-zero layout is left alone.
        /// </summary>
        private void RestoreCollapsedStrip()
        {
            if (!(transform is RectTransform rect))
                return;

            bool pointAnchor = rect.anchorMin == rect.anchorMax;
            bool zeroSize = rect.sizeDelta.sqrMagnitude <= 1f;
            LayoutElement layout = rect.GetComponent<LayoutElement>();
            bool shortLayout = layout == null || layout.preferredHeight < 48f;
            if (!pointAnchor && !zeroSize && !shortLayout)
                return;

            CandyMachineDevGrantStripLayoutUtility.ApplyStripRoot(rect);
            LayoutAuthoredControls();
        }

        private void LayoutAuthoredControls()
        {
            if (initializeSpineButton == null)
                return;

            CandyMachineDevGrantStripLayoutUtility.LayoutControl(initializeSpineButton, 0.02f, 0.17f);
            CandyMachineDevGrantStripLayoutUtility.LayoutControl(grantRamButton, 0.18f, 0.33f);
            CandyMachineDevGrantStripLayoutUtility.LayoutControl(grantKnownButton, 0.34f, 0.49f);
            CandyMachineDevGrantStripLayoutUtility.LayoutControl(clearButton, 0.50f, 0.62f);
            CandyMachineDevGrantStripLayoutUtility.LayoutControl(statusLabel, 0.63f, 0.98f);
        }

        private void Awake()
        {
            WireButtons();
        }

        private void OnEnable()
        {
            RefreshVisibility();
        }

        private void WireButtons()
        {
            if (initializeSpineButton != null)
            {
                initializeSpineButton.onClick.RemoveAllListeners();
                initializeSpineButton.onClick.AddListener(InitializeOwnershipSpine);
            }

            if (grantRamButton != null)
            {
                grantRamButton.onClick.RemoveListener(GrantRam);
                grantRamButton.onClick.AddListener(GrantRam);
            }

            if (grantKnownButton != null)
            {
                grantKnownButton.onClick.RemoveListener(GrantKnown);
                grantKnownButton.onClick.AddListener(GrantKnown);
            }

            if (clearButton != null)
            {
                clearButton.onClick.RemoveListener(ClearInventory);
                clearButton.onClick.AddListener(ClearInventory);
            }
        }

        private void EnsureInitializeButton()
        {
            if (initializeSpineButton == null && grantRamButton != null)
            {
                Transform parent = grantRamButton.transform.parent;
                Transform existing = parent != null ? parent.Find("InitializeSpineButton") : null;
                initializeSpineButton = existing != null
                    ? existing.GetComponent<Button>()
                    : Instantiate(grantRamButton, parent, false);
                if (initializeSpineButton == null)
                    return;

                initializeSpineButton.name = "InitializeSpineButton";
                TMP_Text label = initializeSpineButton.transform.Find("Label")?.GetComponent<TMP_Text>()
                    ?? initializeSpineButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = "INIT SPINE";
            }

            if (initializeSpineButton != null)
                LayoutAuthoredControls();
        }

        private async void InitializeOwnershipSpine()
        {
            if (!GrantSurfaceEnabled)
            {
                SetStatus("Ownership bootstrap is available only in development builds.");
                return;
            }

            WalletOperationGuardResult initialAccess = await EnsureWalletAccessAsync("initialize the Ownership Spine");
            if (initialAccess == null || !initialAccess.success)
            {
                SetStatus(initialAccess?.message ?? "Wallet access was not authorized.");
                return;
            }

            SolanaServiceBridge bridge = SolanaServiceBridge.Instance;
            IWalletService wallet = bridge != null ? bridge.WalletService : null;
            SolanaConfigSO chain = bridge != null ? bridge.Config : null;
            if (bridge == null || wallet == null || !wallet.IsConnected || string.IsNullOrWhiteSpace(wallet.PublicKey))
            {
                SetStatus("Connect the Seed Vault wallet first.");
                return;
            }

            if (chain == null
                || !string.Equals(chain.ownershipCluster?.Trim(), "devnet", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(chain.rpcEndpoint)
                || chain.rpcEndpoint.IndexOf("devnet", StringComparison.OrdinalIgnoreCase) < 0)
            {
                SetStatus("Blocked: Ownership Spine bootstrap is Devnet-only.");
                return;
            }

            SetButtonsInteractable(false);
            try
            {
                string owner = wallet.PublicKey;
                IProfileService profiles = bridge.ProfileService;
                IMoveVaultService vaults = bridge.MoveVaultService;
                if (profiles == null || vaults == null)
                    throw new InvalidOperationException("Ownership Spine services are unavailable.");

                SetStatus("DEVNET: checking PlayerProfile...");
                if (!await profiles.HasProfileAsync(owner))
                {
                    WalletOperationGuardResult signingAccess = await EnsureWalletAccessAsync("create the Player Profile PDA");
                    if (signingAccess == null || !signingAccess.success)
                        throw new InvalidOperationException(signingAccess?.message ?? "Wallet access was not authorized.");
                    SetStatus("DEVNET: confirm CREATE PLAYER PROFILE in wallet.");
                    TxResult result = await profiles.CreateProfileAsync(owner);
                    RequireConfirmed(result, "PlayerProfile");
                }

                SetStatus("DEVNET: checking MoveVault...");
                PlayerMoveVault vault = await vaults.LoadVaultAsync(owner);
                if (vault == null)
                {
                    WalletOperationGuardResult signingAccess = await EnsureWalletAccessAsync("create the Move Vault PDA");
                    if (signingAccess == null || !signingAccess.success)
                        throw new InvalidOperationException(signingAccess?.message ?? "Wallet access was not authorized.");
                    SetStatus("DEVNET: confirm CREATE MOVE VAULT in wallet.");
                    VaultOperationResult result = await vaults.CreateVaultAsync(owner);
                    if (result == null || !result.success)
                        throw new InvalidOperationException(result?.errorMessage ?? "MoveVault creation failed.");
                }

                PlayerProfileData profile = await profiles.LoadProfileAsync(owner);
                vault = await vaults.LoadVaultAsync(owner);
                if (profile == null || vault == null)
                    throw new InvalidOperationException("PDA verification failed after confirmation.");

                if (screen != null && screen.UsesDevnetOwnership)
                    await screen.RefreshOwnershipStateAsync();
                SetStatus($"SPINE READY | RAM {profile.ramBalance} | VAULT {vault.knownCount}/{vault.knownCapacity}");
            }
            catch (Exception exception)
            {
                SetStatus($"INIT FAILED: {exception.Message}");
                Debug.LogError($"[CandyMachineDevGrantPanel] Ownership Spine bootstrap failed: {exception}");
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async Task<WalletOperationGuardResult> EnsureWalletAccessAsync(string actionName)
        {
            IWalletOperationGuard guard = FindObjectOfType<WalletOperationGuard>(true);
            if (guard == null)
            {
                return WalletOperationGuardResult.Failed(
                    WalletOperationGuardStatus.UnsupportedPlatform,
                    "Wallet operation guard is unavailable.");
            }

            return await guard.EnsureVerifiedWalletAsync(new WalletOperationRequest
            {
                operationKind = WalletOperationKind.OnChainTransaction,
                actionName = actionName,
                clientVersion = Application.version,
                requireFreshProof = true,
            });
        }

        private static void RequireConfirmed(TxResult result, string accountName)
        {
            if (result == null || (result.status != TxStatus.Confirmed && result.status != TxStatus.Finalized))
                throw new InvalidOperationException(result?.errorMessage ?? $"{accountName} creation failed.");
        }

        private void SetButtonsInteractable(bool value)
        {
            if (initializeSpineButton != null) initializeSpineButton.interactable = value;
            if (grantRamButton != null)
            {
                bool localPreview = screen != null && screen.DevInventory != null;
                bool freshProfile = screen == null || screen.DisplayRamBalance <= 0;
                grantRamButton.interactable = value && (localPreview || freshProfile);
            }
            if (grantKnownButton != null) grantKnownButton.interactable = value;
            if (clearButton != null) clearButton.interactable = value;
        }

        private async void GrantRam()
        {
            if (!GrantSurfaceEnabled)
                return;

            DevCandyMachineInventory inventory = screen != null ? screen.DevInventory : null;
            if (inventory != null)
            {
                int amount = config != null ? config.DefaultRamGrant : 500;
                inventory.GrantRam(amount);
                SetStatus($"Granted {amount} local-preview RAM.");
                screen.RefreshPresentation();
                return;
            }

            if (screen == null || !screen.UsesDevnetOwnership)
                return;

            SetButtonsInteractable(false);
            try
            {
                if (pendingRamGrantReview == null || string.IsNullOrWhiteSpace(pendingRamGrantIdempotencyKey))
                {
                    pendingRamGrantIdempotencyKey = Guid.NewGuid().ToString("N");
                    SetStatus("DEVNET: preparing and simulating RAM grant...");
                    pendingRamGrantReview = await screen.ReviewDevnetRamGrantAsync(pendingRamGrantIdempotencyKey);
                    if (pendingRamGrantReview == null || !pendingRamGrantReview.simulationSucceeded)
                        throw new InvalidOperationException(
                            pendingRamGrantReview?.simulationMessage ?? "RAM grant simulation failed.");

                    SetStatus(
                        $"REVIEW {pendingRamGrantReview.cluster.ToUpperInvariant()} | +{pendingRamGrantReview.amount} RAM | " +
                        $"FEE {pendingRamGrantReview.estimatedNetworkFeeLamports} LAMPORTS | PRESS GRANT RAM AGAIN TO SIGN");
                    return;
                }

                SetStatus("DEVNET: confirm RAM grant in wallet...");
                DevnetRamGrantResult result = await screen.SubmitDevnetRamGrantAsync(pendingRamGrantIdempotencyKey);
                if (result == null || !result.success || !result.confirmed)
                    throw new InvalidOperationException(result?.error ?? "RAM grant was not confirmed.");

                pendingRamGrantReview = null;
                pendingRamGrantIdempotencyKey = null;
                screen.RefreshPresentation();
                SetStatus($"RAM VERIFIED | +{result.amount} | BALANCE {result.balanceAfter}");
            }
            catch (Exception exception)
            {
                pendingRamGrantReview = null;
                pendingRamGrantIdempotencyKey = null;
                SetStatus($"RAM GRANT FAILED: {exception.Message}");
                Debug.LogError($"[CandyMachineDevGrantPanel] RAM grant failed: {exception}");
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async void GrantKnown()
        {
            if (!GrantSurfaceEnabled)
                return;

            DevCandyMachineInventory inventory = screen != null ? screen.DevInventory : null;
            if (inventory != null)
            {
                string moveId = config != null ? config.DefaultGrantMoveId : null;
                if (string.IsNullOrWhiteSpace(moveId))
                {
                    var catalog = inventory.CatalogMoves;
                    if (catalog.Count == 0)
                    {
                        SetStatus("No catalog moves to grant.");
                        return;
                    }

                    moveId = catalog[0].moveId;
                }

                KnownMoveRecord localRecord = inventory.GrantKnown(moveId);
                SetStatus($"Granted local Known {localRecord.moveId} ({localRecord.instanceId}).");
                screen.RefreshPresentation();
                return;
            }

            if (screen == null || !screen.UsesDevnetOwnership)
                return;

            pendingRamGrantReview = null;
            pendingRamGrantIdempotencyKey = null;
            SetButtonsInteractable(false);
            try
            {
                SetStatus("DEVNET: rolling Known through ownership authority...");
                KnownMoveRecord record = await screen.RollDevnetKnownAsync();
                SetStatus($"KNOWN VERIFIED | {record.moveId} | RAM {screen.DisplayRamBalance}");
            }
            catch (Exception exception)
            {
                SetStatus($"KNOWN ROLL FAILED: {exception.Message}");
                Debug.LogError($"[CandyMachineDevGrantPanel] Known roll failed: {exception}");
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private void ClearInventory()
        {
            DevCandyMachineInventory inventory = screen != null ? screen.DevInventory : null;
            if (inventory == null)
            {
                SetStatus("Authoritative devnet inventory cannot be cleared from the client.");
                return;
            }

            inventory.Clear();
            SetStatus("Cleared Known / Stamped inventory.");
            screen.RefreshPresentation();
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null)
                statusLabel.text = value ?? string.Empty;
        }
    }
}
