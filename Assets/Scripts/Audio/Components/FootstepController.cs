using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Component that plays footstep sounds based on movement and surface type.
/// Uses interval-based playback for tempo control (avoids pitch distortion).
/// Integrates with PlayerMovement3D to avoid redundant ground checks.
/// Attach to player GameObject. Configurable via inspector.
/// </summary>
public class FootstepController : MonoBehaviour
{
    [Header("Footstep Configuration")]
    [Tooltip("Footstep configuration asset")]
    [SerializeField] private FootstepConfigSO footstepConfig;
    
    [Header("Landing Impact Configuration")]
    [Tooltip("Landing impact configuration asset (optional - enables landing sounds)")]
    [SerializeField] private LandingImpactConfigSO landingImpactConfig;
    
    [Header("Player Movement Reference")]
    [Tooltip("PlayerMovement3D component (auto-found if not assigned)")]
    [SerializeField] private PlayerMovement3D playerMovement;
    
    [Header("Movement Detection")]
    [Tooltip("Minimum velocity magnitude to trigger footsteps")]
    [SerializeField] private float minVelocityThreshold = 0.1f;
    
    [Header("Surface Detection")]
    [Tooltip("Additional layers for surface detection beyond PlayerMovement3D's ground layers. Use this to add Wood, Metal, Stone layers if they're not in the player's ground check.")]
    [SerializeField] private LayerMask additionalSurfaceLayers = 0;
    
    [Tooltip("If true, combine additionalSurfaceLayers with PlayerMovement3D's ground layers. If false, use ONLY PlayerMovement3D's layers.")]
    [SerializeField] private bool combineWithPlayerGroundLayers = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Runtime state
    private SurfaceType currentSurfaceType;
    private bool isMoving = false;
    private bool wasMoving = false;
    
    // Interval-based playback
    private Coroutine footstepCoroutine;
    private FootstepConfigSO.SurfaceFootstepEntry currentEntry;
    private AudioHandle currentFootstepHandle; // Track current playing sound to stop it
    
    // Cached references
    private Rigidbody rb;
    private IAudioService audioService;

    private readonly List<SurfaceOverrideBinding> surfaceOverrides = new List<SurfaceOverrideBinding>();
    
    /// <summary>
    /// Volume-authored footstep override. Last registered source wins.
    /// </summary>
    public sealed class FootstepSurfaceOverride
    {
        public SoundEventSO SoundEvent;
        public SoundEventSO LandingSound;
        public SurfaceType SurfaceType = SurfaceType.Water;
        public float WalkInterval = 0.26f;
        public float RunInterval = 0.26f;
        public bool LockCadenceToWalk = true;
        public bool UsePitchVariation;
        public float Pitch = 1f;
    }

