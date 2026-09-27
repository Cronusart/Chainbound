using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Actor-level presenter that plays reusable exploration sprite presentation clips.
/// Supports both TriggerRegistry bindings and direct runtime playback.
/// </summary>
[DisallowMultipleComponent]
public class ExplorationSpritePresentationPresenter : MonoBehaviour
{
    [Serializable]
    public class TriggerClipBinding
    {
        [SerializeField] private string triggerId = string.Empty;
        [SerializeField] private ExplorationSpritePresentationClipSO clip;

        public string TriggerId => triggerId;
        public ExplorationSpritePresentationClipSO Clip => clip;
    }

    [Serializable]
    public class AudioSlotBinding
    {
        [SerializeField] private string slotId = string.Empty;
        [SerializeField] private Transform emitPoint;
        [SerializeField] private bool followTransform = true;
        [SerializeField] private bool usePresenterRootIfMissing = true;

        public string SlotId => slotId;
        public bool FollowTransform => followTransform;

        public Transform ResolveOrigin(Transform presenterRoot)
        {
            if (emitPoint != null)
                return emitPoint;

            return usePresenterRootIfMissing ? presenterRoot : null;
        }
    }

    [Serializable]
    public class EffectSlotBinding
    {
        [SerializeField] private string slotId = string.Empty;
        [SerializeField] private UnityEvent onPlay = new UnityEvent();

        public string SlotId => slotId;

        public void Play()
        {
            onPlay?.Invoke();
        }
    }

    private interface IExplorationSpritePresentationHost
    {
        bool IsReady { get; }
        string HostLabel { get; }
        ExplorationFacingDirection CurrentFacing { get; }
        bool IsClipAnimating { get; }

        void CapturePose();
        void BeginPresentation();
        void PlayClip(
            ExplorationSpritePresentationClipSO clip,
            ExplorationPresentationPlaybackRequest playbackRequest,
            Action onClipReachedTerminalFrame);
        void HoldClip();
        void StopClip();
        void RestoreCapturedPose();
        void ReleaseToGameplay();
    }

    private sealed class ActivePresentation
    {
        public int PlaybackId;
        public ExplorationSpritePresentationClipSO Clip;
        public ExplorationPresentationPlaybackRequest PlaybackRequest;
        public PresentationStatus Status;
        public float DurationEndTime;
    }

    private enum PresentationStatus
    {
        Playing,
        HoldingLastFrame
    }

    private sealed class PlayerPresentationHost : IExplorationSpritePresentationHost
    {
        private readonly PlayerAnimationController3D_Mesh animationController;
        private readonly MeshRendererPlayerVisual visual;
        private MeshRendererPlayerVisual.AnimationState capturedState;
        private ExplorationFacingDirection capturedFacing;
        private Action completionCallback;

        public PlayerPresentationHost(PlayerAnimationController3D_Mesh animationController, MeshRendererPlayerVisual visual)
        {
            this.animationController = animationController;
            this.visual = visual;
        }

        public bool IsReady => animationController != null && visual != null;
        public string HostLabel => "Player";
        public ExplorationFacingDirection CurrentFacing => ToExplorationFacing(visual.GetFacingSnapshot());
        public bool IsClipAnimating => visual != null && visual.IsPresentationPlaybackAnimating;

        public void CapturePose()
        {
            if (!IsReady)
                return;

            capturedState = animationController.GetAnimationState();
            capturedFacing = ToExplorationFacing(animationController.GetFacingDirectionEnum());
        }

        public void BeginPresentation()
        {
            if (!IsReady)
                return;

            animationController.BeginPresentationLock();
            animationController.SetFacingDirection(ToPlayerFacing(capturedFacing));
            visual.SetAnimationState(capturedState);
        }

        public void PlayClip(
            ExplorationSpritePresentationClipSO clip,
            ExplorationPresentationPlaybackRequest playbackRequest,
            Action onClipReachedTerminalFrame)
        {
            if (!IsReady || clip == null)
                return;

            Unsubscribe();
            completionCallback = onClipReachedTerminalFrame;
            visual.OnPresentationPlaybackFinished += HandleClipComplete;
            visual.PlayPresentation(
                clip.DirectionalSprites,
                clip.FramesPerSecond,
                capturedFacing,
                playbackRequest.Mode);
        }

