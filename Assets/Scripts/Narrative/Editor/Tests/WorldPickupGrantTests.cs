using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldPickupGrantTests
{
    private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
    private readonly List<string> playerPrefKeys = new List<string>();

    [TearDown]
    public void TearDown()
    {
        WorldPresenceConfigRuntime.SetTestOverride(null);

        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                UnityEngine.Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();

        DestroyLingering<ProfileWorldFlagStateService>();
        DestroyLingering<WorldState>();
        DestroyLingering<StubPlayerItemInventory>();
        DestroyLingering<CoreBagWorldPickupGrantService>();
        DestroyLingering<ItemObtainPresenter>();
        DestroyLingering<InputLockService>();

        for (int i = 0; i < playerPrefKeys.Count; i++)
            PlayerPrefs.DeleteKey(playerPrefKeys[i]);
        playerPrefKeys.Clear();
        PlayerPrefs.Save();
    }

    [Test]
    public async Task Collect_SetsDurablePickupFlagAndHidesBoundVolume()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("pickup-collect");
        WorldPickupGrant grant = CreatePickup(
            "world.pickup.link1.backpack_01",
            "signal_orb",
            out GameObject volume);

        Assert.That(volume.activeSelf, Is.True);
        Assert.That(await grant.CollectAsync(), Is.True);
        Assert.That(service.GetFlag("world.pickup.link1.backpack_01"), Is.True);
        Assert.That(volume.activeSelf, Is.False);
    }

    [Test]
    public async Task Collect_IsIdempotentAndDoesNotCallGrantTwice()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("pickup-once");
        FakePlayerItemInventory fake = CreateFakeInventory();

        WorldPickupGrant grant = CreatePickup(
            "world.pickup.mart.shelf_orbs_left",
            "signal_orb",
            out _,
            fake);

        Assert.That(await grant.CollectAsync(), Is.True);
        Assert.That(await grant.CollectAsync(), Is.False);
        Assert.That(fake.Calls, Is.EqualTo(1));
        Assert.That(service.GetFlag("world.pickup.mart.shelf_orbs_left"), Is.True);
    }

    [Test]
    public async Task Collect_UnknownItemDoesNotSetFlag()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("pickup-bad-item");
        WorldPickupGrant grant = CreatePickup(
            "world.pickup.link1.backpack_01",
            "not_a_real_item",
            out GameObject volume);

        Assert.That(await grant.CollectAsync(), Is.False);
        Assert.That(service.HasFlag("world.pickup.link1.backpack_01"), Is.False);
        Assert.That(volume.activeSelf, Is.True);
    }

    [Test]
    public async Task Collect_FailedGrantDoesNotSetFlag()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("pickup-grant-fail");
        FakePlayerItemInventory fake = CreateFakeInventory();
        fake.Succeed = false;
        FakeItemObtainPresenter obtain = CreateFakeObtain();

        WorldPickupGrant grant = CreatePickup(
            "world.pickup.link1.backpack_01",
            "chainling_patch",
            out GameObject volume,
            fake,
            obtainPresenter: obtain);

        Assert.That(await grant.CollectAsync(), Is.False);
        Assert.That(service.HasFlag("world.pickup.link1.backpack_01"), Is.False);
        Assert.That(volume.activeSelf, Is.True);
        Assert.That(fake.Calls, Is.EqualTo(1));
        Assert.That(obtain.ShowCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Collect_RequireInventoryGrantFailsWhenServiceMissing()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("pickup-require-grant");
        WorldPickupGrant grant = CreatePickup(
            "world.pickup.link1.backpack_01",
            "chainling_patch",
            out GameObject volume,
            requireInventoryGrant: true);

        Assert.That(await grant.CollectAsync(), Is.False);
        Assert.That(service.HasFlag("world.pickup.link1.backpack_01"), Is.False);
        Assert.That(volume.activeSelf, Is.True);
    }

    [Test]
    public async Task Collect_AlreadyClaimed_SetsFlagWithoutObtainPanelOrSecondQty()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("pickup-already-claimed");
        FakePlayerItemInventory fake = CreateFakeInventory();
        fake.AlreadyClaimed = true;
        fake.RemainingQuantity = 1;
        FakeItemObtainPresenter obtain = CreateFakeObtain();

        WorldPickupGrant grant = CreatePickup(
            "world.pickup.link1.backpack_01",
            "chainling_patch",
            out _,
            fake,
            obtainPresenter: obtain);

        Assert.That(await grant.CollectAsync(), Is.True);
        Assert.That(service.GetFlag("world.pickup.link1.backpack_01"), Is.True);
        Assert.That(fake.Calls, Is.EqualTo(1));
        Assert.That(fake.GrantedQuantity, Is.EqualTo(0));
        Assert.That(obtain.ShowCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Collect_Success_ShowsObtainPresenterOnce()
    {
        await HydrateServiceAsync("pickup-obtain");
        FakePlayerItemInventory fake = CreateFakeInventory();
        FakeItemObtainPresenter obtain = CreateFakeObtain();

        WorldPickupGrant grant = CreatePickup(
            "world.pickup.link1.backpack_01",
            "chainling_patch",
            out _,
            fake,
            obtainPresenter: obtain);

        Assert.That(await grant.CollectAsync(), Is.True);
        Assert.That(obtain.ShowCount, Is.EqualTo(1));
        Assert.That(obtain.LastItemId, Is.EqualTo("chainling_patch"));
        Assert.That(obtain.LastQuantity, Is.EqualTo(1));

        Assert.That(await grant.CollectAsync(), Is.False);
        Assert.That(obtain.ShowCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Collect_HideVisualDisablesSpentTargetNotHost()
    {
        await HydrateServiceAsync("pickup-hide-visual");
        var visual = new GameObject("OrbMesh");
        cleanup.Add(visual);

        WorldPickupGrant grant = CreatePickup(
            "world.pickup.loom_lab.dumpster_orb",
            "signal_orb",
            out GameObject volume,
            spentPresentation: WorldPickupSpentPresentation.HideVisual,
            spentVisual: visual);

        Assert.That(await grant.CollectAsync(), Is.True);
        Assert.That(visual.activeSelf, Is.False);
        Assert.That(grant.gameObject.activeSelf, Is.True);
        Assert.That(volume.activeSelf, Is.False);
    }

    [Test]
    public async Task AlreadyClaimed_StayOpenSnapsDoorWithoutOpeningEvent()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("pickup-stay-open");
        service.SetFlag("world.pickup.link1.backpack_01", true);

        var chest = new GameObject("Chest");
        var hinge = new GameObject("Hinge");
        hinge.transform.SetParent(chest.transform, false);
        cleanup.Add(chest);

        DoorController door = chest.AddComponent<DoorController>();
        NarrativeTestUtility.SetPrivateField(door, "hingePivot", hinge.transform);
        NarrativeTestUtility.SetPrivateField(door, "openAngle", 33f);
        NarrativeTestUtility.SetPrivateField(door, "rotationAxis", Vector3.up);

        int openingStarted = 0;
        door.OnDoorOpeningStarted.AddListener(() => openingStarted++);

        Quaternion closed = hinge.transform.localRotation;
        CreatePickup(
            "world.pickup.link1.backpack_01",
            "signal_orb",
            out _,
            spentPresentation: WorldPickupSpentPresentation.StayOpen,
            door: door,
            parent: chest.transform);

        Assert.That(door.CurrentState, Is.EqualTo(DoorController.DoorState.Open));
        Assert.That(hinge.transform.localRotation, Is.Not.EqualTo(closed));
        Assert.That(openingStarted, Is.EqualTo(0));
    }

    [Test]
    public void SnapOpen_AppliesOpenPoseWithoutSfxEvents()
    {
        var chest = new GameObject("Door");
        var hinge = new GameObject("Hinge");
        hinge.transform.SetParent(chest.transform, false);
        cleanup.Add(chest);

        DoorController door = chest.AddComponent<DoorController>();
        NarrativeTestUtility.SetPrivateField(door, "hingePivot", hinge.transform);
        NarrativeTestUtility.SetPrivateField(door, "openAngle", 90f);
        NarrativeTestUtility.SetPrivateField(door, "rotationAxis", Vector3.up);

        int openingStarted = 0;
        int opened = 0;
        door.OnDoorOpeningStarted.AddListener(() => openingStarted++);
        door.OnDoorOpened.AddListener(() => opened++);

        Quaternion closed = hinge.transform.localRotation;
        door.SnapOpen();

        Assert.That(door.CurrentState, Is.EqualTo(DoorController.DoorState.Open));
        Assert.That(hinge.transform.localRotation, Is.EqualTo(closed * Quaternion.AngleAxis(90f, Vector3.up)));
        Assert.That(openingStarted, Is.EqualTo(0));
        Assert.That(opened, Is.EqualTo(0));
    }

    private WorldPickupGrant CreatePickup(
        string spawnId,
        string itemId,
        out GameObject volume,
        FakePlayerItemInventory inventory = null,
        WorldPickupSpentPresentation spentPresentation = WorldPickupSpentPresentation.HideInteractableOnly,
        DoorController door = null,
        GameObject spentVisual = null,
        Transform parent = null,
        FakeItemObtainPresenter obtainPresenter = null,
        bool requireInventoryGrant = false)
    {
        var root = new GameObject("WorldPickup");
        volume = new GameObject("Interactable");
        volume.transform.SetParent(root.transform, false);
        if (parent != null)
            root.transform.SetParent(parent, false);
        root.SetActive(false);
        cleanup.Add(root);

        WorldPresenceBinding binding = root.AddComponent<WorldPresenceBinding>();
        NarrativeTestUtility.SetPrivateField(binding, "presenceId", spawnId);
        NarrativeTestUtility.SetPrivateField(binding, "target", volume);
        NarrativeTestUtility.SetPrivateField(binding, "activeWhenTrue", false);

        WorldPickupGrant grant = root.AddComponent<WorldPickupGrant>();
        NarrativeTestUtility.SetPrivateField(grant, "presenceId", spawnId);
        NarrativeTestUtility.SetPrivateField(grant, "presenceBinding", binding);
        NarrativeTestUtility.SetPrivateField(grant, "itemId", itemId);
        NarrativeTestUtility.SetPrivateField(grant, "quantity", 1);
        NarrativeTestUtility.SetPrivateField(grant, "requireInventoryGrant", requireInventoryGrant);
        NarrativeTestUtility.SetPrivateField(grant, "spentPresentation", spentPresentation);
        if (door != null)
            NarrativeTestUtility.SetPrivateField(grant, "door", door);
        if (spentVisual != null)
            NarrativeTestUtility.SetPrivateField(grant, "spentVisual", spentVisual);
        if (inventory != null)
            NarrativeTestUtility.SetPrivateField(grant, "inventoryServiceBehaviour", inventory);
        if (obtainPresenter != null)
            NarrativeTestUtility.SetPrivateField(grant, "obtainPresenterBehaviour", obtainPresenter);

        root.SetActive(true);
        return grant;
    }

    private FakePlayerItemInventory CreateFakeInventory()
    {
        var host = new GameObject("FakeInventory");
        cleanup.Add(host);
        return host.AddComponent<FakePlayerItemInventory>();
    }

    private FakeItemObtainPresenter CreateFakeObtain()
    {
        var host = new GameObject("FakeObtain");
        cleanup.Add(host);
        return host.AddComponent<FakeItemObtainPresenter>();
    }

    private async Task<ProfileWorldFlagStateService> HydrateServiceAsync(string profileSuffix)
    {
        WorldPresenceConfigSO config = WorldPresenceConfigSO.CreateRuntimeDefault();
        cleanup.Add(config);
        WorldPresenceConfigRuntime.SetTestOverride(config);

        var identity = new NarrativeProfileIdentity
        {
            playerProfileId = profileSuffix + "_" + Guid.NewGuid().ToString("N")
        };
        string hash = Hash128.Compute(identity.CacheIdentity).ToString();
        playerPrefKeys.Add("SignalJam_ProfileWorldFlags_v1_" + hash);
        playerPrefKeys.Add("SignalJam_ProfileWorldFlagOutbox_v1_" + hash);

        ProfileWorldFlagStateService service = ProfileWorldFlagStateService.Instance;
        cleanup.Add(service.gameObject);
        await service.HydrateProfileAsync(identity, forceOffline: true);
        return service;
    }

    private static void DestroyLingering<T>() where T : UnityEngine.Object
    {
        T lingering = UnityEngine.Object.FindObjectOfType<T>();
        if (lingering != null)
            UnityEngine.Object.DestroyImmediate(lingering is Component component ? component.gameObject : lingering);
    }

    private sealed class FakePlayerItemInventory : MonoBehaviour, IPlayerItemInventory
    {
        public bool Succeed = true;
        public bool AlreadyClaimed;
        public int Calls;
        public int GrantedQuantity;
        public int RemainingQuantity;
        public string ItemId = "chainling_patch";

        public Task<WorldPickupGrantResult> GrantWorldPickupAsync(string spawnId)
        {
            Calls++;
            if (!Succeed)
                return Task.FromResult(WorldPickupGrantResult.Failed("grant_failed"));

            if (AlreadyClaimed)
            {
                return Task.FromResult(WorldPickupGrantResult.Ok(
                    ItemId,
                    1,
                    RemainingQuantity,
                    alreadyClaimed: true));
            }

            GrantedQuantity += 1;
            RemainingQuantity = GrantedQuantity;
            return Task.FromResult(WorldPickupGrantResult.Ok(ItemId, 1, RemainingQuantity, alreadyClaimed: false));
        }
    }

    private sealed class FakeItemObtainPresenter : MonoBehaviour, IItemObtainPresenter
    {
        public int ShowCount;
        public string LastItemId;
        public int LastQuantity;
        public bool IsShowing { get; private set; }

        public void Show(string itemId, int quantity)
        {
            ShowCount++;
            LastItemId = itemId;
            LastQuantity = quantity;
            IsShowing = true;
        }

        public void Hide()
        {
            IsShowing = false;
        }
    }
}
