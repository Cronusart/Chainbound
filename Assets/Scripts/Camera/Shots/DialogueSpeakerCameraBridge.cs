using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional bridge: when <see cref="DialogueService"/> starts a line, plays a <see cref="CameraShotPresetSO"/>
/// based on explicit per-line camera data, with speaker-name rules providing shot guidance when a line does not
/// specify its own preset.
/// Add to a bootstrap / GameServices object. Prefer leaving this component <b>disabled</b> on the prefab and
/// enabling it only for specific dialogues via <see cref="Step_PlayDialogue"/> (<c>useSpeakerCameraBridge</c>).
/// Can optionally gate line advance until the current shot settles on target.
///
/// Uses <see cref="CameraShotController.BeginDialogueCameraSession"/> on dialogue start to snapshot profile/zoom/pitch
/// and pause follow, then <see cref="CameraShotController.PlayDialogueLineShot"/> per line so the rig does not snap back
/// after every line. End with <see cref="CameraShotController.EndDialogueCameraSession(float)"/> (handled in OnDialogueComplete),
/// unless <see cref="Step_PlayDialogue"/> requests persist via <see cref="SetPersistAfterDialogueEnds"/> — then keep the bridge
/// enabled and defer restore until <see cref="Step_ReleaseDialogueCamera"/> (or a non-persist dialogue that ends the session).
/// </summary>
public class DialogueSpeakerCameraBridge : MonoBehaviour
{
    /// <summary>
    /// Finds the bridge in loaded scenes, including when this component is disabled on <c>GameServices</c>.
    /// Used by <see cref="Step_PlayDialogue"/> to opt-in per dialogue step.
    /// </summary>
    public static DialogueSpeakerCameraBridge FindInstance()
    {
        return UnityEngine.Object.FindFirstObjectByType<DialogueSpeakerCameraBridge>(FindObjectsInactive.Include);
    }

    [Serializable]
    public class SpeakerShotRule
    {
        [Tooltip("If dialogue line speakerName contains this (case-insensitive), this rule applies.")]
        public string speakerNameContains = "";

        [Tooltip("Shot to use as guidance when the matched line does not supply its own shot preset.")]
        public CameraShotPresetSO shotPreset;
    }

    [Header("Rules (first match wins)")]
    [SerializeField] private List<SpeakerShotRule> rules = new List<SpeakerShotRule>();

    [Header("Fallback")]
    [Tooltip("When no rule matches and the line has no explicit shot preset, optionally play this shot. Leave null to skip.")]
    [SerializeField] private CameraShotPresetSO defaultShotIfNoMatch;

    [Header("Behavior")]
    [Tooltip("If a shot is still settling, skip starting another (reduces flicker when lines are forced faster than the camera can settle). Settled hold polish can still be interrupted by the next line.")]
    [SerializeField] private bool skipIfShotAlreadyPlaying = true;

    [Header("Anchored session (smoother line-to-line)")]
    [Tooltip("When true, captures camera state once for the dialogue and uses that snapshot for a smooth restore at the end. Individual shot presets can still override follow behavior.")]
    [SerializeField] private bool useAnchoredDialogueSession = true;

    [Tooltip("Default follow-pause baseline for the anchored session. Individual shot presets can temporarily override this via their Follow Mode.")]
    [SerializeField] private bool pauseFollowDuringAnchoredSession = true;

    [Tooltip("Blend time used when restoring camera follow after dialogue camera playback ends.")]
    [SerializeField] private float dialogueExitBlendTime = 0.3f;

    [Tooltip("When true, each line shot applies a runtime copy of ShotProfile with followOffset = 0 so framing comes from rig movement + look-at (less jank vs large profile offsets).")]
    [SerializeField] private bool neutralizeShotProfileFollowOffset;

    [Header("Responsive line timing")]
    [Tooltip("Optional faster transition duration for dialogue line shots. <= 0 keeps the preset duration.")]
    [SerializeField] private float defaultLineTransitionDuration = 0.22f;

    [Tooltip("When true, player advance waits until the current dialogue camera shot has settled on target. Hold polish can continue and be interrupted by the next line.")]
    [SerializeField] private bool gateAdvanceUntilCameraSettles = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private DialogueService dialogueService;
    private DialogueSequenceSO activeDialogueSequence;
    private bool ownsDialogueSession;
    private bool touchedCameraThisDialogue;
    /// <summary>
    /// Set by <see cref="Step_PlayDialogue"/> before starting dialogue; consumed on <see cref="DialogueService.OnDialogueComplete"/>.
    /// </summary>
    private bool persistAfterThisDialogueComplete;
    private static readonly DialogueLineCameraDirective EmptyLineCameraDirective = new DialogueLineCameraDirective();

