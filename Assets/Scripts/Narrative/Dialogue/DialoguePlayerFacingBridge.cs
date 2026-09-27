using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the player-facing policy authored on a dialogue sequence. Completion events are
/// lifecycle-terminal, so explicit stops and interruptions release tracking exactly like normal completion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueService))]
public class DialoguePlayerFacingBridge : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Tag used to resolve the live player object.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private DialogueService dialogueService;
    private PlayerFacingOverrideService facingOverrideService;
    private PlayerFacingOverrideHandle facingOverrideHandle;
    private int activePlaybackId;

    private void OnEnable()
    {
        dialogueService = GetComponent<DialogueService>();
        if (dialogueService == null)
            dialogueService = DialogueService.Instance;

        facingOverrideService = GetComponent<PlayerFacingOverrideService>();
        if (facingOverrideService == null)
            facingOverrideService = PlayerFacingOverrideService.Instance;

        if (dialogueService == null)
        {
            Debug.LogWarning("[DialoguePlayerFacingBridge] DialogueService not found; bridge is inactive.");
            return;
        }

        dialogueService.OnDialoguePlaybackStarted += OnDialoguePlaybackStarted;
        dialogueService.OnDialoguePlaybackComplete += OnDialoguePlaybackComplete;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        if (dialogueService != null)
        {
            dialogueService.OnDialoguePlaybackStarted -= OnDialoguePlaybackStarted;
            dialogueService.OnDialoguePlaybackComplete -= OnDialoguePlaybackComplete;
        }

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        ReleaseTrackingOverride("bridge disabled");
    }

    private void OnDialoguePlaybackStarted(DialoguePlaybackContext playback)
    {
        ReleaseTrackingOverride("new dialogue started");

        DialogueSequenceSO sequence = playback.Sequence;
        if (sequence == null || sequence.PlayerFacingPolicy == DialoguePlayerFacingPolicy.None)
            return;

        string speakerNpcId = sequence.SpeakerNpcId;
        if (string.IsNullOrWhiteSpace(speakerNpcId))
        {
            Debug.LogWarning(
                $"[DialoguePlayerFacingBridge] Dialogue '{sequence.name}' requests player-facing behavior " +
                "but Speaker NPC ID is empty.",
                sequence);
            return;
        }

        if (facingOverrideService == null)
            facingOverrideService = PlayerFacingOverrideService.Instance;
        if (facingOverrideService == null)
        {
            Debug.LogWarning("[DialoguePlayerFacingBridge] PlayerFacingOverrideService not found.");
            return;
        }

        activePlaybackId = playback.PlaybackId;
        switch (sequence.PlayerFacingPolicy)
        {
            case DialoguePlayerFacingPolicy.FaceSpeakerOnStart:
                if (!facingOverrideService.TryFaceNpcOnce(
                        speakerNpcId,
                        playerTag,
                        faceAway: false,
                        out string error))
                {
                    Debug.LogWarning(
                        $"[DialoguePlayerFacingBridge] Could not face the player toward '{speakerNpcId}' " +
                        $"for dialogue '{sequence.name}': {error}",
                        sequence);
                }
                break;

            case DialoguePlayerFacingPolicy.TrackSpeakerDuringDialogue:
                facingOverrideHandle = facingOverrideService.AcquireNpcOverride(
                    $"Dialogue:{sequence.name}",
                    speakerNpcId,
                    playerTag,
                    faceAway: false,
                    owner: this);
                break;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[DialoguePlayerFacingBridge] Dialogue '{sequence.name}' applied " +
                $"{sequence.PlayerFacingPolicy} toward '{speakerNpcId}'.");
        }
    }

    private void OnDialoguePlaybackComplete(DialoguePlaybackContext playback)
    {
        if (activePlaybackId != 0 && playback.PlaybackId != activePlaybackId)
            return;

        ReleaseTrackingOverride("dialogue complete");
    }

    private void OnActiveSceneChanged(Scene _, Scene __)
    {
        ReleaseTrackingOverride("active scene changed");
    }

    private void ReleaseTrackingOverride(string reason)
    {
        if (facingOverrideHandle.IsValid && !facingOverrideHandle.IsReleased)
            facingOverrideHandle.Release();

        facingOverrideHandle = default;
        activePlaybackId = 0;

        if (enableDebugLogs)
            Debug.Log($"[DialoguePlayerFacingBridge] Player-facing tracking released ({reason}).");
    }
}
