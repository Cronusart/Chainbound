using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inner streaming trigger for one overworld seam. Child
/// <see cref="OverworldStreamBuffer"/> colliders supply hysteresis: the reverse
/// load/unload runs when the player leaves the buffer, not when they leave this
/// inner volume.
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Signal Jam/World/Overworld Stream Volume")]
public sealed class OverworldStreamVolume : MonoBehaviour
{
    public enum Operation
    {
        EnsureChunk,
        ReleaseChunk,
        SetActiveChunk,
    }

    [Serializable]
    public struct Directive
    {
        [SerializeField] private Operation operation;
        [SerializeField] private string chunkSceneId;

        public Operation Type => operation;
        public string ChunkSceneId => chunkSceneId?.Trim() ?? string.Empty;

        public Directive(Operation operation, string chunkSceneId)
        {
            this.operation = operation;
            this.chunkSceneId = chunkSceneId;
        }
    }

    [Header("Identity")]
    [SerializeField] private string streamVolumeId;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Inner volume (this collider)")]
    [Tooltip("Fired once when the player first enters this collider. Typical: Ensure next chunk, SetActive this corridor, Release previous town.")]
    [SerializeField] private List<Directive> onPlayerEntered = new List<Directive>();

    [Tooltip("Optional. Leave empty so leaving the inner volume does not restore the previous chunk.")]
    [SerializeField] private List<Directive> onPlayerExited = new List<Directive>();

    [Header("Buffer children")]
    [Tooltip("Fired when the player leaves a child buffer after visiting this inner volume, and is no longer inside the inner volume. Typical: Ensure previous town, Release next chunk. Place buffers on the town side only — do not let them extend past the jungle face.")]
    [SerializeField] private List<Directive> onBufferExited = new List<Directive>();

    [SerializeField] private bool enableDebugLogs;

    private readonly List<OverworldStreamBuffer> buffers = new List<OverworldStreamBuffer>();
    private int innerOverlapCount;
    private int bufferOverlapCount;
    private bool visitedInner;

    public string StreamVolumeId => streamVolumeId?.Trim() ?? string.Empty;
    public string PlayerTag => string.IsNullOrWhiteSpace(playerTag) ? "Player" : playerTag;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;

        RefreshBuffers();
        for (int i = 0; i < buffers.Count; i++)
            buffers[i].Initialize(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        innerOverlapCount++;
        if (innerOverlapCount != 1)
            return;

        visitedInner = true;
        Apply(onPlayerEntered, "inner-enter");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        innerOverlapCount = Mathf.Max(0, innerOverlapCount - 1);
        if (innerOverlapCount == 0)
            Apply(onPlayerExited, "inner-exit");
    }

    public void NotifyBufferEntered()
    {
        bufferOverlapCount++;
    }

    public void NotifyBufferExited()
    {
        bufferOverlapCount = Mathf.Max(0, bufferOverlapCount - 1);
        if (!visitedInner || innerOverlapCount > 0 || bufferOverlapCount > 0)
            return;

        visitedInner = false;
        Apply(onBufferExited, "buffer-exit");
    }

    public bool IsPlayer(Component candidate)
    {
        if (candidate == null)
            return false;

        Transform current = candidate.transform;
        while (current != null)
        {
            if (current.CompareTag(PlayerTag))
                return true;
            current = current.parent;
        }

        return false;
    }

    internal void RefreshBuffers()
    {
        buffers.Clear();
        GetComponentsInChildren(true, buffers);
        for (int i = buffers.Count - 1; i >= 0; i--)
        {
            if (buffers[i] == null || buffers[i].gameObject == gameObject)
                buffers.RemoveAt(i);
        }
    }

    private void Apply(IReadOnlyList<Directive> directives, string reason)
    {
        if (directives == null || directives.Count == 0)
            return;

        OverworldStreamingService service = OverworldStreamingService.Instance;
        if (service == null)
        {
            Debug.LogWarning($"[{nameof(OverworldStreamVolume)}] '{name}' could not find OverworldStreamingService ({reason}).");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[{nameof(OverworldStreamVolume)}] '{name}' ({StreamVolumeId}) {reason}.");

        for (int i = 0; i < directives.Count; i++)
        {
            Directive directive = directives[i];
            if (string.IsNullOrWhiteSpace(directive.ChunkSceneId))
                continue;

            switch (directive.Type)
            {
                case Operation.EnsureChunk:
                    service.EnsureChunk(directive.ChunkSceneId);
                    break;
                case Operation.ReleaseChunk:
                    service.ReleaseChunk(directive.ChunkSceneId);
                    break;
                case Operation.SetActiveChunk:
                    service.SetActiveChunk(directive.ChunkSceneId);
                    break;
            }
        }
    }

    private void OnDrawGizmos()
    {
        Collider inner = GetComponent<Collider>();
        if (inner is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.18f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.85f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
