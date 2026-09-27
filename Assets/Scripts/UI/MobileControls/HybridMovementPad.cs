using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Retro "disc + D-pad" movement pad for mobile landscape play.
    ///
    /// Structure: this component owns the <b>touch rect</b>, which never moves.
    /// A separate child <see cref="padVisual"/> rocks toward the drag direction so the
    /// plate reads like a physical D-pad. Assigning <see cref="padVisual"/> to this
    /// object's own RectTransform makes the touch surface slide under the finger and
    /// feeds displaced coordinates back into the next drag sample, so that wiring is
    /// rejected at runtime (see <see cref="ResolvePadVisual"/>).
    ///
    /// Input pipeline: the final Vector2 is sent through <see cref="OnScreenControl"/> so
    /// systems already listening to the gamepad left stick receive the same value. It is
    /// also exposed via <see cref="GetInput"/> for <c>MobileInputManager.GetMovementInput()</c>.
    ///
    /// Deflection radius (how far the finger travels for full output) and travel radius
    /// (how far the visual moves) are separate: the plate only needs to rock a few pixels
    /// while the finger needs most of the pad to express direction.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class HybridMovementPad : OnScreenControl,
        IPointerDownHandler,
        IPointerUpHandler,
        ICancelHandler,
        IDragHandler
    {
        /// <summary>
        /// 8-way sectors used for the decorative D-pad overlay highlight and
        /// <see cref="SectorChanged"/> notifications.
        /// </summary>
        public enum DPadSector
        {
            None = -1,
            Right = 0,
            UpRight = 1,
            Up = 2,
            UpLeft = 3,
            Left = 4,
            DownLeft = 5,
            Down = 6,
            DownRight = 7,
        }

        /// <summary>How the full-deflection distance is derived.</summary>
        public enum DeflectionRadiusMode
        {
            /// <summary>Fraction of the pad rect's smaller half-extent. Survives layout and resolution changes.</summary>
            FractionOfPadRect = 0,
            /// <summary>Literal pad-local pixels.</summary>
            ExplicitPixels = 1,
        }

        [Header("Visuals")]
        [Tooltip("Rect used to derive the deflection radius. Leave null to use this pad's own rect.")]
        [SerializeField] private RectTransform disc;
        [Tooltip("Child that rocks toward the drag direction. Must NOT be this pad's own RectTransform.")]
        [SerializeField] private RectTransform padVisual;
        [Tooltip("Decorative 8-way D-pad overlay root. Purely cosmetic; never moved by this script.")]
        [SerializeField] private RectTransform dpadOverlay;
        [Tooltip("Optional: 8 segment graphics (0=Right, 1=UpRight, ... 7=DownRight) that highlight with the active sector.")]
        [SerializeField] private Graphic[] segmentGraphics = new Graphic[8];

        [Header("Colors")]
        [SerializeField] private Color segmentIdleColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color segmentActiveColor = new Color(1f, 0.95f, 0.5f, 1f);

        [Header("Deflection (drives reported input)")]
        [Tooltip("How the full-deflection distance is resolved.")]
        [SerializeField] private DeflectionRadiusMode deflectionRadiusMode = DeflectionRadiusMode.FractionOfPadRect;
        [Tooltip("Fraction of the pad's smaller half-extent that counts as full deflection.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float deflectionRadiusFraction = 0.55f;
        [Tooltip("Full-deflection distance in pad-local pixels. Used when mode is ExplicitPixels.")]
        [Min(1f)]
        [SerializeField] private float deflectionRadiusPixels = 150f;
        [Tooltip("Below this magnitude the pad reports zero and no D-pad segment is highlighted.")]
        [Range(0f, 1f)]
        [SerializeField] private float deadzone = 0.12f;
        [Tooltip("When true the pad snaps to 8-way digital output; disables analog walk/run.")]
        [SerializeField] private bool digitalOnly = true;

        [Header("Travel (visual only)")]
        [Tooltip("How far padVisual moves at full deflection, in its own local pixels. Keep small; this is a rocker, not a slide.")]
        [Min(0f)]
        [SerializeField] private float travelRadiusPixels = 26f;
        [Tooltip("With digitalOnly, send the visual to full travel along the snapped axis so directions feel like detents.")]
        [SerializeField] private bool snapTravelToSector = true;
        [Tooltip("Exponential follow rate for padVisual. 0 snaps instantly; ~26 reads as a firm mechanical rocker.")]
        [Min(0f)]
        [SerializeField] private float travelFollowSpeed = 26f;
        [Tooltip("Raw deflection magnitude above which a D-pad segment highlights.")]
        [Range(0f, 1f)]
        [SerializeField] private float segmentActivationMagnitude = 0.35f;

        [Header("Safety")]
        [Tooltip("Releases the pad if pointer-up was swallowed (HUD hidden mid-drag, scene load). Disable only to diagnose input-backend problems.")]
        [SerializeField] private bool useReleaseWatchdog = true;
        [Tooltip("Seconds without a pointer event before the watchdog is allowed to release a tracking pad.")]
        [Min(0.05f)]
        [SerializeField] private float watchdogGraceSeconds = 0.4f;
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Input System")]
        [InputControl(layout = "Vector2")]
        [SerializeField] private string m_ControlPath = "<Gamepad>/leftStick";

        private RectTransform selfRect;
        private Canvas parentCanvas;
        private Vector2 rawInput;
        private Vector2 analogInput;
        private Vector2 travelRest;
        private Vector2 travelCurrent;
        private DPadSector currentSector = DPadSector.None;
        private bool isTracking;
        private int activePointerId = int.MinValue;
        private float lastPointerEventTime;

        /// <summary>Raised when the highlighted 8-way sector changes, including to <see cref="DPadSector.None"/>.</summary>
        public event Action<DPadSector> SectorChanged;

        /// <summary>Current reported input, magnitude 0..1, post-deadzone and post-snap.</summary>
        public Vector2 InputVector => analogInput;

        /// <summary>Raw deflection before deadzone / digital snap (visual + debug).</summary>
        public Vector2 RawInput => rawInput;

        /// <summary>True while the pad is holding a non-neutral vector without an active pointer (stuck/latched).</summary>
        public bool HasLatchedInput => !isTracking && analogInput.sqrMagnitude > 0f;

        /// <summary>True while a finger is actively tracking and output is outside the deadzone.</summary>
        public bool IsActive => isTracking && analogInput.sqrMagnitude > deadzone * deadzone;

        /// <summary>Currently highlighted sector (visual only).</summary>
        public DPadSector ActiveSector => currentSector;

        public bool DigitalOnly
        {
            get => digitalOnly;
            set => digitalOnly = value;
        }

        /// <summary>Resolved full-deflection distance in pad-local pixels.</summary>
        public float DeflectionRadius
        {
            get
            {
                if (deflectionRadiusMode == DeflectionRadiusMode.ExplicitPixels)
                    return Mathf.Max(1f, deflectionRadiusPixels);

                RectTransform source = disc != null ? disc : selfRect;
                if (source == null)
                    return Mathf.Max(1f, deflectionRadiusPixels);

                Rect rect = source.rect;
                float halfExtent = Mathf.Min(rect.width, rect.height) * 0.5f;
                if (halfExtent <= 0f)
                    return Mathf.Max(1f, deflectionRadiusPixels);

                return Mathf.Max(1f, halfExtent * Mathf.Clamp(deflectionRadiusFraction, 0.1f, 1f));
            }
        }

        protected override string controlPathInternal
        {
            get => m_ControlPath;
            set => m_ControlPath = value;
        }

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();

            if (disc == null) disc = selfRect;
            ResolvePadVisual();

            if (dpadOverlay == selfRect)
                dpadOverlay = null;

            // Legacy OnScreenStick on the same object would also write leftStick and can latch independently.
            var legacyStick = GetComponent<OnScreenStick>();
            if (legacyStick != null)
                Destroy(legacyStick);

            travelRest = padVisual != null ? padVisual.anchoredPosition : Vector2.zero;
            travelCurrent = travelRest;

            ResetVisuals();
        }

        /// <summary>
        /// The travelling visual must be a child, otherwise moving it displaces the touch
        /// rect and every subsequent <see cref="RectTransformUtility.ScreenPointToLocalPointInRectangle"/>
        /// sample is measured against a moved origin.
        /// </summary>
        private void ResolvePadVisual()
        {
            if (padVisual != null && padVisual != selfRect)
                return;

            bool wasSelf = padVisual == selfRect && padVisual != null;
            padVisual = null;

            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i) is RectTransform child && child != selfRect)
                {
                    padVisual = child;
                    break;
                }
            }

            if (wasSelf)
            {
                Debug.LogWarning(
                    $"[HybridMovementPad] '{name}' had padVisual pointing at its own RectTransform, which drags the touch " +
                    $"surface along with the finger. {(padVisual != null ? $"Rebound to child '{padVisual.name}'." : "Add a child visual and assign it.")}",
                    this);
            }
        }

        public Vector2 GetInput() => analogInput;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isTracking && activePointerId != eventData.pointerId)
                ReleaseTracking();

            activePointerId = eventData.pointerId;
            isTracking = true;
            UpdateFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isTracking || eventData.pointerId != activePointerId)
                return;

            UpdateFromPointer(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isTracking || eventData.pointerId != activePointerId)
                return;

            ReleaseTracking();
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (!isTracking)
                return;

            ReleaseTracking();
        }

        private void LateUpdate()
        {
            if (isTracking)
            {
                if (ShouldWatchdogRelease())
                {
                    if (enableDebugLogs)
                        Debug.Log("[HybridMovementPad] Watchdog released a latched pad (no pointer events and no pressed pointer).", this);
                    ReleaseTracking();
                }
            }
            else if (analogInput.sqrMagnitude > 0f)
            {
                ReleaseTracking();
            }

            TickTravel(Time.unscaledDeltaTime);
        }

        private bool ShouldWatchdogRelease()
        {
            if (!useReleaseWatchdog)
                return false;

            // A finger held perfectly still produces no drag events, so staleness alone is not
            // enough. Requiring both conditions keeps the pad alive on backends where the
            // pressed-pointer query is unavailable, at the cost of a slightly later release.
            bool stale = Time.unscaledTime - lastPointerEventTime > Mathf.Max(0.05f, watchdogGraceSeconds);
            return stale && !AnyPointerPressed();
        }

        private static bool AnyPointerPressed()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.press.isPressed)
                return true;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;

            Pen pen = Pen.current;
            if (pen != null && pen.press.isPressed)
                return true;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
                return true;
            if (Input.GetMouseButton(0))
                return true;
