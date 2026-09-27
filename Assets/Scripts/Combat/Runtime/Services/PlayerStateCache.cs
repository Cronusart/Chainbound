using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Local cache of all player PDA state, populated by PdaBootstrapService
    /// on wallet connect. Provides quick access for UI and gameplay without RPC calls.
    /// </summary>
    public class PlayerStateCache
    {
        public PlayerProfileData Profile { get; set; }
        public PlayerMoveVault MoveVault { get; set; }
        public ChainlingVaultData ChainlingVault { get; set; }
        public Dictionary<uint, EquippedMoveRegistryData> EquippedRegistries { get; private set; }

        public bool IsLoaded => Profile != null;

        public PlayerStateCache()
        {
            EquippedRegistries = new Dictionary<uint, EquippedMoveRegistryData>();
        }

        public void Clear()
        {
            Profile = null;
            MoveVault = null;
            ChainlingVault = null;
            EquippedRegistries.Clear();
        }
    }
}
