using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Game-state contexts that drive which mobile HUD groups are visible.
    /// </summary>
    public enum MobileHudContext
    {
        /// <summary>Default overworld play: movement + jump + sprint + interact cluster.</summary>
        Overworld,
        /// <summary>Battle HUD: movement hidden (battle is menu-driven), combat cluster visible.</summary>
        Battle,
        /// <summary>Cutscene: all gameplay controls hidden. Optional skip tap-surface shown.</summary>
        Cutscene,
        /// <summary>Dialogue: only advance/skip affordance shown.</summary>
        Dialogue,
        /// <summary>Full-screen menu (pause, inventory, roster): gameplay HUD hidden.</summary>
        Menu,
        /// <summary>Completely hidden (e.g. PSG1 physical device present).</summary>
        Hidden,
    }

    /// <summary>
    /// View component for the mobile HUD. Subscribes to
    /// <see cref="MobileHudService.OnContextChanged"/> and drives <see cref="CanvasGroup"/>
    /// visibility for each declared control group.
    ///
    /// The controller never owns state; <see cref="MobileHudService"/> is the
    /// authority (token-based, see Rule 4 in systems-first-architecture.mdc).
    /// <see cref="SetContext"/> is preserved as a compatibility shim: callers that
    /// cannot yet participate in the token model acquire a single "manual override"
    /// token on their behalf. Prefer the service API directly in new code.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileHudVisibilityController : MonoBehaviour
    {
        [Serializable]
        public class HudGroup
        {
            [Tooltip("CanvasGroup that wraps a cohesive set of controls (movement, action cluster, etc.).")]
            public CanvasGroup group;

            [Tooltip("Contexts where this group is visible + interactive.")]
            public ContextMask visibleIn = ContextMask.Overworld;

            [Tooltip("Optional non-zero alpha to dim (e.g. 0.3) instead of fully hiding.")]
            [Range(0f, 1f)]
            public float dimAlphaWhenHidden = 0f;

            [Tooltip("When false, the group can remain visible without capturing pointer hits (decorative chrome such as the gaming-pad frame).")]
            public bool blockRaycastsWhenVisible = true;
        }

        [Flags]
        public enum ContextMask
        {
            None = 0,
            Overworld = 1 << 0,
            Battle = 1 << 1,
            Cutscene = 1 << 2,
            Dialogue = 1 << 3,
            Menu = 1 << 4,
            All = Overworld | Battle | Cutscene | Dialogue | Menu,
        }

        [Header("References")]
        [Tooltip("Optional explicit service reference. When null the controller resolves MobileHudService.Instance lazily.")]
        [SerializeField] private MobileHudService hudService;

        [Tooltip("Optional CanvasGroup on MobileHudCanvas. Hidden context zeros this so leftover pad chrome cannot sit over keyboards or cinematics.")]
        [SerializeField] private CanvasGroup hudRootGroup;

        [Header("Root Motion")]
        [Tooltip("HudRoot scale used when the overlay is parked off-screen. 1.6 fills past the viewport.")]
        [SerializeField] private float hiddenScale = 1.6f;
        [Tooltip("Peak scale of the landing bounce. Keep at or above 1 so the frame interior never shows.")]
        [SerializeField] private float appearBounceScale = 1.02f;
        [SerializeField, Min(0.01f)] private float appearFallSeconds = 0.28f;
        [SerializeField, Min(0.01f)] private float appearBounceSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float appearSettleSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float hideSeconds = 0.18f;

        [Header("Groups")]
        [SerializeField] private HudGroup movementGroup;
        [SerializeField] private HudGroup jumpSprintGroup;
        [SerializeField] private HudGroup actionClusterGroup;
        [SerializeField] private HudGroup systemButtonsGroup;
        [SerializeField] private HudGroup walletGroup;
        [SerializeField] private HudGroup dialogueAdvanceGroup;
        [SerializeField] private HudGroup gamingPadFrameGroup;

        // Compatibility shim for legacy callers: one persistent token representing
        // imperative SetContext(...) overrides. Released/re-acquired on each change.
        private MobileHudToken manualOverrideToken;
        private bool hasManualOverride;
        private bool hasAppliedContext;
        private bool hasCompletedAppear;
        private bool rootShown;
        private Coroutine rootMotionRoutine;

        /// <summary>Last context applied to the groups.</summary>
        public MobileHudContext Context => hudService != null ? hudService.CurrentContext : MobileHudContext.Overworld;

        private RectTransform MotionRoot => transform as RectTransform;

        private void Awake()
        {
            EnsureServiceReference();
        }

        private void OnEnable()
        {
            EnsureServiceReference();
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (hudService != null)
            {
                hudService.OnContextChanged += HandleContextChanged;
                Apply(hudService.CurrentContext, animateRoot: false);
            }
            else
            {
                Apply(MobileHudContext.Overworld, animateRoot: false);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopRootMotion();

            if (hudService != null)
            {
                hudService.OnContextChanged -= HandleContextChanged;
            }

            if (hasManualOverride)
            {
                manualOverrideToken.Release();
                hasManualOverride = false;
            }
        }

        /// <summary>
        /// Compatibility shim for callers that were written before <see cref="MobileHudService"/>
        /// existed. Acquires a single long-lived token representing "imperative override",
        /// releasing any previous override before taking a new one. New code should call
        /// <see cref="MobileHudService.AcquireContext"/> directly so request ownership is explicit.
        /// </summary>
        public void SetContext(MobileHudContext context)
        {
            EnsureServiceReference();
            if (hudService == null) return;

            if (hasManualOverride)
            {
                manualOverrideToken.Release();
                hasManualOverride = false;
            }

            // Overworld with no override = no token needed; service returns to default.
            if (context == MobileHudContext.Overworld) return;

            manualOverrideToken = hudService.AcquireContext(context, "MobileHudVisibilityController.SetContext");
            hasManualOverride = true;
        }

        private void HandleContextChanged(MobileHudContext context) => Apply(context, animateRoot: true);

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            if (!isActiveAndEnabled)
                return;

            MobileHudContext context = hudService != null ? hudService.CurrentContext : MobileHudContext.Overworld;
            if (context == MobileHudContext.Hidden)
                return;

            // Persistent GameServices HUD must not replay the land-in bounce on additive
            // chunk loads (or any later scene IO) once the frame is already up.
            if (rootShown && (hasCompletedAppear || rootMotionRoutine != null))
                return;

            PlayRootAppear();
        }

        private void Apply(MobileHudContext context, bool animateRoot)
        {
            EnsureGroupReferences();
            bool showRoot = context != MobileHudContext.Hidden;

            if (showRoot)
            {
                ApplyGroups(ToMask(context));
                SetRootShown(true, animateRoot && hasAppliedContext);
            }
            else
            {
                SetRootShown(false, animateRoot && hasCompletedAppear);
                if (!animateRoot || !hasCompletedAppear)
                    ApplyGroups(ContextMask.None);
            }

            hasAppliedContext = true;
        }

        private void ApplyGroups(ContextMask mask)
        {
            ApplyGroup(movementGroup, mask);
            ApplyGroup(jumpSprintGroup, mask);
            ApplyGroup(actionClusterGroup, mask);
            ApplyGroup(systemButtonsGroup, mask);
            ApplyGroup(walletGroup, mask);
            ApplyGroup(dialogueAdvanceGroup, mask);
            ApplyGroup(gamingPadFrameGroup, mask);
        }

        private void SetRootShown(bool showRoot, bool animate)
        {
            EnsureGroupReferences();
            if (hudRootGroup == null)
                return;

            if (showRoot == rootShown)
            {
                if (!animate)
                    ApplyRootImmediate(showRoot);
                return;
            }

            if (showRoot)
            {
                if (animate)
                    PlayRootAppear();
                else
                    ApplyRootImmediate(true);
            }
            else if (animate)
            {
                PlayRootHide();
            }
            else
            {
                ApplyRootImmediate(false);
            }
        }

        private void PlayRootAppear()
        {
            EnsureGroupReferences();
            if (hudRootGroup == null)
                return;

            StopRootMotion();
            rootShown = true;
            hudRootGroup.alpha = 1f;
            hudRootGroup.interactable = false;
            hudRootGroup.blocksRaycasts = false;
            SetRootCanvasEnabled(true);
            rootMotionRoutine = StartCoroutine(AppearRoutine());
        }

        private void PlayRootHide()
        {
            EnsureGroupReferences();
            if (hudRootGroup == null)
                return;

            StopRootMotion();
            rootShown = false;
            hudRootGroup.alpha = 1f;
            hudRootGroup.interactable = false;
            hudRootGroup.blocksRaycasts = false;
            SetRootCanvasEnabled(true);
            rootMotionRoutine = StartCoroutine(HideRoutine());
        }

        private IEnumerator AppearRoutine()
        {
            RectTransform motionRoot = MotionRoot;
            Vector3 start = motionRoot != null ? motionRoot.localScale : Vector3.one * hiddenScale;
            if (start.x < 1.01f)
                start = Vector3.one * hiddenScale;

            Vector3 rest = Vector3.one;
            Vector3 bounce = Vector3.one * Mathf.Max(1f, appearBounceScale);
            if (motionRoot != null)
                motionRoot.localScale = start;

            yield return ScaleOverSeconds(motionRoot, start, rest, appearFallSeconds, EaseOutCubic);
            yield return ScaleOverSeconds(motionRoot, rest, bounce, appearBounceSeconds, EaseOutSine);
            yield return ScaleOverSeconds(motionRoot, bounce, rest, appearSettleSeconds, EaseInOutSine);

            if (motionRoot != null)
                motionRoot.localScale = rest;

            hudRootGroup.interactable = true;
            hudRootGroup.blocksRaycasts = true;
            hasCompletedAppear = true;
            rootMotionRoutine = null;
        }

        private IEnumerator HideRoutine()
        {
            RectTransform motionRoot = MotionRoot;
            Vector3 start = motionRoot != null ? motionRoot.localScale : Vector3.one;
            Vector3 hidden = Vector3.one * hiddenScale;
            yield return ScaleOverSeconds(motionRoot, start, hidden, hideSeconds, EaseInCubic);

            ApplyGroups(ContextMask.None);
            ApplyRootImmediate(false, stopMotion: false);
            hasCompletedAppear = false;
            rootMotionRoutine = null;
        }

        private void ApplyRootImmediate(bool showRoot, bool stopMotion = true)
        {
            EnsureGroupReferences();
            if (hudRootGroup == null)
                return;

            if (stopMotion)
                StopRootMotion();
            rootShown = showRoot;
            hudRootGroup.alpha = showRoot ? 1f : 0f;
            hudRootGroup.interactable = showRoot;
            hudRootGroup.blocksRaycasts = showRoot;
            SetRootCanvasEnabled(showRoot);

            RectTransform motionRoot = MotionRoot;
            if (motionRoot != null)
                motionRoot.localScale = Vector3.one * (showRoot ? 1f : hiddenScale);
        }

        private void SetRootCanvasEnabled(bool enabled)
        {
            if (hudRootGroup == null)
                return;

            Canvas rootCanvas = hudRootGroup.GetComponent<Canvas>();
            if (rootCanvas != null)
                rootCanvas.enabled = enabled;
        }

        private void StopRootMotion()
        {
            if (rootMotionRoutine == null)
                return;

            StopCoroutine(rootMotionRoutine);
            rootMotionRoutine = null;
        }

        private static IEnumerator ScaleOverSeconds(
            RectTransform target,
            Vector3 from,
            Vector3 to,
            float seconds,
            Func<float, float> ease)
        {
            if (target == null)
                yield break;

            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;
            target.localScale = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = ease(Mathf.Clamp01(elapsed / duration));
                target.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            target.localScale = to;
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseOutSine(float t) => Mathf.Sin(t * Mathf.PI * 0.5f);
        private static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
        private static float EaseInCubic(float t) => t * t * t;

        private void EnsureGroupReferences()
        {
            if (hudRootGroup == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                    hudRootGroup = canvas.GetComponent<CanvasGroup>();
            }

            if (gamingPadFrameGroup != null && gamingPadFrameGroup.group == null)
            {
                Transform frame = transform.Find("GamingPadFrameGroup");
                if (frame != null)
                    gamingPadFrameGroup.group = frame.GetComponent<CanvasGroup>();
            }
        }

        private static void ApplyGroup(HudGroup g, ContextMask current)
        {
            if (g == null || g.group == null) return;
            bool visible = current != ContextMask.None && (g.visibleIn & current) != 0;
            g.group.alpha = visible ? 1f : g.dimAlphaWhenHidden;
            g.group.interactable = visible;
            g.group.blocksRaycasts = visible && g.blockRaycastsWhenVisible;
        }

        private static ContextMask ToMask(MobileHudContext ctx) => ctx switch
        {
            MobileHudContext.Overworld => ContextMask.Overworld,
            MobileHudContext.Battle => ContextMask.Battle,
            MobileHudContext.Cutscene => ContextMask.Cutscene,
            MobileHudContext.Dialogue => ContextMask.Dialogue,
            MobileHudContext.Menu => ContextMask.Menu,
            MobileHudContext.Hidden => ContextMask.None,
            _ => ContextMask.None,
        };

        private void EnsureServiceReference()
        {
            if (hudService == null)
            {
                hudService = MobileHudService.Instance;
            }
        }
    }
}