#endif

            return false;
        }

        private void ReleaseTracking()
        {
            isTracking = false;
            activePointerId = int.MinValue;
            rawInput = Vector2.zero;
            analogInput = Vector2.zero;
            RefreshSector();
            SendCurrentInput();
        }

        private void UpdateFromPointer(PointerEventData eventData)
        {
            if (selfRect == null) return;

            lastPointerEventTime = Time.unscaledTime;

            Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 localPoint))
            {
                return;
            }

            // Measure from the rect's centre so a non-centred pivot does not bias the neutral position.
            localPoint -= selfRect.rect.center;

            float radius = DeflectionRadius;
            rawInput = Vector2.ClampMagnitude(localPoint, radius) / radius;

            if (rawInput.sqrMagnitude < deadzone * deadzone)
            {
                analogInput = Vector2.zero;
            }
            else if (digitalOnly)
            {
                analogInput = SnapToDigital(rawInput);
            }
            else
            {
                // Rescale so values just past the deadzone start at small magnitude, not mid-range.
                float scaledMag = Mathf.InverseLerp(deadzone, 1f, rawInput.magnitude);
                analogInput = rawInput.normalized * Mathf.Clamp01(scaledMag);
            }

            RefreshSector();
            SendCurrentInput();
        }

        private static Vector2 SnapToDigital(Vector2 v)
        {
            int sector = SectorIndex(v);
            float rad = sector * 45f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private static int SectorIndex(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            return Mathf.RoundToInt(angle / 45f) % 8;
        }

        private void TickTravel(float deltaTime)
        {
            if (padVisual == null) return;

            Vector2 target = travelRest + ComputeTravelOffset();

            if (travelFollowSpeed <= 0f)
            {
                travelCurrent = target;
            }
            else
            {
                // Exponential smoothing keeps the feel identical at 30 and 60 fps.
                float t = 1f - Mathf.Exp(-travelFollowSpeed * Mathf.Max(0f, deltaTime));
                travelCurrent = Vector2.LerpUnclamped(travelCurrent, target, t);
            }

            padVisual.anchoredPosition = travelCurrent;
        }

        private Vector2 ComputeTravelOffset()
        {
            if (travelRadiusPixels <= 0f)
                return Vector2.zero;

            // analogInput is already zeroed inside the deadzone, so detent travel needs no extra gate.
            if (digitalOnly && snapTravelToSector)
            {
                return analogInput.sqrMagnitude > 0f
                    ? analogInput.normalized * travelRadiusPixels
                    : Vector2.zero;
            }

            float magnitude = Mathf.Clamp01(rawInput.magnitude);
            if (magnitude < deadzone)
                return Vector2.zero;

            float ramp = Mathf.InverseLerp(deadzone, 1f, magnitude);
            return rawInput.normalized * (travelRadiusPixels * ramp);
        }

        private void RefreshSector()
        {
            DPadSector next = DPadSector.None;

            // Threshold against the raw deflection: digitalOnly forces analogInput to unit length,
            // so testing analogInput here would make the threshold meaningless.
            if (analogInput.sqrMagnitude > 0f && rawInput.magnitude >= segmentActivationMagnitude)
            {
                next = (DPadSector)SectorIndex(analogInput);
            }

            if (next == currentSector)
                return;

            currentSector = next;
            RefreshSegmentGraphics();

            try
            {
                SectorChanged?.Invoke(currentSector);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void RefreshSegmentGraphics()
        {
            if (segmentGraphics == null) return;
            for (int i = 0; i < segmentGraphics.Length; i++)
            {
                Graphic g = segmentGraphics[i];
                if (g == null) continue;
                g.color = (int)currentSector == i ? segmentActiveColor : segmentIdleColor;
            }
        }

        private void ResetVisuals()
        {
            travelCurrent = travelRest;
            if (padVisual != null) padVisual.anchoredPosition = travelRest;
            currentSector = DPadSector.None;
            RefreshSegmentGraphics();
        }

        /// <summary>Force the pad back to neutral (used on disable / context change).</summary>
        public void ResetPad()
        {
            ReleaseTracking();
            ResetVisuals();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            lastPointerEventTime = Time.unscaledTime;
            SendCurrentInput();
        }

        protected override void OnDisable()
        {
            ResetPad();
            base.OnDisable();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isTracking)
                ResetPad();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused && isTracking)
                ResetPad();
        }

        private void SendCurrentInput()
        {
            SendValueToControl(analogInput);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var rect = GetComponent<RectTransform>();
            if (padVisual != null && padVisual == rect)
            {
                Debug.LogWarning(
                    $"[HybridMovementPad] '{name}': padVisual must be a child RectTransform, not the pad itself. " +
                    "Pointing it at the pad drags the touch surface along with the finger.",
                    this);
            }
        }
#endif
    }
}