        public void HoldClip()
        {
            if (!IsReady)
                return;

            visual.HoldPresentationFrame();
        }

        public void StopClip()
        {
            if (!IsReady)
                return;

            Unsubscribe();
            visual.StopPresentationPlayback();
        }

        public void RestoreCapturedPose()
        {
            if (!IsReady)
                return;

            visual.StopPresentationPlayback();
            animationController.SetFacingDirection(ToPlayerFacing(capturedFacing));
            visual.SetAnimationState(capturedState);
            animationController.EndPresentationLock();
        }

        public void ReleaseToGameplay()
        {
            if (!IsReady)
                return;

            visual.StopPresentationPlayback();
            animationController.EndPresentationLock();
        }

        private void HandleClipComplete()
        {
            completionCallback?.Invoke();
        }

        private void Unsubscribe()
        {
            if (visual == null)
                return;

            visual.OnPresentationPlaybackFinished -= HandleClipComplete;
            completionCallback = null;
        }

        private static ExplorationFacingDirection ToExplorationFacing(MeshRendererPlayerVisual.Direction direction)
        {
            switch (direction)
            {
                case MeshRendererPlayerVisual.Direction.Down:
                    return ExplorationFacingDirection.Down;
                case MeshRendererPlayerVisual.Direction.Up:
                    return ExplorationFacingDirection.Up;
                case MeshRendererPlayerVisual.Direction.Left:
                    return ExplorationFacingDirection.Left;
                case MeshRendererPlayerVisual.Direction.Right:
                    return ExplorationFacingDirection.Right;
                default:
                    return ExplorationFacingDirection.Down;
            }
        }

        private static MeshRendererPlayerVisual.Direction ToPlayerFacing(ExplorationFacingDirection direction)
        {
            switch (direction)
            {
                case ExplorationFacingDirection.Down:
                    return MeshRendererPlayerVisual.Direction.Down;
                case ExplorationFacingDirection.Up:
                    return MeshRendererPlayerVisual.Direction.Up;
                case ExplorationFacingDirection.Left:
                    return MeshRendererPlayerVisual.Direction.Left;
                case ExplorationFacingDirection.Right:
                    return MeshRendererPlayerVisual.Direction.Right;
                default:
                    return MeshRendererPlayerVisual.Direction.Down;
            }
        }
    }

    private sealed class NpcPresentationHost : IExplorationSpritePresentationHost
    {
        private readonly NPCMovementAgent movementAgent;
        private readonly MeshRendererNPCVisual visual;
        private NPCAnimationState capturedState;
        private ExplorationFacingDirection capturedFacing;
        private Action completionCallback;

        public NpcPresentationHost(NPCMovementAgent movementAgent, MeshRendererNPCVisual visual)
        {
            this.movementAgent = movementAgent;
            this.visual = visual;
        }

        public bool IsReady => movementAgent != null && visual != null;
        public string HostLabel => "NPC";
        public ExplorationFacingDirection CurrentFacing => ToExplorationFacing(visual.GetFacingSnapshot());
        public bool IsClipAnimating => visual != null && visual.IsPresentationPlaybackAnimating;

        public void CapturePose()
        {
            if (!IsReady)
                return;

            capturedState = visual.GetAnimationState();
            capturedFacing = ToExplorationFacing(visual.GetFacingSnapshot());
        }

        public void BeginPresentation()
        {
            if (!IsReady)
                return;

            movementAgent.BeginPresentationLock();
            visual.SetDirection(ToNpcFacing(capturedFacing));
            visual.SetAnimationState(capturedState);
        }

        public void PlayClip(
            ExplorationSpritePresentationClipSO clip,
            ExplorationPresentationPlaybackRequest playbackRequest,
            Action onClipReachedTerminalFrame)
        {
            if (!IsReady || clip == null)
                return;

            Unsubscribe();
            completionCallback = onClipReachedTerminalFrame;
            visual.OnPresentationPlaybackFinished += HandleClipComplete;
            visual.PlayPresentation(
                clip.DirectionalSprites,
                clip.FramesPerSecond,
                capturedFacing,
                playbackRequest.Mode);
        }

        public void HoldClip()
        {
            if (!IsReady)
                return;

            visual.HoldPresentationFrame();
        }

        public void StopClip()
        {
            if (!IsReady)
                return;

            Unsubscribe();
            visual.StopPresentationPlayback();
        }

