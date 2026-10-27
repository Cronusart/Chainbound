using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chainbound.Combat
{
    /// <summary>
    /// Activates one scenery root in the shared combat scene. An encounter writes
    /// <see cref="CombatStageRequest"/> before load. Opening the scene directly
    /// leaves authored active states and still publishes the visible stage so
    /// atmosphere can follow it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatStageDirector : MonoBehaviour
    {
        private readonly List<StageHandle> stages = new List<StageHandle>(4);
        private string publishedStageId;

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            if (string.IsNullOrEmpty(publishedStageId))
                return;

            CombatStageRequest.ClearActive(publishedStageId);
            publishedStageId = null;
        }

        private void Apply()
        {
            Collect(gameObject.scene, stages);
            if (stages.Count == 0)
            {
                Debug.LogWarning($"[CombatStageDirector] No combat stages found in '{gameObject.scene.name}'.");
                return;
            }

            if (!CombatStageRequest.TryConsumePending(out string requested))
            {
                PublishVisible();
                return;
            }

            int match = IndexOf(requested);
            if (match < 0)
            {
                Debug.LogWarning(
                    $"[CombatStageDirector] Stage '{requested}' is not in '{gameObject.scene.name}'. " +
                    "Leaving authored stages unchanged.");
                PublishVisible();
                return;
            }

            for (int i = 0; i < stages.Count; i++)
                stages[i].Root.SetActive(i == match);

            Publish(stages[match].StageId);
        }

        private void PublishVisible()
        {
            for (int i = 0; i < stages.Count; i++)
            {
                if (!stages[i].Root.activeSelf)
                    continue;

                Publish(stages[i].StageId);
                return;
            }

            Publish(stages[0].StageId);
        }

        private void Publish(string stageId)
        {
            publishedStageId = stageId;
            CombatStageRequest.PublishActive(stageId);
        }

        private int IndexOf(string stageId)
        {
            for (int i = 0; i < stages.Count; i++)
            {
                if (string.Equals(stages[i].StageId, stageId, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void Collect(Scene scene, List<StageHandle> results)
        {
            results.Clear();
            var seen = new HashSet<EntityId>();
            CombatStageMarker[] markers = FindObjectsByType<CombatStageMarker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                CombatStageMarker marker = markers[i];
                if (marker == null || marker.gameObject.scene != scene)
                    continue;

                GameObject root = marker.gameObject;
                if (!seen.Add(root.GetEntityId()))
                    continue;

                results.Add(new StageHandle(root, marker.StageId));
            }

            if (!scene.IsValid())
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || !seen.Add(root.GetEntityId()))
                    continue;

                if (!CombatStageIds.TryMatchKnownName(root.name, out string stageId))
                    continue;

                results.Add(new StageHandle(root, stageId));
            }
        }

        private readonly struct StageHandle
        {
            public StageHandle(GameObject root, string stageId)
            {
                Root = root;
                StageId = stageId;
            }

            public GameObject Root { get; }
            public string StageId { get; }
        }
    }
}
