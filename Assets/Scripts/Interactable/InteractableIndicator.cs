using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Handle for a temporary indicator suppression request.
/// Release when the owning system is ready for the indicator to become visible again.
/// </summary>
public struct InteractableIndicatorSuppressionHandle : IDisposable
{
    private readonly int id;
    private readonly Action<int> releaseCallback;
    private bool isReleased;

    public bool IsReleased => isReleased;
    public bool IsValid => releaseCallback != null;

    internal InteractableIndicatorSuppressionHandle(int id, Action<int> releaseCallback)
    {
        this.id = id;
        this.releaseCallback = releaseCallback;
        isReleased = false;
    }

    public void Release()
    {
        if (isReleased || releaseCallback == null)
            return;

        isReleased = true;
        releaseCallback(id);
    }

    public void Dispose() => Release();
}

/// <summary>
/// Visual indicator for interactables: bubble quad mesh + key binding text.
/// Billboarded above the object, shown when player is in range.
/// Uses MeshQuadImage for the bubble (reusable quad mesh). Supports fallback prompt when Input System lookup fails.
/// </summary>
public class InteractableIndicator : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Offset above the pivot (world units)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("Uniform local scale. Final size is scale * scaleMultiplier * parent lossy scale.")]
    [SerializeField] private float scale = 0.5f;

    [Tooltip("Non-uniform local scale. NPC Interactable volumes are (4,3,4); identity parents should use the same multiplier so the bubble matches.")]
    [SerializeField] private Vector3 scaleMultiplier = Vector3.one;

    [Header("Visual")]
    [Tooltip("Bubble/icon quad mesh. Optional — can use only text. Assign a child with MeshQuadImage.")]
    [SerializeField] private MeshQuadImage bubbleMesh;

    [Tooltip("Text for key binding (e.g. 'E', 'X'). Updated from InputAction or fallback.")]
    [SerializeField] private TextMeshProUGUI keyText;

    [Header("Input (for dynamic key)")]
    [Tooltip("Input action for Interact. If set, key text updates from current binding.")]
    [SerializeField] private InputActionReference interactActionRef;

    [Tooltip("Fallback when InputAction lookup fails (e.g. 'E', 'X')")]
    [SerializeField] private string fallbackPrompt = "E";

    [Header("Animation")]
    [Tooltip("Fade in/out duration")]
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Camera mainCam;
    private InputLockService inputLockService;
    private bool inRange;
    private float targetAlpha;
    private int nextSuppressionId = 1;
    private readonly HashSet<int> activeSuppressions = new HashSet<int>();

    public bool IsSuppressed => activeSuppressions.Count > 0;
    public int SuppressionCount => activeSuppressions.Count;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        mainCam = SceneCameraResolver.ResolveGameplayCamera();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCam;
        }
        targetAlpha = 0f;
        canvasGroup.alpha = 0f;

        UpdateKeyText();
    }

    private void Start()
    {
        // Sync bubble alpha after MeshQuadImage has initialized (runs after child Awake)
        if (bubbleMesh != null)
            bubbleMesh.SetAlpha(canvasGroup.alpha);
    }

    private void OnEnable()
    {
        if (interactActionRef != null && interactActionRef.action != null)
            interactActionRef.action.Enable();
        UpdateKeyText();
    }

    private void UpdateKeyText()
    {
        if (keyText == null) return;

        string prompt = fallbackPrompt;
        if (interactActionRef != null && interactActionRef.action != null)
        {
            try
            {
                var action = interactActionRef.action;
                if (action.bindings.Count > 0)
                {
                    string display = action.GetBindingDisplayString(0,
                        InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
                    if (!string.IsNullOrEmpty(display))
                        prompt = display;
                }
            }
            catch (System.Exception)
            {
                // Use fallback
            }
        }

        keyText.text = prompt;
    }

    /// <summary>Called by InteractableObject when player enters/exits range.</summary>
    public void SetInRange(bool inRange)
    {
        this.inRange = inRange;
        RefreshTargetAlpha();
    }

    /// <summary>
    /// Acquire a suppression handle that keeps the indicator hidden until released.
    /// Multiple systems can safely overlap suppression ownership.
    /// </summary>
    public InteractableIndicatorSuppressionHandle AcquireSuppression()
    {
        int suppressionId = nextSuppressionId++;
        activeSuppressions.Add(suppressionId);
        RefreshTargetAlpha();

        if (enableDebugLogs)
            Debug.Log($"[InteractableIndicator] Suppression acquired on '{name}' (count={activeSuppressions.Count})", this);

        return new InteractableIndicatorSuppressionHandle(suppressionId, ReleaseSuppression);
    }

    private void LateUpdate()
    {
        RefreshTargetAlpha();

        // Billboard: face camera
        if (!SceneCameraResolver.IsUsableFor(mainCam, gameObject))
            mainCam = SceneCameraResolver.ResolveGameplayCamera();
        if (canvas != null && canvas.worldCamera != mainCam)
            canvas.worldCamera = mainCam;
        if (mainCam != null && transform.parent != null)
        {
            transform.position = transform.parent.TransformPoint(offset);
            transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
            transform.localScale = Vector3.Scale(Vector3.one * scale, scaleMultiplier);
        }

        // Fade
        float diff = targetAlpha - canvasGroup.alpha;
        if (Mathf.Abs(diff) > 0.01f)
        {
            float step = (1f / Mathf.Max(0.01f, fadeDuration)) * Time.deltaTime;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, step);
        }

        // Sync bubble mesh alpha (MeshRenderer is not affected by CanvasGroup)
        if (bubbleMesh != null)
            bubbleMesh.SetAlpha(canvasGroup.alpha);
    }

    private void RefreshTargetAlpha()
    {
        targetAlpha = ShouldBeVisible() ? 1f : 0f;
    }

    private void ReleaseSuppression(int suppressionId)
    {
        if (!activeSuppressions.Remove(suppressionId))
            return;

        RefreshTargetAlpha();

        if (enableDebugLogs)
            Debug.Log($"[InteractableIndicator] Suppression released on '{name}' (count={activeSuppressions.Count})", this);
    }

    private bool ShouldBeVisible()
    {
        return inRange &&
               activeSuppressions.Count == 0 &&
               !IsInteractionHiddenBySystemLock();
    }

    private bool IsInteractionHiddenBySystemLock()
    {
        if (inputLockService == null)
            inputLockService = FindFirstObjectByType<InputLockService>();

        return inputLockService != null && inputLockService.IsInteractionLocked;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
        Vector3 pos = transform.parent != null ? transform.parent.position + offset : offset;
        Gizmos.DrawWireSphere(pos, 0.2f);
    }
#endif
}