        public void RestoreCapturedPose()
        {
            if (!IsReady)
                return;

            visual.StopPresentationPlayback();
            visual.SetDirection(ToNpcFacing(capturedFacing));
            visual.SetAnimationState(capturedState);
            movementAgent.EndPresentationLock();
        }

        public void ReleaseToGameplay()
        {
            if (!IsReady)
                return;

            visual.StopPresentationPlayback();
            movementAgent.EndPresentationLock();
        }

        private void HandleClipComplete()
        {
            completionCallback?.Invoke();
        }

        private void Unsubscribe()
        {
            if (visual == null)
                return;

            visual.OnPresentationPlaybackFinished -= HandleClipComplete;
            completionCallback = null;
        }

        private static ExplorationFacingDirection ToExplorationFacing(MeshRendererNPCVisual.Direction direction)
        {
            switch (direction)
            {
                case MeshRendererNPCVisual.Direction.Down:
                    return ExplorationFacingDirection.Down;
                case MeshRendererNPCVisual.Direction.Up:
                    return ExplorationFacingDirection.Up;
                case MeshRendererNPCVisual.Direction.Left:
                    return ExplorationFacingDirection.Left;
                case MeshRendererNPCVisual.Direction.Right:
                    return ExplorationFacingDirection.Right;
                default:
                    return ExplorationFacingDirection.Down;
            }
        }

        private static MeshRendererNPCVisual.Direction ToNpcFacing(ExplorationFacingDirection direction)
        {
            switch (direction)
            {
                case ExplorationFacingDirection.Down:
                    return MeshRendererNPCVisual.Direction.Down;
                case ExplorationFacingDirection.Up:
                    return MeshRendererNPCVisual.Direction.Up;
                case ExplorationFacingDirection.Left:
                    return MeshRendererNPCVisual.Direction.Left;
                case ExplorationFacingDirection.Right:
                    return MeshRendererNPCVisual.Direction.Right;
                default:
                    return MeshRendererNPCVisual.Direction.Down;
            }
        }
    }

    [Header("Trigger Bindings")]
    [Tooltip("Existing narrative trigger IDs that should start a local presentation clip on this actor.")]
    [SerializeField] private TriggerClipBinding[] triggerBindings = Array.Empty<TriggerClipBinding>();

    [Header("Local Slots")]
    [Tooltip("Scene-local audio slots resolved by reusable clip assets.")]
    [SerializeField] private AudioSlotBinding[] audioSlots = Array.Empty<AudioSlotBinding>();

    [Tooltip("Scene-local effect slots resolved by reusable clip assets.")]
    [SerializeField] private EffectSlotBinding[] effectSlots = Array.Empty<EffectSlotBinding>();

    [Header("Optional References")]
    [Tooltip("Optional TriggerRegistry override. Auto-resolves when left empty.")]
    [SerializeField] private TriggerRegistry triggerRegistry;

    [Tooltip("Optional player animation controller override. Auto-resolves under this actor root when left empty.")]
    [SerializeField] private PlayerAnimationController3D_Mesh playerAnimationController;

    [Tooltip("Optional player visual override. Auto-resolves under this actor root when left empty.")]
    [SerializeField] private MeshRendererPlayerVisual playerVisual;

    [Tooltip("Optional NPC controller override. Auto-resolves on this actor root when left empty.")]
    [SerializeField] private NPCController npcController;

    [Tooltip("Optional NPC movement agent override. Auto-resolves under this actor root when left empty.")]
    [SerializeField] private NPCMovementAgent npcMovementAgent;

    [Tooltip("Optional NPC visual override. Auto-resolves under this actor root when left empty.")]
    [SerializeField] private MeshRendererNPCVisual npcVisual;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private IExplorationSpritePresentationHost presentationHost;
    private ActivePresentation activePresentation;
    private int nextPlaybackId = 1;

    public event Action<ExplorationSpritePresentationPresenter, ExplorationSpritePresentationClipSO> OnClipPlaybackFinished;
    public event Action<ExplorationSpritePresentationPresenter, ExplorationSpritePresentationClipSO> OnPresentationCompleted;