    /// <summary>
    /// When true, the next dialogue completion will not end the camera session or restore follow (camera/follow stay as the last line left them).
    /// Pair with <see cref="Step_ReleaseDialogueCamera"/> to restore the pre-beat snapshot.
    /// </summary>
    public void SetPersistAfterDialogueEnds(bool persist)
    {
        persistAfterThisDialogueComplete = persist;
    }

    private void OnEnable()
    {
        dialogueService = DialogueService.Instance;
        if (dialogueService != null)
        {
            dialogueService.OnDialogueStarted += OnDialogueStarted;
            dialogueService.OnLineStarted += OnLineStarted;
            dialogueService.OnLineAdvanceReady += IsCameraReadyForLineAdvance;
            dialogueService.OnDialogueComplete += OnDialogueComplete;
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[DialogueSpeakerCameraBridge] DialogueService not found - bridge inactive.");
        }
    }

    private void OnDisable()
    {
        if (dialogueService != null)
        {
            dialogueService.OnDialogueStarted -= OnDialogueStarted;
            dialogueService.OnLineStarted -= OnLineStarted;
            dialogueService.OnLineAdvanceReady -= IsCameraReadyForLineAdvance;
            dialogueService.OnDialogueComplete -= OnDialogueComplete;
        }

        // After persist, local flags are cleared but CameraShotController may still have an active session
        // until Step_ReleaseDialogueCamera. If the bridge is disabled without that step, end the session here.
        if (!ownsDialogueSession && !touchedCameraThisDialogue)
        {
            CameraShotController shotsEarly = CameraShotController.Instance;
            if (shotsEarly != null && shotsEarly.IsDialogueCameraSessionActive)
                shotsEarly.EndDialogueCameraSession(dialogueExitBlendTime);

            return;
        }

        CleanupDialogueCameraState("bridge disabled");
    }

    private void OnDialogueStarted(DialogueSequenceSO sequence)
    {
        ResetDialogueTracking();
        activeDialogueSequence = sequence;

        if (!useAnchoredDialogueSession)
            return;

        CameraShotController shots = CameraShotController.Instance;
        if (shots == null)
            return;

        shots.BeginDialogueCameraSession(pauseFollowDuringAnchoredSession);
        ownsDialogueSession = true;

        if (enableDebugLogs)
            Debug.Log("[DialogueSpeakerCameraBridge] Anchored dialogue camera session started.");
    }

    private void OnLineStarted(DialogueLine line, int index)
    {
        if (line == null)
            return;

        CameraShotController shots = CameraShotController.Instance;
        if (shots == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[DialogueSpeakerCameraBridge] CameraShotController not found.");
            return;
        }

        if (skipIfShotAlreadyPlaying && shots.IsPlayingShot && !shots.HasCurrentShotSettled)
        {
            if (enableDebugLogs)
                Debug.Log("[DialogueSpeakerCameraBridge] Skipping shot - previous line camera is still settling.");
            return;
        }

        if (useAnchoredDialogueSession && !shots.IsDialogueCameraSessionActive)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[DialogueSpeakerCameraBridge] Anchored session expected but not active - calling BeginDialogueCameraSession.");

            shots.BeginDialogueCameraSession(pauseFollowDuringAnchoredSession);
            ownsDialogueSession = true;
        }

        SpeakerShotRule matchedRule = FindMatchingRule(line.speakerName ?? string.Empty);
        DialogueLineCameraDirective lineCamera = GetLineCameraDirective(line);

        CameraShotPresetSO preset = lineCamera.shotPreset != null
            ? lineCamera.shotPreset
            : matchedRule != null ? matchedRule.shotPreset : defaultShotIfNoMatch;

        if (preset == null)
        {
            if (enableDebugLogs)
                Debug.Log($"[DialogueSpeakerCameraBridge] Line {index} has no camera shot preset; skipping.");

            return;
        }

        Transform target = ResolveLineTarget(lineCamera, index, preset);
        Coroutine lineShot;
        if (useAnchoredDialogueSession)
        {
            lineShot = shots.PlayDialogueLineShot(
                preset,
                target,
                neutralizeShotProfileFollowOffset,
                defaultLineTransitionDuration);
        }
        else
        {
            lineShot = shots.PlayShot(preset, target, defaultLineTransitionDuration);
        }

        if (lineShot != null)
            touchedCameraThisDialogue = true;

