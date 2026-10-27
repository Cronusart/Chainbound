using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class ChainlingCosmeticOverlayController : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private Transform overlayRoot;
        [SerializeField] private Transform auraRoot;
        [SerializeField] private Transform transientFxRoot;

        [Header("Idle Flourish")]
        [SerializeField] private int idleLoopsPerFlourish = 2;

        private CombatChainlingVisual _visual;
        private bool _flipX;

        private GameObject _auraInstance;
        private string _auraKey;

        private ResolvedChainlingVisualIdentity _resolvedIdentity;
        private ChainlingSwapInFxDefinitionSO _swapInFx;
        private ChainlingSwapOutFxDefinitionSO _swapOutFx;
        private ChainlingKoFxDefinitionSO _koFx;
        private ChainlingVictoryPoseDefinitionSO _victoryPose;
        private ChainlingIdleFlourishDefinitionSO _idleFlourish;
        private ChainlingMeleeAttackStyleDefinitionSO _meleeAttackStyle;
        private ChainlingRangedAttackStyleDefinitionSO _rangedAttackStyle;
        private ChainlingHitReactionStyleDefinitionSO _hitReactionStyle;
        private ChainlingAttackerKoStyleDefinitionSO _attackerKoStyle;
        private int _idleLoopCount;

        private void OnDisable()
        {
            StopIdleFlourishLoop();
            ClearPersistentOverlays();
            ClearTransientEffects();
        }

        public void Apply(
            ResolvedChainlingVisualIdentity resolved,
            CombatChainlingVisual visual,
            Transform defaultRoot,
            bool flipX)
        {
            EnsureRoots(defaultRoot != null ? defaultRoot : transform);

            if (_visual != null)
                _visual.OnLoopCompleted -= HandleVisualLoopCompleted;

            _visual = visual;
            _flipX = flipX;
            if (_visual != null)
                _visual.OnLoopCompleted += HandleVisualLoopCompleted;

            if (resolved == null)
            {
                ClearAll();
                return;
            }

            ApplyOverlay(resolved.aura, auraRoot, ref _auraInstance, ref _auraKey);

            _resolvedIdentity = resolved;
            _swapInFx = resolved.swapInFx;
            _swapOutFx = resolved.swapOutFx;
            _koFx = resolved.koFx;
            _victoryPose = resolved.victoryPose;
            _meleeAttackStyle = resolved.meleeAttackStyle;
            _rangedAttackStyle = resolved.rangedAttackStyle;
            _hitReactionStyle = resolved.hitReactionStyle;
            _attackerKoStyle = resolved.attackerKoStyle;
            ApplyBodyOverrides();
            SetIdleFlourish(resolved.idleFlourish);
        }

        public void ClearAll()
        {
            ClearPersistentOverlays();
            ClearTransientEffects();
            StopIdleFlourishLoop();
            _resolvedIdentity = null;
            _swapInFx = null;
            _swapOutFx = null;
            _koFx = null;
            _victoryPose = null;
            _idleFlourish = null;
            _meleeAttackStyle = null;
            _rangedAttackStyle = null;
            _hitReactionStyle = null;
            _attackerKoStyle = null;
            _visual?.ClearBodyOverrides();
            if (_visual != null)
                _visual.OnLoopCompleted -= HandleVisualLoopCompleted;
            _visual = null;
        }

        public float PlaySwapInFx()
        {
            return PlayFx(_swapInFx);
        }

        public float GetSwapInFxDuration()
        {
            return _swapInFx != null ? _swapInFx.ResolveDurationSeconds() : 0f;
        }

        public float PlaySwapOutFx()
        {
            return PlayFx(_swapOutFx);
        }

        public float PlayKoFx()
        {
            return PlayFx(_koFx);
        }

        public float PlayVictoryPose()
        {
            return PlayPose(_victoryPose, allowInterrupt: true);
        }

        public float PlayAttackerKoTaunt()
        {
            return PlayPose(_attackerKoStyle, allowInterrupt: true);
        }

        public void StopIdleFlourish()
        {
            StopIdleFlourishLoop();
        }

        private void ApplyBodyOverrides()
        {
            if (_visual == null)
                return;

            _visual.ClearBodyOverrides();
            ApplyResolvedOverride(_visual, CombatChainlingVisual.CombatAnimState.Idle, ChainlingAnimSlot.Idle);
            ApplyResolvedOverride(_visual, CombatChainlingVisual.CombatAnimState.MeleeAttack, ChainlingAnimSlot.Melee);
            ApplyResolvedOverride(_visual, CombatChainlingVisual.CombatAnimState.RangedAttack, ChainlingAnimSlot.Ranged);
            ApplyResolvedOverride(_visual, CombatChainlingVisual.CombatAnimState.Hit, ChainlingAnimSlot.Hit);
            ApplyResolvedOverride(_visual, CombatChainlingVisual.CombatAnimState.SwapIn, ChainlingAnimSlot.SwapIn);
            ApplyTransitionOutBodyOverride(ko: false);
        }

        public void ApplyTransitionOutBodyOverride(bool ko)
        {
            if (_visual == null)
                return;

            ApplyResolvedOverride(
                _visual,
                CombatChainlingVisual.CombatAnimState.SwapOut,
                ko ? ChainlingAnimSlot.Ko : ChainlingAnimSlot.SwapOut);
        }

        private void ApplyResolvedOverride(
            CombatChainlingVisual visual,
            CombatChainlingVisual.CombatAnimState state,
            ChainlingAnimSlot slot)
        {
            ResolvedChainlingVisualClip clip = ChainlingVisualSequenceResolver.Resolve(_resolvedIdentity, slot);
            if (visual == null || !clip.HasSequence)
                return;

            visual.SetBodyOverride(state, clip.sequence, clip.fps, clip.markerFrame);
        }

        private void SetIdleFlourish(ChainlingIdleFlourishDefinitionSO definition)
        {
            if (_idleFlourish == definition)
                return;

            _idleFlourish = definition;
            StopIdleFlourishLoop();
            _idleLoopCount = 0;
        }

        private void HandleVisualLoopCompleted(CombatChainlingVisual.CombatAnimState state)
        {
            if (state != CombatChainlingVisual.CombatAnimState.Idle || _idleFlourish == null)
                return;

            _idleLoopCount++;
            int loopsPerFlourish = Mathf.Max(1, idleLoopsPerFlourish);
            if (_idleLoopCount % loopsPerFlourish != 0)
                return;

            if (_visual == null || !_visual.CanPlayTemporarySequence)
                return;

            PlayPose(_idleFlourish, allowInterrupt: false);
        }

        private float PlayPose(ChainlingPoseDefinitionSO definition, bool allowInterrupt)
        {
            if (definition == null)
                return 0f;

            float duration = 0f;
            if (definition.vfxPrefab != null)
                duration = Mathf.Max(duration, SpawnTimedPrefab(definition.vfxPrefab, transientFxRoot, definition.vfxLocalPosition, Vector3.zero, Vector3.one * Mathf.Max(0.01f, definition.vfxScale), definition.vfxDurationSeconds));

            if (definition.flipbookVfx != null)
                duration = Mathf.Max(duration, SpawnFlipbook(definition.flipbookVfx, transientFxRoot, definition.vfxLocalPosition, Mathf.Max(0.01f, definition.vfxScale), parentToSlot: true));

            if (_visual != null && definition.HasSequence && (allowInterrupt || _visual.CanPlayTemporarySequence))
                duration = Mathf.Max(duration, _visual.PlayTemporarySequence(definition.sequence, definition.fps, returnToIdle: true));

            return Mathf.Max(duration, definition.ResolveDurationSeconds());
        }

        private float PlayFx(ChainlingFxDefinitionSO definition)
        {
            if (definition == null)
                return 0f;

            EnsureRoots(transform);
            float duration = definition.ResolveDurationSeconds();
            Vector3 scale = Vector3.one * Mathf.Max(0.01f, definition.scale);

            if (definition.prefab != null)
                duration = Mathf.Max(duration, SpawnTimedPrefab(definition.prefab, transientFxRoot, definition.localPosition, definition.localEulerAngles, scale, duration, definition.parentToSlot));

            if (definition.flipbookVfx != null)
                duration = Mathf.Max(duration, SpawnFlipbook(definition.flipbookVfx, transientFxRoot, definition.localPosition, Mathf.Max(0.01f, definition.scale), definition.parentToSlot));

            return duration;
        }

        private void ApplyOverlay(ChainlingOverlayDefinitionSO definition, Transform root, ref GameObject instance, ref string key)
        {
            if (definition == null || definition.prefab == null)
            {
                DestroyObject(instance);
                instance = null;
                key = string.Empty;
                return;
            }

            string nextKey = $"{definition.Id}:{definition.GetEntityId()}";
            if (instance != null && key == nextKey)
                return;

            DestroyObject(instance);
            instance = Instantiate(definition.prefab, root);
            ApplyLocalTransform(instance.transform, definition.localPosition, definition.localEulerAngles, definition.ResolvedLocalScale, definition.mirrorWithChainling);
            key = nextKey;
        }

        private float SpawnTimedPrefab(
            GameObject prefab,
            Transform root,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale,
            float durationSeconds,
            bool parentToSlot = true)
        {
            if (prefab == null)
                return 0f;

            Transform parent = parentToSlot ? root : null;
            GameObject instance = Instantiate(prefab, parent);
            ApplyLocalTransform(instance.transform, localPosition, localEulerAngles, localScale, mirror: true);

            if (!parentToSlot && root != null)
                instance.transform.position = root.TransformPoint(ResolveMirroredPosition(localPosition, mirror: true));

            float duration = Mathf.Max(0f, durationSeconds);
            if (duration > 0f)
                DestroyObject(instance, duration);

            return duration;
        }

        private float SpawnFlipbook(SpriteFlipbookVfxDefinitionSO definition, Transform root, Vector3 localPosition, float scale, bool parentToSlot)
        {
            if (definition == null || root == null)
                return 0f;

            Vector3 worldPosition = root.TransformPoint(ResolveMirroredPosition(localPosition, mirror: true));
            var player = SpriteFlipbookVfxPlayer.Spawn(definition, worldPosition, scale, parentToSlot ? root : null);
            if (player != null && parentToSlot)
                player.transform.position = worldPosition;

            return definition.GetDurationSeconds();
        }

        private void ApplyLocalTransform(Transform target, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, bool mirror)
        {
            if (target == null)
                return;

            target.localPosition = ResolveMirroredPosition(localPosition, mirror);
            target.localRotation = Quaternion.Euler(localEulerAngles);

            Vector3 scale = localScale == Vector3.zero ? Vector3.one : localScale;
            if (mirror && _flipX)
                scale.x *= -1f;

            target.localScale = scale;
        }

        private Vector3 ResolveMirroredPosition(Vector3 localPosition, bool mirror)
        {
            if (mirror && _flipX)
                localPosition.x = -localPosition.x;
            return localPosition;
        }

        private void EnsureRoots(Transform defaultRoot)
        {
            if (defaultRoot == null)
                defaultRoot = transform;

            if (overlayRoot == null)
                overlayRoot = CreateRoot("IdentityOverlays", defaultRoot);
            if (auraRoot == null)
                auraRoot = CreateRoot("IdentityAura", defaultRoot);
            if (transientFxRoot == null)
                transientFxRoot = CreateRoot("IdentityTransientFx", defaultRoot);
        }

        private static Transform CreateRoot(string rootName, Transform parent)
        {
            var go = new GameObject(rootName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private void ClearPersistentOverlays()
        {
            DestroyObject(_auraInstance);
            _auraInstance = null;
            _auraKey = string.Empty;
        }

        private void ClearTransientEffects()
        {
            if (transientFxRoot == null)
                return;

            for (int i = transientFxRoot.childCount - 1; i >= 0; i--)
                DestroyObject(transientFxRoot.GetChild(i).gameObject);
        }

        private void StopIdleFlourishLoop()
        {
            _idleLoopCount = 0;
        }

        private static void DestroyObject(GameObject target, float delay = 0f)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target, delay);
            else
                DestroyImmediate(target);
        }
    }
}