    private sealed class SurfaceOverrideBinding
    {
        public object Source;
        public FootstepSurfaceOverride Override;
    }
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Auto-find PlayerMovement3D if not assigned
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement3D>();
        }
        
        if (playerMovement == null)
        {
            Debug.LogError("[FootstepController] PlayerMovement3D not found! FootstepController requires PlayerMovement3D on the same GameObject.");
            enabled = false;
        }
    }
    
    private void Start()
    {
        audioService = AudioService.Instance;
        
        if (footstepConfig == null)
        {
            Debug.LogWarning("[FootstepController] FootstepConfig not assigned!");
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[FootstepController] Initialized. Using PlayerMovement3D ground check data.");
            
            // Log which layers are included in surface detection
            LayerMask effectiveLayers = GetEffectiveSurfaceLayers();
            string includedLayers = "";
            for (int i = 0; i < 32; i++)
            {
                if ((effectiveLayers.value & (1 << i)) != 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        includedLayers += $"{i}({layerName}), ";
                    }
                }
            }
            Debug.Log($"[FootstepController] Surface detection layers: {includedLayers}");
        }
    }
    
    private void Update()
    {
        if (audioService == null) return;
        
        UpdateGroundedState();
        UpdateMovementState();
    }
    
    /// <summary>
    /// Get the effective surface detection layers (PlayerMovement3D's layers + additional layers if configured).
    /// </summary>
    private LayerMask GetEffectiveSurfaceLayers()
    {
        if (playerMovement == null)
            return additionalSurfaceLayers;
        
        if (combineWithPlayerGroundLayers)
        {
            // Combine PlayerMovement3D's ground layers with additional surface layers
            return playerMovement.GroundLayers | additionalSurfaceLayers;
        }
        else
        {
            // Use only PlayerMovement3D's ground layers
            return playerMovement.GroundLayers;
        }
    }
    
    private void UpdateGroundedState()
    {
        if (playerMovement == null) return;

        if (TryGetActiveOverride(out FootstepSurfaceOverride surfaceOverride))
        {
            ApplySurfaceType(surfaceOverride.SurfaceType);
            return;
        }

        if (footstepConfig == null) return;
        
        bool isGrounded = playerMovement.IsGrounded;
        RaycastHit? groundHit = null;
        
        // Use PlayerMovement3D's ground check parameters for surface detection raycast
        Vector3 origin = transform.position + playerMovement.GroundCheckOffset;
        float checkRadius = playerMovement.GroundCheckRadius;
        float checkDistance = playerMovement.GroundCheckDistance;
        LayerMask effectiveLayers = GetEffectiveSurfaceLayers();
        
        // Do a raycast to detect the surface type (separate from grounded check)
        if (Physics.SphereCast(origin, checkRadius, Vector3.down, out RaycastHit hit, checkDistance, effectiveLayers, QueryTriggerInteraction.Ignore))
        {
            groundHit = hit;
            
            // DEBUG: Log every raycast hit
            if (enableDebugLogs && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[FootstepController] Raycast HIT: '{hit.collider.gameObject.name}' on layer {hit.collider.gameObject.layer} ({LayerMask.LayerToName(hit.collider.gameObject.layer)})");
            }
        }
        else if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            // DEBUG: Log when raycast misses
            Debug.Log($"[FootstepController] Raycast MISS - effectiveLayers mask: {effectiveLayers.value}, checking from {origin} down {checkDistance}m");
        }
        
        // Update surface type if grounded
        if (isGrounded && groundHit.HasValue)
        {
            int layer = groundHit.Value.collider.gameObject.layer;
            SurfaceType newSurfaceType = footstepConfig.GetSurfaceTypeFromLayer(layer);
            
            // DEBUG: Always log layer detection to help diagnose surface mapping
            if (enableDebugLogs && Time.frameCount % 60 == 0)
            {
                string objectName = groundHit.Value.collider.gameObject.name;
                Debug.Log($"[FootstepController] LAYER DEBUG - Hit: '{objectName}', Layer: {layer} ({LayerMask.LayerToName(layer)}), Mapped SurfaceType: {newSurfaceType}");
            }
            
            // Check if surface changed
            ApplySurfaceType(newSurfaceType);
        }
    }

    private void ApplySurfaceType(SurfaceType newSurfaceType)
    {
        if (newSurfaceType == currentSurfaceType)
            return;

        if (enableDebugLogs)
            Debug.Log($"[FootstepController] Surface changed: {currentSurfaceType} -> {newSurfaceType}");

        currentSurfaceType = newSurfaceType;

        if (isMoving)
        {
            StopFootsteps();
            StartFootsteps();
        }
    }
    
    private void UpdateMovementState()
    {
        if (rb == null || playerMovement == null) return;
        
        // Get grounded state from PlayerMovement3D
        bool isGrounded = playerMovement.IsGrounded;
        
        // Get horizontal velocity
        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float velocity = horizontalVel.magnitude;
        
        // Get sprint state from PlayerMovement3D if available
        bool isRunning = playerMovement != null ? playerMovement.IsSprinting : false;
        
        // Determine if moving (must be grounded and have velocity)
        wasMoving = isMoving;
        isMoving = velocity > minVelocityThreshold && isGrounded;
        
        // #region agent log
        try {
            string logPath = @".cursor\debug.log";
            string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:UpdateMovementState\",\"message\":\"Movement state update\",\"data\":{{\"velocity\":{velocity:F4},\"isGrounded\":{isGrounded.ToString().ToLower()},\"isMoving\":{isMoving.ToString().ToLower()},\"wasMoving\":{wasMoving.ToString().ToLower()},\"threshold\":{minVelocityThreshold:F4},\"coroutineActive\":{(footstepCoroutine != null ? "true" : "false")}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"A\"}}\n";
            File.AppendAllText(logPath, logEntry);
        } catch {}
        // #endregion agent log
        
        // Debug logging
        if (enableDebugLogs && Time.frameCount % 30 == 0) // Log every 30 frames to avoid spam
        {
            Debug.Log($"[FootstepController] State - Moving: {isMoving}, Velocity: {velocity:F3}, Grounded: {isGrounded}, Running: {isRunning}, Coroutine: {(footstepCoroutine != null ? "Active" : "None")}");
        }
        
        // Movement state changed
        if (isMoving != wasMoving)
        {
            // #region agent log
            try {
                string logPath = @".cursor\debug.log";
                string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:UpdateMovementState\",\"message\":\"Movement state CHANGED\",\"data\":{{\"wasMoving\":{wasMoving.ToString().ToLower()},\"isMoving\":{isMoving.ToString().ToLower()},\"velocity\":{velocity:F4},\"isGrounded\":{isGrounded.ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"B\"}}\n";
                File.AppendAllText(logPath, logEntry);
            } catch {}
            // #endregion agent log
            
            if (enableDebugLogs)
            {
                Debug.Log($"[FootstepController] Movement state changed: {wasMoving} -> {isMoving} (velocity: {velocity:F2}, grounded: {isGrounded}, threshold: {minVelocityThreshold})");
            }
            
            if (isMoving)
            {
                StartFootsteps();
            }
            else
            {
                StopFootsteps();
            }
        }
    }
    
    private void StartFootsteps()
    {
        FootstepConfigSO.SurfaceFootstepEntry entry = ResolveActiveFootstepEntry();
        if (entry == null || entry.soundEvent == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[FootstepController] No footstep entry found for surface: {currentSurfaceType}");
            }
            return;
        }
        
        // Stop existing footsteps
        StopFootsteps();
        
        // Store current entry
        currentEntry = entry;
        
        // #region agent log
        try {
            string logPath = @".cursor\debug.log";
            string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:StartFootsteps\",\"message\":\"CONFIG_VALUES\",\"data\":{{\"surface\":\"{currentSurfaceType}\",\"walkInterval\":{entry.walkInterval:F3},\"runInterval\":{entry.runInterval:F3},\"soundEventName\":\"{(entry.soundEvent != null ? entry.soundEvent.name : "null")}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run4\",\"hypothesisId\":\"CONFIG\"}}\n";
            File.AppendAllText(logPath, logEntry);
        } catch {}
        // #endregion agent log
        
        // Start interval-based playback coroutine
        footstepCoroutine = StartCoroutine(FootstepIntervalCoroutine());
        
        bool isRunning = playerMovement != null ? playerMovement.IsSprinting : false;
        if (enableDebugLogs)
        {
            Debug.Log($"[FootstepController] Started footsteps for surface: {currentSurfaceType}, running: {isRunning}, walkInterval: {entry.walkInterval:F2}s, runInterval: {entry.runInterval:F2}s");
        }
    }
    
    private void StopFootsteps()
    {
        // #region agent log
        try {
            string logPath = @".cursor\debug.log";
            bool handleWasValid = currentFootstepHandle.IsValid;
            bool handleWasPlaying = handleWasValid && audioService != null && audioService.IsPlaying(currentFootstepHandle);
            string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:StopFootsteps\",\"message\":\"StopFootsteps called\",\"data\":{{\"coroutineWasActive\":{(footstepCoroutine != null ? "true" : "false")},\"handleWasValid\":{handleWasValid.ToString().ToLower()},\"handleWasPlaying\":{handleWasPlaying.ToString().ToLower()},\"isMoving\":{isMoving.ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"B\"}}\n";
            File.AppendAllText(logPath, logEntry);
        } catch {}
        // #endregion agent log
        
        if (footstepCoroutine != null)
        {
            StopCoroutine(footstepCoroutine);
            footstepCoroutine = null;
        }
        
        // CRITICAL: Stop any currently playing footstep sound
        if (currentFootstepHandle.IsValid && audioService != null)
        {
            // #region agent log
            try {
                string logPath = @".cursor\debug.log";
                string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:StopFootsteps\",\"message\":\"Stopping audio handle\",\"data\":{{\"handleValid\":true}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"B2\"}}\n";
                File.AppendAllText(logPath, logEntry);
            } catch {}
            // #endregion agent log
            
            audioService.Stop(currentFootstepHandle);
            currentFootstepHandle = default;
        }
        
        currentEntry = null;
        
        if (enableDebugLogs)
        {
            Debug.Log("[FootstepController] Stopped footsteps");
        }
    }
    
    /// <summary>
    /// Coroutine that plays footstep sounds at intervals based on walk/run state.
    /// Uses tempo (interval) instead of pitch to avoid distortion.
    /// Re-checks movement state each iteration to stop when movement stops.
    /// </summary>
    private IEnumerator FootstepIntervalCoroutine()
    {
        int iterationCount = 0;
        
        while (currentEntry != null)
        {
            iterationCount++;
            
            // Re-check movement state each iteration (values update in UpdateMovementState)
            // #region agent log
            try {
                string logPath = @".cursor\debug.log";
                string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:FootstepIntervalCoroutine\",\"message\":\"Coroutine iteration check\",\"data\":{{\"iteration\":{iterationCount},\"isMoving\":{isMoving.ToString().ToLower()},\"currentEntryNotNull\":{(currentEntry != null ? "true" : "false")}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"D\"}}\n";
                File.AppendAllText(logPath, logEntry);
            } catch {}
            // #endregion agent log
            
            if (!isMoving)
            {
                // #region agent log
                try {
                    string logPath = @".cursor\debug.log";
                    string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:FootstepIntervalCoroutine\",\"message\":\"Exiting coroutine - not moving\",\"data\":{{\"iteration\":{iterationCount}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"E\"}}\n";
                    File.AppendAllText(logPath, logEntry);
                } catch {}
                // #endregion agent log
                
                // Stop the currently playing footstep sound
                if (currentFootstepHandle.IsValid && audioService != null)
                {
                    audioService.Stop(currentFootstepHandle);
                    currentFootstepHandle = default;
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log("[FootstepController] Movement stopped, exiting coroutine");
                }
                break;
            }
            
            // Get current grounded state from PlayerMovement3D
            bool isGrounded = playerMovement != null && playerMovement.IsGrounded;
            
            // #region agent log
            try {
                string logPath = @".cursor\debug.log";
                Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                float velocity = horizontalVel.magnitude;
                string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:FootstepIntervalCoroutine\",\"message\":\"Before play check\",\"data\":{{\"iteration\":{iterationCount},\"isGrounded\":{isGrounded.ToString().ToLower()},\"velocity\":{velocity:F4}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"F\"}}\n";
                File.AppendAllText(logPath, logEntry);
            } catch {}
            // #endregion agent log
            
            if (!isGrounded)
            {
                // #region agent log
                try {
                    string logPath = @".cursor\debug.log";
                    string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:FootstepIntervalCoroutine\",\"message\":\"Exiting coroutine - not grounded\",\"data\":{{\"iteration\":{iterationCount}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"G\"}}\n";
                    File.AppendAllText(logPath, logEntry);
                } catch {}
                // #endregion agent log
                
                // Stop the currently playing footstep sound
                if (currentFootstepHandle.IsValid && audioService != null)
                {
                    audioService.Stop(currentFootstepHandle);
                    currentFootstepHandle = default;
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log("[FootstepController] No longer grounded, exiting coroutine");
                }
                break;
            }
            
            RefreshOverrideOnCurrentEntry();
            if (currentEntry == null || currentEntry.soundEvent == null)
                break;

            // Get current running state. Surface volumes can lock cadence to walk
            // so sprinting still splashes at the same interval as footsteps.
            bool lockCadenceToWalk = TryGetActiveOverride(out FootstepSurfaceOverride cadenceOverride)
                && cadenceOverride.LockCadenceToWalk;
            bool isRunning = !lockCadenceToWalk && playerMovement != null && playerMovement.IsSprinting;
            
            // #region agent log
            try {
                string logPath = @".cursor\debug.log";
                float walkInt = currentEntry != null ? currentEntry.walkInterval : 0f;
                float runInt = currentEntry != null ? currentEntry.runInterval : 0f;
                float usedInterval = isRunning ? runInt : walkInt;
                string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:FootstepIntervalCoroutine\",\"message\":\"Sprint state check\",\"data\":{{\"iteration\":{iterationCount},\"isRunning\":{isRunning.ToString().ToLower()},\"playerMovementNotNull\":{(playerMovement != null).ToString().ToLower()},\"walkInterval\":{walkInt:F3},\"runInterval\":{runInt:F3},\"usedInterval\":{usedInterval:F3}}},\"sessionId\":\"debug-session\",\"runId\":\"run3\",\"hypothesisId\":\"SPRINT\"}}\n";
                File.AppendAllText(logPath, logEntry);
            } catch {}
            // #endregion agent log
            
            // Play footstep sound (non-looping, single play)
            // IMPORTANT: Stop any previous footstep sound before playing new one
            if (currentFootstepHandle.IsValid && audioService.IsPlaying(currentFootstepHandle))
            {
                audioService.Stop(currentFootstepHandle);
            }
            
            currentFootstepHandle = audioService.Play(currentEntry.soundEvent, followTransform: transform);
            
            // #region agent log
            try {
                string logPath = @".cursor\debug.log";
                bool soundLoopSetting = currentEntry.soundEvent != null ? currentEntry.soundEvent.Loop : false;
                string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:FootstepIntervalCoroutine\",\"message\":\"Footstep played\",\"data\":{{\"iteration\":{iterationCount},\"handleValid\":{currentFootstepHandle.IsValid.ToString().ToLower()},\"soundEventLoop\":{soundLoopSetting.ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"A\"}}\n";
                File.AppendAllText(logPath, logEntry);
            } catch {}
            // #endregion agent log
            
            // Apply pitch if variation is enabled (otherwise stays at 1.0)
            if (currentEntry.usePitchVariation && currentFootstepHandle.IsValid)
            {
                currentFootstepHandle.SetPitch(currentEntry.pitch);
            }
            
            // Wait for the appropriate interval based on running state
            float interval = isRunning ? currentEntry.runInterval : currentEntry.walkInterval;
            
            // #region agent log
            try {
                string logPath = @".cursor\debug.log";
                string modeLabel = isRunning ? "RUNNING" : "WALKING";
                long beforeWaitTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string logEntry = $"{{\"timestamp\":{beforeWaitTime},\"location\":\"FootstepController.cs:FootstepIntervalCoroutine\",\"message\":\"INTERVAL_START\",\"data\":{{\"iteration\":{iterationCount},\"mode\":\"{modeLabel}\",\"intervalUsed\":{interval:F3},\"walkInterval\":{currentEntry.walkInterval:F3},\"runInterval\":{currentEntry.runInterval:F3}}},\"sessionId\":\"debug-session\",\"runId\":\"run4\",\"hypothesisId\":\"INTERVAL\"}}\n";
                File.AppendAllText(logPath, logEntry);
            } catch {}
            // #endregion agent log
            
            // Debug console log for immediate feedback
            if (enableDebugLogs)
            {
                string modeStr = isRunning ? "RUNNING" : "WALKING";
                Debug.Log($"[FootstepController] {modeStr} - Interval: {interval:F2}s (walk={currentEntry.walkInterval:F2}, run={currentEntry.runInterval:F2})");
            }
            
            yield return new WaitForSeconds(interval);
        }
        
        footstepCoroutine = null;
        
        // Stop any still-playing sound when coroutine ends naturally
        if (currentFootstepHandle.IsValid && audioService != null)
        {
            audioService.Stop(currentFootstepHandle);
            currentFootstepHandle = default;
        }
        
        // #region agent log
        try {
            string logPath = @".cursor\debug.log";
            string logEntry = $"{{\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"location\":\"FootstepController.cs:FootstepIntervalCoroutine\",\"message\":\"Coroutine ended\",\"data\":{{\"totalIterations\":{iterationCount}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"I\"}}\n";
            File.AppendAllText(logPath, logEntry);
        } catch {}
        // #endregion agent log
        
        if (enableDebugLogs)
        {
            Debug.Log("[FootstepController] Footstep coroutine ended");
        }
    }
    
    private void OnDisable()
    {
        StopFootsteps();
    }
    
    private void OnDestroy()
    {
        StopFootsteps();
    }
    
    // ============================================
    // PUBLIC API (for external control)
    // ============================================
    
    /// <summary>
    /// Force stop footsteps (e.g., when entering cutscene).
    /// </summary>
    public void ForceStop()
    {
        StopFootsteps();
        isMoving = false;
    }
    
    /// <summary>
    /// Set running state externally (if not using PlayerMovement3D).
    /// Note: This is deprecated - running state is now read from PlayerMovement3D automatically.
    /// </summary>
    [System.Obsolete("Running state is now automatically read from PlayerMovement3D. This method does nothing.")]
    public void SetRunning(bool running)
    {
        // Running state is now read from PlayerMovement3D in the coroutine
        // This method is kept for backwards compatibility but does nothing
    }
    
    /// <summary>
    /// Manually set surface type (overrides automatic detection).
    /// </summary>
    public void SetSurfaceType(SurfaceType surfaceType)
    {
        ApplySurfaceType(surfaceType);
    }

    /// <summary>
    /// Register a volume-authored footstep override. Last source wins.
    /// </summary>
    public void RegisterSurfaceOverride(object source, FootstepSurfaceOverride surfaceOverride)
    {
        if (source == null || surfaceOverride == null)
            return;

        SurfaceOverrideBinding existing = FindOverrideBinding(source);
        bool isNew = existing == null;
        if (existing != null)
            existing.Override = surfaceOverride;
        else
            surfaceOverrides.Add(new SurfaceOverrideBinding { Source = source, Override = surfaceOverride });

        ApplySurfaceType(surfaceOverride.SurfaceType);

        bool needsStart = isMoving && (isNew || footstepCoroutine == null);
        if (needsStart)
        {
            StopFootsteps();
            StartFootsteps();
        }
    }

    /// <summary>
    /// Remove a previously registered footstep override.
    /// </summary>
    public void UnregisterSurfaceOverride(object source)
    {
        if (source == null)
            return;

        for (int i = surfaceOverrides.Count - 1; i >= 0; i--)
        {
            if (surfaceOverrides[i].Source == source)
                surfaceOverrides.RemoveAt(i);
        }

        if (isMoving)
        {
            StopFootsteps();
            StartFootsteps();
        }
    }

    private FootstepConfigSO.SurfaceFootstepEntry ResolveActiveFootstepEntry()
    {
        if (TryGetActiveOverride(out FootstepSurfaceOverride surfaceOverride))
        {
            if (surfaceOverride.SoundEvent == null)
                return null;

            return BuildOverrideEntry(surfaceOverride);
        }

        if (footstepConfig == null)
            return null;

        return footstepConfig.GetFootstepEntry(currentSurfaceType);
    }

    private static FootstepConfigSO.SurfaceFootstepEntry BuildOverrideEntry(FootstepSurfaceOverride surfaceOverride)
    {
        float cadence = surfaceOverride.LockCadenceToWalk
            ? surfaceOverride.WalkInterval
            : surfaceOverride.RunInterval;

        return new FootstepConfigSO.SurfaceFootstepEntry
        {
            surfaceType = surfaceOverride.SurfaceType,
            soundEvent = surfaceOverride.SoundEvent,
            walkInterval = surfaceOverride.WalkInterval,
            runInterval = cadence,
            pitch = surfaceOverride.Pitch,
            usePitchVariation = surfaceOverride.UsePitchVariation
        };
    }

    private void RefreshOverrideOnCurrentEntry()
    {
        if (!TryGetActiveOverride(out FootstepSurfaceOverride surfaceOverride) || currentEntry == null)
            return;

        currentEntry.soundEvent = surfaceOverride.SoundEvent;
        currentEntry.walkInterval = surfaceOverride.WalkInterval;
        currentEntry.runInterval = surfaceOverride.LockCadenceToWalk
            ? surfaceOverride.WalkInterval
            : surfaceOverride.RunInterval;
        currentEntry.pitch = surfaceOverride.Pitch;
        currentEntry.usePitchVariation = surfaceOverride.UsePitchVariation;
        currentEntry.surfaceType = surfaceOverride.SurfaceType;
    }

    private bool TryGetActiveOverride(out FootstepSurfaceOverride surfaceOverride)
    {
        for (int i = surfaceOverrides.Count - 1; i >= 0; i--)
        {
            SurfaceOverrideBinding binding = surfaceOverrides[i];
            if (binding == null || binding.Source == null || binding.Override == null)
            {
                surfaceOverrides.RemoveAt(i);
                continue;
            }

            surfaceOverride = binding.Override;
            return true;
        }

        surfaceOverride = null;
        return false;
    }

    private SurfaceOverrideBinding FindOverrideBinding(object source)
    {
        for (int i = 0; i < surfaceOverrides.Count; i++)
        {
            if (surfaceOverrides[i].Source == source)
                return surfaceOverrides[i];
        }
        return null;
    }
    
    /// <summary>
    /// Get current surface type.
    /// </summary>
    public SurfaceType CurrentSurfaceType => currentSurfaceType;
    
    /// <summary>
    /// Check if footsteps are currently playing.
    /// </summary>
    public bool IsPlayingFootsteps => isMoving && footstepCoroutine != null;
    
    // ============================================
    // LANDING IMPACT SOUNDS
    // ============================================
    
    /// <summary>
    /// Play landing impact sound based on current surface type and fall height.
    /// Called by PlayerMovement3D when player lands from a fall.
    /// Volume scales dynamically with fall height.
    /// 
    /// NOTE: This performs a FRESH surface detection raycast to get the landing surface,
    /// not the cached currentSurfaceType (which may be stale from before the jump).
    /// </summary>
    /// <param name="fallHeight">Height of the fall in units</param>
    public void PlayLandingImpact(float fallHeight)
    {
        if (audioService == null)
        {
            audioService = AudioService.Instance;
            if (audioService == null)
            {
                Debug.LogWarning("[FootstepController] Cannot play landing impact - AudioService not available");
                return;
            }
        }

        if (TryPlayOverrideLanding(fallHeight))
            return;
        
        if (landingImpactConfig == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[FootstepController] Landing impact skipped - no LandingImpactConfig assigned");
            }
            return;
        }

        if (!landingImpactConfig.ShouldPlayLandingSound(fallHeight))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[FootstepController] Landing impact skipped - fall height {fallHeight:F2} below threshold {landingImpactConfig.MinFallHeightForSound:F2}");
            }
            return;
        }
        
        // IMPORTANT: Detect landing surface NOW, not using cached currentSurfaceType
        // The cached value is from before the jump - we need the surface we're landing ON
        SurfaceType landingSurface = DetectLandingSurface();
        
        // Get sound for landing surface
        SoundEventSO landingSound = landingImpactConfig.GetLandingSound(landingSurface);
        if (landingSound == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[FootstepController] No landing sound found for surface: {landingSurface}");
            }
            return;
        }
        
        // Calculate volume based on fall height
        float volumeMultiplier = landingImpactConfig.CalculateVolumeMultiplier(fallHeight, landingSurface);
        
        // Play the landing sound
        AudioHandle handle = audioService.Play(landingSound, transform.position);
        
        // Apply volume scaling
        if (handle.IsValid && volumeMultiplier < 1f)
        {
            audioService.SetVolume(handle, volumeMultiplier);
        }
        
        // Update cached surface type to match landing surface (keeps footsteps in sync)
        if (landingSurface != currentSurfaceType)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[FootstepController] Surface updated on landing: {currentSurfaceType} -> {landingSurface}");
            }
            currentSurfaceType = landingSurface;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[FootstepController] Landing impact played - Surface: {landingSurface}, FallHeight: {fallHeight:F2}, Volume: {volumeMultiplier:F2}, Sound: {landingSound.name}");
        }
    }
    
    /// <summary>
    /// Detect the surface type at the current position using a fresh raycast.
    /// Used for landing impact to get the ACTUAL landing surface, not cached value.
    /// 
    /// NOTE: This raycasts straight down from the player's ROOT position (with small Y offset),
    /// NOT from the visual ground check offset. The visual offset is designed for sprite-relative
    /// ground detection and can be 1+ units offset on XZ, which would miss the actual landing spot.
    /// </summary>
    private SurfaceType DetectLandingSurface()
    {
        if (TryGetActiveOverride(out FootstepSurfaceOverride surfaceOverride))
            return surfaceOverride.SurfaceType;

        if (playerMovement == null || footstepConfig == null)
        {
            return currentSurfaceType; // Fallback to cached
        }
        
        LayerMask effectiveLayers = GetEffectiveSurfaceLayers();
        
        // IMPORTANT: Raycast straight down from player's actual position, NOT the visual offset!
        // The visual offset (GroundCheckOffset) is for sprite-relative detection and can be
        // 1+ units away on XZ, which would miss the actual landing surface.
        // Use a small Y offset to start the ray slightly inside the player.
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float checkRadius = 0.3f; // Slightly larger radius to catch nearby surfaces
        float checkDistance = 2f; // Check a reasonable distance below
        
        // Do a fresh raycast to detect landing surface
        if (Physics.SphereCast(origin, checkRadius, Vector3.down, out RaycastHit hit, checkDistance, effectiveLayers, QueryTriggerInteraction.Ignore))
        {
            int layer = hit.collider.gameObject.layer;
            SurfaceType detectedSurface = footstepConfig.GetSurfaceTypeFromLayer(layer);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[FootstepController] Landing surface detected: {detectedSurface} (layer: {LayerMask.LayerToName(layer)}, object: {hit.collider.gameObject.name}, distance: {hit.distance:F2})");
            }
            
            return detectedSurface;
        }
        
        // Primary raycast missed - try using PlayerMovement3D's last ground hit as fallback
        // This hit was captured in CheckGrounded() which ran just before HandleLanding()
        RaycastHit lastGroundHit = playerMovement.LastGroundHit;
        if (lastGroundHit.collider != null)
        {
            int layer = lastGroundHit.collider.gameObject.layer;
            SurfaceType detectedSurface = footstepConfig.GetSurfaceTypeFromLayer(layer);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[FootstepController] Landing surface from PlayerMovement3D: {detectedSurface} (layer: {LayerMask.LayerToName(layer)}, object: {lastGroundHit.collider.gameObject.name})");
            }
            
            return detectedSurface;
        }
        
        // Both methods failed - use cached value as last resort
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[FootstepController] Landing surface detection failed - using cached surface: {currentSurfaceType}");
        }
        return currentSurfaceType;
    }
    
    /// <summary>
    /// Play landing impact sound with explicit surface type override.
    /// Use this when you want to force a specific surface sound (e.g., special landing zones).
    /// For normal landings, use PlayLandingImpact(float fallHeight) which auto-detects surface.
    /// </summary>
    /// <param name="fallHeight">Height of the fall in units</param>
    /// <param name="overrideSurface">Surface type to force (bypasses auto-detection)</param>
    public void PlayLandingImpactWithSurface(float fallHeight, SurfaceType overrideSurface)
    {
        if (landingImpactConfig == null || audioService == null) return;
        
        if (!landingImpactConfig.ShouldPlayLandingSound(fallHeight)) return;
        
        SoundEventSO landingSound = landingImpactConfig.GetLandingSound(overrideSurface);
        if (landingSound == null) return;
        
        float volumeMultiplier = landingImpactConfig.CalculateVolumeMultiplier(fallHeight, overrideSurface);
        AudioHandle handle = audioService.Play(landingSound, transform.position);
        
        if (handle.IsValid && volumeMultiplier < 1f)
        {
            audioService.SetVolume(handle, volumeMultiplier);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[FootstepController] Landing impact (override) - Surface: {overrideSurface}, FallHeight: {fallHeight:F2}, Volume: {volumeMultiplier:F2}");
        }
    }
    
    /// <summary>
    /// Check if landing impact configuration is available.
    /// </summary>
    public bool HasLandingImpactConfig => landingImpactConfig != null;

    private bool TryPlayOverrideLanding(float fallHeight)
    {
        if (!TryGetActiveOverride(out FootstepSurfaceOverride surfaceOverride))
            return false;

        SoundEventSO sound = surfaceOverride.LandingSound != null
            ? surfaceOverride.LandingSound
            : surfaceOverride.SoundEvent;
        if (sound == null)
            return true;

        if (landingImpactConfig != null && !landingImpactConfig.ShouldPlayLandingSound(fallHeight))
            return true;

        float volumeMultiplier = landingImpactConfig != null
            ? landingImpactConfig.CalculateVolumeMultiplier(fallHeight, surfaceOverride.SurfaceType)
            : 1f;

        AudioHandle handle = audioService.Play(sound, transform.position);
        if (handle.IsValid && volumeMultiplier < 1f)
            audioService.SetVolume(handle, volumeMultiplier);

        currentSurfaceType = surfaceOverride.SurfaceType;

        if (enableDebugLogs)
        {
            Debug.Log($"[FootstepController] Landing impact (volume override) - Surface: {surfaceOverride.SurfaceType}, FallHeight: {fallHeight:F2}, Volume: {volumeMultiplier:F2}");
        }

        return true;
    }
}
