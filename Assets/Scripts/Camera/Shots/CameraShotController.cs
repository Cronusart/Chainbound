using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for executing camera shots (zoom, pan, look-at).
/// Builds on TopDownCameraController3D's existing capabilities.
///
/// Lives on GameServices (DontDestroyOnLoad). Auto-finds TopDownCameraController3D
/// on scene load since the camera rig changes between scenes.
/// </summary>
public class CameraShotController : MonoBehaviour
{
    private static CameraShotController instance;

    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static CameraShotController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CameraShotController>();
            }

            return instance;
        }
    }

    [Header("References")]
    [Tooltip("Camera controller to control. Auto-finds on scene load if null or destroyed.")]
    [SerializeField] private TopDownCameraController3D cameraController;

    [Tooltip("Optional reveal service. Auto-finds on demand when null.")]
    [SerializeField] private CinematicOcclusionRevealService occlusionRevealService;

    [Header("Settings")]
    [Tooltip("Restore camera follow after shot completes")]
    [SerializeField] private bool restoreFollowAfterShot = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // State
    private bool isPlayingShot = false;
    private CameraStateSnapshot savedState;
    private Coroutine activeShotCoroutine;

    /// <summary>
    /// When true, <see cref="PlayDialogueLineShot"/> will not overwrite <see cref="savedState"/> at the start of each line
    /// (snapshot was captured in <see cref="BeginDialogueCameraSession"/> for a smooth exit restore).
    /// </summary>
    private bool dialogueCameraSessionActive;
    private CameraProfile neutralizedProfileClone;
    private bool shotTemporarilyOverrodePauseFollow;
    private bool shotPreviousPauseFollowState;
    private bool currentShotSettled = true;
    private bool dialogueSessionHasTransientYawOverride;
    private bool dialogueSessionPauseFollowDefault = true;
    private bool activeShotIsDialogueLine;
    private CinematicOcclusionRevealService.RevealToken activeShotRevealToken;
    private CinematicOcclusionRevealService.RevealToken dialogueRevealToken;

    /// <summary>True while a dialogue camera session is active (see <see cref="BeginDialogueCameraSession"/>).</summary>
    public bool IsDialogueCameraSessionActive => dialogueCameraSessionActive;

    /// <summary>Is a shot currently playing?</summary>
    public bool IsPlayingShot => isPlayingShot;

    /// <summary>True once the active shot has finished its transition and reached its framing target.</summary>
    public bool HasCurrentShotSettled => !isPlayingShot || currentShotSettled;

    /// <summary>True when a saved restore snapshot exists.</summary>
    public bool HasSavedRestoreState => savedState.isValid;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ReleaseAllRevealTokens("controller disabled");
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[CameraShotController] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        instance = this;

        RefreshCameraController();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive overworld chunks must not abort an in-progress shot or drop
        // the persistent rig that was just reparented under GameServices.
        if (mode == LoadSceneMode.Additive)
        {
            RefreshCameraController();
            return;
        }

        ReleaseAllRevealTokens($"scene loaded: {scene.name}");

        // Clear stale state from previous scene. This prevents camera snapping to old scene position
        // when a deferred restore runs before the first play_cam in the new scene.
        savedState = CameraStateSnapshot.Invalid;
        dialogueCameraSessionActive = false;
        currentShotSettled = true;
        isPlayingShot = false;
        activeShotCoroutine = null;
        activeShotIsDialogueLine = false;
        dialogueSessionHasTransientYawOverride = false;
        dialogueSessionPauseFollowDefault = true;
        RefreshCameraController();
    }

    /// <summary>
    /// Re-acquire TopDownCameraController3D reference. Call when scene loads since camera rig changes.
    /// </summary>
    private void RefreshCameraController()
    {
        TopDownCameraController3D resolved = SceneCameraResolver.ResolveGameplayCameraController();
        if (resolved != null && resolved.isActiveAndEnabled)
        {
            cameraController = resolved;
        }
        else
        {
            TopDownCameraController3D local = SceneCameraResolver.FindLocalController(SceneManager.GetActiveScene());
            if (local != null)
                cameraController = local;
            else if (cameraController != null && !cameraController.isActiveAndEnabled)
                cameraController = null;
        }

        if (cameraController != null && enableDebugLogs)
        {
            Debug.Log($"[CameraShotController] Refreshed camera controller reference (scene: {SceneManager.GetActiveScene().name})");
        }
        else if (cameraController == null && enableDebugLogs)
        {
            Debug.LogWarning($"[CameraShotController] No TopDownCameraController3D found in scene: {SceneManager.GetActiveScene().name}");
        }
    }

    private void OnDestroy()
    {
        ReleaseAllRevealTokens("controller destroyed");

        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Play a camera shot. Returns coroutine for async completion.
    /// </summary>
    /// <param name="preset">Shot preset to execute</param>
    /// <param name="targetTransform">Optional target transform (overrides preset)</param>
    /// <returns>Coroutine that completes when shot finishes</returns>
    public Coroutine PlayShot(
        CameraShotPresetSO preset,
        Transform targetTransform = null,
        float transitionDurationOverride = -1f)
    {
        if (preset == null)
        {
            Debug.LogWarning("[CameraShotController] Cannot play null shot preset!");
            return null;
        }

        RefreshCameraController();
        if (cameraController == null)
        {
            Debug.LogError("[CameraShotController] No TopDownCameraController3D found in scene!");
            return null;
        }

        if (isPlayingShot)
        {
            Debug.LogWarning("[CameraShotController] Shot already playing! Stopping current shot.");
            StopShot();
        }

        activeShotCoroutine = StartCoroutine(PlayShotCoroutine(
            preset,
            targetTransform,
            dialogueLineShot: false,
            neutralizeProfileFollowOffset: false,
            transitionDurationOverride));
        return activeShotCoroutine;
    }

    /// <summary>
    /// Play a shot during dialogue-driven framing: does not overwrite the pre-dialogue
    /// <see cref="savedState"/> and does not restore at the end of each line so transitions stay relative to the
    /// anchored session. Call <see cref="BeginDialogueCameraSession"/> before the first line.
    /// </summary>
    /// <param name="neutralizeProfileFollowOffset">If true, shot profile follow offsets are zeroed on a runtime copy so scripted rig moves + look-at drive framing (less fight with profile offset).</param>
    public Coroutine PlayDialogueLineShot(
        CameraShotPresetSO preset,
        Transform targetTransform = null,
        bool neutralizeProfileFollowOffset = false,
        float transitionDurationOverride = -1f)
    {
        if (preset == null)
        {
            Debug.LogWarning("[CameraShotController] Cannot play null shot preset!");
            return null;
        }

        RefreshCameraController();
        if (cameraController == null)
        {
            Debug.LogError("[CameraShotController] No TopDownCameraController3D found in scene!");
            return null;
        }

        if (!dialogueCameraSessionActive)
        {
            Debug.LogWarning("[CameraShotController] PlayDialogueLineShot called without BeginDialogueCameraSession - falling back to normal PlayShot.");
            return PlayShot(preset, targetTransform, transitionDurationOverride);
        }

        if (isPlayingShot)
        {
            Debug.LogWarning("[CameraShotController] Shot already playing! Replacing current dialogue shot without restoring.");
            CancelActiveShotWithoutRestore();
        }

        activeShotCoroutine = StartCoroutine(PlayShotCoroutine(
            preset,
            targetTransform,
            dialogueLineShot: true,
            neutralizeProfileFollowOffset,
            transitionDurationOverride));
        return activeShotCoroutine;
    }

    /// <summary>
    /// Capture camera profile/zoom/pitch snapshot and optional rig anchor before dialogue-driven shots run.
    /// Prevents each line shot from saving/restoring mid-sequence (jank). Pair with <see cref="EndDialogueCameraSession(float)"/>.
    /// </summary>
    public void BeginDialogueCameraSession(bool pauseFollowAtStart = true)
    {
        RefreshCameraController();
        if (cameraController == null)
        {
            Debug.LogWarning("[CameraShotController] BeginDialogueCameraSession: no camera controller.");
            return;
        }

        // Continuation after persist-without-end: keep original restore snapshot from first dialogue in the chain.
        if (dialogueCameraSessionActive && savedState.isValid)
        {
            dialogueSessionHasTransientYawOverride = false;
            dialogueSessionPauseFollowDefault = pauseFollowAtStart;

            if (pauseFollowAtStart)
                cameraController.SetPauseFollowPosition(true);
            else
                cameraController.SetPauseFollowPosition(false);

            if (enableDebugLogs)
                Debug.Log("[CameraShotController] Dialogue camera session continued (preserving pre-beat snapshot).");

            return;
        }

        dialogueCameraSessionActive = true;
        savedState = cameraController.GetStateSnapshot();
        dialogueSessionHasTransientYawOverride = false;
        dialogueSessionPauseFollowDefault = pauseFollowAtStart;

        if (pauseFollowAtStart)
            cameraController.SetPauseFollowPosition(true);
        else
            cameraController.SetPauseFollowPosition(false);

        if (enableDebugLogs)
        {
            Vector3 rig = cameraController.GetRigPosition();
            Debug.Log($"[CameraShotController] Dialogue camera session began - snapshot saved, rig={rig}, pauseFollow={pauseFollowAtStart}");
        }
    }

    /// <summary>
    /// End dialogue shot session: stop active line shot if any, then restore to the snapshot from
    /// <see cref="BeginDialogueCameraSession"/>.
    /// </summary>
    public void EndDialogueCameraSession(float blendTime = 0.3f)
    {
        RefreshCameraController();

        if (!dialogueCameraSessionActive)
            return;

        dialogueCameraSessionActive = false;
        dialogueSessionHasTransientYawOverride = false;
        dialogueSessionPauseFollowDefault = true;

        if (isPlayingShot)
            CancelActiveShotWithoutRestore();

        ReleaseRevealToken(ref dialogueRevealToken, "dialogue camera session ended");
        RestoreCameraFollow(blendTime);

        if (enableDebugLogs)
            Debug.Log("[CameraShotController] Dialogue camera session ended.");
    }

    /// <summary>
    /// Like <see cref="EndDialogueCameraSession(float)"/> but waits until follow restore / profile blend completes.
    /// </summary>
    public IEnumerator EndDialogueCameraSessionAndWait(float blendTime = 0.3f)
    {
        RefreshCameraController();

        if (!dialogueCameraSessionActive)
            yield break;

        dialogueCameraSessionActive = false;
        dialogueSessionHasTransientYawOverride = false;
        dialogueSessionPauseFollowDefault = true;

        if (isPlayingShot)
            CancelActiveShotWithoutRestore();

        ReleaseRevealToken(ref dialogueRevealToken, "dialogue camera session ended");
        yield return RestoreCameraFollowAndWait(blendTime);

        if (enableDebugLogs)
            Debug.Log("[CameraShotController] Dialogue camera session ended (waited for restore).");
    }

    /// <summary>
    /// Stop current shot immediately and restore camera.
    /// </summary>
    public void StopShot()
    {
        if (!isPlayingShot)
            return;

        bool hadSavedState = savedState.isValid;
        StopActiveShotCoroutineOnly();
        ReleaseAllRevealTokens("shot stopped");

        if (restoreFollowAfterShot && hadSavedState && cameraController != null)
        {
            EnsureFollowTarget();
            cameraController.SetPauseFollowPosition(false);
            cameraController.RestoreStateSnapshot(savedState, 0f);
            savedState = CameraStateSnapshot.Invalid;
        }

        dialogueSessionHasTransientYawOverride = false;

        if (enableDebugLogs)
            Debug.Log("[CameraShotController] Shot stopped");
    }

    private void CancelActiveShotWithoutRestore()
    {
        if (!isPlayingShot)
            return;

        bool cancelledDialogueLine = activeShotIsDialogueLine;
        StopActiveShotCoroutineOnly();

        if (!cancelledDialogueLine)
        {
            ReleaseRevealToken(ref activeShotRevealToken, "active shot cancelled without restore");
        }

        if (enableDebugLogs)
            Debug.Log("[CameraShotController] Active dialogue shot cancelled without restore.");
    }

    private void StopActiveShotCoroutineOnly()
    {
        if (activeShotCoroutine != null)
        {
            StopCoroutine(activeShotCoroutine);
            activeShotCoroutine = null;
        }

        RestoreTemporaryShotPauseOverride();

        if (neutralizedProfileClone != null)
        {
            Destroy(neutralizedProfileClone);
            neutralizedProfileClone = null;
        }

        isPlayingShot = false;
        currentShotSettled = true;
        activeShotIsDialogueLine = false;
    }

    /// <summary>
    /// Re-acquire player as follow target when restoring. Ensures camera locks back onto player
    /// after a shot, handling null/destroyed references or scene refresh.
    /// </summary>
    private void EnsureFollowTarget()
    {
        if (cameraController == null)
            return;

        SpawnService spawnService = FindFirstObjectByType<SpawnService>();
        GameObject player = spawnService != null ? spawnService.GetResolvedPlayerRoot() : null;
        if (player == null)
        {
            PlayerMovement3D movement = FindFirstObjectByType<PlayerMovement3D>();
            player = movement != null ? movement.gameObject : null;
        }
        if (player != null)
        {
            cameraController.SetTarget(player.transform);
            if (enableDebugLogs)
                Debug.Log("[CameraShotController] Re-acquired player as follow target");
        }
    }

    /// <summary>
    /// Restore camera to normal follow behavior.
    /// </summary>
    public void RestoreCameraFollow(float blendTime = 0.3f)
    {
        RefreshCameraController();
        ReleaseAllRevealTokens("camera follow restore");

        if (cameraController == null)
            return;

        EnsureFollowTarget();
        cameraController.SetPauseFollowPosition(false);

        if (savedState.isValid)
        {
            cameraController.RestoreStateSnapshot(savedState, blendTime);
            savedState = CameraStateSnapshot.Invalid;
        }
        else
        {
            cameraController.ResetToDefaultProfile(blendTime);
        }

        if (enableDebugLogs)
            Debug.Log("[CameraShotController] Camera follow restored");

        dialogueSessionHasTransientYawOverride = false;
    }

    /// <summary>
    /// Restore camera to normal follow behavior and yield until restore completes.
    /// </summary>
    public IEnumerator RestoreCameraFollowAndWait(float blendTime = 0.3f)
    {
        RefreshCameraController();
        ReleaseAllRevealTokens("camera follow restore");

        if (cameraController == null)
            yield break;

        EnsureFollowTarget();
        cameraController.SetPauseFollowPosition(false);

        if (enableDebugLogs)
            Debug.Log("[CameraShotController] Restoring camera follow (waiting for completion)");

        if (savedState.isValid)
        {
            yield return cameraController.RestoreStateSnapshotAndWait(savedState, blendTime);
            savedState = CameraStateSnapshot.Invalid;
        }
        else
        {
            yield return cameraController.ResetToDefaultProfileAndWait(blendTime);
        }

        float safetyTimeout = 2f;
        float elapsed = 0f;
        while (cameraController.IsBlendingProfile && elapsed < safetyTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (cameraController.IsBlendingProfile)
        {
            cameraController.ResetToDefaultProfile(0f);
            if (enableDebugLogs)
                Debug.LogWarning("[CameraShotController] Safety: forced immediate reset after timeout");
        }

        if (enableDebugLogs)
            Debug.Log("[CameraShotController] Camera follow restored");

        dialogueSessionHasTransientYawOverride = false;
    }

    private IEnumerator PlayShotCoroutine(
        CameraShotPresetSO preset,
        Transform targetTransform,
        bool dialogueLineShot,
        bool neutralizeProfileFollowOffset,
        float transitionDurationOverride)
    {
        isPlayingShot = true;
        currentShotSettled = false;
        activeShotIsDialogueLine = dialogueLineShot;

        float transitionDuration = ResolveTransitionDuration(preset, dialogueLineShot, transitionDurationOverride);

        Transform shotTarget = targetTransform != null ? targetTransform : ResolvePresetTarget(preset);
        Vector3 targetPos = GetTargetPosition(preset, shotTarget);

        if (cameraController != null &&
            shotTarget != null &&
            shotTarget.CompareTag("Player") &&
            cameraController.Target == null)
        {
            cameraController.SetTarget(shotTarget);
        }

        bool isSameAsFollowTarget = cameraController == null || shotTarget == null || cameraController.Target == shotTarget;
        CameraShotFollowMode followMode = ResolveShotFollowMode(preset, shotTarget);
        bool moveRigManually = ShouldMoveRigManually(preset, followMode, isSameAsFollowTarget);

        if (cameraController != null && !dialogueLineShot)
        {
            savedState = cameraController.GetStateSnapshot();
        }

        CameraProfile profileForBlend = preset.ShotProfile;
        if (neutralizeProfileFollowOffset && profileForBlend != null)
        {
            if (neutralizedProfileClone != null)
                Destroy(neutralizedProfileClone);

            neutralizedProfileClone = Instantiate(profileForBlend);
            neutralizedProfileClone.followOffset = Vector3.zero;
            profileForBlend = neutralizedProfileClone;
        }

        if (shotTarget != null && cameraController != null)
        {
            cameraController.SetTarget(shotTarget);
        }

        ApplyShotFollowBehavior(
            shotTarget,
            dialogueLineShot,
            isSameAsFollowTarget,
            followMode,
            moveRigManually);

        if (profileForBlend != null && cameraController != null)
        {
            yield return cameraController.ApplyProfileAndWait(profileForBlend, transitionDuration);
        }
        else if (cameraController != null)
        {
            if (preset.TargetZoom >= 0f)
                cameraController.SetZoomTarget(preset.TargetZoom);

            if (preset.TargetPitch >= 0f)
                cameraController.SetPitchTarget(preset.TargetPitch, transitionDuration);
        }

        bool hasYawTransition = ApplyShotYawBehavior(preset, dialogueLineShot, transitionDuration);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[CameraShotController] Playing shot: {preset.name} -> {targetPos} " +
                $"(sameAsFollow={isSameAsFollowTarget}, followMode={followMode}, manualMove={moveRigManually})");
        }

        if (moveRigManually)
        {
            float heightOffset = GetHeightOffset(preset);
            Vector3 rigTargetPos = targetPos + preset.Offset + new Vector3(0f, heightOffset, 0f);
            yield return StartCoroutine(MoveRigToTarget(
                rigTargetPos,
                transitionDuration,
                preset.EasingCurve,
                preset.LookAtTarget,
                preset.SmoothRotation));
        }

        if (hasYawTransition)
        {
            yield return StartCoroutine(WaitForYawTransition(transitionDuration));
        }

        currentShotSettled = true;

        AcquireRevealForShot(preset, shotTarget, targetPos, dialogueLineShot);

        if (preset.HoldTime > 0f)
        {
            bool hasHoldSettle = preset.HoldSettleOffset.sqrMagnitude > 0.0001f;
            bool hasHoldZoom = !Mathf.Approximately(preset.HoldZoomOffset, 0f);
            bool hasHoldBob = preset.HoldBobAmplitude.sqrMagnitude > 0.0001f;

            if (hasHoldSettle || hasHoldZoom || hasHoldBob)
            {
                Vector3 holdBaseRigPosition = cameraController != null ? cameraController.GetRigPosition() : Vector3.zero;
                float holdBaseZoomTarget = cameraController != null ? cameraController.TargetZoom : 0f;
                yield return StartCoroutine(PlayCinematicHold(
                    preset,
                    holdBaseRigPosition,
                    holdBaseZoomTarget,
                    allowRigMotionDuringHold: followMode != CameraShotFollowMode.FollowShotTarget));
            }
            else
            {
                yield return new WaitForSeconds(preset.HoldTime);
            }
        }

        if (neutralizedProfileClone != null)
        {
            Destroy(neutralizedProfileClone);
            neutralizedProfileClone = null;
        }

        bool skipRestoreAtEnd = preset.DeferRestore || dialogueLineShot;
        if (restoreFollowAfterShot && !skipRestoreAtEnd && cameraController != null)
        {
            EnsureFollowTarget();
            const float restoreBlendTime = 0.3f;
            if (savedState.isValid)
            {
                yield return cameraController.RestoreStateSnapshotAndWait(savedState, restoreBlendTime);
                savedState = CameraStateSnapshot.Invalid;
            }
            else
            {
                yield return cameraController.ResetToDefaultProfileAndWait(restoreBlendTime);
            }

            float safetyTimeout = 2f;
            float elapsed = 0f;
            while (cameraController.IsBlendingProfile && elapsed < safetyTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (cameraController.IsBlendingProfile)
            {
                cameraController.ResetToDefaultProfile(0f);
                if (enableDebugLogs)
                    Debug.LogWarning("[CameraShotController] Safety: forced immediate reset after timeout");
            }
        }

        if (!dialogueLineShot && !preset.DeferRestore)
        {
            ReleaseRevealToken(ref activeShotRevealToken, "shot completed");
        }

        isPlayingShot = false;
        activeShotCoroutine = null;
        activeShotIsDialogueLine = false;

        if (enableDebugLogs)
        {
            Debug.Log($"[CameraShotController] Shot completed: {preset.name} (dialogueLine={dialogueLineShot})");
        }
    }

    private void AcquireRevealForShot(
        CameraShotPresetSO preset,
        Transform shotTarget,
        Vector3 targetPosition,
        bool dialogueLineShot)
    {
        CinematicOcclusionProfileSO profile = preset != null ? preset.OcclusionRevealProfile : null;
        if (profile == null)
        {
            ReleaseScopedRevealToken(dialogueLineShot, "shot has no reveal profile");
            return;
        }

        if (!HasRevealTarget(preset, shotTarget))
        {
            ReleaseScopedRevealToken(dialogueLineShot, "shot has no reveal target");
            if (enableDebugLogs || profile.EnableDebugLogs)
            {
                Debug.Log($"[CameraShotController] Reveal skipped for '{preset.name}': no transform/world-position target.");
            }

            return;
        }

        RefreshOcclusionRevealService();
        if (occlusionRevealService == null)
        {
            ReleaseScopedRevealToken(dialogueLineShot, "reveal service missing");
            if (enableDebugLogs || profile.EnableDebugLogs)
            {
                Debug.Log($"[CameraShotController] Reveal skipped for '{preset.name}': CinematicOcclusionRevealService not found.");
            }

            return;
        }

        Camera shotCamera = ResolveRevealCamera();
        if (shotCamera == null)
        {
            ReleaseScopedRevealToken(dialogueLineShot, "reveal camera missing");
            if (enableDebugLogs || profile.EnableDebugLogs)
            {
                Debug.Log($"[CameraShotController] Reveal skipped for '{preset.name}': no active camera.");
            }

            return;
        }

        string reason = $"{(dialogueLineShot ? "dialogue" : "shot")}:{preset.name}";
        CinematicOcclusionRevealService.RevealToken newToken = occlusionRevealService.AcquireReveal(
            shotCamera,
            shotTarget,
            targetPosition,
            profile,
            reason);

        if (dialogueLineShot)
        {
            ReplaceRevealToken(ref dialogueRevealToken, newToken, "dialogue line reveal replaced");
        }
        else
        {
            ReplaceRevealToken(ref activeShotRevealToken, newToken, "shot reveal replaced");
        }
    }

    private bool HasRevealTarget(CameraShotPresetSO preset, Transform shotTarget)
    {
        return shotTarget != null || (preset != null && preset.UseWorldPosition);
    }

    private void RefreshOcclusionRevealService()
    {
        if (occlusionRevealService != null && occlusionRevealService.gameObject != null)
            return;

        occlusionRevealService = GetComponent<CinematicOcclusionRevealService>();
        if (occlusionRevealService != null)
            return;

        occlusionRevealService = GetComponentInChildren<CinematicOcclusionRevealService>(true);
        if (occlusionRevealService != null)
            return;

        if (transform.parent != null)
        {
            occlusionRevealService = transform.parent.GetComponentInChildren<CinematicOcclusionRevealService>(true);
            if (occlusionRevealService != null)
                return;
        }

        if (GameServicesContainer.Instance != null)
        {
            occlusionRevealService = GameServicesContainer.Instance.GetComponentInChildren<CinematicOcclusionRevealService>(true);
            if (occlusionRevealService != null)
                return;
        }

        occlusionRevealService = CinematicOcclusionRevealService.Instance;
    }

    private Camera ResolveRevealCamera()
    {
        Camera fromController = SceneCameraResolver.GetCamera(cameraController);
        return SceneCameraResolver.ResolveOrKeep(fromController, gameObject);
    }

    private void ReplaceRevealToken(
        ref CinematicOcclusionRevealService.RevealToken tokenField,
        CinematicOcclusionRevealService.RevealToken newToken,
        string reason)
    {
        CinematicOcclusionRevealService.RevealToken previous = tokenField;
        tokenField = newToken;

        if (previous != null)
        {
            previous.Dispose();
            if (enableDebugLogs)
                Debug.Log($"[CameraShotController] Released previous reveal token ({reason}).");
        }
    }

    private void ReleaseScopedRevealToken(bool dialogueLineShot, string reason)
    {
        if (dialogueLineShot)
        {
            ReleaseRevealToken(ref dialogueRevealToken, reason);
        }
        else
        {
            ReleaseRevealToken(ref activeShotRevealToken, reason);
        }
    }

    private void ReleaseAllRevealTokens(string reason)
    {
        ReleaseRevealToken(ref activeShotRevealToken, reason);
        ReleaseRevealToken(ref dialogueRevealToken, reason);
    }

    private void ReleaseRevealToken(ref CinematicOcclusionRevealService.RevealToken tokenField, string reason)
    {
        if (tokenField == null)
            return;

        tokenField.Dispose();
        tokenField = null;

        if (enableDebugLogs)
        {
            Debug.Log($"[CameraShotController] Released reveal token ({reason}).");
        }
    }

    private CameraShotFollowMode ResolveShotFollowMode(CameraShotPresetSO preset, Transform shotTarget)
    {
        CameraShotFollowMode followMode = preset != null
            ? preset.FollowMode
            : CameraShotFollowMode.UseSessionDefault;

        if (followMode == CameraShotFollowMode.FollowShotTarget && shotTarget == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[CameraShotController] Shot '{preset.name}' requested FollowShotTarget but has no resolved transform target. " +
                    "Falling back to PauseFollowAndMoveRig.");
            }

            return CameraShotFollowMode.PauseFollowAndMoveRig;
        }

        return followMode;
    }

    private bool ShouldMoveRigManually(
        CameraShotPresetSO preset,
        CameraShotFollowMode followMode,
        bool isSameAsFollowTarget)
    {
        switch (followMode)
        {
            case CameraShotFollowMode.FollowShotTarget:
                return false;

            case CameraShotFollowMode.PauseFollowAndMoveRig:
                return true;

            default:
                return preset != null && (preset.UseWorldPosition || !isSameAsFollowTarget);
        }
    }

    private void ApplyShotFollowBehavior(
        Transform shotTarget,
        bool dialogueLineShot,
        bool isSameAsFollowTarget,
        CameraShotFollowMode followMode,
        bool moveRigManually)
    {
        if (cameraController == null)
            return;

        if (followMode == CameraShotFollowMode.FollowShotTarget)
        {
            cameraController.SetPauseFollowPosition(false);
            return;
        }

        if (moveRigManually)
        {
            cameraController.SetPauseFollowPosition(true);
            return;
        }

        if (shotTarget == null || !isSameAsFollowTarget)
            return;

        bool pauseFollow = dialogueLineShot
            ? dialogueSessionPauseFollowDefault
            : false;

        cameraController.SetPauseFollowPosition(pauseFollow);
        cameraController.SnapToTarget();
    }

    private float ResolveTransitionDuration(
        CameraShotPresetSO preset,
        bool dialogueLineShot,
        float transitionDurationOverride)
    {
        if (dialogueLineShot && preset != null && preset.DialogueLineDurationOverride > 0f)
        {
            return preset.DialogueLineDurationOverride;
        }

        if (transitionDurationOverride > 0f)
        {
            return transitionDurationOverride;
        }

        return preset != null ? preset.Duration : 0.1f;
    }

    private bool ApplyShotYawBehavior(
        CameraShotPresetSO preset,
        bool dialogueLineShot,
        float transitionDuration)
    {
        if (cameraController == null || preset == null)
            return false;

        if (preset.HasYawOverride)
        {
            bool requestedYawTransition = false;

            switch (preset.YawMode)
            {
                case CameraShotYawMode.Relative90:
                    requestedYawTransition = true;
                    cameraController.RotateYaw90(
                        preset.YawRotation90Steps,
                        transitionDuration <= 0f,
                        transitionDuration);
                    break;

                case CameraShotYawMode.Absolute90:
                    if (cameraController.YawIndex90 != preset.AbsoluteYawIndex)
                    {
                        requestedYawTransition = true;
                        cameraController.SetYawIndex90(
                            preset.AbsoluteYawIndex,
                            transitionDuration <= 0f,
                            transitionDuration);
                    }
                    break;
            }

            if (dialogueLineShot)
            {
                dialogueSessionHasTransientYawOverride = true;
            }

            return requestedYawTransition;
        }

        if (dialogueLineShot && dialogueSessionHasTransientYawOverride && savedState.isValid)
        {
            dialogueSessionHasTransientYawOverride = false;

            if (cameraController.YawIndex90 != savedState.yawIndex90)
            {
                cameraController.SetYawIndex90(
                    savedState.yawIndex90,
                    transitionDuration <= 0f,
                    transitionDuration);
                return true;
            }
        }

        return false;
    }

    private IEnumerator WaitForYawTransition(float fallbackDuration)
    {
        if (cameraController == null)
            yield break;

        float safetyTimeout = Mathf.Max(0.25f, fallbackDuration + 0.25f);
        float elapsed = 0f;

        while (cameraController.IsRotatingYaw && elapsed < safetyTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private Transform ResolvePresetTarget(CameraShotPresetSO preset)
    {
        if (preset.UseWorldPosition)
            return null;

        if (!string.IsNullOrEmpty(preset.TargetTransformId))
        {
            GameObject found = GameObject.Find(preset.TargetTransformId);
            if (found != null)
                return found.transform;
        }

        return null;
    }

    private float GetHeightOffset(CameraShotPresetSO preset)
    {
        if (preset.ShotProfile != null)
            return preset.ShotProfile.yAxisOffset;

        if (cameraController?.ActiveProfile != null)
            return cameraController.ActiveProfile.yAxisOffset;

        return 25f;
    }

    private Vector3 GetTargetPosition(CameraShotPresetSO preset, Transform targetTransform)
    {
        if (targetTransform != null)
            return targetTransform.position;

        if (preset.UseWorldPosition)
            return preset.TargetPosition;

        if (!string.IsNullOrEmpty(preset.TargetTransformId))
        {
            GameObject found = GameObject.Find(preset.TargetTransformId);
            if (found != null)
            {
                return found.transform.position;
            }
        }

        return preset.TargetPosition;
    }

    private IEnumerator PlayCinematicHold(
        CameraShotPresetSO preset,
        Vector3 holdBaseRigPosition,
        float holdBaseZoomTarget,
        bool allowRigMotionDuringHold)
    {
        if (cameraController == null)
        {
            yield return new WaitForSeconds(preset.HoldTime);
            yield break;
        }

        bool hasHoldSettle = allowRigMotionDuringHold && preset.HoldSettleOffset.sqrMagnitude > 0.0001f;
        bool hasHoldZoom = !Mathf.Approximately(preset.HoldZoomOffset, 0f);
        bool hasHoldBob = allowRigMotionDuringHold && preset.HoldBobAmplitude.sqrMagnitude > 0.0001f;

        if ((hasHoldSettle || hasHoldBob) && !cameraController.IsFollowPositionPaused)
            ApplyTemporaryShotPauseOverride();

        float elapsed = 0f;

        while (elapsed < preset.HoldTime)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / preset.HoldTime);
            Vector3 rigOffset = Vector3.zero;

            if (hasHoldSettle)
            {
                float settleT = EvaluateCurve(preset.HoldSettleCurve, normalizedTime);
                rigOffset += preset.HoldSettleOffset * settleT;
            }

            if (hasHoldBob)
            {
                float bobEnvelope = Mathf.Sin(normalizedTime * Mathf.PI);
                float bobCycles = elapsed * Mathf.Max(0f, preset.HoldBobFrequency) * Mathf.PI * 2f;
                float bobValue = Mathf.Sin(bobCycles) * bobEnvelope;
                rigOffset += preset.HoldBobAmplitude * bobValue;
            }

            if (hasHoldSettle || hasHoldBob)
            {
                cameraController.SetRigPosition(holdBaseRigPosition + rigOffset);
            }

            if (hasHoldZoom)
            {
                float zoomT = EvaluateCurve(preset.HoldZoomCurve, normalizedTime);
                cameraController.SetZoomTarget(holdBaseZoomTarget + (preset.HoldZoomOffset * zoomT));
            }

            yield return null;
        }

        if (hasHoldSettle)
        {
            float finalSettleT = EvaluateCurve(preset.HoldSettleCurve, 1f);
            cameraController.SetRigPosition(holdBaseRigPosition + (preset.HoldSettleOffset * finalSettleT));
        }
        else if (hasHoldBob)
        {
            cameraController.SetRigPosition(holdBaseRigPosition);
        }

        if (hasHoldZoom)
        {
            float finalZoomT = EvaluateCurve(preset.HoldZoomCurve, 1f);
            cameraController.SetZoomTarget(holdBaseZoomTarget + (preset.HoldZoomOffset * finalZoomT));
        }

        RestoreTemporaryShotPauseOverride();
    }

    private float EvaluateCurve(AnimationCurve curve, float normalizedTime)
    {
        if (curve == null || curve.length == 0)
            return normalizedTime;

        return curve.Evaluate(normalizedTime);
    }

    private void ApplyTemporaryShotPauseOverride()
    {
        if (cameraController == null || shotTemporarilyOverrodePauseFollow)
            return;

        shotPreviousPauseFollowState = cameraController.IsFollowPositionPaused;
        cameraController.SetPauseFollowPosition(true);
        shotTemporarilyOverrodePauseFollow = true;
    }

    private void RestoreTemporaryShotPauseOverride()
    {
        if (!shotTemporarilyOverrodePauseFollow)
            return;

        if (cameraController != null)
            cameraController.SetPauseFollowPosition(shotPreviousPauseFollowState);

        shotTemporarilyOverrodePauseFollow = false;
        shotPreviousPauseFollowState = false;
    }

    private IEnumerator MoveRigToTarget(
        Vector3 targetPos,
        float duration,
        AnimationCurve easing,
        bool lookAtTarget,
        bool smoothRotation)
    {
        if (cameraController == null)
            yield break;

        Vector3 startPos = cameraController.GetRigPosition();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (easing != null && easing.length > 0)
                t = easing.Evaluate(t);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            cameraController.SetRigPosition(currentPos);

            yield return null;
        }

        cameraController.SetRigPosition(targetPos);
    }

    /// <summary>
    /// Set the camera controller reference.
    /// </summary>
    public void SetCameraController(TopDownCameraController3D controller)
    {
        cameraController = controller;
    }
}
