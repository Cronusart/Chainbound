using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls door animation for swing and sliding door types.
/// Supports smooth easing and state management to prevent spam.
/// </summary>
public class DoorController : MonoBehaviour
{
    public enum DoorMode { Swing, SlideDouble, SwingDouble }
    public enum DoorState { Closed, Opening, Open, Closing }

    [Header("Door Mode")]
    [SerializeField] private DoorMode mode = DoorMode.Swing;
    
    [Header("Swing Settings")]
    [Tooltip("Transform to rotate (hinge pivot). Should be parent of door mesh. Used by Swing.")]
    [SerializeField] private Transform hingePivot;
    [Tooltip("Left hinge pivot. Should be parent of the left door mesh. Used by SwingDouble.")]
    [SerializeField] private Transform leftHinge;
    [Tooltip("Right hinge pivot. Should be parent of the right door mesh. Used by SwingDouble.")]
    [SerializeField] private Transform rightHinge;
    [Tooltip("Angle to rotate when opening (degrees). SwingDouble uses +angle on the left hinge and -angle on the right.")]
    [SerializeField] private float openAngle = 90f;
    [Tooltip("Axis to rotate around")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [Tooltip("When true, SwingDouble swaps leaf directions (use if both doors swing the same way).")]
    [SerializeField] private bool invertSwingDirection = false;
    
    [Header("Slide Settings")]
    [Tooltip("Left panel transform (slides left)")]
    [SerializeField] private Transform leftPanel;
    [Tooltip("Right panel transform (slides right)")]
    [SerializeField] private Transform rightPanel;
    [Tooltip("Distance each panel slides")]
    [SerializeField] private float slideDistance = 1f;
    [Tooltip("Axis to slide along (local space)")]
    [SerializeField] private Vector3 slideAxis = Vector3.right;
    
    [Header("Timing")]
    [Tooltip("Duration to open door")]
    [SerializeField] private float openDuration = 0.5f;
    [Tooltip("Duration to close door")]
    [SerializeField] private float closeDuration = 0.5f;
    [Tooltip("Easing curve for animation")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Events")]
    [Tooltip("Invoked when door motion begins (Open); use for hinge/slide SFX.")]
    public UnityEvent OnDoorOpeningStarted;
    [Tooltip("Invoked when door motion begins (Close); use for hinge/slide SFX.")]
    public UnityEvent OnDoorClosingStarted;
    [Tooltip("Invoked when door finishes opening")]
    public UnityEvent OnDoorOpened;
    [Tooltip("Invoked when door finishes closing")]
    public UnityEvent OnDoorClosed;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public DoorState CurrentState { get; private set; } = DoorState.Closed;

    // Store original positions/rotations for slide doors
    private Vector3 leftPanelStartPos;
    private Vector3 rightPanelStartPos;
    private Quaternion hingeStartRotation;
    private Quaternion leftHingeStartRotation;
    private Quaternion rightHingeStartRotation;
    private bool hasCapturedStart;

    // Track active coroutine
    private Coroutine activeCoroutine;

    private void Awake()
    {
        CaptureStartPose();
    }

    /// <summary>
    /// Open the door (if closed or closing).
    /// </summary>
    public void Open()
    {
        CaptureStartPose();

        if (CurrentState == DoorState.Open || CurrentState == DoorState.Opening)
        {
            if (enableDebugLogs)
                Debug.Log($"[DoorController] Door on {gameObject.name} is already open or opening.");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[DoorController] Opening door on {gameObject.name}");

        CurrentState = DoorState.Opening;

        // Stop any active coroutine
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }

        OnDoorOpeningStarted?.Invoke();

        // Start opening animation
        activeCoroutine = StartCoroutine(AnimateOpen());
    }

    /// <summary>
    /// Instantly apply the open pose. Does not fire opening/opened events (no SFX).
    /// Use for continue-game / already-claimed chests.
    /// </summary>
    public void SnapOpen()
    {
        CaptureStartPose();
        if (CurrentState == DoorState.Open && activeCoroutine == null)
            return;

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        if (!ApplyOpenPose())
            return;

        CurrentState = DoorState.Open;

        if (enableDebugLogs)
            Debug.Log($"[DoorController] Snapped open on {gameObject.name}");
    }

    /// <summary>
    /// Close the door (if open or opening).
    /// </summary>
    public void Close()
    {
        if (CurrentState == DoorState.Closed || CurrentState == DoorState.Closing)
        {
            if (enableDebugLogs)
                Debug.Log($"[DoorController] Door on {gameObject.name} is already closed or closing.");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[DoorController] Closing door on {gameObject.name}");

        CurrentState = DoorState.Closing;

        // Stop any active coroutine
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }

        OnDoorClosingStarted?.Invoke();

        // Start closing animation
        activeCoroutine = StartCoroutine(AnimateClose());
    }

    /// <summary>
    /// Toggle door state (open if closed, close if open).
    /// </summary>
    public void Toggle()
    {
        if (CurrentState == DoorState.Closed || CurrentState == DoorState.Closing)
        {
            Open();
        }
        else
        {
            Close();
        }
    }

    private void CaptureStartPose()
    {
        if (hasCapturedStart)
            return;

        if (mode == DoorMode.SlideDouble)
        {
            if (leftPanel == null || rightPanel == null)
                return;
            leftPanelStartPos = leftPanel.localPosition;
            rightPanelStartPos = rightPanel.localPosition;
            hasCapturedStart = true;
            return;
        }

        if (mode == DoorMode.SwingDouble)
        {
            if (leftHinge == null || rightHinge == null)
                return;
            leftHingeStartRotation = leftHinge.localRotation;
            rightHingeStartRotation = rightHinge.localRotation;
            hasCapturedStart = true;
            return;
        }

        if (hingePivot == null)
            return;

        hingeStartRotation = hingePivot.localRotation;
        hasCapturedStart = true;
    }

    private bool ApplyOpenPose()
    {
        if (mode == DoorMode.Swing)
        {
            if (hingePivot == null)
            {
                Debug.LogError($"[DoorController] HingePivot not assigned for swing door on {gameObject.name}!");
                return false;
            }

            hingePivot.localRotation = SwingOpenRotation(hingeStartRotation, false);
            return true;
        }

        if (mode == DoorMode.SwingDouble)
        {
            if (leftHinge == null || rightHinge == null)
            {
                Debug.LogError($"[DoorController] LeftHinge or RightHinge not assigned for double swing door on {gameObject.name}!");
                return false;
            }

            leftHinge.localRotation = SwingOpenRotation(leftHingeStartRotation, false);
            rightHinge.localRotation = SwingOpenRotation(rightHingeStartRotation, true);
            return true;
        }

        if (leftPanel == null || rightPanel == null)
        {
            Debug.LogError($"[DoorController] LeftPanel or RightPanel not assigned for slide door on {gameObject.name}!");
            return false;
        }

        Vector3 axis = slideAxis.normalized * slideDistance;
        leftPanel.localPosition = leftPanelStartPos - axis;
        rightPanel.localPosition = rightPanelStartPos + axis;
        return true;
    }

    private IEnumerator AnimateOpen()
    {
        float elapsed = 0f;
        float duration = openDuration;

        if (mode == DoorMode.Swing)
        {
            if (hingePivot == null)
            {
                Debug.LogError($"[DoorController] HingePivot not assigned for swing door on {gameObject.name}!");
                CurrentState = DoorState.Closed;
                yield break;
            }

            Quaternion startRotation = hingePivot.localRotation;
            Quaternion targetRotation = SwingOpenRotation(hingeStartRotation, false);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = easeCurve.Evaluate(t);
                hingePivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
                yield return null;
            }

            hingePivot.localRotation = targetRotation;
        }
        else if (mode == DoorMode.SwingDouble)
        {
            if (leftHinge == null || rightHinge == null)
            {
                Debug.LogError($"[DoorController] LeftHinge or RightHinge not assigned for double swing door on {gameObject.name}!");
                CurrentState = DoorState.Closed;
                yield break;
            }

            Quaternion leftStart = leftHinge.localRotation;
            Quaternion rightStart = rightHinge.localRotation;
            Quaternion leftTarget = SwingOpenRotation(leftHingeStartRotation, false);
            Quaternion rightTarget = SwingOpenRotation(rightHingeStartRotation, true);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = easeCurve.Evaluate(t);
                leftHinge.localRotation = Quaternion.Slerp(leftStart, leftTarget, easedT);
                rightHinge.localRotation = Quaternion.Slerp(rightStart, rightTarget, easedT);
                yield return null;
            }

            leftHinge.localRotation = leftTarget;
            rightHinge.localRotation = rightTarget;
        }
        else if (mode == DoorMode.SlideDouble)
        {
            if (leftPanel == null || rightPanel == null)
            {
                Debug.LogError($"[DoorController] LeftPanel or RightPanel not assigned for slide door on {gameObject.name}!");
                CurrentState = DoorState.Closed;
                yield break;
            }

            Vector3 leftStart = leftPanel.localPosition;
            Vector3 rightStart = rightPanel.localPosition;
            Vector3 axis = slideAxis.normalized * slideDistance;
            Vector3 leftTarget = leftPanelStartPos - axis;
            Vector3 rightTarget = rightPanelStartPos + axis;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = easeCurve.Evaluate(t);
                leftPanel.localPosition = Vector3.Lerp(leftStart, leftTarget, easedT);
                rightPanel.localPosition = Vector3.Lerp(rightStart, rightTarget, easedT);
                yield return null;
            }

            leftPanel.localPosition = leftTarget;
            rightPanel.localPosition = rightTarget;
        }

