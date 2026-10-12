using UnityEngine;

/// <summary>
/// Keeps a stationed NPC facing one direction and playing a looping work sound.
/// The loop stops for that NPC's interaction and starts again from the beginning
/// once the dialogue session ends.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcStationWorkLoop : MonoBehaviour
{
    [SerializeField] private SoundEventSO workLoop;
    [SerializeField] private NPCFaceDirectionPreset restFacing = NPCFaceDirectionPreset.WorldNorth;

    private NPCController npc;
    private AudioHandle loopHandle;
    private bool waitingToResume;

    private void Awake()
    {
        npc = GetComponent<NPCController>();
    }

    private void Start()
    {
        ApplyRestFacing();
        PlayLoop();
    }

    private void Update()
    {
        bool interacting = IsThisNpcInteracting();
        if (interacting)
        {
            if (!waitingToResume)
            {
                StopLoop();
                waitingToResume = true;
            }

            return;
        }

        if (waitingToResume)
        {
            ApplyRestFacing();
            PlayLoop();
            waitingToResume = false;
            return;
        }

        if (!loopHandle.IsValid || AudioService.Instance == null || !AudioService.Instance.IsPlaying(loopHandle))
            PlayLoop();
    }

    private void OnDisable()
    {
        StopLoop();
        waitingToResume = false;
    }

    private void ApplyRestFacing()
    {
        if (npc == null)
            npc = GetComponent<NPCController>();
        npc?.FaceDirection(restFacing);
    }

    private bool IsThisNpcInteracting()
    {
        if (npc == null)
            return false;

        NPCInteractionSessionService sessions = NPCInteractionSessionService.Instance;
        return sessions != null && sessions.TryGetActiveSessionId(npc, out _);
    }

    private void PlayLoop()
    {
        if (workLoop == null || !isActiveAndEnabled)
            return;

        AudioService audio = AudioService.Instance;
        if (audio == null)
            return;

        if (loopHandle.IsValid && audio.IsPlaying(loopHandle))
            return;

        StopLoop();
        loopHandle = audio.Play(workLoop, null, transform);
    }

    private void StopLoop()
    {
        if (!loopHandle.IsValid)
            return;

        loopHandle.Stop();
        loopHandle = default;
    }
}
