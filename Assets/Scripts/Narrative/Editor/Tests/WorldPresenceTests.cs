using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldPresenceTests
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

        for (int i = 0; i < playerPrefKeys.Count; i++)
            PlayerPrefs.DeleteKey(playerPrefKeys[i]);
        playerPrefKeys.Clear();
        PlayerPrefs.Save();
    }

    [Test]
    public void Classify_UsesRailThenDurablePrefixesAndDurableDefault()
    {
        WorldPresenceConfigSO config = WorldPresenceConfigSO.CreateRuntimeDefault();
        cleanup.Add(config);

        Assert.That(config.Classify("intro.guardrail.house"), Is.EqualTo(WorldPresenceLifetime.Rail));
        Assert.That(config.Classify("sequence.intro.lock"), Is.EqualTo(WorldPresenceLifetime.Rail));
        Assert.That(config.Classify("world.pickup.loom_lab.orb"), Is.EqualTo(WorldPresenceLifetime.Durable));
        Assert.That(config.Classify("world.obstacle.link1.canopy_block"), Is.EqualTo(WorldPresenceLifetime.Durable));
        Assert.That(config.Classify("jungle.obstacle.vine"), Is.EqualTo(WorldPresenceLifetime.Durable));
    }

    [Test]
    public async Task CaptureRailSnapshot_OmitsDurablePickupAndWorldKeys()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("presence-capture");
        service.SetFlag("intro.guardrail.house", true);
        service.SetFlag("sequence.intro.lock", false);
        service.SetFlag("world.pickup.loom_lab.orb", true);
        service.SetFlag("world.obstacle.link1.canopy_block", true);

        List<NarrativeFlagValue> snapshot = service.CaptureRailSnapshot();
        Assert.That(snapshot, Has.Count.EqualTo(2));
        Assert.That(snapshot[0].key, Is.EqualTo("intro.guardrail.house"));
        Assert.That(snapshot[0].value, Is.True);
        Assert.That(snapshot[1].key, Is.EqualTo("sequence.intro.lock"));
        Assert.That(snapshot[1].value, Is.False);
    }

    [Test]
    public async Task RestoreRailSnapshot_RewindsMissingRailsAndNeverWritesDurableKeys()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("presence-restore");
        service.SetFlag("intro.guardrail.house", true);
        service.SetFlag("intro.guardrail.collider3", true);
        service.SetFlag("world.pickup.loom_lab.orb", true);

        service.RestoreRailSnapshot(new List<NarrativeFlagValue>
        {
            new NarrativeFlagValue("intro.guardrail.house", true),
            new NarrativeFlagValue("world.pickup.loom_lab.orb", false)
        });

        Assert.That(service.GetFlag("intro.guardrail.house"), Is.True);
        Assert.That(service.GetFlag("intro.guardrail.collider3"), Is.False);
        Assert.That(service.GetFlag("world.pickup.loom_lab.orb"), Is.True);
    }

    [Test]
    public void ResumePolicy_EmptySnapshotFallsBackToAuthoredList()
    {
        var authored = new List<Step_SetCheckpoint.ResumeProfileWorldFlag>
        {
            new Step_SetCheckpoint.ResumeProfileWorldFlag
            {
                flagKey = "intro.guardrail.house",
                flagValue = false
            }
        };

        List<(string key, bool value)> resolved = WorldPresenceResumePolicy.Resolve(
            new List<NarrativeFlagValue>(),
            authored);

        Assert.That(resolved, Has.Count.EqualTo(1));
        Assert.That(resolved[0].key, Is.EqualTo("intro.guardrail.house"));
        Assert.That(resolved[0].value, Is.False);
    }

    [Test]
    public void ResumePolicy_CheckpointSnapshotWinsOverAuthoredList()
    {
        var snapshot = new List<NarrativeFlagValue>
        {
            new NarrativeFlagValue("intro.guardrail.house", true)
        };
        var authored = new List<Step_SetCheckpoint.ResumeProfileWorldFlag>
        {
            new Step_SetCheckpoint.ResumeProfileWorldFlag
            {
                flagKey = "intro.guardrail.house",
                flagValue = false
            }
        };

        List<(string key, bool value)> resolved = WorldPresenceResumePolicy.Resolve(snapshot, authored);
        Assert.That(resolved, Has.Count.EqualTo(1));
        Assert.That(resolved[0].value, Is.True);
    }

    [Test]
    public async Task Binding_LeavesSceneDefaultWhenFlagMissing()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("presence-binding");

        var host = new GameObject("PresenceHost");
        var target = new GameObject("CanopyBlock");
        host.SetActive(false);
        cleanup.Add(host);
        cleanup.Add(target);
        target.SetActive(true);

        WorldPresenceBinding binding = host.AddComponent<WorldPresenceBinding>();
        NarrativeTestUtility.SetPrivateField(binding, "presenceId", "world.obstacle.link1.canopy_block");
        NarrativeTestUtility.SetPrivateField(binding, "target", target);
        NarrativeTestUtility.SetPrivateField(binding, "activeWhenTrue", false);
        host.SetActive(true);
        InvokeBindingLifecycle(binding, "OnEnable");

        Assert.That(target.activeSelf, Is.True);

        service.SetFlag("world.obstacle.link1.canopy_block", true);
        Assert.That(target.activeSelf, Is.False);
    }

    [Test]
    public async Task Binding_ApplyOnEnableOnly_WaitsForNextEnable()
    {
        ProfileWorldFlagStateService service = await HydrateServiceAsync("presence-deferred");

        var host = new GameObject("DeferredPresenceHost");
        var target = new GameObject("Zeb");
        host.SetActive(false);
        cleanup.Add(host);
        cleanup.Add(target);
        target.SetActive(true);

        WorldPresenceBinding binding = host.AddComponent<WorldPresenceBinding>();
        NarrativeTestUtility.SetPrivateField(binding, "presenceId", "world.coffee.zeb_departed");
        NarrativeTestUtility.SetPrivateField(binding, "target", target);
        NarrativeTestUtility.SetPrivateField(binding, "activeWhenTrue", false);
        NarrativeTestUtility.SetPrivateField(binding, "applyOnEnableOnly", true);
        host.SetActive(true);
        InvokeBindingLifecycle(binding, "OnEnable");

        Assert.That(target.activeSelf, Is.True);
        service.SetFlag("world.coffee.zeb_departed", true);
        Assert.That(target.activeSelf, Is.True, "A deferred presence stays through the current visit.");

        host.SetActive(false);
        InvokeBindingLifecycle(binding, "OnDisable");
        host.SetActive(true);
        InvokeBindingLifecycle(binding, "OnEnable");
        Assert.That(target.activeSelf, Is.False);
    }

    [Test]
    public void Gate_DoesNotHideTargetBeforeHydrate()
    {
        var host = new GameObject("GateHost");
        var target = new GameObject("MartNpc");
        host.SetActive(false);
        cleanup.Add(host);
        cleanup.Add(target);
        target.SetActive(true);

        ProfileWorldFlagObjectGate gate = host.AddComponent<ProfileWorldFlagObjectGate>();
        NarrativeTestUtility.SetPrivateField(gate, "target", target);
        NarrativeTestUtility.SetPrivateField(gate, "flagKey", "mart_departed_loomville");
        NarrativeTestUtility.SetPrivateField(gate, "expectedFlagValue", true);
        NarrativeTestUtility.SetPrivateField(gate, "activeWhenMatched", false);
        host.SetActive(true);

        Assert.That(target.activeSelf, Is.True);
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

    private static void InvokeBindingLifecycle(WorldPresenceBinding binding, string methodName)
    {
        MethodInfo method = typeof(WorldPresenceBinding).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(binding, null);
    }

    private static void DestroyLingering<T>() where T : UnityEngine.Object
    {
        T lingering = UnityEngine.Object.FindObjectOfType<T>();
        if (lingering != null)
            UnityEngine.Object.DestroyImmediate(lingering is Component component ? component.gameObject : lingering);
    }
}
