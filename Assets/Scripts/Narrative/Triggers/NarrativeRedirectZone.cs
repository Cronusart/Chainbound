using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Reusable "guide rail" component.
///
/// Place a trigger collider in any scene and assign a DialogueSequenceSO to give
/// the player a contextual nudge whenever they wander into a restricted area.
///
/// Features:
///   - Plays dialogue via DialogueService (never interrupts an already-playing dialogue).
///   - Optional per-activation cooldown so the message doesn't spam every frame.
///   - Can be armed/disarmed at runtime or via Step_EnableGameObject so it only
///     activates during the relevant quest phase.
///   - If noReturnCollider is assigned, the collider is also enabled on first fire
///     (useful for "block going back through door" patterns).
///
/// Usage:
///   1. Add this component + a Trigger Collider to a zone GameObject.
///   2. Assign a DialogueSequenceSO with your redirect message.
///   3. (Optional) set startArmed = false and enable via Step_EnableGameObject
///      only while the relevant sequence phase is active.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NarrativeRedirectZone : MonoBehaviour
{
    [Header("Dialogue")]
    [Tooltip("Dialogue to play when the player enters this zone.")]
    [SerializeField] private DialogueSequenceSO redirectDialogue;

    [Header("Behaviour")]
    [Tooltip("Tag of the player GameObject.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Seconds before the same message can play again (0 = once per enter).")]
    [SerializeField] private float cooldownSeconds = 4f;

    [Tooltip("Lock player input (movement, jump, interaction) while redirect dialogue plays. " +
             "Set false for topical nudges where player can keep moving and dialogue auto-confirms.")]
    [SerializeField] private bool lockInputDuringDialogue = false;

    [Tooltip("If true, zone is active from the start. If false, enable this GameObject via " +
             "Step_EnableGameObject when the relevant quest phase begins.")]
    [SerializeField] private bool startArmed = true;

    [Header("Quest Guard (optional)")]
    [Tooltip("Lifecycle-aware quest guard. When configured, this zone evaluates the guard dynamically on each enter.")]
    [SerializeField] private NarrativeQuestGuard questGuard = new NarrativeQuestGuard();

    [Header("Optional — Physical Block")]
    [Tooltip("A separate Collider (e.g. an invisible wall) to enable when this zone first fires. " +
             "Useful to permanently block a path after the player tries to go the wrong way.")]
    [SerializeField] private Collider noReturnCollider;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private bool _armed;
    private float _lastFireTime = float.MinValue;

    [SerializeField, HideInInspector, FormerlySerializedAs("guardQuestId")] private string deprecatedGuardQuestId = "";

    public bool HasDeprecatedGuardQuestId => !string.IsNullOrWhiteSpace(deprecatedGuardQuestId);

    private void Awake()
    {
        _armed = startArmed;

        // Make sure our own collider is flagged as trigger.
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // noReturnCollider starts disabled; activated on first redirect fire.
        if (noReturnCollider != null)
            noReturnCollider.enabled = false;
    }

    private void OnValidate()
    {
        if (HasDeprecatedGuardQuestId)
        {
            Debug.LogWarning(
                "[NarrativeRedirectZone] Legacy guardQuestId data is still serialized on this component. Author gating through NarrativeQuestGuard instead.",
                this);
        }
    }

    // ── Public control ────────────────────────────────────────────────────────

    public void Arm()   => _armed = true;
    public void Disarm() => _armed = false;

    // ── Trigger ───────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;
        if (!other.CompareTag(playerTag)) return;
        if (redirectDialogue == null) return;
        if (Time.time - _lastFireTime < cooldownSeconds) return;

        if (NarrativeQuestGuardUtility.HasConfiguredClauses(questGuard))
        {
            NarrativeQuestGuardResult guardResult = NarrativeQuestGuardUtility.Evaluate(
                QuestService.Instance,
                questGuard,
                emptyGuardPasses: true);

            if (!guardResult.Passes)
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[NarrativeRedirectZone] Guard blocked redirect on '{name}' (quest='{guardResult.MatchedQuestId}', lifecycle={guardResult.MatchedLifecycleState}).",
                        this);
                }
                return;
            }
        }

        _lastFireTime = Time.time;

        // Enable any physical block on first fire.
        if (noReturnCollider != null && !noReturnCollider.enabled)
            noReturnCollider.enabled = true;

        StartCoroutine(PlayRedirect());
    }

    private IEnumerator PlayRedirect()
    {
        DialogueService ds = DialogueService.Instance;
        if (ds == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[NarrativeRedirectZone] DialogueService not found.");
            yield break;
        }

        // Wait if another dialogue is already playing, but bail after 5 s to avoid
        // getting stuck if a sequence-driven dialogue hangs.
        float waited = 0f;
        while (ds.IsPlaying && waited < 5f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (ds.IsPlaying) yield break; // still playing; skip this nudge

        if (enableDebugLogs)
            Debug.Log($"[NarrativeRedirectZone] Playing redirect: {redirectDialogue.name}");

        yield return ds.PlayDialogue(redirectDialogue, lockInputDuringDialogue);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius);
        }
    }
#endif
}
