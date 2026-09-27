using UnityEngine;

/// <summary>
/// Binds a stable narrative ID to a scene GameObject so <see cref="NarrativeSceneObjectStateService"/>
/// can reapply enable/disable after scene reload. Place on the object (or a parent) that defines the barrier, door, etc.
/// </summary>
[DisallowMultipleComponent]
public sealed class NarrativeSceneObjectBinding : MonoBehaviour
{
    [Tooltip("Stable ID, e.g. chainbound_intro.loomville.intro_barriers. Must match keys used by Step_EnableGameObject persistence.")]
    [SerializeField] private string objectId = "";

    [Tooltip("If set, this object receives SetActive; otherwise this GameObject is used.")]
    [SerializeField] private GameObject target;

    public string ObjectId => objectId;

    /// <summary>
    /// Returns the GameObject whose active state should be toggled.
    /// </summary>
    public GameObject ResolveTarget()
    {
        return target != null ? target : gameObject;
    }

    private void OnValidate()
    {
        objectId = objectId?.Trim() ?? "";
    }
}