    public bool IsPlaying => activePresentation != null;
    public bool IsPlaybackAnimating => activePresentation != null &&
                                      activePresentation.Status == PresentationStatus.Playing &&
                                      presentationHost != null &&
                                      presentationHost.IsClipAnimating;
    public int CurrentPlaybackId => activePresentation != null ? activePresentation.PlaybackId : 0;
    public ExplorationSpritePresentationClipSO ActiveClip => activePresentation != null ? activePresentation.Clip : null;
    public ExplorationPresentationPlaybackMode CurrentPlaybackMode => activePresentation != null
        ? activePresentation.PlaybackRequest.Mode
        : ExplorationPresentationPlaybackMode.OneShot;

    private void Awake()
    {
        ResolveReferences();
        ResolvePresentationHost();
    }

    private void OnEnable()
    {
        AttachTriggerRegistry();
    }

    private void OnDisable()
    {
        DetachTriggerRegistry();
        StopActiveClipInternal(notifyCompletion: false, useClipRestoreMode: false, reason: "presenter disabled");
    }

    private void Update()
    {
        if (triggerRegistry == null)
            AttachTriggerRegistry();

        if (presentationHost == null || !presentationHost.IsReady)
        {
            ResolveReferences();
            ResolvePresentationHost();
        }

        if (activePresentation == null || activePresentation.Status != PresentationStatus.Playing)
            return;

        if (activePresentation.PlaybackRequest.Mode == ExplorationPresentationPlaybackMode.LoopForDuration &&
            Time.unscaledTime >= activePresentation.DurationEndTime)
        {
            HandleFiniteLoopDurationElapsed();
        }
    }

    public bool PlayClip(ExplorationSpritePresentationClipSO clip)
    {
        if (clip == null)
        {
            LogWarning("Cannot play a null presentation clip.");
            return false;
        }

        if (!clip.TryGetDefaultPlaybackRequest(out ExplorationPresentationPlaybackRequest playbackRequest, out string validationMessage))
        {
            LogWarning(validationMessage);
            return false;
        }

        return PlayClip(clip, playbackRequest);
    }

    public bool PlayClip(
        ExplorationSpritePresentationClipSO clip,
        ExplorationPresentationPlaybackRequest playbackRequest)
    {
        ResolveReferences();
        ResolvePresentationHost();

        if (clip == null)
        {
            LogWarning("Cannot play a null presentation clip.");
            return false;
        }

        if (!clip.HasDirectionalSprites)
        {
            LogWarning($"Clip '{clip.name}' has no directional sprites assigned.");
            return false;
        }

        if (!ValidatePlaybackRequest(clip, playbackRequest, out string validationMessage))
        {
            LogWarning(validationMessage);
            return false;
        }

        if (presentationHost == null || !presentationHost.IsReady)
        {
            LogWarning("No valid player or NPC presentation host was resolved on this actor root.");
            return false;
        }

        StopActiveClipInternal(notifyCompletion: false, useClipRestoreMode: false, reason: "replaced by a newer presentation");

        presentationHost.CapturePose();
        presentationHost.BeginPresentation();
        presentationHost.PlayClip(clip, playbackRequest, HandleClipReachedTerminalFrame);

        activePresentation = new ActivePresentation
        {
            PlaybackId = nextPlaybackId++,
            Clip = clip,
            PlaybackRequest = playbackRequest,
            Status = PresentationStatus.Playing,
            DurationEndTime = playbackRequest.HasFiniteDuration
                ? Time.unscaledTime + playbackRequest.DurationSeconds
                : 0f
        };

        PlayCompanionCues(clip);
        TryBindLoopingPresentationToInteractionSession();

        LogDebug(
            $"Started {presentationHost.HostLabel} presentation '{clip.name}' " +
            $"(mode={playbackRequest.Mode}, duration={playbackRequest.DurationSeconds:0.###}).");
        return true;
    }

    public void StopActiveClip()
    {
        StopActiveClipInternal(notifyCompletion: true, useClipRestoreMode: true, reason: "stopped explicitly");
    }

