using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chainbound.Combat
{
    [Serializable]
    public class EncounterReturnContext
    {
        public string sceneId;
        public Vector3 position;
        public Quaternion rotation;
        /// <summary>Optional SpawnPoint id in the return scene for post-battle placement (authored +Y).</summary>
        public string returnSpawnId;
        /// <summary>All loaded build scenes at encounter start; used to restore additive overworld residency.</summary>
        public string[] loadedChunkIds;
        public bool isValid;
    }

    public interface IEncounterReturnContextService
    {
        /// <param name="returnSpawnId">Optional spawn point ID in the scene; when set, post-battle prefers SpawnService.SpawnPlayerAt over raw snapshot teleport.</param>
        void SaveReturnContext(
            string sceneId,
            Vector3 position,
            Quaternion rotation,
            string returnSpawnId = null,
            string[] loadedChunkIds = null);
        /// <summary>
        /// Updates only <see cref="EncounterReturnContext.returnSpawnId"/> when a prior snapshot already exists
        /// (e.g. sequence step saved position; encounter bridge only adds spawn id).
        /// </summary>
        void SetReturnSpawnIdOnly(string returnSpawnId);
        EncounterReturnContext GetReturnContext();
        void ClearReturnContext();
    }

    /// <summary>
    /// Shared PlayerPrefs-backed encounter return context storage for overworld -> battle -> return flow.
    /// </summary>
    public class EncounterReturnContextService : IEncounterReturnContextService
    {
        public const string KeyScene = "EncReturn_Scene";
        public const string KeyPosX = "EncReturn_PosX";
        public const string KeyPosY = "EncReturn_PosY";
        public const string KeyPosZ = "EncReturn_PosZ";
        public const string KeyRotX = "EncReturn_RotX";
        public const string KeyRotY = "EncReturn_RotY";
        public const string KeyRotZ = "EncReturn_RotZ";
        public const string KeyRotW = "EncReturn_RotW";
        public const string KeyValid = "EncReturn_Valid";
        public const string KeyReturnSpawnId = "EncReturn_SpawnId";
        public const string KeyLoadedChunkIds = "EncReturn_LoadedChunks";

        [Serializable]
        private sealed class LoadedChunkIdsPayload
        {
            public List<string> ids = new List<string>();
        }

        public void SaveReturnContext(
            string sceneId,
            Vector3 position,
            Quaternion rotation,
            string returnSpawnId = null,
            string[] loadedChunkIds = null)
        {
            PlayerPrefs.SetString(KeyScene, sceneId ?? string.Empty);
            PlayerPrefs.SetFloat(KeyPosX, position.x);
            PlayerPrefs.SetFloat(KeyPosY, position.y);
            PlayerPrefs.SetFloat(KeyPosZ, position.z);
            PlayerPrefs.SetFloat(KeyRotX, rotation.x);
            PlayerPrefs.SetFloat(KeyRotY, rotation.y);
            PlayerPrefs.SetFloat(KeyRotZ, rotation.z);
            PlayerPrefs.SetFloat(KeyRotW, rotation.w);
            if (string.IsNullOrEmpty(returnSpawnId))
                PlayerPrefs.DeleteKey(KeyReturnSpawnId);
            else
                PlayerPrefs.SetString(KeyReturnSpawnId, returnSpawnId);

            if (loadedChunkIds == null || loadedChunkIds.Length == 0)
            {
                PlayerPrefs.DeleteKey(KeyLoadedChunkIds);
            }
            else
            {
                LoadedChunkIdsPayload payload = new LoadedChunkIdsPayload();
                for (int i = 0; i < loadedChunkIds.Length; i++)
                {
                    string scene = loadedChunkIds[i]?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(scene) && !payload.ids.Contains(scene))
                        payload.ids.Add(scene);
                }

                PlayerPrefs.SetString(KeyLoadedChunkIds, JsonUtility.ToJson(payload));
            }
            PlayerPrefs.SetInt(KeyValid, 1);
            PlayerPrefs.Save();
        }

        public void SetReturnSpawnIdOnly(string returnSpawnId)
        {
            if (PlayerPrefs.GetInt(KeyValid, 0) != 1)
                return;

            if (string.IsNullOrEmpty(returnSpawnId))
                PlayerPrefs.DeleteKey(KeyReturnSpawnId);
            else
                PlayerPrefs.SetString(KeyReturnSpawnId, returnSpawnId);
            PlayerPrefs.Save();
        }

        public EncounterReturnContext GetReturnContext()
        {
            if (PlayerPrefs.GetInt(KeyValid, 0) != 1)
                return new EncounterReturnContext { isValid = false };

            return new EncounterReturnContext
            {
                sceneId = PlayerPrefs.GetString(KeyScene, string.Empty),
                position = new Vector3(
                    PlayerPrefs.GetFloat(KeyPosX, 0f),
                    PlayerPrefs.GetFloat(KeyPosY, 0f),
                    PlayerPrefs.GetFloat(KeyPosZ, 0f)),
                rotation = new Quaternion(
                    PlayerPrefs.GetFloat(KeyRotX, 0f),
                    PlayerPrefs.GetFloat(KeyRotY, 0f),
                    PlayerPrefs.GetFloat(KeyRotZ, 0f),
                    PlayerPrefs.GetFloat(KeyRotW, 1f)),
                returnSpawnId = PlayerPrefs.GetString(KeyReturnSpawnId, string.Empty),
                loadedChunkIds = ReadLoadedChunkIds(),
                isValid = true,
            };
        }

        public void ClearReturnContext()
        {
            PlayerPrefs.DeleteKey(KeyScene);
            PlayerPrefs.DeleteKey(KeyPosX);
            PlayerPrefs.DeleteKey(KeyPosY);
            PlayerPrefs.DeleteKey(KeyPosZ);
            PlayerPrefs.DeleteKey(KeyRotX);
            PlayerPrefs.DeleteKey(KeyRotY);
            PlayerPrefs.DeleteKey(KeyRotZ);
            PlayerPrefs.DeleteKey(KeyRotW);
            PlayerPrefs.DeleteKey(KeyValid);
            PlayerPrefs.DeleteKey(KeyReturnSpawnId);
            PlayerPrefs.DeleteKey(KeyLoadedChunkIds);
            PlayerPrefs.Save();
        }

        private static string[] ReadLoadedChunkIds()
        {
            string json = PlayerPrefs.GetString(KeyLoadedChunkIds, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<string>();

            try
            {
                LoadedChunkIdsPayload payload = JsonUtility.FromJson<LoadedChunkIdsPayload>(json);
                return payload?.ids?.ToArray() ?? Array.Empty<string>();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }
    }

    /// <summary>
    /// Saves overworld return position from the active Player (tag "Player") into the same PlayerPrefs-backed
    /// store used by <see cref="EncounterReturnContextService"/>. Call from sequence steps (e.g. when a zone
    /// trigger fires) so post-battle return matches the encounter location, not a later position.
    /// </summary>
    public static class EncounterReturnSnapshotUtility
    {
        /// <param name="returnSpawnId">Optional spawn point ID in this scene for post-battle (authored placement).</param>
        public static void SaveFromActivePlayer(bool logToConsole = false, string returnSpawnId = null)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 position = player != null ? player.transform.position : Vector3.zero;
            Quaternion rotation = player != null ? player.transform.rotation : Quaternion.identity;

            SolanaServiceBridge bridge = SolanaServiceBridge.Instance != null
                ? SolanaServiceBridge.Instance
                : UnityEngine.Object.FindObjectOfType<SolanaServiceBridge>(true);

            IEncounterReturnContextService svc = bridge != null && bridge.EncounterReturnContextService != null
                ? bridge.EncounterReturnContextService
                : new EncounterReturnContextService();

            svc.SaveReturnContext(sceneName, position, rotation, returnSpawnId, CaptureLoadedBuildSceneIds());

            if (logToConsole)
            {
                Debug.Log(
                    $"[EncounterReturnSnapshot] Saved return context: scene='{sceneName}', pos={position}, rot={rotation.eulerAngles}, returnSpawnId='{returnSpawnId ?? ""}'");
            }
        }

        /// <summary>
        /// Captures scene names without taking a dependency on the Assembly-CSharp
        /// streaming coordinator. The coordinator later filters this list to its
        /// configured overworld set.
        /// </summary>
        public static string[] CaptureLoadedBuildSceneIds()
        {
            List<string> sceneIds = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
                    continue;

                if (!sceneIds.Contains(scene.name))
                    sceneIds.Add(scene.name);
            }

            return sceneIds.ToArray();
        }
    }
}
