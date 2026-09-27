using UnityEngine;

[DisallowMultipleComponent]
public sealed class ContinuousRotator : MonoBehaviour
{
    public enum SpeedMode { DegreesPerSecond, RPM }
    public enum SpaceMode { Local, World }

    [Header("Target / Pivot")]
    [Tooltip("If set, rotate this target instead of the GameObject this script is on.")]
    [SerializeField] private Transform target;

    [Tooltip("Optional pivot/anchor to rotate around (like a door hinge). If null, rotates in place.")]
    [SerializeField] private Transform pivot;

    [Header("Rotation")]
    [SerializeField] private SpeedMode speedMode = SpeedMode.RPM;

    [Tooltip("When SpeedMode=RPM, this is rotations per minute. When DegreesPerSecond, it's degrees/sec.")]
    [SerializeField] private float speed = 120f;

    [Tooltip("Rotation axis. For a vertical fan facing you, try Vector3.forward or Vector3.up depending on model orientation.")]
    [SerializeField] private Vector3 axis = Vector3.forward;

    [Tooltip("If true, rotates clockwise when looking along the axis direction. (Clockwise is view-dependent.)")]
    [SerializeField] private bool clockwise = true;

    [SerializeField] private SpaceMode spaceMode = SpaceMode.Local;

    [Header("Timing")]
    [Tooltip("Use unscaled time so it keeps spinning even if Time.timeScale=0 (menus/loading).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Tooltip("Start spinning immediately on enable.")]
    [SerializeField] private bool playOnEnable = true;

    private Transform _t;
    private bool _isPlaying;

    private void Awake()
    {
        _t = target != null ? target : transform;

        // Normalize axis defensively
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.up;
        else
            axis = axis.normalized;
    }

    private void OnEnable()
    {
        _isPlaying = playOnEnable;
    }

    private void Update()
    {
        if (!_isPlaying) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f) return;

        float degPerSec = speedMode == SpeedMode.RPM ? (speed * 360f / 60f) : speed;
        float direction = clockwise ? -1f : 1f; // negative gives "clockwise" when looking along +axis
        float deltaDegrees = degPerSec * dt * direction;

        if (pivot != null)
        {
            // Rotate around pivot point + pivot axis orientation
            Vector3 worldAxis = (spaceMode == SpaceMode.World)
                ? axis
                : pivot.TransformDirection(axis);

            _t.RotateAround(pivot.position, worldAxis, deltaDegrees);
        }
        else
        {
            // Rotate in place
            if (spaceMode == SpaceMode.World)
                _t.Rotate(axis, deltaDegrees, Space.World);
            else
                _t.Rotate(axis, deltaDegrees, Space.Self);
        }
    }

    // Optional public controls (handy for triggers/testing)
    public void Play() => _isPlaying = true;
    public void Stop() => _isPlaying = false;
    public void SetSpeed(float newSpeed) => speed = newSpeed;
}

