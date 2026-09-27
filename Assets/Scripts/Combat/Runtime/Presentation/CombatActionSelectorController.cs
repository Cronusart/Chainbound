using System;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Controls the 3D action selector object in the combat scene.
    /// Attach this to the root of your 3D selector model.
    /// Child transforms serve as button anchor positions (Fight, Swap, Item, Flee, Record).
    ///
    /// The selector does NOT create any mesh â€” it expects the 3D model to already exist.
    /// It handles:
    ///   - Option cycling via Navigate input (left/right from the orchestrator)
    ///   - Tilt/rotation toward the currently highlighted button anchor
    ///   - Subtle idle bob animation
    ///   - Scale pulse on the highlighted button child
    ///   - Show/Hide with smooth transitions
    ///
    /// Input is routed by the combat action orchestrator, not read directly.
    /// </summary>
    public class CombatActionSelectorController : MonoBehaviour
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Inspector
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("Pivot / Root")]
        [Tooltip("The logical center of the selector model. Tilt rotations orbit around this point. " +
                 "If null, falls back to this GameObject's transform (which may be off-center). " +
                 "Tip: use the middle button anchor (Item) if it sits at the visual center.")]
        [SerializeField] private Transform tiltPivot;

        [Header("Button Anchors (child transforms on the 3D model)")]
        [Tooltip("Order: Fight=0, Swap=1, Item=2, Flee=3, Record=4")]
        [SerializeField] private Transform[] buttonAnchors = new Transform[5];

        [Header("Tilt Animation")]
        [Tooltip("Maximum tilt angle (degrees) toward the selected button.")]
        [SerializeField] private float tiltAmount = 8f;
        [Tooltip("Tilt interpolation speed.")]
        [SerializeField] private float tiltSpeed = 6f;

        [Header("Bob Animation")]
        [Tooltip("Vertical bob amplitude (world units).")]
        [SerializeField] private float bobAmount = 0.04f;
        [Tooltip("Bob cycles per second.")]
        [SerializeField] private float bobSpeed = 1.2f;

        [Header("Highlight")]
        [Tooltip("Scale multiplier on the highlighted button anchor.")]
        [SerializeField] private float highlightScaleMultiplier = 1.15f;
        [Tooltip("Scale pulse interpolation speed.")]
        [SerializeField] private float highlightScaleSpeed = 8f;

        [Header("Show / Hide (Put-Down Animation)")]
        [Tooltip("Seconds for the show/hide transition.")]
        [SerializeField] private float showHideDuration = 0.25f;
        [Tooltip("X-axis rotation (degrees) applied when hiding. Positive tilts the top away from the camera. " +
                 "Use ~80 for a 'lay the device flat on the table' look.")]
        [SerializeField] private float hideTiltX = 80f;
        [Tooltip("Local-space Y offset when hidden (negative = downward). " +
                 "Combined with hideTiltX to create a 'put down' motion.")]
        [SerializeField] private float hideYOffset = -0.3f;
        [Tooltip("If true, also scale down slightly when hidden (legacy behavior). " +
                 "If false, only tilt and translate â€” no scale change.")]
        [SerializeField] private bool hideScaleDown = false;
        [Tooltip("Minimum scale factor when hidden (only used if hideScaleDown is true).")]
        [Range(0f, 1f)]
        [SerializeField] private float hideMinScale = 0.6f;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip navigateSound;
        [SerializeField] private AudioClip confirmSound;
        [SerializeField] private AudioClip cancelSound;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Events
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Fires when the highlighted option changes (index 0-4).</summary>
        public event Action<int> OnOptionHighlighted;

        /// <summary>Fires when the player confirms the highlighted option (index 0-4).</summary>
        public event Action<int> OnOptionConfirmed;

        /// <summary>Fires when an action-pad option becomes available or unavailable.</summary>
        public event Action<SelectorOption, bool> OnOptionAvailabilityChanged;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Public enums
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Named indices matching the buttonAnchors array order.</summary>
        public enum SelectorOption
        {
            Fight   = 0,
            Swap    = 1,
            Item    = 2,
            Flee    = 3,
            Record  = 4,
        }

        public const int OPTION_COUNT = 5;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Runtime State
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private int _currentIndex;
        private bool[] _enabledOptions = new bool[OPTION_COUNT];
        private bool _isVisible;
        private float _showProgress; // 0 = hidden, 1 = fully shown
        private Vector3 _baseLocalPosition;
        private Vector3 _visibilityLocalPosition;
        private CombatHardwareWalletOffsetController _walletOffsetController;
        private Quaternion _baseLocalRotation;
        private Vector3 _baseLocalScale;
        private Vector3[] _anchorDefaultScales;
        private float _bobPhase;

        // Navigate repeat gating
        private float _navigateCooldown;
        private const float NAVIGATE_REPEAT_DELAY = 0.2f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Public Properties
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public int CurrentIndex => _currentIndex;
        public bool IsVisible => _isVisible;

        public bool IsOptionAvailable(SelectorOption option)
        {
            int index = (int)option;
            return index >= 0 && index < OPTION_COUNT && _enabledOptions[index];
        }

        /// <summary>
        /// Keeps the wallet clear of the floor during the move/swap-panel tilt.
        /// The wallet controller applies this request only while settled 1v1 framing is active.
        /// </summary>
        public void SetSingleCombatTiltLiftActive(bool active)
        {
            if (_walletOffsetController == null)
                _walletOffsetController = GetComponent<CombatHardwareWalletOffsetController>();

            _walletOffsetController?.SetSelectorTiltLiftActive(active);
        }

        public void SetVerboseLogging(bool enabled)
        {
            verboseLogs = enabled;
            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorController] Verbose logging enabled on '{name}'.");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Unity Lifecycle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _visibilityLocalPosition = _baseLocalPosition;
            _walletOffsetController = GetComponent<CombatHardwareWalletOffsetController>();
            _baseLocalRotation = transform.localRotation;
            _baseLocalScale = transform.localScale;

            // If no explicit pivot is assigned, use this transform
            if (tiltPivot == null)
                tiltPivot = transform;

            // Cache default scales of button anchors
            _anchorDefaultScales = new Vector3[OPTION_COUNT];
            for (int i = 0; i < OPTION_COUNT; i++)
            {
                if (buttonAnchors != null && i < buttonAnchors.Length && buttonAnchors[i] != null)
                    _anchorDefaultScales[i] = buttonAnchors[i].localScale;
                else
                    _anchorDefaultScales[i] = Vector3.one;
            }

            // Default all options enabled
            for (int i = 0; i < OPTION_COUNT; i++)
                _enabledOptions[i] = true;

            // Start hidden
            _showProgress = 0f;
            _isVisible = false;
            UpdateVisibility();

            if (verboseLogs)
            {
                Debug.Log($"[CombatActionSelectorController] Awake on '{name}'. Anchors={buttonAnchors?.Length ?? 0}, pivot='{tiltPivot?.name ?? "<self>"}'.");
            }
        }

        private void Update()
        {
            if (_navigateCooldown > 0f)
                _navigateCooldown -= Time.deltaTime;

            // Show/hide interpolation
            float target = _isVisible ? 1f : 0f;
            if (!Mathf.Approximately(_showProgress, target))
            {
                float speed = showHideDuration > 0f ? 1f / showHideDuration : 100f;
                _showProgress = Mathf.MoveTowards(_showProgress, target, speed * Time.deltaTime);
                UpdateVisibility();
            }

            if (_showProgress <= 0f)
                return;

            // Tilt toward selected anchor
            UpdateTilt();

            // Bob animation
            UpdateBob();

            // Highlight scale pulse
            UpdateHighlightScales();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Public API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Show the selector and reset to Fight (index 0).</summary>
        public void Show()
        {
            _isVisible = true;
            _currentIndex = 0;

            // If the default option is disabled, find the first enabled one
            if (!_enabledOptions[_currentIndex])
                MoveToNextEnabled(1);

            OnOptionHighlighted?.Invoke(_currentIndex);

            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorController] Show -> option {(SelectorOption)_currentIndex} ({_currentIndex}).");
        }

        /// <summary>Hide the selector with a smooth transition.</summary>
        public void Hide()
        {
            _isVisible = false;
            if (verboseLogs)
                Debug.Log("[CombatActionSelectorController] Hide.");
        }

        /// <summary>
        /// Set which options are available (e.g., disable Swap if no bench).
        /// Array must be length 5 matching SelectorOption order.
        /// </summary>
        public void SetAvailableOptions(bool[] enabled)
        {
            if (enabled == null || enabled.Length < OPTION_COUNT)
                return;

            Array.Copy(enabled, _enabledOptions, OPTION_COUNT);

            for (int i = 0; i < OPTION_COUNT; i++)
                OnOptionAvailabilityChanged?.Invoke((SelectorOption)i, _enabledOptions[i]);

            // If current option is now disabled, move to next enabled
            if (!_enabledOptions[_currentIndex])
                MoveToNextEnabled(1);

            if (verboseLogs)
            {
                Debug.Log($"[CombatActionSelectorController] SetAvailableOptions -> Fight={_enabledOptions[(int)SelectorOption.Fight]}, Swap={_enabledOptions[(int)SelectorOption.Swap]}, Item={_enabledOptions[(int)SelectorOption.Item]}, Flee={_enabledOptions[(int)SelectorOption.Flee]}, Record={_enabledOptions[(int)SelectorOption.Record]}.");
            }
        }

        /// <summary>
        /// Called by the UI orchestrator when Navigate input is received.
        /// Supports both horizontal and vertical directional navigation.
        /// </summary>
        public void HandleNavigate(Vector2 direction)
        {
            if (!_isVisible || _navigateCooldown > 0f)
                return;

            const float threshold = 0.4f;
            int startIndex = _currentIndex;
            int targetIndex = -1;
            int fallbackStep = 1;

            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                if (direction.x > threshold)
                {
                    // Right: Flee -> Item -> Swap
                    targetIndex = _currentIndex == (int)SelectorOption.Flee
                        ? (int)SelectorOption.Item
                        : (int)SelectorOption.Swap;
                    fallbackStep = 1;
                }
                else if (direction.x < -threshold)
                {
                    // Left: Swap -> Item -> Flee
                    targetIndex = _currentIndex == (int)SelectorOption.Swap
                        ? (int)SelectorOption.Item
                        : (int)SelectorOption.Flee;
                    fallbackStep = -1;
                }
            }
            else
            {
                if (direction.y > threshold)
                {
                    // Up: Record -> Item -> Fight
                    targetIndex = _currentIndex == (int)SelectorOption.Record
                        ? (int)SelectorOption.Item
                        : (int)SelectorOption.Fight;
                    fallbackStep = -1;
                }
                else if (direction.y < -threshold)
                {
                    // Down: Fight -> Item -> Record
                    targetIndex = _currentIndex == (int)SelectorOption.Fight
                        ? (int)SelectorOption.Item
                        : (int)SelectorOption.Record;
                    fallbackStep = 1;
                }
            }

            if (targetIndex < 0)
                return;

            if (!TrySetCurrentIndex(targetIndex))
                MoveToNextEnabled(fallbackStep);

            if (_currentIndex != startIndex)
            {
                _navigateCooldown = NAVIGATE_REPEAT_DELAY;
                PlaySound(navigateSound);
                if (verboseLogs)
                    Debug.Log($"[CombatActionSelectorController] Navigate {direction} -> {(SelectorOption)_currentIndex} ({_currentIndex}).");
            }
        }

        /// <summary>
        /// Called by the UI orchestrator when Confirm is pressed.
        /// </summary>
        public void HandleConfirm()
        {
            if (!_isVisible)
            {
                if (verboseLogs)
                    Debug.Log("[CombatActionSelectorController] Confirm ignored (selector hidden).");
                return;
            }

            if (!_enabledOptions[_currentIndex])
            {
                if (verboseLogs)
                    Debug.Log($"[CombatActionSelectorController] Confirm ignored (option {(SelectorOption)_currentIndex} disabled).");
                return;
            }

            PlaySound(confirmSound);
            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorController] Confirm -> {(SelectorOption)_currentIndex} ({_currentIndex}).");
            OnOptionConfirmed?.Invoke(_currentIndex);
        }

        /// <summary>
        /// Called by the UI orchestrator when Cancel is pressed.
        /// The selector itself doesn't handle cancel â€” it's the orchestrator's job.
        /// This is provided for symmetry and audio feedback.
        /// </summary>
        public void HandleCancel()
        {
            PlaySound(cancelSound);
            if (verboseLogs)
                Debug.Log("[CombatActionSelectorController] Cancel.");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Navigation Helpers
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void MoveToNextEnabled(int step)
        {
            int start = _currentIndex;
            for (int i = 0; i < OPTION_COUNT; i++)
            {
                int next = ((_currentIndex + step) % OPTION_COUNT + OPTION_COUNT) % OPTION_COUNT;
                _currentIndex = next;
                if (_enabledOptions[next])
                {
                    if (next != start)
                        OnOptionHighlighted?.Invoke(_currentIndex);
                    return;
                }
            }

            // Fallback: all disabled (shouldn't happen, but be safe)
            _currentIndex = start;
        }

        private bool TrySetCurrentIndex(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= OPTION_COUNT)
                return false;
            if (!_enabledOptions[targetIndex])
                return false;

            if (_currentIndex != targetIndex)
            {
                _currentIndex = targetIndex;
                OnOptionHighlighted?.Invoke(_currentIndex);
            }

            return true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Animation
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void UpdateTilt()
        {
            if (buttonAnchors == null || _currentIndex >= buttonAnchors.Length)
                return;

            Transform anchor = buttonAnchors[_currentIndex];
            if (anchor == null)
            {
                // No anchor â€” ease back to base rotation
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation, _baseLocalRotation, tiltSpeed * Time.deltaTime);
                return;
            }

            // Compute direction from the configured tilt pivot to the anchor in the
            // selector's local space. This ensures rotations orbit around the visual
            // center of the model rather than its transform origin (which may be offset).
            Vector3 pivotWorld = tiltPivot != null ? tiltPivot.position : transform.position;
            Vector3 anchorWorld = anchor.position;
            Vector3 worldDir = (anchorWorld - pivotWorld).normalized;

            // Project into the selector's local axes so the tilt is relative to
            // the model's orientation, not world axes.
            Vector3 localDir = transform.InverseTransformDirection(worldDir);

            // Pitch (X-axis rotation): driven by the anchor's local forward/back offset (Z).
            // Positive Z = anchor is in front = tilt top backward (negative X rotation).
            // Yaw (Y-axis rotation): driven by the anchor's local left/right offset (X).
            // Also apply a small X-tilt from the Y component (up/down anchors) so that
            // anchors above or below the pivot produce noticeable pitch, not just yaw.
            float tiltX = (-localDir.z + localDir.y) * tiltAmount;
            float tiltY = localDir.x * tiltAmount;

            Quaternion targetRot = _baseLocalRotation * Quaternion.Euler(tiltX, tiltY, 0f);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, targetRot, tiltSpeed * Time.deltaTime);
        }

        private void UpdateBob()
        {
            _bobPhase += Time.deltaTime * bobSpeed * Mathf.PI * 2f;
            if (_bobPhase > Mathf.PI * 2f)
                _bobPhase -= Mathf.PI * 2f;

            // Add bob on top of the current position (which already includes the hide Y offset
            // from UpdateVisibility). Scale bob amplitude by _showProgress so it fades out
            // as the selector hides.
            float offset = Mathf.Sin(_bobPhase) * bobAmount * _showProgress;
            SetAnimationLocalPosition(_visibilityLocalPosition + Vector3.up * offset);
        }

        private void UpdateHighlightScales()
        {
            if (buttonAnchors == null)
                return;

            for (int i = 0; i < OPTION_COUNT && i < buttonAnchors.Length; i++)
            {
                if (buttonAnchors[i] == null)
                    continue;

                Vector3 defaultScale = _anchorDefaultScales[i];
                Vector3 targetScale = (i == _currentIndex)
                    ? defaultScale * highlightScaleMultiplier
                    : defaultScale;

                buttonAnchors[i].localScale = Vector3.Lerp(
                    buttonAnchors[i].localScale, targetScale, highlightScaleSpeed * Time.deltaTime);
            }
        }

        private void UpdateVisibility()
        {
            // t = 0 â†’ fully hidden, t = 1 â†’ fully shown
            float t = Mathf.SmoothStep(0f, 1f, _showProgress);
            float hiddenT = 1f - t; // 0 = shown, 1 = hidden

            // â”€â”€ Position: slide downward when hiding â”€â”€
            Vector3 pos = _baseLocalPosition + Vector3.up * (hideYOffset * hiddenT);
            // Bob is applied separately in UpdateBob, but when hiding we want the
            // Y offset to dominate, so bob is already scaled by _showProgress.
            _visibilityLocalPosition = pos;
            SetAnimationLocalPosition(pos);

            // â”€â”€ Rotation: tilt forward (X-axis) when hiding â”€â”€
            Quaternion hideRot = _baseLocalRotation * Quaternion.Euler(hideTiltX * hiddenT, 0f, 0f);
            // Only apply the hide rotation when the tilt animation isn't driving rotation
            // (i.e., when transitioning). During fully-shown state, UpdateTilt takes over.
            if (hiddenT > 0.01f)
                transform.localRotation = Quaternion.Slerp(transform.localRotation, hideRot, 8f * Time.deltaTime);

            // â”€â”€ Scale (optional) â”€â”€
            if (hideScaleDown)
            {
                float s = Mathf.Lerp(hideMinScale, 1f, t);
                transform.localScale = _baseLocalScale * s;
            }
            else
            {
                transform.localScale = _baseLocalScale;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Audio
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void SetAnimationLocalPosition(Vector3 position)
        {
            if (_walletOffsetController == null)
                _walletOffsetController = GetComponent<CombatHardwareWalletOffsetController>();

            if (_walletOffsetController != null)
                _walletOffsetController.SetAnimationAuthoredLocalPosition(position);
            else
                transform.localPosition = position;
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
}
