#if UNITY_EDITOR
using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.Narrative.Combat;
using UnityEngine;

public sealed class NarrativeCombatLaunchHandoffTests
{
    [SetUp]
    public void SetUp()
    {
        NarrativeCombatLaunchBridge.ClearPendingPayload();
        DestroyExistingBridge();
    }

    [TearDown]
    public void TearDown()
    {
        NarrativeCombatLaunchBridge.ClearPendingPayload();
        DestroyExistingBridge();
    }

    [Test]
    public void Payload_SurvivesBridgeDestruction()
    {
        ManualEncounterProviderSO provider = ScriptableObject.CreateInstance<ManualEncounterProviderSO>();
        var host = new GameObject("NarrativeCombatLaunchBridge.Test");
        try
        {
            NarrativeCombatLaunchBridge bridge = host.AddComponent<NarrativeCombatLaunchBridge>();
            bridge.Publish(new NarrativeCombatLaunchContext(
                "intro_first_creature",
                provider,
                null,
                true,
                true,
                true,
                EncounterFleePolicy.UseDifficultyDefault));

            Object.DestroyImmediate(host);

            Assert.That(NarrativeCombatLaunchBridge.TryPeekPending(out NarrativeCombatLaunchContext peeked), Is.True);
            Assert.That(peeked.Provider, Is.SameAs(provider));
            Assert.That(peeked.EncounterId, Is.EqualTo("intro_first_creature"));
            Assert.That(peeked.DisableSupabasePartyResolution, Is.True);
            Assert.That(peeked.ArmIntroAutoEscaper, Is.True);
            Assert.That(NarrativeCombatLaunchBridge.FindExisting(), Is.Null);
        }
        finally
        {
            if (provider != null)
                Object.DestroyImmediate(provider);
            if (host != null)
                Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Clear_DropsUnconsumedPayload()
    {
        ManualEncounterProviderSO provider = ScriptableObject.CreateInstance<ManualEncounterProviderSO>();
        var host = new GameObject("NarrativeCombatLaunchBridge.Clear");
        try
        {
            NarrativeCombatLaunchBridge bridge = host.AddComponent<NarrativeCombatLaunchBridge>();
            bridge.Publish(new NarrativeCombatLaunchContext(
                "intro_first_creature",
                provider,
                null,
                false,
                false,
                false,
                EncounterFleePolicy.UseDifficultyDefault));

            bridge.Clear();

            Assert.That(NarrativeCombatLaunchBridge.HasPending, Is.False);
            Assert.That(NarrativeCombatLaunchBridge.TryPeekPending(out _), Is.False);
        }
        finally
        {
            if (provider != null)
                Object.DestroyImmediate(provider);
            if (host != null)
                Object.DestroyImmediate(host);
        }
    }

    private static void DestroyExistingBridge()
    {
        NarrativeCombatLaunchBridge existing = NarrativeCombatLaunchBridge.FindExisting();
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);
    }
}
#endif
