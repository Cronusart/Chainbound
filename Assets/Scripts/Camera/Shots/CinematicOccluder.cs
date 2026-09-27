using UnityEngine;

/// <summary>
/// Marker for foreground objects that cinematic shots are allowed to hide temporarily.
/// </summary>
[DisallowMultipleComponent]
public class CinematicOccluder : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Stable debug identifier. Falls back to the GameObject name when empty.")]
    [SerializeField] private string occluderId = "";

    [Header("Renderers")]
    [Tooltip("Optional root used when resolving child renderers for this occluder group.")]
    [SerializeField] private Transform rendererRoot;

    [Tooltip("Optional explicit renderer list. When set, this takes priority over root/child discovery.")]
    [SerializeField] private Renderer[] explicitRenderers;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmo = true;

    public string OccluderId => string.IsNullOrWhiteSpace(occluderId) ? gameObject.name : occluderId;

    public Renderer[] ResolveRenderers(bool includeChildren)
    {
        if (explicitRenderers != null && explicitRenderers.Length > 0)
        {
            return explicitRenderers;
        }

        Transform root = rendererRoot != null ? rendererRoot : transform;
        return includeChildren
            ? root.GetComponentsInChildren<Renderer>(true)
            : root.GetComponents<Renderer>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(occluderId))
        {
            occluderId = gameObject.name;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmo)
            return;

        Gizmos.color = new Color(1f, 0.72f, 0.18f, 0.35f);
        Bounds bounds = new Bounds(transform.position, Vector3.one * 0.25f);
        Renderer[] renderers = ResolveRenderers(true);
        bool hasBounds = false;

        if (renderers != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
#endif
}
