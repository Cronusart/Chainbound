using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AtmosphericSceneBinding
{
    [Tooltip("Exact scene name (Loomville, Link1, Jungle). Case-insensitive.")]
    [SerializeField] private string sceneId;

    [SerializeField] private AtmosphericProfileSO profile;

    public string SceneId => sceneId;
    public AtmosphericProfileSO Profile => profile;

#if UNITY_EDITOR
    public void EditorSet(string id, AtmosphericProfileSO assignedProfile)
    {
        sceneId = id;
        profile = assignedProfile;
    }
#endif
}

[CreateAssetMenu(fileName = "AtmosphericCatalog", menuName = "Chainbound/World/Environment/Atmospheric Catalog")]
public sealed class AtmosphericCatalogSO : ScriptableObject
{
    [Tooltip("Used when the active overworld scene has no binding.")]
    [SerializeField] private AtmosphericProfileSO defaultProfile;

    [Tooltip("Seconds to blend rates/colors when the active scene profile changes.")]
    [SerializeField, Min(0f)] private float sceneBlendSeconds = 1.2f;

    [SerializeField] private List<AtmosphericSceneBinding> sceneBindings = new List<AtmosphericSceneBinding>();

    public AtmosphericProfileSO DefaultProfile => defaultProfile;
    public float SceneBlendSeconds => sceneBlendSeconds;

    public AtmosphericProfileSO Resolve(string sceneId)
    {
        if (!string.IsNullOrWhiteSpace(sceneId) && sceneBindings != null)
        {
            for (int i = 0; i < sceneBindings.Count; i++)
            {
                AtmosphericSceneBinding binding = sceneBindings[i];
                if (binding == null || binding.Profile == null)
                    continue;
                if (string.Equals(binding.SceneId, sceneId, StringComparison.OrdinalIgnoreCase))
                    return binding.Profile;
            }
        }

        return defaultProfile;
    }

#if UNITY_EDITOR
    public void EditorSetDefaultProfile(AtmosphericProfileSO profile)
    {
        defaultProfile = profile;
    }

    public void EditorSetSceneBlendSeconds(float seconds)
    {
        sceneBlendSeconds = Mathf.Max(0f, seconds);
    }

    public void EditorSetBindings(List<AtmosphericSceneBinding> bindings)
    {
        sceneBindings = bindings ?? new List<AtmosphericSceneBinding>();
    }
#endif
}