    private void HandleTriggerFired(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId) || triggerBindings == null)
            return;

        for (int i = 0; i < triggerBindings.Length; i++)
        {
            TriggerClipBinding binding = triggerBindings[i];
            if (binding == null || string.IsNullOrEmpty(binding.TriggerId))
                continue;

            if (!string.Equals(binding.TriggerId, triggerId, StringComparison.Ordinal))
                continue;

            PlayClip(binding.Clip);
            return;
        }
    }

    private void HandleClipReachedTerminalFrame()
    {
        if (activePresentation == null ||
            activePresentation.PlaybackRequest.Mode != ExplorationPresentationPlaybackMode.OneShot)
        {
            return;
        }

        OnClipPlaybackFinished?.Invoke(this, activePresentation.Clip);
        HandleProgrammedPlaybackFinished("clip reached terminal frame");
    }

    private void HandleFiniteLoopDurationElapsed()
    {
        if (activePresentation == null ||
            activePresentation.PlaybackRequest.Mode != ExplorationPresentationPlaybackMode.LoopForDuration)
        {
            return;
        }

        OnClipPlaybackFinished?.Invoke(this, activePresentation.Clip);
        HandleProgrammedPlaybackFinished("loop duration elapsed");
    }

    private void HandleProgrammedPlaybackFinished(string reason)
    {
        if (activePresentation == null)
            return;

        ActivePresentation completed = activePresentation;

        if (completed.Clip != null &&
            completed.Clip.CompletionBehavior == ExplorationPresentationRestoreMode.HoldLastFrame)
        {
            presentationHost?.HoldClip();
            completed.Status = PresentationStatus.HoldingLastFrame;
            activePresentation = completed;
            LogDebug($"Holding current frame for presentation '{completed.Clip.name}' after {reason}.");
            return;
        }

        FinishActivePresentation(
            completed.Clip != null
                ? completed.Clip.CompletionBehavior
                : ExplorationPresentationRestoreMode.ReleaseToGameplay,
            notifyCompletion: true,
            reason);
    }

    private void FinishActivePresentation(
        ExplorationPresentationRestoreMode restoreMode,
        bool notifyCompletion,
        string reason)
    {
        if (activePresentation == null || presentationHost == null)
            return;

        ExplorationSpritePresentationClipSO completedClip = activePresentation.Clip;

        switch (restoreMode)
        {
            case ExplorationPresentationRestoreMode.RestoreCapturedPose:
                presentationHost.RestoreCapturedPose();
                break;

            case ExplorationPresentationRestoreMode.ReleaseToGameplay:
            case ExplorationPresentationRestoreMode.HoldLastFrame:
            default:
                presentationHost.ReleaseToGameplay();
                break;
        }

        activePresentation = null;
        LogDebug($"Finished presentation '{completedClip?.name ?? "<null>"}': {reason}.");

        if (notifyCompletion && completedClip != null)
            OnPresentationCompleted?.Invoke(this, completedClip);
    }

    private void StopActiveClipInternal(bool notifyCompletion, bool useClipRestoreMode, string reason)
    {
        if (activePresentation == null || presentationHost == null)
            return;

        ExplorationPresentationRestoreMode restoreMode = ExplorationPresentationRestoreMode.ReleaseToGameplay;
        if (useClipRestoreMode && activePresentation.Clip != null)
        {
            restoreMode = activePresentation.Clip.CompletionBehavior == ExplorationPresentationRestoreMode.HoldLastFrame
                ? ExplorationPresentationRestoreMode.ReleaseToGameplay
                : activePresentation.Clip.CompletionBehavior;
        }

        presentationHost.StopClip();
        FinishActivePresentation(restoreMode, notifyCompletion, reason);
    }

    private void PlayCompanionCues(ExplorationSpritePresentationClipSO clip)
    {
        if (clip == null)
            return;

        PlaySoundCues(clip.SoundCues);
        PlayEffectCues(clip.LocalEffectCues);
    }

    private void PlaySoundCues(ExplorationPresentationSoundCue[] soundCues)
    {
        if (soundCues == null || soundCues.Length == 0)
            return;

        AudioService audioService = AudioService.Instance;
        if (audioService == null)
        {
            LogWarning("AudioService.Instance was null while trying to play presentation sound cues.");
            return;
        }

        for (int i = 0; i < soundCues.Length; i++)
        {
            ExplorationPresentationSoundCue cue = soundCues[i];
            if (cue == null || cue.SoundEvent == null || string.IsNullOrWhiteSpace(cue.SlotId))
                continue;

            AudioSlotBinding slot = FindAudioSlot(cue.SlotId);
            if (slot == null)
            {
                LogWarning($"Audio slot '{cue.SlotId}' was not found on presenter '{name}'.");
                continue;
            }

            Transform origin = slot.ResolveOrigin(transform);
            if (cue.FollowSlotTransform && slot.FollowTransform && origin != null)
            {
                audioService.Play(cue.SoundEvent, followTransform: origin);
            }
            else if (origin != null)
            {
                audioService.Play(cue.SoundEvent, origin.position);
            }
            else
            {
                audioService.Play(cue.SoundEvent);
            }
        }
    }

    private void PlayEffectCues(ExplorationPresentationLocalEffectCue[] effectCues)
    {
        if (effectCues == null || effectCues.Length == 0)
            return;

        for (int i = 0; i < effectCues.Length; i++)
        {
            ExplorationPresentationLocalEffectCue cue = effectCues[i];
            if (cue == null || string.IsNullOrWhiteSpace(cue.SlotId))
                continue;

            EffectSlotBinding slot = FindEffectSlot(cue.SlotId);
            if (slot == null)
            {
                LogWarning($"Effect slot '{cue.SlotId}' was not found on presenter '{name}'.");
                continue;
            }

            slot.Play();
        }
    }

    private void AttachTriggerRegistry()
    {
        if (triggerRegistry == null)
            triggerRegistry = FindFirstObjectByType<TriggerRegistry>();

        if (triggerRegistry == null)
            return;

        triggerRegistry.OnTriggerFired -= HandleTriggerFired;
        triggerRegistry.OnTriggerFired += HandleTriggerFired;
    }

    private void DetachTriggerRegistry()
    {
        if (triggerRegistry == null)
            return;

        triggerRegistry.OnTriggerFired -= HandleTriggerFired;
    }

    private AudioSlotBinding FindAudioSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId) || audioSlots == null)
            return null;

        for (int i = 0; i < audioSlots.Length; i++)
        {
            AudioSlotBinding slot = audioSlots[i];
            if (slot != null && string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))
                return slot;
        }

        return null;
    }

    private EffectSlotBinding FindEffectSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId) || effectSlots == null)
            return null;

        for (int i = 0; i < effectSlots.Length; i++)
        {
            EffectSlotBinding slot = effectSlots[i];
            if (slot != null && string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))
                return slot;
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (playerAnimationController == null)
            playerAnimationController = GetComponent<PlayerAnimationController3D_Mesh>() ??
                                        GetComponentInChildren<PlayerAnimationController3D_Mesh>(true);

        if (playerVisual == null)
            playerVisual = GetComponentInChildren<MeshRendererPlayerVisual>(true);

        if (npcController == null)
            npcController = GetComponent<NPCController>();

        if (npcMovementAgent == null)
            npcMovementAgent = GetComponent<NPCMovementAgent>() ??
                               GetComponentInChildren<NPCMovementAgent>(true);

        if (npcVisual == null)
            npcVisual = GetComponentInChildren<MeshRendererNPCVisual>(true);
    }

    private void ResolvePresentationHost()
    {
        if (npcMovementAgent != null && npcVisual != null)
        {
            presentationHost = new NpcPresentationHost(npcMovementAgent, npcVisual);
            return;
        }

        if (playerAnimationController != null && playerVisual != null)
        {
            presentationHost = new PlayerPresentationHost(playerAnimationController, playerVisual);
            return;
        }

        presentationHost = null;
    }

    private void TryBindLoopingPresentationToInteractionSession()
    {
        if (activePresentation == null ||
            activePresentation.PlaybackRequest.Mode != ExplorationPresentationPlaybackMode.LoopUntilStopped ||
            npcController == null)
        {
            return;
        }

        NPCInteractionSessionService sessionService = FindFirstObjectByType<NPCInteractionSessionService>();
        if (sessionService == null)
            return;

        sessionService.TryBindPresentationOwnership(npcController, this, activePresentation.PlaybackId);
    }

    private static bool ValidatePlaybackRequest(
        ExplorationSpritePresentationClipSO clip,
        ExplorationPresentationPlaybackRequest playbackRequest,
        out string validationMessage)
    {
        validationMessage = null;

        if (playbackRequest.Mode == ExplorationPresentationPlaybackMode.LoopForDuration &&
            playbackRequest.DurationSeconds <= 0f)
        {
            validationMessage = $"Clip '{clip.name}' uses LoopForDuration but the playback duration was not positive.";
            return false;
        }

        return true;
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[ExplorationSpritePresentationPresenter] {message}", this);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ExplorationSpritePresentationPresenter] {message}", this);
    }
}
