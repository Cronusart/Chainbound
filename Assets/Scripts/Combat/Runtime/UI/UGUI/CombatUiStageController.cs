using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Staged combat UI presentation: applies hidden state before first frame, then reveals
    /// HUD roots and wallet with mobile-safe animations based on encounter UI mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatUiStageController : MonoBehaviour
    {
        [Header("Info HUD (player/enemy cards)")]
        [SerializeField] private RectTransform playerHudPanel;
        [SerializeField] private RectTransform enemyHudPanel;

        [Header("Interaction HUD (action panel, turn ribbon, targeting)")]
        [SerializeField] private RectTransform turnOrderRibbon;
        [SerializeField] private RectTransform targetIndicatorsRoot;
        [SerializeField] private RectTransform bottomActionPanel;

        [Header("Wallet")]
        [Tooltip("Leave empty to auto-resolve the prefab root via CombatActionSelectorController (required when the device has duplicate child names like Hardware_Wallet).")]
        [SerializeField] private Transform walletRoot;

        [Header("Reveal Timing")]
        [SerializeField] private float hudRevealDuration = 0.28f;
        [SerializeField] private float hudRevealScaleFrom = 0.96f;
        [SerializeField] private float hudRevealOffsetY = -24f;
        [SerializeField] private float walletRevealDuration = 0.32f;
        [SerializeField] private float walletStaggerDelay = 0.08f;
        [SerializeField] private float walletStartOffsetY = -120f;
        [SerializeField] private float walletStartTiltX = -8f;
        [SerializeField] private Ease hudEase = Ease.OutCubic;
        [SerializeField] private Ease walletEase = Ease.OutCubic;
        [SerializeField] private bool useUnscaledTime = true;

        private readonly List<CanvasGroup> _infoHudGroups = new List<CanvasGroup>(2);
        private readonly List<CanvasGroup> _interactionHudGroups = new List<CanvasGroup>(3);
        private CanvasGroup _walletGroup;
        private readonly Dictionary<RectTransform, Vector2> _cachedAnchoredPositions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector3> _cachedLocalEulers = new Dictionary<RectTransform, Vector3>();
        private Vector3 _cachedWalletLocalPos;
        private Vector3 _cachedWalletLocalEulers;
        private CombatHardwareWalletOffsetController _walletOffsetController;
        private Sequence _activeRevealSequence;

        /// <summary>
        /// Apply immediate hidden state before any HUD refresh. Call as early as possible (e.g. Awake).
        /// </summary>
        public void ApplyImmediateHidden()
        {
            KillActiveSequence();
            SetGroupHidden(GetInfoHudTransforms(), _infoHudGroups, true);
            SetGroupHidden(GetInteractionHudTransforms(), _interactionHudGroups, true);
            SetWalletHidden(true);
        }

        /// <summary>
        /// Reveal UI groups based on mode. Full = info + interaction + wallet; InfoOnly = info only; Hidden = none.
        /// </summary>
        public void RevealByMode(CombatUiPresentationMode mode)
        {
            KillActiveSequence();
            if (mode == CombatUiPresentationMode.Hidden)
            {
                ApplyImmediateHidden();
                return;
            }

            _activeRevealSequence = DOTween.Sequence().SetUpdate(useUnscaledTime);

            if (mode == CombatUiPresentationMode.Full || mode == CombatUiPresentationMode.InfoOnly)
            {
                RevealInfoHud(_activeRevealSequence, 0f);
                if (mode == CombatUiPresentationMode.Full)
                {
                    RevealInteractionHud(_activeRevealSequence, 0f);
                    RevealWallet(_activeRevealSequence, walletStaggerDelay);
                }
            }

            _activeRevealSequence.OnKill(() => _activeRevealSequence = null);
            _activeRevealSequence.Play();
        }

        /// <summary>
        /// Keep all combat UI hidden (e.g. intro flee encounter).
        /// </summary>
        public void KeepAllHidden()
        {
            KillActiveSequence();
            ApplyImmediateHidden();
        }

        private void Awake()
        {
            ResolveReferences();
            CacheCanvasGroups();
        }

        private void Start()
        {
            ApplyImmediateHidden();
        }

        private void OnDestroy()
        {
            KillActiveSequence();
        }

        private void ResolveReferences()
        {
            if (playerHudPanel == null) playerHudPanel = FindRect("PlayerHudPanel");
            if (enemyHudPanel == null) enemyHudPanel = FindRect("EnemyHudPanel");
            if (turnOrderRibbon == null) turnOrderRibbon = FindRect("TurnOrderRibbon");
            if (targetIndicatorsRoot == null) targetIndicatorsRoot = FindRect("TargetIndicatorsRoot");
            if (bottomActionPanel == null) bottomActionPanel = FindRect("BottomActionPanel");
            if (walletRoot == null)
                walletRoot = CombatWalletRootResolver.Resolve();

            if (walletRoot != null)
                _walletOffsetController = walletRoot.GetComponent<CombatHardwareWalletOffsetController>();
        }

        private RectTransform FindRect(string name)
        {
            var rects = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name == name)
                    return rects[i];
            }
            return null;
        }

        private void CacheCanvasGroups()
        {
            _infoHudGroups.Clear();
            _interactionHudGroups.Clear();
            _walletGroup = null;

            foreach (var rt in GetInfoHudTransforms())
            {
                if (rt == null) continue;
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
                _infoHudGroups.Add(cg);
                _cachedAnchoredPositions[rt] = rt.anchoredPosition;
                _cachedLocalEulers[rt] = rt.localEulerAngles;
            }

            foreach (var rt in GetInteractionHudTransforms())
            {
                if (rt == null) continue;
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
                _interactionHudGroups.Add(cg);
                _cachedAnchoredPositions[rt] = rt.anchoredPosition;
                _cachedLocalEulers[rt] = rt.localEulerAngles;
            }

            if (walletRoot != null)
            {
                _walletGroup = walletRoot.GetComponent<CanvasGroup>();
                if (_walletGroup == null) _walletGroup = walletRoot.gameObject.AddComponent<CanvasGroup>();
                _cachedWalletLocalPos = _walletOffsetController != null
                    ? _walletOffsetController.AnimationAuthoredLocalPosition
                    : walletRoot.localPosition;
                _cachedWalletLocalEulers = walletRoot.localEulerAngles;
            }
        }

        private void SetWalletAnimationPosition(Vector3 position)
        {
            ResolveWalletOffsetController();
            if (_walletOffsetController != null)
                _walletOffsetController.SetAnimationAuthoredLocalPosition(position);
            else if (walletRoot != null)
                walletRoot.localPosition = position;
        }

        private Tween TweenWalletAnimationPosition(Vector3 target, float duration)
        {
            ResolveWalletOffsetController();
            if (_walletOffsetController == null)
                return walletRoot.DOLocalMove(target, duration);

            return DOTween.To(
                () => _walletOffsetController.AnimationAuthoredLocalPosition,
                position => _walletOffsetController.SetAnimationAuthoredLocalPosition(position),
                target,
                duration);
        }

        private void ResolveWalletOffsetController()
        {
            if (_walletOffsetController == null && walletRoot != null)
                _walletOffsetController = walletRoot.GetComponent<CombatHardwareWalletOffsetController>();
        }

        private List<RectTransform> GetInfoHudTransforms()
        {
            var list = new List<RectTransform>(2);
            if (playerHudPanel != null) list.Add(playerHudPanel);
            if (enemyHudPanel != null) list.Add(enemyHudPanel);
            return list;
        }

        private List<RectTransform> GetInteractionHudTransforms()
        {
            var list = new List<RectTransform>(3);
            if (turnOrderRibbon != null) list.Add(turnOrderRibbon);
            if (targetIndicatorsRoot != null) list.Add(targetIndicatorsRoot);
            if (bottomActionPanel != null) list.Add(bottomActionPanel);
            return list;
        }

        private void SetGroupHidden(List<RectTransform> transforms, List<CanvasGroup> groups, bool hidden)
        {
            for (int i = 0; i < transforms.Count; i++)
            {
                var rt = transforms[i];
                var cg = i < groups.Count ? groups[i] : null;
                if (rt == null) continue;

                rt.gameObject.SetActive(!hidden);
                if (cg != null)
                    cg.alpha = hidden ? 0f : 1f;
                if (hidden && _cachedAnchoredPositions.TryGetValue(rt, out var pos))
                    rt.anchoredPosition = pos + new Vector2(0, hudRevealOffsetY);
                if (hidden)
                    rt.localScale = Vector3.one * hudRevealScaleFrom;
            }
        }

        private void SetWalletHidden(bool hidden)
        {
            if (walletRoot == null) return;

            walletRoot.gameObject.SetActive(!hidden);
            if (_walletGroup != null)
                _walletGroup.alpha = hidden ? 0f : 1f;
            if (hidden)
            {
                SetWalletAnimationPosition(_cachedWalletLocalPos + new Vector3(0, walletStartOffsetY * 0.01f, 0));
                walletRoot.localEulerAngles = _cachedWalletLocalEulers + new Vector3(walletStartTiltX, 0, 0);
            }
        }

        private void RevealInfoHud(Sequence seq, float delay)
        {
            var transforms = GetInfoHudTransforms();
            for (int i = 0; i < transforms.Count; i++)
            {
                var rt = transforms[i];
                var cg = i < _infoHudGroups.Count ? _infoHudGroups[i] : null;
                if (rt == null || cg == null) continue;

                rt.gameObject.SetActive(true);
                var startPos = _cachedAnchoredPositions.TryGetValue(rt, out var pos) ? pos + new Vector2(0, hudRevealOffsetY) : rt.anchoredPosition;
                rt.anchoredPosition = startPos;
                rt.localScale = Vector3.one * hudRevealScaleFrom;
                cg.alpha = 0f;

                seq.Insert(delay, rt.DOAnchorPos(_cachedAnchoredPositions[rt], hudRevealDuration).SetEase(hudEase));
                seq.Insert(delay, rt.DOScale(1f, hudRevealDuration).SetEase(hudEase));
                seq.Insert(delay, DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, hudRevealDuration).SetEase(hudEase).SetTarget(cg));
            }
        }

        private void RevealInteractionHud(Sequence seq, float delay)
        {
            var transforms = GetInteractionHudTransforms();
            for (int i = 0; i < transforms.Count; i++)
            {
                var rt = transforms[i];
                var cg = i < _interactionHudGroups.Count ? _interactionHudGroups[i] : null;
                if (rt == null || cg == null) continue;

                rt.gameObject.SetActive(true);
                var startPos = _cachedAnchoredPositions.TryGetValue(rt, out var pos) ? pos + new Vector2(0, hudRevealOffsetY) : rt.anchoredPosition;
                rt.anchoredPosition = startPos;
                rt.localScale = Vector3.one * hudRevealScaleFrom;
                cg.alpha = 0f;

                seq.Insert(delay, rt.DOAnchorPos(_cachedAnchoredPositions[rt], hudRevealDuration).SetEase(hudEase));
                seq.Insert(delay, rt.DOScale(1f, hudRevealDuration).SetEase(hudEase));
                seq.Insert(delay, DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, hudRevealDuration).SetEase(hudEase).SetTarget(cg));
            }
        }

        private void RevealWallet(Sequence seq, float delay)
        {
            if (walletRoot == null || _walletGroup == null) return;

            walletRoot.gameObject.SetActive(true);
            var startPos = _cachedWalletLocalPos + new Vector3(0, walletStartOffsetY * 0.01f, 0);
            var startEulers = _cachedWalletLocalEulers + new Vector3(walletStartTiltX, 0, 0);
            SetWalletAnimationPosition(startPos);
            walletRoot.localEulerAngles = startEulers;
            _walletGroup.alpha = 0f;

            seq.Insert(delay, TweenWalletAnimationPosition(_cachedWalletLocalPos, walletRevealDuration).SetEase(walletEase));
            seq.Insert(delay, walletRoot.DOLocalRotate(_cachedWalletLocalEulers, walletRevealDuration).SetEase(walletEase));
            seq.Insert(delay, DOTween.To(() => _walletGroup.alpha, x => _walletGroup.alpha = x, 1f, walletRevealDuration).SetEase(walletEase).SetTarget(_walletGroup));
        }

        private void KillActiveSequence()
        {
            if (_activeRevealSequence != null)
            {
                _activeRevealSequence.Kill();
                _activeRevealSequence = null;
            }
        }

    }
}
