using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Information needed to return the player to the overworld after battle.
    /// Used by CombatPostBattleRouter's OnReturnToOverworld event.
    /// </summary>
    public struct OverworldReturnInfo
    {
        /// <summary>Scene name to load.</summary>
        public string sceneName;

        /// <summary>Spawn point ID (used when useExactPosition is false).</summary>
        public string spawnId;

        /// <summary>If true, teleport the player to the exact position/rotation.</summary>
        public bool useExactPosition;

        /// <summary>Exact world position (only valid if useExactPosition is true).</summary>
        public Vector3 position;

        /// <summary>Exact rotation (only valid if useExactPosition is true).</summary>
        public Quaternion rotation;

        /// <summary>
        /// Optional SpawnPoint id in the return scene. When set and registered after load,
        /// post-battle placement uses SpawnService.SpawnPlayerAt instead of raw snapshot teleport.
        /// </summary>
        public string returnSpawnId;

        /// <summary>
        /// Additive build scenes that were loaded when the encounter began.
        /// Assembly-CSharp filters these to the configured overworld set before
        /// restoring residency, so combat does not depend on world services.
        /// </summary>
        public string[] loadedChunkIds;

        /// <summary>
        /// True when this return is a party-wipe home route. Assembly-CSharp
        /// may remap destination to the last outdoor sanctuary portal.
        /// </summary>
        public bool isDefeatReturn;

        /// <summary>
        /// Authored overworld chunks that must be resident on sanctuary continue/wipe.
        /// Empty keeps the streaming set's preload-on-entry list.
        /// </summary>
        public string[] preferredOverworldChunkSceneIds;
    }
}
