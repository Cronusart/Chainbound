using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Visual controller for a single Chainling slot in the battle scene.
    /// Drives CombatChainlingVisual animations, melee lunge, recoil, and targeting.
    ///
    /// The presenter calls high-level methods (PlayMeleeAttack, PlayRangedAttack,
    /// PlayHitReaction) and yields on the returned durations â€” no fixed delays.
    /// </summary>
    public class Combat2ChainlingView : MonoBehaviour, ICombatSlotView
    {
        [Header("Visual Roots")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform cameraFocus;
        [SerializeField] private Transform popupAnchor;
        [SerializeField] private Transform meleeStopAnchor;
        [SerializeField] private Transform impactAnchor;
        [SerializeField] private Transform projectileOriginAnchor;
        [SerializeField] private Transform barrierAnchor;
        [SerializeField] private Transform barrierStopAnchor;
        [SerializeField] private Transform targeter;
        [SerializeField] private CombatSlotGroundCircle groundCircle;

        [Header("Combat Visual")]
        [Tooltip("The CombatChainlingVisual driving sprite animation. Auto-found on visualRoot if null.")]
        [SerializeField] private CombatChainlingVisual combatVisual;
        [Tooltip("Glitch overlay for faint/spawn. Auto-found on visualRoot if null.")]
        [SerializeField] private SignalGlitchEffect glitchEffect;
        [Tooltip("Applies Chainling identity overlays and transient identity effects. Auto-created if null.")]
        [SerializeField] private ChainlingCosmeticOverlayController cosmeticOverlayController;
        [Tooltip("Brief hold after a transition-out body clip when no glitch overlay is available.")]
        [SerializeField] private float transitionOutFreezeHold = 0.05f;

        [Header("Melee Lunge")]
        [SerializeField] private float meleeStandoffDistance = 0.18f;
        [SerializeField] private float minLungeDistance = 0.2f;
        [SerializeField] private float maxLungeDistance = 1.4f;
        [SerializeField] private float lungeDuration = 0.12f;
        [SerializeField] private float returnDuration = 0.12f;

        [Header("Recoil (Jump-Back + Bounce)")]
        [SerializeField] private float recoilDistance = 0.18f;
        [SerializeField] private float recoilHeight = 0.08f;
        [SerializeField] private float recoilDuration = 0.14f;
        [SerializeField] private float recoilBounceDistance = 0.04f;
        [SerializeField] private float recoilBounceHeight = 0.02f;
        [SerializeField] private float recoilBounceDuration = 0.10f;
        [SerializeField] private float recoilSettleDuration = 0.06f;

        [Header("Visibility")]
        [SerializeField] private bool hideWhenUnassigned = false;

        [Header("Spawn / Swap Emphasis")]
        [SerializeField] private bool spawnEmphasisEnabled = true;
        [SerializeField] private float spawnEmphasisScale = 1.07f;
        [SerializeField] private float spawnEmphasisUpDuration = 0.10f;
        [SerializeField] private float spawnEmphasisHoldDuration = 0.02f;
        [SerializeField] private float spawnEmphasisDownDuration = 0.16f;
        [SerializeField] private Ease spawnEmphasisUpEase = Ease.OutBack;
        [SerializeField] private Ease spawnEmphasisDownEase = Ease.InOutSine;
        [SerializeField] private bool spawnEmphasisUseUnscaledTime;

        private string _instanceId;
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale;
        private bool _hasBasePosition;
        private Coroutine _lungeRoutine;
        private bool _meleePlanted;
        private Vector3 _meleePeakLocal;
        private System.Action _meleeStrikeIdleHandler;
        private Coroutine _recoilRoutine;
        private Coroutine _faintRoutine;
        private bool _hiddenForCaptureAttempt;
        private Coroutine _targeterRoutine;
        private Coroutine _outlineFlashRoutine;
        private Tween _spawnEmphasisTween;
        private CombatWallVisual _wallVisual;
        private string _wallVisualKey;
        private bool _highlighted;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Public Accessors
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public string InstanceId => _instanceId;
        public Transform CameraFocus => cameraFocus != null ? cameraFocus : (visualRoot != null ? visualRoot : transform);
        /// <summary>Explicit anchor for floating combat popups. Never falls back to <see cref="CameraFocus"/>.</summary>
        public Transform PopupAnchor => popupAnchor;
        public Transform MeleeStopAnchor => meleeStopAnchor != null ? meleeStopAnchor : CameraFocus;
        public Transform ImpactAnchor => impactAnchor != null ? impactAnchor : CameraFocus;
        public Transform ProjectileOriginAnchor => projectileOriginAnchor != null ? projectileOriginAnchor : CameraFocus;
        public Transform BarrierAnchor => barrierAnchor != null ? barrierAnchor : ImpactAnchor;
        /// <summary>Melee lunge stop in front of the wall surface; falls back to <see cref="BarrierAnchor"/>.</summary>
        public Transform BarrierStopAnchor => barrierStopAnchor != null ? barrierStopAnchor : BarrierAnchor;
        public Transform VisualRootTransform => visualRoot != null ? visualRoot : transform;
        public Transform Targeter => targeter;
        public CombatSlotGroundCircle GroundCircle => groundCircle;
        public CombatChainlingVisual Visual => combatVisual;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Unity Lifecycle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform.Find("VisualRoot");

            if (visualRoot == null)
                visualRoot = transform;

            if (visualRoot != null)
            {
                cameraFocus = ResolveAnchor(visualRoot, cameraFocus, "CameraFocus", new Vector3(0f, 1f, 0f));
                popupAnchor = ResolveOptionalAnchor(visualRoot, popupAnchor, "PopUpAnchor", "PopupAnchor");
                meleeStopAnchor = ResolveAnchor(visualRoot, meleeStopAnchor, "MeleeStopAnchor", new Vector3(0f, 0.9f, 0.65f));
                impactAnchor = ResolveAnchor(visualRoot, impactAnchor, "ImpactAnchor", new Vector3(0f, 1f, 0f));
                projectileOriginAnchor = ResolveAnchor(visualRoot, projectileOriginAnchor, "ProjectileOriginAnchor", new Vector3(0f, 1f, 0.25f));
            }

            barrierAnchor = ResolveOptionalAnchor(transform, barrierAnchor, "BarrierAnchor", "Barrier");
            if (barrierAnchor == null && visualRoot != null)
                barrierAnchor = ResolveOptionalAnchor(visualRoot, barrierAnchor, "BarrierAnchor", "Barrier");
            if (barrierAnchor == null)
                barrierAnchor = ResolveAnchor(transform, null, "BarrierAnchor", new Vector3(0f, 1f, 0.55f));

            if (popupAnchor == null)
                popupAnchor = ResolveOptionalAnchor(transform, popupAnchor, "PopUpAnchor", "PopupAnchor");

            if (targeter == null)
                targeter = transform.Find("Targeter");

            EnsureStableBarrierAnchor();
            EnsureBarrierStopAnchor();

            if (groundCircle == null)
                groundCircle = GetComponentInChildren<CombatSlotGroundCircle>(true);
            if (groundCircle == null)
                groundCircle = CreateRuntimeGroundCircle();

            if (combatVisual == null && visualRoot != null)
                combatVisual = visualRoot.GetComponentInChildren<CombatChainlingVisual>();

            if (glitchEffect == null && visualRoot != null)
                glitchEffect = visualRoot.GetComponentInChildren<SignalGlitchEffect>();

            if (cosmeticOverlayController == null && visualRoot != null)
                cosmeticOverlayController = visualRoot.GetComponentInChildren<ChainlingCosmeticOverlayController>(true);
            if (cosmeticOverlayController == null)
                cosmeticOverlayController = GetComponent<ChainlingCosmeticOverlayController>();
            if (cosmeticOverlayController == null)
                cosmeticOverlayController = gameObject.AddComponent<ChainlingCosmeticOverlayController>();

            CacheBasePosition();

            if (targeter != null)
                targeter.gameObject.SetActive(false);
            if (groundCircle != null)
                groundCircle.SetState(CombatSlotGroundCircle.CircleState.Off);
        }

        private void OnDisable()
        {
            KillSpawnEmphasis(resetScale: true);
            SetHighlighted(false);
            ForceHideWall();
        }

        private CombatSlotGroundCircle CreateRuntimeGroundCircle()
        {
            var root = new GameObject("GroundCircle");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var ringPivot = new GameObject("RingPivot");
            ringPivot.transform.SetParent(root.transform, false);
            ringPivot.transform.localPosition = Vector3.zero;
            ringPivot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ringPivot.transform.localScale = Vector3.one;

            var sr = ringPivot.AddComponent<SpriteRenderer>();
            var circle = root.AddComponent<CombatSlotGroundCircle>();
            return circle;
        }

        private static Transform ResolveAnchor(Transform root, Transform existing, string anchorName, Vector3 localPosition)
        {
            if (existing != null)
                return existing;

            Transform found = root.Find(anchorName);
            if (found != null)
                return found;

            var anchorGo = new GameObject(anchorName);
            anchorGo.transform.SetParent(root, false);
            anchorGo.transform.localPosition = localPosition;
            anchorGo.transform.localRotation = Quaternion.identity;
            anchorGo.transform.localScale = Vector3.one;
            return anchorGo.transform;
        }

        private static Transform ResolveOptionalAnchor(Transform root, Transform existing, params string[] anchorNames)
        {
            if (existing != null || root == null || anchorNames == null)
                return existing;

            for (int i = 0; i < anchorNames.Length; i++)
            {
                if (string.IsNullOrEmpty(anchorNames[i]))
                    continue;

                Transform found = root.Find(anchorNames[i]);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Barrier anchors must stay world-stable while a wall is active. Keep them on the slot root,
        /// not under VisualRoot (melee lunge/recoil/faint) or Targeter (selection toggles).
        /// </summary>
        private void EnsureStableBarrierAnchor()
        {
            if (barrierAnchor == null || barrierAnchor == transform)
                return;

            if (barrierAnchor.parent == transform)
                return;

            bool underTargeter = targeter != null && barrierAnchor.IsChildOf(targeter);
            bool underVisualRoot = visualRoot != null && barrierAnchor.IsChildOf(visualRoot);
            if (underTargeter || underVisualRoot)
                barrierAnchor.SetParent(transform, true);
        }

        /// <summary>
        /// Melee stop point in front of the barrier mesh, analogous to <see cref="MeleeStopAnchor"/> for body hits.
        /// </summary>
        private void EnsureBarrierStopAnchor()
        {
            if (barrierAnchor == null)
                return;

            barrierStopAnchor = ResolveOptionalAnchor(barrierAnchor, barrierStopAnchor, "BarrierStop", "BarrierStopAnchor");
            if (barrierStopAnchor != null)
                return;

            float xSign = Mathf.Sign(barrierAnchor.localPosition.x);
            if (Mathf.Approximately(xSign, 0f))
                xSign = 1f;

            barrierStopAnchor = ResolveAnchor(
                barrierAnchor,
                null,
                "BarrierStop",
                new Vector3(1.5f * xSign, 0f, -0.1f));
        }

        /// <summary>
        /// Resolves world-space spawn position for combat popups.
        /// Prefers <see cref="PopupAnchor"/> and never uses <see cref="CameraFocus"/> (reserved for camera tracking).
        /// </summary>
        public Vector3 ResolvePopupWorldPosition(bool preferBarrierAnchor = false)
        {
            if (preferBarrierAnchor)
            {
                if (barrierAnchor != null)
                    return barrierAnchor.position;
                if (impactAnchor != null)
                    return impactAnchor.position;
            }

            if (popupAnchor != null)
                return popupAnchor.position;

            if (impactAnchor != null)
                return impactAnchor.position;

            return transform.position;
        }

        /// <summary>
        /// Returns battlefield-relative facing for popup offset mirroring.
        /// </summary>
        public int ResolvePopupFacingSign()
        {
            if (combatVisual != null && combatVisual.FlipX)
                return -1;
            return 1;
        }

        private void CacheBasePosition()
        {
            if (visualRoot == null)
                return;

            _baseLocalPosition = visualRoot.localPosition;
            _baseLocalScale = visualRoot.localScale;
            _hasBasePosition = true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Slot Binding
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Bind a <see cref="ChainlingInstance"/> to this slot.
        /// When a <see cref="CombatVisualDatabaseSO"/> is provided the visual
        /// profile is resolved automatically from <c>chainling.speciesId</c>,
        /// so the correct sprites appear without manual assignment.
        ///
        /// Skips re-application if the same instance is already bound, preventing
        /// the polling refresh from stomping Inspector or runtime overrides.
        /// </summary>
        public void AssignChainling(ChainlingInstance chainling, CombatVisualDatabaseSO visualDB = null, ChainlingVisualIdentityDatabaseSO identityDB = null)
        {
            string newId = chainling != null ? chainling.instanceId : string.Empty;

            // Early-out if this exact instance is already bound
            if (newId == _instanceId)
                return;

            _instanceId = newId;
            SetAssigned(chainling != null);
            ForceHideWall();
            if (chainling == null)
            {
                combatVisual?.ClearBodyOverrides();
                cosmeticOverlayController?.ClearAll();
            }

            if (groundCircle != null)
                groundCircle.SetState(CombatSlotGroundCircle.CircleState.Off);

            if (chainling != null)
            {
                ChainlingType entangledType = chainling.isEntangled ? chainling.entangledType : ChainlingType.None;
                var resolved = ChainlingVisualIdentityResolver.Resolve(
                    chainling.speciesId,
                    chainling.visualIdentity,
                    visualDB,
                    identityDB,
                    entangledType);
                if (resolved.baseProfile != null && combatVisual != null)
                    combatVisual.ApplyProfile(resolved.baseProfile);

                cosmeticOverlayController?.Apply(resolved, combatVisual, VisualRootTransform, combatVisual != null && combatVisual.FlipX);
            }

            SetIdle();
            SetHighlighted(false);
        }

        public void SetAssigned(bool assigned)
        {
            if (!hideWhenUnassigned)
                return;

            gameObject.SetActive(assigned);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Targeting Indicator
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void SetTargeted(bool targeted)
        {
            if (targeter == null)
                return;

            targeter.gameObject.SetActive(targeted);
        }

        public void ConfigureOutlineTuning(CombatActionOutlineTuningSO tuning)
        {
            combatVisual?.ConfigureOutlineTuning(tuning);
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            if (combatVisual == null)
                return;

            if (highlighted)
                combatVisual.ApplySelectionOutlineDefaults();

            combatVisual.SetOutlineEnabled(highlighted);
        }

        /// <summary>
        /// Flash the depth-write outline with a temporary color, then restore selection state.
        /// Returns total duration for presenter pacing.
        /// </summary>
        public float PlayOutlineFlash(Color color, float widthPixels, float softness, float fadeIn, float fadeOut, float holdSeconds)
        {
            if (combatVisual == null)
                return 0f;

            if (_outlineFlashRoutine != null)
                StopCoroutine(_outlineFlashRoutine);

            bool wasHighlighted = _highlighted;
            _outlineFlashRoutine = StartCoroutine(OutlineFlashRoutine(color, widthPixels, softness, fadeIn, fadeOut, holdSeconds, wasHighlighted));
            return Mathf.Max(0f, fadeIn) + Mathf.Max(0f, holdSeconds) + Mathf.Max(0f, fadeOut);
        }

        private IEnumerator OutlineFlashRoutine(Color color, float widthPixels, float softness, float fadeIn, float fadeOut, float holdSeconds, bool restoreHighlighted)
        {
            combatVisual.BeginTemporaryOutlineFlash(color, widthPixels, softness);

            if (fadeIn > 0f)
                yield return new WaitForSeconds(fadeIn);
            if (holdSeconds > 0f)
                yield return new WaitForSeconds(holdSeconds);

            combatVisual.RestoreOutlineAfterFlash(restoreHighlighted);

            if (fadeOut > 0f)
                yield return new WaitForSeconds(fadeOut);

            _outlineFlashRoutine = null;
        }

        public void FlashTargeter(float duration)
        {
            if (targeter == null)
                return;

            if (_targeterRoutine != null)
                StopCoroutine(_targeterRoutine);

            _targeterRoutine = StartCoroutine(FlashTargeterRoutine(duration));
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // High-Level Combat Actions
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Melee presentation is split so the attacker can rush, then swing once per hit:
        ///   BeginMeleeApproach → PlayMeleeStrikeCycle (xN) → EndMeleeApproach.
        /// </summary>
        public float BeginMeleeApproach(Transform strikeAnchor)
        {
            if (visualRoot == null)
                return 0f;

            KillSpawnEmphasis(resetScale: true);
            CacheBasePosition();
            SetFacingToward(strikeAnchor);
            UnsubscribeMeleeStrikeIdle();

            Vector3 targetLocal = ResolveLungeTargetLocal(strikeAnchor);
            _meleePeakLocal = targetLocal;
            _meleePlanted = true;

            if (_lungeRoutine != null)
                StopCoroutine(_lungeRoutine);
            _lungeRoutine = StartCoroutine(MeleeApproachRoutine(targetLocal));

            return Mathf.Max(0.01f, lungeDuration);
        }

        /// <summary>
        /// Play MeleeAttack at the planted peak. Returns to Idle when the one-shot ends
        /// so the attacker does not freeze on the last frame while Strike/Impact finish.
        /// </summary>
        public float PlayMeleeStrikeCycle()
        {
            float attackAnimDuration = 0.08f;
            if (combatVisual != null)
            {
                UnsubscribeMeleeStrikeIdle();
                _meleeStrikeIdleHandler = HandleMeleeStrikeAnimationComplete;
                combatVisual.OnAnimationComplete += _meleeStrikeIdleHandler;
                combatVisual.PlayOneShot(CombatChainlingVisual.CombatAnimState.MeleeAttack);
                attackAnimDuration = Mathf.Max(
                    0.08f,
                    combatVisual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.MeleeAttack));
            }

            return attackAnimDuration;
        }

        public float GetMeleeContactMarkerTime()
        {
            if (combatVisual == null)
                return 0f;
            return Mathf.Max(0f, combatVisual.GetMarkerTimeOrDefault(CombatChainlingVisual.CombatAnimState.MeleeAttack));
        }

        /// <summary>Return from the planted melee peak to the home pose and idle.</summary>
        public float EndMeleeApproach()
        {
            UnsubscribeMeleeStrikeIdle();

            if (visualRoot == null || !_meleePlanted)
            {
                SetIdle();
                return 0f;
            }

            if (_lungeRoutine != null)
            {
                StopCoroutine(_lungeRoutine);
                _lungeRoutine = null;
            }

            Vector3 peak = visualRoot.localPosition;
            _lungeRoutine = StartCoroutine(MeleeReturnRoutine(peak));
            _meleePlanted = false;
            return Mathf.Max(0.01f, returnDuration);
        }

        /// <summary>
        /// Legacy full melee sequence (approach + one strike + return). Prefer the split API.
        /// </summary>
        public float PlayMeleeAttack(Transform strikeAnchor)
        {
            if (visualRoot == null)
                return 0f;

            float approach = BeginMeleeApproach(strikeAnchor);
            float strikeDuration = combatVisual != null
                ? Mathf.Max(0.08f, combatVisual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.MeleeAttack))
                : 0.08f;

            if (_lungeRoutine != null)
                StopCoroutine(_lungeRoutine);
            _lungeRoutine = StartCoroutine(LegacyMeleeFullRoutine(strikeDuration));
            return approach + strikeDuration + returnDuration;
        }

        /// <summary>
        /// Play the ranged attack animation in place (no lunge).
        /// Returns the animation duration.
        /// </summary>
        public float PlayRangedAttack()
        {
            KillSpawnEmphasis(resetScale: true);

            float animDuration = 0f;
            if (combatVisual != null)
            {
                combatVisual.PlayOneShot(CombatChainlingVisual.CombatAnimState.RangedAttack);
                animDuration = combatVisual.GetAnimationDuration(
                    CombatChainlingVisual.CombatAnimState.RangedAttack);
            }

            // Return to idle after animation completes
            if (animDuration > 0f)
                StartCoroutine(ReturnToIdleAfter(animDuration));

            return Mathf.Max(animDuration, 0.08f);
        }

        /// <summary>
        /// Play the hit reaction: flash + Hit animation + recoil motion.
        /// Returns the total reaction duration.
        /// </summary>
        public float PlayHitReaction(Transform knockbackSource = null)
        {
            if (visualRoot == null)
                return 0f;

            KillSpawnEmphasis(resetScale: true);
            CacheBasePosition();

            // Flash
            if (combatVisual != null)
            {
                combatVisual.Flash();
                combatVisual.PlayOneShot(CombatChainlingVisual.CombatAnimState.Hit);
            }

            float hitAnimDuration = combatVisual != null
                ? combatVisual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.Hit)
                : 0f;

            Vector3 worldDirection = ResolveRecoilWorldDirection(knockbackSource);
            Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
            if (_recoilRoutine != null)
                StopCoroutine(_recoilRoutine);

            _recoilRoutine = StartCoroutine(RecoilRoutine(localDirection, hitAnimDuration));

            float recoilTotal = recoilDuration + recoilBounceDuration + recoilSettleDuration;
            float total = Mathf.Max(recoilTotal, hitAnimDuration);

            StartCoroutine(ReturnToIdleAfter(total));

            return total;
        }

        public float PlayHitImpactOnly()
        {
            if (combatVisual == null)
                return 0.08f;

            combatVisual.Flash();
            combatVisual.PlayOneShot(CombatChainlingVisual.CombatAnimState.Hit);
            return Mathf.Max(combatVisual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.Hit), 0.08f);
        }

        public float PlayNoActionNudge()
        {
            if (visualRoot == null)
                return 0f;

            KillSpawnEmphasis(resetScale: true);
            CacheBasePosition();
            if (_recoilRoutine != null)
                StopCoroutine(_recoilRoutine);

            float duration = 0.28f;
            _recoilRoutine = StartCoroutine(NoActionNudgeRoutine(duration));
            return duration;
        }

        /// <summary>Set visual to idle loop.</summary>
        public void SetIdle()
        {
            if (combatVisual != null)
            {
                combatVisual.SetFrozen(false);
                combatVisual.PlayLoop(CombatChainlingVisual.CombatAnimState.Idle);
            }
        }

        public void ResetPose()
        {
            if (!_hasBasePosition || visualRoot == null)
                return;

            visualRoot.localPosition = _baseLocalPosition;
        }

        public CombatWallVisual EnsureWallVisual(
            CombatWallVisual wallPrefab,
            string visualKey,
            CombatWallVisualProfileSO profile,
            MoveWallPresentation wallPresentation,
            int facingSign)
        {
            string normalizedKey = string.IsNullOrWhiteSpace(visualKey) ? string.Empty : visualKey.Trim();
            Transform parent = BarrierAnchor != null ? BarrierAnchor : transform;

            if (_wallVisual != null && _wallVisualKey == normalizedKey)
            {
                if (_wallVisual.transform.parent != parent)
                    _wallVisual.transform.SetParent(parent, false);
                _wallVisual.transform.localPosition = Vector3.zero;
                _wallVisual.transform.localRotation = Quaternion.identity;
                _wallVisual.Configure(profile, facingSign);
                // Refresh textures/materials only — ApplyArt must not interrupt active tweens.
                _wallVisual.ApplyArt(wallPresentation, profile);
                return _wallVisual;
            }

            ForceHideWall();

            _wallVisual = wallPrefab != null
                ? Instantiate(wallPrefab, parent.position, parent.rotation, parent)
                : CreateRuntimeWallVisual(parent);
            _wallVisual.transform.localPosition = Vector3.zero;
            _wallVisual.transform.localRotation = Quaternion.identity;
            _wallVisualKey = normalizedKey;
            _wallVisual.Configure(profile, facingSign);
            _wallVisual.ApplyArt(wallPresentation, profile);
            // Stay inactive at full authored scale until Raise()/PlayHit() activates + tweens.
            // Avoids a one-frame full-size pop when created mid-sequence before Raise runs.
            _wallVisual.gameObject.SetActive(false);
            return _wallVisual;
        }

        public CombatWallVisual GetWallVisual()
        {
            return _wallVisual;
        }

        public void ForceHideWall()
        {
            if (_wallVisual != null)
            {
                _wallVisual.ForceHide();
                Destroy(_wallVisual.gameObject);
                _wallVisual = null;
            }

            _wallVisualKey = string.Empty;
        }

        private static CombatWallVisual CreateRuntimeWallVisual(Transform parent)
        {
            var root = new GameObject("CombatWallVisual_Runtime");
            root.transform.SetParent(parent, false);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>();
            visual.AddComponent<MeshRenderer>();
            return root.AddComponent<CombatWallVisual>();
        }

        public float PlayTransitionOut()
        {
            return PlaySwapOutTransition();
        }

        public float PlaySwapOutTransition()
        {
            return PlayTransitionOutInternal(ko: false);
        }

        public bool IsHiddenForCaptureAttempt => _hiddenForCaptureAttempt;

        public float PlayCaptureAttemptHide()
        {
            _hiddenForCaptureAttempt = true;
            return PlaySwapOutTransition();
        }

        public float PlayCaptureAttemptRestore()
        {
            _hiddenForCaptureAttempt = false;
            return PlaySpawnAnimation();
        }

        public void ClearCaptureAttemptHidden()
        {
            _hiddenForCaptureAttempt = false;
        }

        public float PlayKoTransition()
        {
            return PlayTransitionOutInternal(ko: true);
        }

        public float PlayDefenderKoTransition()
        {
            return PlayKoTransition();
        }

        private float PlayTransitionOutInternal(bool ko)
        {
            if (visualRoot == null)
                return 0f;

            KillSpawnEmphasis(resetScale: true);
            CacheBasePosition();

            // Cancel any in-progress motion routines
            if (_lungeRoutine != null) { StopCoroutine(_lungeRoutine); _lungeRoutine = null; }
            UnsubscribeMeleeStrikeIdle();
            _meleePlanted = false;
            if (_recoilRoutine != null) { StopCoroutine(_recoilRoutine); _recoilRoutine = null; }
            if (_faintRoutine != null) { StopCoroutine(_faintRoutine); _faintRoutine = null; }

            cosmeticOverlayController?.StopIdleFlourish();
            cosmeticOverlayController?.ApplyTransitionOutBodyOverride(ko);
            float identityFxDuration = cosmeticOverlayController != null
                ? ko
                    ? cosmeticOverlayController.PlayKoFx()
                    : cosmeticOverlayController.PlaySwapOutFx()
                : 0f;

            float bodyDuration = 0f;
            if (combatVisual != null)
            {
                combatVisual.SetFrozen(false);
                if (combatVisual.HasSwapOutSequence)
                {
                    combatVisual.PlayOneShot(CombatChainlingVisual.CombatAnimState.SwapOut);
                    bodyDuration = combatVisual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.SwapOut);
                }
                else
                {
                    combatVisual.SetFrozen(true);
                }
            }

            float shaderDuration = glitchEffect != null ? glitchEffect.GlitchOutDuration : Mathf.Max(0f, transitionOutFreezeHold);
            _faintRoutine = StartCoroutine(TransitionOutRoutine(bodyDuration, shaderDuration));
            return Mathf.Max(identityFxDuration, bodyDuration + shaderDuration);
        }

        public float PlayFaintAnimation()
        {
            return PlayKoTransition();
        }

        public bool IsTransitionOutActive => _faintRoutine != null;

        private IEnumerator TransitionOutRoutine(float bodyDuration, float fallbackHideDelay)
        {
            if (bodyDuration > 0f)
                yield return new WaitForSeconds(bodyDuration);

            combatVisual?.SetFrozen(true);

            if (glitchEffect != null)
            {
                bool hiddenByCallback = false;
                glitchEffect.PlayGlitchOut(onComplete: () =>
                {
                    HideVisualRoot();
                    hiddenByCallback = true;
                });

                if (glitchEffect.IsPlaying)
                    yield return new WaitForSeconds(glitchEffect.GlitchOutDuration);

                if (!hiddenByCallback)
                    HideVisualRoot();
            }
            else
            {
                if (fallbackHideDelay > 0f)
                    yield return new WaitForSeconds(fallbackHideDelay);

                HideVisualRoot();
            }

            _faintRoutine = null;
        }

        private void HideVisualRoot()
        {
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Restore the slot to a usable state after a faint animation.
        /// Called when a new chainling is assigned (bench swap-in or new battle).
        /// </summary>
        public void ResetFaintState()
        {
            KillSpawnEmphasis(resetScale: true);

            if (_faintRoutine != null)
            {
                StopCoroutine(_faintRoutine);
                _faintRoutine = null;
            }

            glitchEffect?.StopGlitch();
            combatVisual?.SetFrozen(false);

            if (visualRoot != null)
            {
                if (_hasBasePosition)
                {
                    visualRoot.localPosition = _baseLocalPosition;
                    visualRoot.localScale = _baseLocalScale;
                }
                visualRoot.gameObject.SetActive(true);
            }
        }

        public void HideForBattleIntro()
        {
            KillSpawnEmphasis(resetScale: true);
            glitchEffect?.StopGlitch();
            combatVisual?.SetFrozen(false);

            if (visualRoot != null)
                visualRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Play the swap-in animation. Returns duration.
        /// </summary>
        public float PlaySpawnAnimation()
        {
            return PlaySpawnAnimation(null);
        }

        /// <summary>
        /// Play the swap-in animation. The settle callback fires after the overlay resolves.
        /// </summary>
        public float PlaySpawnAnimation(Action onSettled)
        {
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(true);

            KillSpawnEmphasis(resetScale: true);
            CacheBasePosition();

            bool hasSwapInAnim = combatVisual != null && combatVisual.HasSwapInSequence;
            float animDuration = hasSwapInAnim && combatVisual != null
                ? combatVisual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.SwapIn)
                : 0f;
            float identityFxDuration = cosmeticOverlayController != null ? cosmeticOverlayController.GetSwapInFxDuration() : 0f;
            float emphasisDuration = ResolveSpawnEmphasisDuration();
            bool settlePlayed = false;

            void PlaySettle()
            {
                if (settlePlayed)
                    return;

                settlePlayed = true;
                PlaySpawnSettlePhase(hasSwapInAnim, onSettled);
            }

            float overlayDuration = 0f;
            if (glitchEffect != null)
            {
                glitchEffect.PlayGlitchIn(onComplete: PlaySettle);

                if (glitchEffect.IsPlaying)
                {
                    overlayDuration = glitchEffect.GlitchInDuration;
                }
                else
                {
                    PlaySettle();
                }
            }
            else
            {
                PlaySettle();
            }

            float settleDuration = Mathf.Max(animDuration, identityFxDuration, emphasisDuration);
            return overlayDuration + settleDuration;
        }

        private void PlaySpawnSettlePhase(bool hasSwapInAnim, Action onSettled)
        {
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(true);

            cosmeticOverlayController?.PlaySwapInFx();

            float animDuration = 0f;
            if (hasSwapInAnim && combatVisual != null)
            {
                combatVisual.SetFrozen(false);
                combatVisual.PlayOneShot(CombatChainlingVisual.CombatAnimState.SwapIn);
                animDuration = combatVisual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.SwapIn);
            }
            else
            {
                combatVisual?.SetFrozen(false);
            }

            onSettled?.Invoke();
            PlaySpawnEmphasis();

            if (hasSwapInAnim && animDuration > 0f)
                StartCoroutine(ReturnToIdleAfter(animDuration));
        }

        public float PlayVictoryPose()
        {
            return cosmeticOverlayController != null ? cosmeticOverlayController.PlayVictoryPose() : 0f;
        }

        public float PlayAttackerKoTaunt()
        {
            return cosmeticOverlayController != null ? cosmeticOverlayController.PlayAttackerKoTaunt() : 0f;
        }

        public float PlayEnemyKoPose()
        {
            return PlayAttackerKoTaunt();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Faint Sprint Coroutine
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Lunge Coroutine (animation-driven hold)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private IEnumerator MeleeApproachRoutine(Vector3 targetLocal)
        {
            float elapsed = 0f;
            Vector3 start = _baseLocalPosition;

            while (elapsed < lungeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lungeDuration);
                t = t * t * (3f - 2f * t); // smoothstep
                visualRoot.localPosition = Vector3.Lerp(start, targetLocal, t);
                yield return null;
            }

            visualRoot.localPosition = targetLocal;
            _lungeRoutine = null;
        }

        private IEnumerator MeleeReturnRoutine(Vector3 peak)
        {
            float elapsed = 0f;
            Vector3 startHome = _baseLocalPosition;

            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                t = t * t * (3f - 2f * t);
                visualRoot.localPosition = Vector3.Lerp(peak, startHome, t);
                yield return null;
            }

            visualRoot.localPosition = startHome;
            SetIdle();
            _lungeRoutine = null;
        }

        private IEnumerator LegacyMeleeFullRoutine(float holdDuration)
        {
            float elapsed = 0f;
            Vector3 start = _baseLocalPosition;
            Vector3 peak = _meleePeakLocal;

            while (elapsed < lungeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lungeDuration);
                t = t * t * (3f - 2f * t);
                visualRoot.localPosition = Vector3.Lerp(start, peak, t);
                yield return null;
            }

            visualRoot.localPosition = peak;
            PlayMeleeStrikeCycle();

            if (holdDuration > 0f)
                yield return new WaitForSeconds(holdDuration);

            UnsubscribeMeleeStrikeIdle();
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                t = t * t * (3f - 2f * t);
                visualRoot.localPosition = Vector3.Lerp(peak, start, t);
                yield return null;
            }

            visualRoot.localPosition = start;
            _meleePlanted = false;
            SetIdle();
            _lungeRoutine = null;
        }

        private void HandleMeleeStrikeAnimationComplete()
        {
            UnsubscribeMeleeStrikeIdle();
            if (_meleePlanted)
                SetIdle();
        }

        private void UnsubscribeMeleeStrikeIdle()
        {
            if (combatVisual != null && _meleeStrikeIdleHandler != null)
                combatVisual.OnAnimationComplete -= _meleeStrikeIdleHandler;
            _meleeStrikeIdleHandler = null;
        }

        private IEnumerator LungeRoutine(Vector3 targetLocal, float holdDuration)
        {
            float elapsed = 0f;
            Vector3 start = _baseLocalPosition;
            Vector3 peak = targetLocal;

            // Lunge out
            while (elapsed < lungeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lungeDuration);
                t = t * t * (3f - 2f * t); // smoothstep
                visualRoot.localPosition = Vector3.Lerp(start, peak, t);
                yield return null;
            }

            // Hold at peak while attack animation plays
            if (holdDuration > 0f)
                yield return new WaitForSeconds(holdDuration);

            // Return
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                t = t * t * (3f - 2f * t);
                visualRoot.localPosition = Vector3.Lerp(peak, start, t);
                yield return null;
            }

            visualRoot.localPosition = start;
            SetIdle();
            _lungeRoutine = null;
        }

        private Vector3 ResolveLungeTargetLocal(Transform target)
        {
            Vector3 start = _baseLocalPosition;
            if (visualRoot == null || visualRoot.parent == null || target == null)
                return start;

            Vector3 targetWorld = target.position;
            Vector3 targetLocal = visualRoot.parent.InverseTransformPoint(targetWorld);

            Vector3 towardTarget = targetLocal - start;
            towardTarget.y = 0f;

            Vector3 desired = targetLocal;
            desired.y = start.y;
            if (towardTarget.sqrMagnitude > 0.0001f)
            {
                Vector3 approachDir = towardTarget.normalized;
                desired = targetLocal - approachDir * Mathf.Max(0f, meleeStandoffDistance);
                desired.y = start.y;
            }

            Vector3 offset = desired - start;
            float distance = offset.magnitude;
            if (distance > 0.001f)
            {
                float clamped = Mathf.Clamp(distance, minLungeDistance, maxLungeDistance);
                desired = start + offset.normalized * clamped;
            }

            return desired;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Recoil Coroutine
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private Vector3 ResolveRecoilWorldDirection(Transform knockbackSource)
        {
            if (visualRoot != null && knockbackSource != null)
            {
                Vector3 awayFromSource = visualRoot.position - knockbackSource.position;
                awayFromSource.y = 0f;
                if (awayFromSource.sqrMagnitude > 0.001f)
                    return awayFromSource.normalized;
            }

            return ResolveBacklineWorldDirection();
        }

        /// <summary>Pushes toward the slot's own backline when no attacker transform is available.</summary>
        private Vector3 ResolveBacklineWorldDirection()
        {
            Vector3 localBack = new Vector3(-ResolvePopupFacingSign(), 0f, 0f);
            Vector3 worldBack = transform.TransformDirection(localBack);
            worldBack.y = 0f;
            if (worldBack.sqrMagnitude > 0.001f)
                return worldBack.normalized;

            return -transform.forward;
        }

        private IEnumerator RecoilRoutine(Vector3 localDirection, float hitAnimDuration)
        {
            Vector3 dir = localDirection.normalized;
            Vector3 start = _baseLocalPosition;

            // Phase 1: Jump-back arc
            Vector3 jumpEnd = start + dir * recoilDistance;
            float elapsed = 0f;
            while (elapsed < recoilDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / recoilDuration);
                float easeT = 1f - (1f - t) * (1f - t); // ease-out quadratic
                Vector3 flat = Vector3.Lerp(start, jumpEnd, easeT);
                flat.y = start.y + Mathf.Sin(t * Mathf.PI) * recoilHeight;
                visualRoot.localPosition = flat;
                yield return null;
            }

            // Land at jumpEnd
            jumpEnd.y = start.y;
            visualRoot.localPosition = jumpEnd;

            // Phase 2: Small bounce
            Vector3 bounceEnd = jumpEnd + dir * recoilBounceDistance;
            elapsed = 0f;
            while (elapsed < recoilBounceDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / recoilBounceDuration);
                float halfT = t * 2f;
                if (halfT <= 1f)
                {
                    float easeT = 1f - (1f - halfT) * (1f - halfT);
                    Vector3 flat = Vector3.Lerp(jumpEnd, bounceEnd, easeT);
                    flat.y = jumpEnd.y + Mathf.Sin(halfT * Mathf.PI) * recoilBounceHeight;
                    visualRoot.localPosition = flat;
                }
                else
                {
                    float returnT = halfT - 1f;
                    returnT = returnT * returnT; // ease-in
                    Vector3 flat = Vector3.Lerp(bounceEnd, jumpEnd, returnT);
                    flat.y = jumpEnd.y + Mathf.Sin((2f - halfT) * Mathf.PI) * recoilBounceHeight * 0.3f;
                    visualRoot.localPosition = flat;
                }
                yield return null;
            }

            visualRoot.localPosition = jumpEnd;

            // Phase 3: Settle back to base
            elapsed = 0f;
            while (elapsed < recoilSettleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / recoilSettleDuration);
                t = t * t * (3f - 2f * t); // smoothstep
                visualRoot.localPosition = Vector3.Lerp(jumpEnd, start, t);
                yield return null;
            }

            visualRoot.localPosition = start;
            _recoilRoutine = null;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Helpers
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private IEnumerator NoActionNudgeRoutine(float duration)
        {
            Vector3 start = _baseLocalPosition;
            float elapsed = 0f;
            const float amplitude = 0.07f;
            const float cycles = 3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                float offset = Mathf.Sin(t * Mathf.PI * 2f * cycles) * amplitude * (1f - t);
                visualRoot.localPosition = start + Vector3.right * offset;
                yield return null;
            }

            visualRoot.localPosition = start;
            _recoilRoutine = null;
        }

        public void SetFacingToward(Transform target)
        {
            // In combat, facing is handled by flipX on the visual.
            // This is a no-op unless you want runtime facing changes.
        }

        private IEnumerator ReturnToIdleAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetIdle();
        }

        private void PlaySpawnEmphasis()
        {
            if (!spawnEmphasisEnabled || visualRoot == null)
                return;

            float upDuration = Mathf.Max(0f, spawnEmphasisUpDuration);
            float downDuration = Mathf.Max(0f, spawnEmphasisDownDuration);
            if (upDuration <= 0f && downDuration <= 0f)
                return;

            Vector3 baseScale = _hasBasePosition ? _baseLocalScale : visualRoot.localScale;
            Vector3 peakScale = baseScale * Mathf.Max(1f, spawnEmphasisScale);

            KillSpawnEmphasis(resetScale: false);
            visualRoot.localScale = baseScale;

            var sequence = DOTween.Sequence()
                .SetUpdate(spawnEmphasisUseUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            if (upDuration > 0f)
                sequence.Append(visualRoot.DOScale(peakScale, upDuration).SetEase(spawnEmphasisUpEase));
            else
                visualRoot.localScale = peakScale;

            sequence.AppendInterval(Mathf.Max(0f, spawnEmphasisHoldDuration));

            if (downDuration > 0f)
                sequence.Append(visualRoot.DOScale(baseScale, downDuration).SetEase(spawnEmphasisDownEase));
            else
                sequence.AppendCallback(() => visualRoot.localScale = baseScale);

            sequence.OnComplete(() =>
            {
                if (visualRoot != null)
                    visualRoot.localScale = baseScale;
                _spawnEmphasisTween = null;
            });

            _spawnEmphasisTween = sequence;
        }

        private float ResolveSpawnEmphasisDuration()
        {
            if (!spawnEmphasisEnabled)
                return 0f;

            return Mathf.Max(0f, spawnEmphasisUpDuration)
                + Mathf.Max(0f, spawnEmphasisHoldDuration)
                + Mathf.Max(0f, spawnEmphasisDownDuration);
        }

        private void KillSpawnEmphasis(bool resetScale)
        {
            if (_spawnEmphasisTween != null && _spawnEmphasisTween.IsActive())
                _spawnEmphasisTween.Kill();
            _spawnEmphasisTween = null;

            if (resetScale && visualRoot != null && _hasBasePosition)
                visualRoot.localScale = _baseLocalScale;
        }

        private void OnValidate()
        {
            spawnEmphasisScale = Mathf.Max(1f, spawnEmphasisScale);
            spawnEmphasisUpDuration = Mathf.Max(0f, spawnEmphasisUpDuration);
            spawnEmphasisHoldDuration = Mathf.Max(0f, spawnEmphasisHoldDuration);
            spawnEmphasisDownDuration = Mathf.Max(0f, spawnEmphasisDownDuration);
        }

        private IEnumerator FlashTargeterRoutine(float duration)
        {
            targeter.gameObject.SetActive(true);
            yield return new WaitForSeconds(duration);
            targeter.gameObject.SetActive(false);
            _targeterRoutine = null;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Legacy compatibility â€” kept so the old presenter code doesn't break
        // during migration. Remove once fully switched over.
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Legacy: play melee lunge with fixed hold. Use PlayMeleeAttack instead.</summary>
        public void PlayMeleeLunge(Transform target)
        {
            PlayMeleeAttack(target);
        }

        /// <summary>Legacy: recoil without animation. Use PlayHitReaction instead.</summary>
        public void PlayRecoil(Camera referenceCamera = null)
        {
            PlayHitReaction(null);
        }

        /// <summary>Legacy: fixed lunge duration. Use PlayMeleeAttack return value instead.</summary>
        public float GetLungeTotalDuration()
        {
            float attackAnimDuration = combatVisual != null
                ? combatVisual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.MeleeAttack)
                : 0.3f;
            return lungeDuration + Mathf.Max(attackAnimDuration, 0.08f) + returnDuration;
        }
    }
}
