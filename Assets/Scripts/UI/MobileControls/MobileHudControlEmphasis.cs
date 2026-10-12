using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MobileControls
{
    public enum MobileHudEmphasisTarget
    {
        None = 0,
        MovePad = 1,
        InteractA = 2,
        SprintX = 3,
        MenuY = 4,
        JumpB = 5,
    }

    /// <summary>
    /// Pulses one mobile HUD control and its outline during a tutorial callout.
    /// The outline is a child, so it scales with the control.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileHudControlEmphasis : MonoBehaviour
    {
        public const int DefaultPulseCount = 4;

        [SerializeField] private RectTransform movePad;
        [SerializeField] private Image moveOutline;
        [SerializeField] private RectTransform interactButton;
        [SerializeField] private Image interactOutline;
        [SerializeField] private RectTransform sprintButton;
        [SerializeField] private Image sprintOutline;
        [SerializeField] private RectTransform menuButton;
        [SerializeField] private Image menuOutline;
        [SerializeField] private RectTransform jumpButton;
        [SerializeField] private Image jumpOutline;
        [SerializeField] private float pulseScale = 1.08f;
        [SerializeField, Min(0.01f)] private float pulseHalfSeconds = 0.18f;
        [SerializeField, Min(1)] private int pulseCount = DefaultPulseCount;

        private RectTransform activeTarget;
        private Image activeOutline;
        private Vector3 activeStartScale = Vector3.one;
        private Coroutine routine;
        private bool playing;

        public static MobileHudControlEmphasis Instance { get; private set; }

        public bool IsPlaying => playing;

        private void Awake()
        {
            Instance = this;
            HideOutlines();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Cancel();
        }

        public void Begin(MobileHudEmphasisTarget target)
        {
            Cancel();
            if (!isActiveAndEnabled)
                return;

            playing = true;
            routine = StartCoroutine(Play(target));
        }

        public IEnumerator Play(MobileHudEmphasisTarget target)
        {
            if (!TryResolve(target, out RectTransform control, out Image outline) || control == null)
            {
                routine = null;
                Finish();
                yield break;
            }

            playing = true;
            activeTarget = control;
            activeOutline = outline;
            activeStartScale = control.localScale;
            if (outline != null)
                outline.gameObject.SetActive(true);

            int pulses = Mathf.Max(1, pulseCount);
            Vector3 expanded = activeStartScale * pulseScale;
            for (int i = 0; i < pulses && playing; i++)
            {
                yield return Scale(control, activeStartScale, expanded, pulseHalfSeconds);
                if (!playing)
                    yield break;
                yield return Scale(control, expanded, activeStartScale, pulseHalfSeconds);
            }

            routine = null;
            Finish();
        }

        public void Cancel()
        {
            playing = false;
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            Finish();
        }

        private void Finish()
        {
            playing = false;
            if (activeTarget != null)
                activeTarget.localScale = activeStartScale;
            if (activeOutline != null)
                activeOutline.gameObject.SetActive(false);
            activeTarget = null;
            activeOutline = null;
            activeStartScale = Vector3.one;
        }

        private void HideOutlines()
        {
            SetOutlineActive(moveOutline, false);
            SetOutlineActive(interactOutline, false);
            SetOutlineActive(sprintOutline, false);
            SetOutlineActive(menuOutline, false);
            SetOutlineActive(jumpOutline, false);
        }

        private static void SetOutlineActive(Image outline, bool active)
        {
            if (outline != null)
                outline.gameObject.SetActive(active);
        }

        private bool TryResolve(MobileHudEmphasisTarget target, out RectTransform control, out Image outline)
        {
            switch (target)
            {
                case MobileHudEmphasisTarget.MovePad:
                    control = movePad;
                    outline = moveOutline;
                    return true;
                case MobileHudEmphasisTarget.InteractA:
                    control = interactButton;
                    outline = interactOutline;
                    return true;
                case MobileHudEmphasisTarget.SprintX:
                    control = sprintButton;
                    outline = sprintOutline;
                    return true;
                case MobileHudEmphasisTarget.MenuY:
                    control = menuButton;
                    outline = menuOutline;
                    return true;
                case MobileHudEmphasisTarget.JumpB:
                    control = jumpButton;
                    outline = jumpOutline;
                    return true;
                default:
                    control = null;
                    outline = null;
                    return false;
            }
        }

        private static IEnumerator Scale(RectTransform target, Vector3 from, Vector3 to, float seconds)
        {
            if (target == null)
                yield break;

            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            target.localScale = to;
        }
    }
}
