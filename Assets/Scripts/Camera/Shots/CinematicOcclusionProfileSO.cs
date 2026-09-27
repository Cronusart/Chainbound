using UnityEngine;

public enum CinematicOcclusionHideMode
{
    DisableRenderer = 0
}

/// <summary>
/// Data asset controlling how a camera shot temporarily reveals its framed target.
/// Only objects marked with <see cref="CinematicOccluder"/> are eligible.
/// </summary>
[CreateAssetMenu(fileName = "CinematicOcclusionProfile", menuName = "Chainbound/Camera/Shots/Cinematic Occlusion Profile")]
public class CinematicOcclusionProfileSO : ScriptableObject
{
    [Header("Detection")]
    [Tooltip("Layers that can contain CinematicOccluder markers.")]
    [SerializeField] private LayerMask occluderLayerMask = -1;

    [Tooltip("Maximum distance to check from the camera toward each target sample.")]
    [SerializeField] private float maxDistance = 80f;

    [Tooltip("Radius for reveal sphere casts. Zero uses thin raycasts.")]
    [SerializeField] private float sphereCastRadius = 0.15f;

    [Tooltip("World-space offsets sampled around the framed target.")]
    [SerializeField] private Vector3[] targetSampleOffsets =
    {
        new Vector3(0f, 0.25f, 0f),
        new Vector3(0f, 0.85f, 0f),
        new Vector3(0f, 1.45f, 0f)
    };

    [Header("Hide Behavior")]
    [Tooltip("Renderer disabling is the only supported first-pass mode.")]
    [SerializeField] private CinematicOcclusionHideMode hideMode = CinematicOcclusionHideMode.DisableRenderer;

    [Tooltip("Reserved for a future fade mode. Renderer disable is immediate in this first pass.")]
    [SerializeField] private float fadeDuration = 0f;

    [Tooltip("When true, a hit marker hides renderers under its configured root.")]
    [SerializeField] private bool includeChildren = true;

    [Tooltip("Temporarily suppress the target's exploration silhouette during cinematic reveal.")]
    [SerializeField] private bool suppressTargetSilhouette = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool drawDebugRays = false;

    public LayerMask OccluderLayerMask => occluderLayerMask;
    public float MaxDistance => maxDistance;
    public float SphereCastRadius => sphereCastRadius;
    public Vector3[] TargetSampleOffsets => targetSampleOffsets;
    public CinematicOcclusionHideMode HideMode => hideMode;
    public float FadeDuration => fadeDuration;
    public bool IncludeChildren => includeChildren;
    public bool SuppressTargetSilhouette => suppressTargetSilhouette;
    public bool EnableDebugLogs => enableDebugLogs;
    public bool DrawDebugRays => drawDebugRays;

    private void OnValidate()
    {
        maxDistance = Mathf.Max(0.1f, maxDistance);
        sphereCastRadius = Mathf.Max(0f, sphereCastRadius);
        fadeDuration = Mathf.Max(0f, fadeDuration);

        if (targetSampleOffsets == null || targetSampleOffsets.Length == 0)
        {
            targetSampleOffsets = new[] { Vector3.zero };
        }
    }
}
