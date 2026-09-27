using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Defines the provider boundary for Chainlings menu data. Production screens may only
    /// resolve production implementations; harness screens may only resolve the explicit
    /// test-harness contract.
    /// </summary>
    public static class ChainlingsDataSourcePolicy
    {
        public static bool IsProviderAllowed(ChainlingsDataSourceMode mode, object provider)
        {
            if (provider == null)
                return false;

            bool isHarnessProvider = provider is IChainlingsMenuTestHarness;
            return mode == ChainlingsDataSourceMode.Harness
                ? isHarnessProvider
                : !isHarnessProvider;
        }

        public static MonoBehaviour KeepAllowed(
            ChainlingsDataSourceMode mode,
            MonoBehaviour provider)
        {
            return IsProviderAllowed(mode, provider) ? provider : null;
        }
    }
}