        CurrentState = DoorState.Open;
        activeCoroutine = null;
        OnDoorOpened?.Invoke();

        if (enableDebugLogs)
            Debug.Log($"[DoorController] Door on {gameObject.name} opened.");
    }

    private IEnumerator AnimateClose()
    {
        float elapsed = 0f;
        float duration = closeDuration;

        if (mode == DoorMode.Swing)
        {
            if (hingePivot == null)
            {
                Debug.LogError($"[DoorController] HingePivot not assigned for swing door on {gameObject.name}!");
                CurrentState = DoorState.Open;
                yield break;
            }

            Quaternion startRotation = hingePivot.localRotation;
            Quaternion targetRotation = hingeStartRotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = easeCurve.Evaluate(t);
                hingePivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
                yield return null;
            }

            hingePivot.localRotation = targetRotation;
        }
        else if (mode == DoorMode.SwingDouble)
        {
            if (leftHinge == null || rightHinge == null)
            {
                Debug.LogError($"[DoorController] LeftHinge or RightHinge not assigned for double swing door on {gameObject.name}!");
                CurrentState = DoorState.Open;
                yield break;
            }

            Quaternion leftStart = leftHinge.localRotation;
            Quaternion rightStart = rightHinge.localRotation;
            Quaternion leftTarget = leftHingeStartRotation;
            Quaternion rightTarget = rightHingeStartRotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = easeCurve.Evaluate(t);
                leftHinge.localRotation = Quaternion.Slerp(leftStart, leftTarget, easedT);
                rightHinge.localRotation = Quaternion.Slerp(rightStart, rightTarget, easedT);
                yield return null;
            }

            leftHinge.localRotation = leftTarget;
            rightHinge.localRotation = rightTarget;
        }
        else if (mode == DoorMode.SlideDouble)
        {
            if (leftPanel == null || rightPanel == null)
            {
                Debug.LogError($"[DoorController] LeftPanel or RightPanel not assigned for slide door on {gameObject.name}!");
                CurrentState = DoorState.Open;
                yield break;
            }

            Vector3 leftStart = leftPanel.localPosition;
            Vector3 rightStart = rightPanel.localPosition;
            Vector3 leftTarget = leftPanelStartPos;
            Vector3 rightTarget = rightPanelStartPos;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = easeCurve.Evaluate(t);
                leftPanel.localPosition = Vector3.Lerp(leftStart, leftTarget, easedT);
                rightPanel.localPosition = Vector3.Lerp(rightStart, rightTarget, easedT);
                yield return null;
            }

            leftPanel.localPosition = leftTarget;
            rightPanel.localPosition = rightTarget;
        }

        CurrentState = DoorState.Closed;
        activeCoroutine = null;
        OnDoorClosed?.Invoke();

        if (enableDebugLogs)
            Debug.Log($"[DoorController] Door on {gameObject.name} closed.");
    }

    private Quaternion SwingOpenRotation(Quaternion startRotation, bool isRightLeaf)
    {
        float angle = openAngle;
        if (invertSwingDirection)
            angle = -angle;
        if (isRightLeaf)
            angle = -angle;

        return startRotation * Quaternion.AngleAxis(angle, rotationAxis);
    }
}
