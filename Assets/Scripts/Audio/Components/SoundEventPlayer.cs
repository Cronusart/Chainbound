using UnityEngine;

/// <summary>
/// Small inspector-driven player for SoundEventSO one-shots.
/// Intended for UnityEvent wiring from doors, interactables, cutscenes, and similar systems.
/// </summary>
public class SoundEventPlayer : MonoBehaviour
{
    [Header("Playback")]
    [Tooltip("Sound event to play when Play() is invoked.")]
    [SerializeField] private SoundEventSO soundEvent;

    [Tooltip("Optional origin for 3D playback. Falls back to this transform if enabled below.")]
    [SerializeField] private Transform emitPoint;

    [Tooltip("If true, parents playback to the resolved origin instead of using a one-shot world position.")]
    [SerializeField] private bool followTransform = false;

    [Tooltip("If no emit point is assigned, use this GameObject's transform as the playback origin.")]
    [SerializeField] private bool useSelfIfEmitPointMissing = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    /// <summary>
    /// Parameterless entry point for UnityEvent wiring.
    /// </summary>
    public void Play()
    {
        if (soundEvent == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[SoundEventPlayer] No SoundEventSO assigned on {gameObject.name}.", this);
            return;
        }

        AudioService service = AudioService.Instance;
        if (service == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[SoundEventPlayer] AudioService.Instance is null on {gameObject.name}.", this);
            return;
        }

        Transform origin = ResolveOrigin();
        if (followTransform && origin != null)
        {
            service.Play(soundEvent, followTransform: origin);
            return;
        }

        if (origin != null)
        {
            service.Play(soundEvent, origin.position);
            return;
        }

        service.Play(soundEvent);
    }

    private Transform ResolveOrigin()
    {
        if (emitPoint != null)
            return emitPoint;

        return useSelfIfEmitPointMissing ? transform : null;
    }
}