        if (enableDebugLogs)
        {
            string presetSource = lineCamera.shotPreset != null
                ? "line"
                : matchedRule != null ? $"rule '{matchedRule.speakerNameContains}'" : "default";

            Debug.Log(
                $"[DialogueSpeakerCameraBridge] Line {index} -> shot '{preset.name}' from {presetSource}, " +
                $"target={(target != null ? target.name : "preset/null")}.");
        }
    }

    private bool IsCameraReadyForLineAdvance(DialogueLine line, int index)
    {
        if (!gateAdvanceUntilCameraSettles || line == null || !touchedCameraThisDialogue)
        {
            return true;
        }

        CameraShotController shots = CameraShotController.Instance;
        if (shots == null)
        {
            return true;
        }

        return shots.HasCurrentShotSettled;
    }

    private DialogueLineCameraDirective GetLineCameraDirective(DialogueLine line)
    {
        return line != null && line.camera != null
            ? line.camera
            : EmptyLineCameraDirective;
    }

    private SpeakerShotRule FindMatchingRule(string speaker)
    {
        foreach (SpeakerShotRule rule in rules)
        {
            if (rule == null || rule.shotPreset == null || string.IsNullOrEmpty(rule.speakerNameContains))
                continue;

            if (speaker.IndexOf(rule.speakerNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                return rule;
        }

        return null;
    }

    private Transform ResolveLineTarget(DialogueLineCameraDirective lineCamera, int lineIndex, CameraShotPresetSO preset)
    {
        DialogueCameraTargetSource targetSource = lineCamera != null
            ? lineCamera.targetSource
            : DialogueCameraTargetSource.None;

        switch (targetSource)
        {
            case DialogueCameraTargetSource.None:
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[DialogueSpeakerCameraBridge] Line {lineIndex} uses shot '{preset.name}' with no explicit per-line target; " +
                        "passing null so the preset/default framing drives the shot.");
                }

                return null;

            case DialogueCameraTargetSource.PresetDefined:
                return null;

            case DialogueCameraTargetSource.Player:
                return ResolvePlayerTarget(lineIndex);

            case DialogueCameraTargetSource.NpcId:
                return ResolveNpcTarget(lineCamera.npcId, lineIndex);

            case DialogueCameraTargetSource.GameObjectTag:
                return ResolveTaggedTarget(lineCamera.targetTag, lineIndex);

            case DialogueCameraTargetSource.SequenceSpeaker:
                return ResolveNpcTarget(
                    activeDialogueSequence != null ? activeDialogueSequence.SpeakerNpcId : string.Empty,
                    lineIndex);

            default:
                return null;
        }
    }

    private Transform ResolvePlayerTarget(int lineIndex)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null && enableDebugLogs)
        {
            Debug.LogWarning($"[DialogueSpeakerCameraBridge] Line {lineIndex} requested Player target, but none was found.");
        }

        return player != null ? player.transform : null;
    }

    private Transform ResolveNpcTarget(string npcId, int lineIndex)
    {
        NPCController npc = NPCRegistry.Instance != null ? NPCRegistry.Instance.GetNPC(npcId) : null;
        if (npc == null && enableDebugLogs)
        {
            Debug.LogWarning($"[DialogueSpeakerCameraBridge] Line {lineIndex} requested NPC target '{npcId}', but it was not found in NPCRegistry.");
        }

        return npc != null ? npc.transform : null;
    }

    private Transform ResolveTaggedTarget(string targetTag, int lineIndex)
    {
        if (string.IsNullOrEmpty(targetTag))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[DialogueSpeakerCameraBridge] Line {lineIndex} requested GameObjectTag target, but targetTag is empty.");

            return null;
        }

        GameObject taggedObject = GameObject.FindGameObjectWithTag(targetTag);
        if (taggedObject == null && enableDebugLogs)
        {
            Debug.LogWarning($"[DialogueSpeakerCameraBridge] Line {lineIndex} requested tag target '{targetTag}', but no object with that tag was found.");
        }

        return taggedObject != null ? taggedObject.transform : null;
    }

    private void OnDialogueComplete(DialogueSequenceSO _)
    {
        if (persistAfterThisDialogueComplete)
        {
            persistAfterThisDialogueComplete = false;
            ResetDialogueTracking();

            if (enableDebugLogs)
                Debug.Log("[DialogueSpeakerCameraBridge] Persisting camera after dialogue (session not ended; restore later).");

            return;
        }

        CleanupDialogueCameraState("dialogue complete");
    }

    private void CleanupDialogueCameraState(string reason)
    {
        if (!ownsDialogueSession && !touchedCameraThisDialogue)
            return;

        CameraShotController shots = CameraShotController.Instance;
        if (shots != null)
        {
            if (ownsDialogueSession)
            {
                shots.EndDialogueCameraSession(dialogueExitBlendTime);
            }
            else if (touchedCameraThisDialogue)
            {
                if (shots.IsPlayingShot)
                {
                    shots.StopShot();
                }
                else if (shots.HasSavedRestoreState)
                {
                    shots.RestoreCameraFollow(dialogueExitBlendTime);
                }
            }
        }

        if (enableDebugLogs)
            Debug.Log($"[DialogueSpeakerCameraBridge] Camera playback cleaned up ({reason}).");

        ResetDialogueTracking();
    }

    private void ResetDialogueTracking()
    {
        ownsDialogueSession = false;
        touchedCameraThisDialogue = false;
        activeDialogueSequence = null;
    }
}
