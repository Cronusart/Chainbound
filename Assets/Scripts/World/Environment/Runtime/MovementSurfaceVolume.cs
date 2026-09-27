using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger volume that overrides footstep audio and applies movement penalties
/// while the player is inside. Reusable for streams, mud, snow, or similar surfaces.
///
/// Footsteps use the same interval system as ground layers. Optionally lock cadence
/// to the walk interval so sprinting still splashes at walk tempo.
///
/// Setup:
/// 1. Add a Collider and set it as a trigger covering the walkable area.
/// 2. Attach this component.
/// 3. Assign a footstep SoundEventSO (and optional landing event).
/// 4. Tune speed / jump multipliers in the inspector.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MovementSurfaceVolume : MonoBehaviour, IPlayerMovementModifier
{
    [Header("Footsteps")]
    [Tooltip("Splash / surface footstep event. Leave empty until you assign clips.")]
    [SerializeField] private SoundEventSO footstepSound;

    [Tooltip("Optional landing splash. Falls back to the footstep event when empty.")]
    [SerializeField] private SoundEventSO landingSound;

    [Tooltip("Surface type reported to landing/footstep systems while inside.")]
    [SerializeField] private SurfaceType surfaceType = SurfaceType.Water;

    [Tooltip("Seconds between footsteps while walking. Matches FootstepConfig walk cadence by default.")]
    [Range(0.05f, 3f)]
    [SerializeField] private float walkInterval = 0.26f;

    [Tooltip("Seconds between footsteps while sprinting. Ignored when cadence is locked to walk.")]
    [Range(0.05f, 2f)]
    [SerializeField] private float runInterval = 0.26f;

    [Tooltip("If true, sprinting still uses the walk interval so splash cadence stays even.")]
    [SerializeField] private bool lockCadenceToWalk = true;

    [SerializeField] private bool usePitchVariation = false;

    [Range(0.5f, 2f)]
    [SerializeField] private float pitch = 1f;

    [Header("Movement")]
    [Tooltip("Walk and sprint speed multiplier while inside. 1 = normal.")]
    [Range(0.05f, 2f)]
    [SerializeField] private float speedMultiplier = 0.7f;

    [Tooltip("Jump impulse multiplier while inside. 1 = normal.")]
    [Range(0.05f, 2f)]
    [SerializeField] private float jumpImpulseMultiplier = 0.85f;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private GameObject playerObject;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    private readonly FootstepController.FootstepSurfaceOverride footstepOverride =
        new FootstepController.FootstepSurfaceOverride();

    private PlayerMovement3D playerMovement;
    private FootstepController footstepController;
    private bool effectsApplied;

    public float MovementSpeedMultiplier => speedMultiplier;
    public float JumpImpulseMultiplier => jumpImpulseMultiplier;
    public bool IsPlayerInside => playerColliders.Count > 0;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;

        ResolveConfiguredPlayer();
        RefreshOverrideData();
    }

    private void OnValidate()
    {
        walkInterval = Mathf.Clamp(walkInterval, 0.05f, 3f);
        runInterval = Mathf.Clamp(runInterval, 0.05f, 2f);
        speedMultiplier = Mathf.Clamp(speedMultiplier, 0.05f, 2f);
        jumpImpulseMultiplier = Mathf.Clamp(jumpImpulseMultiplier, 0.05f, 2f);
        pitch = Mathf.Clamp(pitch, 0.5f, 2f);

        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;

        RefreshOverrideData();

        if (Application.isPlaying && effectsApplied)
        {
            if (playerMovement != null)
                playerMovement.RegisterMovementModifier(this);
            if (footstepController != null)
                footstepController.RegisterSurfaceOverride(this, footstepOverride);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolvePlayer(other, out PlayerMovement3D movement, out FootstepController footsteps))
            return;

        bool wasOutside = playerColliders.Count == 0;
        playerColliders.Add(other);
        if (!wasOutside)
            return;

        BindPlayer(movement, footsteps);
        ApplyEffects();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!TryResolvePlayer(other, out PlayerMovement3D movement, out FootstepController footsteps))
            return;

        bool wasOutside = playerColliders.Count == 0;
        playerColliders.Add(other);
        BindPlayer(movement, footsteps);
        if (wasOutside)
            ApplyEffects();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerColliders.Remove(other) || playerColliders.Count > 0)
            return;

        ClearEffects();
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        ClearEffects();
    }

    private bool TryResolvePlayer(Collider other, out PlayerMovement3D movement, out FootstepController footsteps)
    {
        movement = null;
        footsteps = null;
        if (other == null)
            return false;

        if (playerObject != null &&
            (other.gameObject == playerObject || other.transform.IsChildOf(playerObject.transform)))
        {
            movement = playerObject.GetComponent<PlayerMovement3D>();
            footsteps = playerObject.GetComponent<FootstepController>();
            return movement != null;
        }

        movement = other.GetComponentInParent<PlayerMovement3D>();
        if (movement == null)
        {
            if (string.IsNullOrEmpty(playerTag))
                return false;

            Transform candidate = other.transform;
            while (candidate != null)
            {
                if (candidate.CompareTag(playerTag))
                {
                    movement = candidate.GetComponent<PlayerMovement3D>();
                    break;
                }
                candidate = candidate.parent;
            }
        }

        if (movement == null)
            return false;

        playerObject = movement.gameObject;
        footsteps = movement.GetComponent<FootstepController>();
        return true;
    }

    private void ResolveConfiguredPlayer()
    {
        if (playerObject == null && !string.IsNullOrEmpty(playerTag))
        {
            try { playerObject = GameObject.FindGameObjectWithTag(playerTag); }
            catch (UnityException) { playerObject = null; }
        }

        if (playerObject == null)
        {
            PlayerMovement3D movement = FindFirstObjectByType<PlayerMovement3D>();
            if (movement != null)
                playerObject = movement.gameObject;
        }
    }

    private void BindPlayer(PlayerMovement3D movement, FootstepController footsteps)
    {
        playerMovement = movement;
        footstepController = footsteps;
        if (movement != null)
            playerObject = movement.gameObject;
    }

    private void ApplyEffects()
    {
        RefreshOverrideData();

        if (playerMovement != null)
            playerMovement.RegisterMovementModifier(this);

        if (footstepController != null)
            footstepController.RegisterSurfaceOverride(this, footstepOverride);

        effectsApplied = true;

        if (enableDebugLogs)
            Debug.Log($"[MovementSurfaceVolume] Player entered {name}.", this);
    }

    private void ClearEffects()
    {
        if (!effectsApplied)
            return;

        if (playerMovement != null)
            playerMovement.UnregisterMovementModifier(this);

        if (footstepController != null)
            footstepController.UnregisterSurfaceOverride(this);

        effectsApplied = false;
        playerMovement = null;
        footstepController = null;

        if (enableDebugLogs)
            Debug.Log($"[MovementSurfaceVolume] Player exited {name}.", this);
    }

    private void RefreshOverrideData()
    {
        footstepOverride.SoundEvent = footstepSound;
        footstepOverride.LandingSound = landingSound;
        footstepOverride.SurfaceType = surfaceType;
        footstepOverride.WalkInterval = walkInterval;
        footstepOverride.RunInterval = runInterval;
        footstepOverride.LockCadenceToWalk = lockCadenceToWalk;
        footstepOverride.UsePitchVariation = usePitchVariation;
        footstepOverride.Pitch = pitch;
    }
}
