using System;
using System.Collections;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// How a unique pickup looks after <c>world.pickup.*</c> is spent.
/// Binding still hides the interact volume; this is the extra spent look.
/// </summary>
public enum WorldPickupSpentPresentation
{
    HideInteractableOnly = 0,
    StayOpen = 1,
    HideVisual = 2
}

/// <summary>
/// Unique world pickup interpreter. Grants a catalog item via
/// <see cref="IPlayerItemInventory"/> then writes <c>world.pickup.*</c>.
/// Pair with <see cref="WorldPresenceBinding"/> on the same host
/// (never disable this host; bind a child volume).
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldPickupGrant : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Stable unique spawn key. Prefer world.pickup.<scene>.<object>.")]
    [SerializeField] private string presenceId = "world.pickup.unassigned";

    [Tooltip("Optional. When set, Collect stamps this binding with Presence Id.")]
    [SerializeField] private WorldPresenceBinding presenceBinding;

    [Header("Item")]
    [Tooltip("Catalog itemId preview. Production grants look up this spawn on the server.")]
    [SerializeField] private string itemId = "signal_orb";

    [SerializeField] [Min(1)] private int quantity = 1;

    [Tooltip("When true, Collect fails unless a grant service is present.")]
    [SerializeField] private bool requireInventoryGrant;

    [Tooltip("Optional IPlayerItemInventory override (tests). Leave empty to discover GameServices.")]
    [SerializeField] private MonoBehaviour inventoryServiceBehaviour;

    [Header("Presentation")]
    [Tooltip("HideInteractableOnly: prompt gone. StayOpen: lid snaps open on continue (no SFX). HideVisual: disable spentVisual.")]
    [SerializeField] private WorldPickupSpentPresentation spentPresentation =
        WorldPickupSpentPresentation.HideInteractableOnly;

    [Tooltip("Optional lid/chest. Auto-finds in parents when empty. Used by StayOpen.")]
    [SerializeField] private DoorController door;

    [Tooltip("Mesh/item to disable for HideVisual. Must not be this host or an ancestor.")]
    [SerializeField] private GameObject spentVisual;

    [Tooltip("Optional IItemObtainPresenter override. Leave empty to use ItemObtainPresenter.Instance.")]
    [SerializeField] private MonoBehaviour obtainPresenterBehaviour;

    [Tooltip("Unscaled delay after the spent presentation begins before showing the item-obtain panel. Use this to let an opening sound or animation lead the panel.")]
    [SerializeField] [Min(0f)] private float obtainPanelDelaySeconds;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private ProfileWorldFlagStateService flagService;
    private bool hasAppliedClaimedPresentation;
    private bool isLiveCollecting;
    private bool collectInFlight;

    public string PresenceId => presenceId != null ? presenceId.Trim() : "";
    public string ItemId => itemId != null ? itemId.Trim() : "";
    public int Quantity => Mathf.Max(1, quantity);
    public bool IsClaimed => flagService != null && flagService.HasFlag(PresenceId) && flagService.GetFlag(PresenceId);

    private void Awake()
    {
        if (presenceBinding == null)
            presenceBinding = GetComponent<WorldPresenceBinding>();
        if (door == null)
            door = GetComponentInParent<DoorController>();

        StampBindingPresenceId();
        flagService = ProfileWorldFlagStateService.Instance;
    }

    private void OnEnable()
    {
        if (flagService == null)
            flagService = ProfileWorldFlagStateService.Instance;
        if (flagService != null)
        {
            flagService.OnReadyChanged -= HandleReadyChanged;
            flagService.OnReadyChanged += HandleReadyChanged;
        }

        TryApplyClaimedPresentation(animateDoor: false);
    }

    private void OnDisable()
    {
        if (flagService == null)
            return;
        flagService.OnReadyChanged -= HandleReadyChanged;
    }

    /// <summary>UnityEvent target for <see cref="InteractableObject"/>.</summary>
    public void Collect()
    {
        if (collectInFlight)
            return;
        StartCoroutine(CollectRoutine());
    }

    public Task<bool> CollectAsync()
    {
        return CollectInternalAsync();
    }

    private IEnumerator CollectRoutine()
    {
        Task<bool> task = CollectInternalAsync();
        while (!task.IsCompleted)
            yield return null;
        if (task.IsFaulted && task.Exception != null)
            Debug.LogException(task.Exception, this);
    }

    private async Task<bool> CollectInternalAsync()
    {
        if (collectInFlight)
            return false;

        string spawnId = PresenceId;
        if (string.IsNullOrWhiteSpace(spawnId) ||
            string.Equals(spawnId, "world.pickup.unassigned", StringComparison.Ordinal))
        {
            Debug.LogWarning("[WorldPickupGrant] Presence Id is unassigned.", this);
            return false;
        }

        if (flagService == null)
            flagService = ProfileWorldFlagStateService.Instance;
        if (flagService == null)
        {
            Debug.LogError("[WorldPickupGrant] ProfileWorldFlagStateService not found.", this);
            return false;
        }

        if (IsClaimed)
        {
            if (enableDebugLogs)
                Debug.Log($"[WorldPickupGrant] Already claimed '{spawnId}'.", this);
            TryApplyClaimedPresentation(animateDoor: false);
            return false;
        }

        string previewItemId = ItemId;
        if (!string.IsNullOrWhiteSpace(previewItemId) && !PlayerItemCatalogSeed.TryGet(previewItemId, out _))
        {
            Debug.LogError($"[WorldPickupGrant] Unknown itemId '{previewItemId}'.", this);
            return false;
        }

        collectInFlight = true;
        InputLockHandle lockHandle = default;
        try
        {
            InputLockService inputLock = InputLockService.Instance;
            if (inputLock != null)
                lockHandle = inputLock.AcquireLock("WorldPickupGrant", InputLockChannel.All, this);

            IPlayerItemInventory inventory = ResolveInventoryService();
            WorldPickupGrantResult grantResult = null;
            if (inventory != null)
            {
                grantResult = await inventory.GrantWorldPickupAsync(spawnId);
                if (grantResult == null || !grantResult.ok)
                {
                    Debug.LogWarning(
                        $"[WorldPickupGrant] Grant failed for '{spawnId}': {(grantResult != null ? grantResult.error : "null_result")}.",
                        this);
                    return false;
                }
            }
            else if (requireInventoryGrant)
            {
                Debug.LogWarning("[WorldPickupGrant] requireInventoryGrant is set but no grant service is available.", this);
                return false;
            }
            else if (enableDebugLogs)
            {
                Debug.Log(
                    $"[WorldPickupGrant] No grant service. Flag-only claim '{spawnId}' → {previewItemId} x{Quantity}.",
                    this);
            }

            StampBindingPresenceId();
            isLiveCollecting = true;
            flagService.SetFlag(spawnId, true);
            ApplySpentPresentation(animateDoor: true);
            isLiveCollecting = false;
            hasAppliedClaimedPresentation = true;

            bool alreadyClaimed = grantResult != null && grantResult.alreadyClaimed;
            string obtainedItemId = grantResult != null && !string.IsNullOrWhiteSpace(grantResult.itemId)
                ? grantResult.itemId
                : previewItemId;
            int obtainedQty = grantResult != null && grantResult.quantity > 0
                ? grantResult.quantity
                : Quantity;

            if (!alreadyClaimed && await WaitForObtainPanelDelayAsync())
                ShowObtainPanel(obtainedItemId, obtainedQty);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[WorldPickupGrant] Claimed '{spawnId}' ({obtainedItemId} x{obtainedQty}, already_claimed={alreadyClaimed}).",
                    this);
            }

            return true;
        }
        finally
        {
            if (lockHandle.IsValid)
                lockHandle.Release();
            collectInFlight = false;
            isLiveCollecting = false;
        }
    }

    private void ShowObtainPanel(string obtainedItemId, int obtainedQty)
    {
        if (string.IsNullOrWhiteSpace(obtainedItemId) || obtainedQty <= 0)
            return;

        IItemObtainPresenter presenter = obtainPresenterBehaviour as IItemObtainPresenter;
        if (presenter == null)
            presenter = ItemObtainPresenter.Instance;
        if (presenter == null)
            return;

        presenter.Show(obtainedItemId, obtainedQty);
    }

    private async Task<bool> WaitForObtainPanelDelayAsync()
    {
        float delay = Mathf.Max(0f, obtainPanelDelaySeconds);
        if (delay <= 0f)
            return this != null && isActiveAndEnabled;

        float showAt = Time.realtimeSinceStartup + delay;
        while (this != null && isActiveAndEnabled && Time.realtimeSinceStartup < showAt)
            await Task.Yield();

        return this != null && isActiveAndEnabled;
    }

    private void HandleReadyChanged(bool ready)
    {
        if (ready)
            TryApplyClaimedPresentation(animateDoor: false);
    }

    private void TryApplyClaimedPresentation(bool animateDoor)
    {
        if (!IsClaimed || isLiveCollecting)
            return;
        if (hasAppliedClaimedPresentation && !animateDoor)
            return;

        hasAppliedClaimedPresentation = true;
        ApplySpentPresentation(animateDoor);
    }

    private void ApplySpentPresentation(bool animateDoor)
    {
        switch (spentPresentation)
        {
            case WorldPickupSpentPresentation.StayOpen:
                if (door == null)
                    door = GetComponentInParent<DoorController>();
                if (door == null)
                    break;
                if (animateDoor)
                    door.Open();
                else
                    door.SnapOpen();
                break;

            case WorldPickupSpentPresentation.HideVisual:
                HideSpentVisual();
                break;
        }
    }

    private void HideSpentVisual()
    {
        if (spentVisual == null)
            return;
        if (WouldDisableHost(spentVisual))
        {
            Debug.LogWarning(
                "[WorldPickupGrant] spentVisual cannot be this host or an ancestor. Binding would die with it.",
                this);
            return;
        }

        spentVisual.SetActive(false);
    }

    private bool WouldDisableHost(GameObject visual)
    {
        if (visual == gameObject)
            return true;

        Transform current = transform;
        while (current != null)
        {
            if (current.gameObject == visual)
                return true;
            current = current.parent;
        }

        return false;
    }

    private IPlayerItemInventory ResolveInventoryService()
    {
        if (inventoryServiceBehaviour is IPlayerItemInventory overrideInventory)
            return overrideInventory;

        CoreBagWorldPickupGrantService core = FindFirstObjectByType<CoreBagWorldPickupGrantService>(FindObjectsInactive.Include);
        if (core != null)
            return core;

        StubPlayerItemInventory stub = FindFirstObjectByType<StubPlayerItemInventory>(FindObjectsInactive.Include);
        if (stub != null)
            return stub;

        if (!Application.isPlaying)
            return null;

        if (Resources.Load<SupabaseConfigSO>("SupabaseConfig") != null)
            return CoreBagWorldPickupGrantService.EnsureInstance();

        return StubPlayerItemInventory.EnsureInstance();
    }

    private void StampBindingPresenceId()
    {
        if (presenceBinding == null || string.IsNullOrWhiteSpace(PresenceId))
            return;
        presenceBinding.AuthorPresenceId(PresenceId);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        presenceId = presenceId != null ? presenceId.Trim() : "";
        itemId = itemId != null ? itemId.Trim() : "";
        quantity = Mathf.Max(1, quantity);

        if (string.IsNullOrWhiteSpace(presenceId) ||
            string.Equals(presenceId, "world.pickup.unassigned", StringComparison.Ordinal))
        {
            Debug.LogWarning("[WorldPickupGrant] Assign a unique presenceId (world.pickup.<scene>.<spawn>).", this);
        }

        if (!string.IsNullOrWhiteSpace(itemId) && PlayerItemCatalogSeed.TryGet(itemId, out PlayerItemDefinition def))
        {
            if (def.ownershipKind != PlayerItemOwnershipKind.ProfileStack)
            {
                Debug.LogWarning(
                    $"[WorldPickupGrant] itemId '{itemId}' is not a profile_stack item. World chests never mint Core collectibles.",
                    this);
            }
        }
        else if (!string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogWarning($"[WorldPickupGrant] itemId '{itemId}' is not in PlayerItemCatalogSeed.", this);
        }

        if (inventoryServiceBehaviour != null && !(inventoryServiceBehaviour is IPlayerItemInventory))
        {
            Debug.LogWarning(
                "[WorldPickupGrant] Inventory Service Behaviour must implement IPlayerItemInventory.",
                this);
        }

        if (obtainPresenterBehaviour != null && !(obtainPresenterBehaviour is IItemObtainPresenter))
        {
            Debug.LogWarning(
                "[WorldPickupGrant] Obtain Presenter Behaviour must implement IItemObtainPresenter.",
                this);
        }

        if (presenceBinding == null)
            presenceBinding = GetComponent<WorldPresenceBinding>();
        if (presenceBinding != null &&
            !string.IsNullOrWhiteSpace(presenceBinding.PresenceId) &&
            !string.IsNullOrWhiteSpace(PresenceId) &&
            !string.Equals(presenceBinding.PresenceId, PresenceId, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"[WorldPickupGrant] Presence Id '{PresenceId}' does not match WorldPresenceBinding '{presenceBinding.PresenceId}'.",
                this);
        }

        if (spentPresentation == WorldPickupSpentPresentation.HideVisual)
        {
            if (spentVisual == null)
            {
                Debug.LogWarning("[WorldPickupGrant] HideVisual requires a spentVisual that is not this host.", this);
            }
            else if (WouldDisableHost(spentVisual))
            {
                Debug.LogWarning(
                    "[WorldPickupGrant] spentVisual cannot be this host or an ancestor.",
                    this);
            }
        }
    }
#endif
}
