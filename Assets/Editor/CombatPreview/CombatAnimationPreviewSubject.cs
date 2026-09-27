using System;
using System.Collections.Generic;
using System.Reflection;
using SignalJam.VisualFx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chainbound.Combat.Editor
{
    internal readonly struct CombatPreviewAnchorInfo
    {
        public CombatPreviewAnchorInfo(PresentationAnchorTarget target, string label, Transform transform)
        {
            Target = target;
            Label = label;
            Transform = transform;
        }

        public PresentationAnchorTarget Target { get; }
        public string Label { get; }
        public Transform Transform { get; }
    }

    internal sealed class CombatAnimationPreviewSubject
    {
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo CombatViewAwakeMethod = typeof(Combat2ChainlingView).GetMethod("Awake", InstancePrivate);
        private static readonly MethodInfo CombatVisualAwakeMethod = typeof(CombatChainlingVisual).GetMethod("Awake", InstancePrivate);
        private static readonly MethodInfo CombatMoveVfxAwakeMethod = typeof(CombatMoveVFX).GetMethod("Awake", InstancePrivate);
        private static readonly MethodInfo CombatMoveVfxOnEnableMethod = typeof(CombatMoveVFX).GetMethod("OnEnable", InstancePrivate);

        private readonly List<GameObject> _spawnedPreviewObjects = new List<GameObject>();
        private readonly List<CombatPreviewAnchorInfo> _anchors = new List<CombatPreviewAnchorInfo>();

        public CombatAnimationPreviewSubject(Combat2ChainlingView template)
        {
            Root = template != null ? Object.Instantiate(template.gameObject) : new GameObject("CombatPreviewRoot");
            Root.name = "CombatAnimationPreviewSubject";
            Root.transform.position = Vector3.zero;
            Root.transform.rotation = Quaternion.identity;
            Root.transform.localScale = Vector3.one;

            View = Root.GetComponent<Combat2ChainlingView>();
            if (View == null)
                View = Root.AddComponent<Combat2ChainlingView>();

            VisualRoot = FindOrCreateChild(Root.transform, "VisualRoot", Vector3.zero);
            PreviewVfxRoot = FindOrCreateChild(Root.transform, "PreviewVfxRoot", Vector3.zero);

            EnsurePreviewAnchors(VisualRoot);
            Visual = EnsureCombatVisual(VisualRoot);

            ApplyHideFlags(Root);
            InvokePrivateAwake(Visual, CombatVisualAwakeMethod);
            InvokePrivateAwake(View, CombatViewAwakeMethod);
            RefreshAnchors();
        }

        public GameObject Root { get; }
        public Combat2ChainlingView View { get; private set; }
        public CombatChainlingVisual Visual { get; private set; }
        public Transform VisualRoot { get; private set; }
        public Transform PreviewVfxRoot { get; private set; }
        public IReadOnlyList<CombatPreviewAnchorInfo> Anchors => _anchors;

        public void Dispose()
        {
            ClearSpawnedPreviewObjects();

            if (Root != null)
                Object.DestroyImmediate(Root);
        }

        public void SetPosition(Vector3 position)
        {
            if (Root != null)
                Root.transform.position = position;
        }

        public void SetFlipX(bool flipX)
        {
            if (Visual == null)
                return;

            Visual.SetFlipX(flipX);
            bool oneShot = Visual.CurrentState != CombatChainlingVisual.CombatAnimState.Idle;
            Visual.SampleFrame(Visual.CurrentState, Visual.CurrentFrame, oneShot);
        }

        public Transform GetAnchor(PresentationAnchorTarget anchorTarget)
        {
            switch (anchorTarget)
            {
                case PresentationAnchorTarget.CameraFocus:
                    return View != null ? View.CameraFocus : null;
                case PresentationAnchorTarget.MeleeStop:
                    return View != null ? View.MeleeStopAnchor : null;
                case PresentationAnchorTarget.Impact:
                    return View != null ? View.ImpactAnchor : null;
                case PresentationAnchorTarget.ProjectileOrigin:
                    return View != null ? View.ProjectileOriginAnchor : null;
                case PresentationAnchorTarget.Barrier:
                    return View != null ? View.BarrierAnchor : null;
                case PresentationAnchorTarget.CustomOffset:
                case PresentationAnchorTarget.SelfRoot:
                default:
                    return Visual != null ? Visual.transform : (Root != null ? Root.transform : null);
            }
        }

        public GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            return SpawnPrefab(prefab, position, rotation, parent, out _);
        }

        public GameObject SpawnPrefab(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            out ParticleSystem[] previewParticleSystems)
        {
            previewParticleSystems = Array.Empty<ParticleSystem>();

            if (prefab == null)
                return null;

            Transform actualParent = parent != null ? parent : PreviewVfxRoot;
            var instance = Object.Instantiate(prefab, position, rotation, actualParent);
            ApplyHideFlags(instance);
            InitializePreviewPrefab(instance, out previewParticleSystems);
            _spawnedPreviewObjects.Add(instance);
            return instance;
        }

        public SpriteFlipbookVfxPlayer SpawnSpriteVfx(SpriteFlipbookVfxDefinitionSO definition, Vector3 position, Transform parent)
        {
            Transform actualParent = parent != null ? parent : PreviewVfxRoot;
            var player = SpriteFlipbookVfxPlayer.Spawn(definition, position, definition != null ? definition.defaultScale : 1f, actualParent);
            if (player == null)
                return null;

            ApplyHideFlags(player.gameObject);
            _spawnedPreviewObjects.Add(player.gameObject);
            return player;
        }

        public GameObject SpawnGraphVfx(GraphVfxDefinitionSO definition, Vector3 position, Transform parent)
        {
            if (definition == null || definition.vfxGraphPrefab == null)
                return null;

            var instance = Object.Instantiate(definition.vfxGraphPrefab, position, Quaternion.identity, parent != null ? parent : PreviewVfxRoot);
            ApplyHideFlags(instance);
            _spawnedPreviewObjects.Add(instance);
            return instance;
        }

        public void ClearSpawnedPreviewObjects()
        {
            for (int i = _spawnedPreviewObjects.Count - 1; i >= 0; i--)
            {
                GameObject spawned = _spawnedPreviewObjects[i];
                if (spawned != null)
                    Object.DestroyImmediate(spawned);
            }

            _spawnedPreviewObjects.Clear();
        }

        private void RefreshAnchors()
        {
            _anchors.Clear();
            _anchors.Add(new CombatPreviewAnchorInfo(PresentationAnchorTarget.SelfRoot, "Self Root", Visual != null ? Visual.transform : Root.transform));
            _anchors.Add(new CombatPreviewAnchorInfo(PresentationAnchorTarget.CameraFocus, "Camera Focus", View != null ? View.CameraFocus : null));
            _anchors.Add(new CombatPreviewAnchorInfo(PresentationAnchorTarget.MeleeStop, "Melee Stop", View != null ? View.MeleeStopAnchor : null));
            _anchors.Add(new CombatPreviewAnchorInfo(PresentationAnchorTarget.Impact, "Impact", View != null ? View.ImpactAnchor : null));
            _anchors.Add(new CombatPreviewAnchorInfo(PresentationAnchorTarget.ProjectileOrigin, "Projectile Origin", View != null ? View.ProjectileOriginAnchor : null));
            _anchors.Add(new CombatPreviewAnchorInfo(PresentationAnchorTarget.Barrier, "Barrier", View != null ? View.BarrierAnchor : null));
        }

        private static CombatChainlingVisual EnsureCombatVisual(Transform visualRoot)
        {
            var visual = visualRoot.GetComponentInChildren<CombatChainlingVisual>(true);
            if (visual != null)
                return visual;

            var visualGo = new GameObject("CombatVisual");
            visualGo.transform.SetParent(visualRoot, false);
            visualGo.AddComponent<MeshFilter>();
            visualGo.AddComponent<MeshRenderer>();
            return visualGo.AddComponent<CombatChainlingVisual>();
        }

        private static void EnsurePreviewAnchors(Transform visualRoot)
        {
            FindOrCreateChild(visualRoot, "CameraFocus", new Vector3(0f, 1f, 0f));
            FindOrCreateChild(visualRoot, "MeleeStopAnchor", new Vector3(0f, 0.9f, 0.65f));
            FindOrCreateChild(visualRoot, "ImpactAnchor", new Vector3(0f, 1f, 0f));
            FindOrCreateChild(visualRoot, "ProjectileOriginAnchor", new Vector3(0f, 1f, 0.25f));
            FindOrCreateChild(visualRoot, "BarrierAnchor", new Vector3(0f, 1f, 0.55f));
        }

        private static Transform FindOrCreateChild(Transform parent, string childName, Vector3 localPosition)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
                return existing;

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        public static void StepPreviewParticleSystems(ParticleSystem[] particleSystems, float deltaTime)
        {
            if (particleSystems == null || deltaTime <= 0f)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Simulate(deltaTime, false, false, false);
            }
        }

        public static void StopPreviewParticleSystems(ParticleSystem[] particleSystems)
        {
            if (particleSystems == null)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void InitializePreviewPrefab(GameObject instance, out ParticleSystem[] previewParticleSystems)
        {
            previewParticleSystems = Array.Empty<ParticleSystem>();

            if (instance == null)
                return;

            var moveVfx = instance.GetComponent<CombatMoveVFX>();
            if (moveVfx != null)
            {
                InvokePrivateAwake(moveVfx, CombatMoveVfxAwakeMethod);
                InvokePrivateAwake(moveVfx, CombatMoveVfxOnEnableMethod);
            }

            ApplyPixelBattleApplicators(instance);
            previewParticleSystems = PreparePreviewParticleSystems(instance);
        }

        private static void ApplyPixelBattleApplicators(GameObject instance)
        {
            var applicators = instance.GetComponentsInChildren<PixelBattleVfxApplicator>(true);
            for (int i = 0; i < applicators.Length; i++)
            {
                PixelBattleVfxApplicator applicator = applicators[i];
                if (applicator != null)
                    applicator.Apply();
            }
        }

        private static ParticleSystem[] PreparePreviewParticleSystems(GameObject instance)
        {
            var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(false);
                particleSystem.Simulate(0f, false, true, false);

                if (particleSystem.main.playOnAwake)
                    particleSystem.Play(false);
            }

            return particleSystems;
        }

        private static void InvokePrivateAwake(Component component, MethodInfo method)
        {
            if (component == null || method == null)
                return;

            method.Invoke(component, null);
        }

        private static void ApplyHideFlags(GameObject root)
        {
            if (root == null)
                return;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }
    }
}
