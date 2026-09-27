using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Retro-styled virtual button for mobile touch input.
    /// Composes with Unity Input System's <c>OnScreenButton</c> (attached on the same
    /// GameObject) so taps fire real <c>InputAction</c> events for any subscriber.
    /// Supports Tap, Hold, and Toggle interaction modes (e.g. toggle sprint).
    /// </summary>
    [DisallowMultipleComponent]
    public class GameboyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum InteractionMode
        {
            /// <summary>Button reports pressed only while finger is held down.</summary>
            Hold,
            /// <summary>Tap toggles latched on/off. Useful for sprint lock.</summary>
            Toggle,
            /// <summary>Tap fires a single press+release; ignores hold duration.</summary>
            Tap,
        }

        [Header("Visual Feedback")]
        [SerializeField] private Image buttonImage;
        [Tooltip("Default sprite for the unpressed state. If null, the current Image sprite is left unchanged.")]
        [SerializeField] private Sprite normalSprite;
        [Tooltip("Sprite used while the button is pressed. Falls back to color tint when null.")]
        [SerializeField] private Sprite pressedSprite;
        [Tooltip("Sprite used while a toggle-mode button is latched. Falls back to pressedSprite/color tint when null.")]
        [SerializeField] private Sprite toggledSprite;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color toggledColor = new Color(0.6f, 0.85f, 1f, 1f);
        [Tooltip("Optional scale feedback. Disable when sprite swaps already contain the pressed pose.")]
        [SerializeField] private bool useScaleAnimation = false;
        [SerializeField] private Vector3 pressedScale = new Vector3(0.95f, 0.95f, 1f);
        [SerializeField] private float pressAnimationDuration = 0.1f;

        [Header("Interaction")]
        [SerializeField] private InteractionMode interactionMode = InteractionMode.Hold;
        [Tooltip("For Tap mode: how long the synthesized press is held before auto-release.")]
        [SerializeField] private float tapPulseSeconds = 0.08f;

        [Header("Haptics")]
        [SerializeField] private bool hapticOnPress = true;
        [SerializeField] private HapticStrength hapticStrength = HapticStrength.Light;

        [Header("Audio (Legacy Optional)")]
        [Tooltip("Deprecated for mobile HUD buttons. Prefer MobileHudButtonAudioBridge on the parent HUD root.")]
        [SerializeField] private bool playLegacyAudio = false;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pressSound;
        [SerializeField] private AudioClip releaseSound;

        private bool pointerDown;
        private bool latched;
        private Vector3 originalScale;
        private Coroutine scaleCoroutine;
        private Coroutine tapCoroutine;

        /// <summary>Fired when the button enters the pressed state.</summary>
        public System.Action OnButtonDown;
        /// <summary>Fired when the button leaves the pressed state.</summary>
        public System.Action OnButtonUp;

        /// <summary>True when the button currently reports pressed (hold, tap pulse, or toggle latched).</summary>
        public bool IsPressed => pointerDown || latched;

        /// <summary>Toggle latch state (only meaningful in <see cref="InteractionMode.Toggle"/>).</summary>
        public bool IsLatched => latched;

        public InteractionMode Mode
        {
            get => interactionMode;
            set => interactionMode = value;
        }

        private void Awake()
        {
            if (buttonImage == null)
                buttonImage = GetComponent<Image>();

            originalScale = transform.localScale;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            ApplyVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerDown) return;
            pointerDown = true;

            switch (interactionMode)
            {
                case InteractionMode.Hold:
                    FirePress();
                    break;
                case InteractionMode.Toggle:
                    latched = !latched;
                    if (latched) FirePress();
                    else FireRelease();
                    break;
                case InteractionMode.Tap:
                    FirePress();
                    if (tapCoroutine != null) StopCoroutine(tapCoroutine);
                    tapCoroutine = StartCoroutine(TapPulseCoroutine());
                    break;
            }

            ApplyVisualState();
            PlaySound(pressSound);
            PulseHaptic();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pointerDown) return;
            pointerDown = false;

            if (interactionMode == InteractionMode.Hold)
            {
                FireRelease();
                PlaySound(releaseSound);
            }

            ApplyVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (pointerDown)
            {
                OnPointerUp(eventData);
            }
        }

        private IEnumerator TapPulseCoroutine()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, tapPulseSeconds));
            FireRelease();
            tapCoroutine = null;
        }

        private void FirePress() => OnButtonDown?.Invoke();
        private void FireRelease() => OnButtonUp?.Invoke();

        private void ApplyVisualState()
        {
            Color target = normalColor;
            Sprite targetSprite = normalSprite;
            if (interactionMode == InteractionMode.Toggle && latched)
            {
                target = toggledColor;
                targetSprite = toggledSprite != null ? toggledSprite : pressedSprite;
            }
            else if (pointerDown)
            {
                target = pressedColor;
                targetSprite = pressedSprite;
            }

            if (buttonImage != null)
            {
                if (targetSprite != null)
                    buttonImage.sprite = targetSprite;
                buttonImage.color = target;
            }

            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }

            if (!useScaleAnimation)
            {
                transform.localScale = originalScale;
                return;
            }

            Vector3 targetScale = IsPressed ? pressedScale : originalScale;
            scaleCoroutine = StartCoroutine(ScaleTo(targetScale));
        }

        private IEnumerator ScaleTo(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < pressAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / pressAnimationDuration;
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleCoroutine = null;
        }

        private void PlaySound(AudioClip clip)
        {
            if (!playLegacyAudio) return;
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void PulseHaptic()
        {
            if (!hapticOnPress) return;
            HapticFeedback.Pulse(hapticStrength);
        }

        /// <summary>Programmatically latch a Toggle-mode button without firing events.</summary>
        public void SetLatched(bool value)
        {
            if (interactionMode != InteractionMode.Toggle) return;
            if (latched == value) return;
            latched = value;
            if (latched) FirePress();
            else FireRelease();
            ApplyVisualState();
        }

        /// <summary>Programmatically press the button (testing / scripted input).</summary>
        public void Press()
        {
            if (!pointerDown) OnPointerDown(null);
        }

        /// <summary>Programmatically release the button.</summary>
        public void Release()
        {
            if (pointerDown) OnPointerUp(null);
        }

        private void OnDisable()
        {
            if (pointerDown) FireRelease();
            pointerDown = false;
            if (tapCoroutine != null) { StopCoroutine(tapCoroutine); tapCoroutine = null; }
            if (scaleCoroutine != null) { StopCoroutine(scaleCoroutine); scaleCoroutine = null; }
        }
    }
}
