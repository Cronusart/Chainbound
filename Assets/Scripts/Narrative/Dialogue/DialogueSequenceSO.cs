using System.Collections.Generic;
using UnityEngine;

public enum DialoguePlayerFacingPolicy
{
    None = 0,
    FaceSpeakerOnStart = 1,
    TrackSpeakerDuringDialogue = 2
}

/// <summary>
/// ScriptableObject containing a sequence of dialogue lines.
/// Used for monologues, NPC conversations, and scripted dialogue beats.
/// </summary>
[CreateAssetMenu(fileName = "DialogueSequence", menuName = "Chainbound/Narrative/Dialogue/Dialogue Sequence")]
public class DialogueSequenceSO : ScriptableObject
{
    [Header("Participants")]
    [Tooltip("Optional stable NPCRegistry ID for the primary speaker. Used by sequence-speaker camera targets and player-facing policies; display names remain presentation-only.")]
    [SerializeField] private string speakerNpcId = "";

    [Header("Player Presentation")]
    [Tooltip("Optional policy for turning the player toward Speaker NPC ID while this dialogue plays.")]
    [SerializeField] private DialoguePlayerFacingPolicy playerFacingPolicy = DialoguePlayerFacingPolicy.None;

    [Header("Dialogue Lines")]
    [Tooltip("Ordered list of dialogue lines")]
    [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();
    
    [Header("Settings")]
    [Tooltip("Auto-advance after typing completes (false = wait for input)")]
    [SerializeField] private bool autoAdvance = false;
    
    [Tooltip("Auto-advance delay after typing completes (seconds)")]
    [SerializeField] private float autoAdvanceDelay = 2f;
    
    [Tooltip("Allow skipping dialogue with input")]
    [SerializeField] private bool allowSkip = true;
    
    [Tooltip("Skip to end of current line or skip entire sequence")]
    [SerializeField] private bool skipToEndOfLine = true;
    
    /// <summary>List of dialogue lines.</summary>
    public IReadOnlyList<DialogueLine> Lines => lines;

    /// <summary>Stable NPCRegistry ID for the sequence's primary speaker.</summary>
    public string SpeakerNpcId => speakerNpcId;

    /// <summary>Authored player-facing behavior for this dialogue.</summary>
    public DialoguePlayerFacingPolicy PlayerFacingPolicy => playerFacingPolicy;
    
    /// <summary>Number of lines in this sequence.</summary>
    public int LineCount => lines.Count;
    
    /// <summary>Get a specific line by index.</summary>
    public DialogueLine GetLine(int index)
    {
        if (index < 0 || index >= lines.Count) return null;
        return lines[index];
    }
    
    /// <summary>Auto-advance setting.</summary>
    public bool AutoAdvance => autoAdvance;
    
    /// <summary>Auto-advance delay.</summary>
    public float AutoAdvanceDelay => autoAdvanceDelay;
    
    /// <summary>Allow skipping.</summary>
    public bool AllowSkip => allowSkip;
    
    /// <summary>Skip behavior.</summary>
    public bool SkipToEndOfLine => skipToEndOfLine;
    
    private void OnValidate()
    {
        autoAdvanceDelay = Mathf.Max(0f, autoAdvanceDelay);

        bool requiresSpeakerId = playerFacingPolicy != DialoguePlayerFacingPolicy.None;
        if (!requiresSpeakerId)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                DialogueLine line = lines[i];
                if (line != null &&
                    line.camera != null &&
                    line.camera.targetSource == DialogueCameraTargetSource.SequenceSpeaker)
                {
                    requiresSpeakerId = true;
                    break;
                }
            }
        }

        if (requiresSpeakerId && string.IsNullOrWhiteSpace(speakerNpcId))
        {
            Debug.LogWarning(
                $"[DialogueSequenceSO] '{name}' uses speaker-driven presentation but Speaker NPC ID is empty.",
                this);
        }
    }
}

