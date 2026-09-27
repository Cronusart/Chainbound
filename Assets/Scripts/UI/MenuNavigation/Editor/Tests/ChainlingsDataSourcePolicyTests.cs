using System.Reflection;
using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

public sealed class ChainlingsDataSourcePolicyTests
{
    [Test]
    public void IsProviderAllowed_SeparatesProductionAndHarnessProviders()
    {
        GameObject root = new GameObject("ChainlingsDataSourcePolicyTests");
        root.SetActive(false);
        try
        {
            SupabasePlayerRosterProvider production = root.AddComponent<SupabasePlayerRosterProvider>();
            ChainlingsMenuTestHarness harness = root.AddComponent<ChainlingsMenuTestHarness>();

            Assert.IsTrue(ChainlingsDataSourcePolicy.IsProviderAllowed(
                ChainlingsDataSourceMode.Production,
                production));
            Assert.IsFalse(ChainlingsDataSourcePolicy.IsProviderAllowed(
                ChainlingsDataSourceMode.Production,
                harness));
            Assert.IsFalse(ChainlingsDataSourcePolicy.IsProviderAllowed(
                ChainlingsDataSourceMode.Harness,
                production));
            Assert.IsTrue(ChainlingsDataSourcePolicy.IsProviderAllowed(
                ChainlingsDataSourceMode.Harness,
                harness));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ProductionScreen_IgnoresLocalHarnessAndResolvesSupabaseRosterProvider()
    {
        GameObject root = new GameObject("ChainlingsProductionProviderIsolationTests");
        root.SetActive(false);
        try
        {
            ChainlingsPartyScreen screen = root.AddComponent<ChainlingsPartyScreen>();
            root.AddComponent<ChainlingsMenuTestHarness>();
            screen.SetDataSourceMode(ChainlingsDataSourceMode.Production);

            InvokePrivate(screen, "EnsureServiceBehaviours");

            MonoBehaviour resolved = GetPrivateField<MonoBehaviour>(screen, "rosterProviderBehaviour");
            Assert.IsInstanceOf<SupabasePlayerRosterProvider>(resolved);
            Assert.IsFalse(resolved is IChainlingsMenuTestHarness);
            Assert.IsNull(InvokePrivate(screen, "ResolveTestHarness"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static object InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method, $"Expected private method '{methodName}'.");
        return method.Invoke(target, null);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field, $"Expected private field '{fieldName}'.");
        return (T)field.GetValue(target);
    }
}
