using System.Collections;
using UnityEngine;

/// <summary>
/// Step that plays a camera shot.
/// Waits for shot to complete before continuing.
/// Target: assign Transform/RectTransform in inspector, or use fallback ID (tag or name).
/// </summary>
[CreateAssetMenu(fileName = "Step_PlayCameraShot", menuName = "Chainbound/Narrative/Steps/Play Camera Shot")]
public class Step_PlayCameraShot : SequenceStep
{
    [Header("Camera Shot")]
    [Tooltip("Camera shot preset to play")]
    [SerializeField] private CameraShotPresetSO shotPreset;

    [Header("Target (Option C)")]
    [Tooltip("Assign in inspector (Player, object, RectTransform). Works when step runs in same scene.")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("Fallback when targetTransform is null: 'Player' = FindWithTag, else GameObject.Find(name)")]
    [SerializeField] private string fallbackTargetId = "";

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (shotPreset == null)
        {
            Debug.LogError("[Step_PlayCameraShot] Shot preset is null!");
            onComplete?.Invoke(StepOutcome.Failed("Shot preset is null"));
            yield break;
        }

        CameraShotController shotController = context.CameraShotController;
        if (shotController == null)
        {
            Debug.LogError("[Step_PlayCameraShot] CameraShotController not found!");
            onComplete?.Invoke(StepOutcome.Failed("CameraShotController not found"));
            yield break;
        }

        Transform target = ResolveTargetTransform();
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_PlayCameraShot] Playing shot: {shotPreset.name} target={target?.name ?? "preset default"}");
        }

        Coroutine shotCoroutine = shotController.PlayShot(shotPreset, target);
        if (shotCoroutine != null)
        {
            yield return shotCoroutine;
        }

        while (shotController.IsPlayingShot)
        {
            yield return null;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[Step_PlayCameraShot] Shot complete: {shotPreset.name}");
        }

        onComplete?.Invoke(StepOutcome.Success());
    }

    private Transform ResolveTargetTransform()
    {
        // Sequence steps are ScriptableObjects, so this field can accidentally point at a prefab asset
        // instead of the live scene instance. Only use direct references from loaded scenes.
        if (targetTransform != null &&
            targetTransform.gameObject != null &&
            targetTransform.gameObject.scene.IsValid() &&
            targetTransform.gameObject.scene.isLoaded)
            return targetTransform;

        if (string.IsNullOrEmpty(fallbackTargetId))
            return null;

        if (string.Equals(fallbackTargetId, "Player", System.StringComparison.OrdinalIgnoreCase))
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform : null;
        }

        GameObject found = GameObject.Find(fallbackTargetId);
        return found != null ? found.transform : null;
    }
    
    public override bool Validate()
    {
        if (shotPreset == null)
        {
            Debug.LogWarning("[Step_PlayCameraShot] Shot preset is not assigned!");
            return false;
        }
        return true;
    }
}

