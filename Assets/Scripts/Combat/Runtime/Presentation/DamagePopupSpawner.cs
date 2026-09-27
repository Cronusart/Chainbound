using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class DamagePopupSpawner : MonoBehaviour
    {
        [SerializeField] private DamagePopup popupPrefab;
        [Tooltip("Inactive pool parent only. Active popups play in world space at the resolved slot anchor.")]
        [SerializeField] private Transform popupRoot;
        [Tooltip("Optional parent for active popups. Leave empty to play at the scene root. Do not assign the pool root.")]
        [SerializeField] private Transform worldPlayRoot;
        [SerializeField] private int initialPoolSize = 8;
        [SerializeField] private CombatUiAnimTuning uiAnimTuning;
        [SerializeField] private CombatPopupVisualCatalogSO visualCatalog;
        [SerializeField] private Camera renderCamera;

        private const string CombatPopupLayerName = "CombatPopup";
        private static int s_combatPopupLayer = int.MinValue;
        private static bool s_missingCombatPopupLayerWarningLogged;

        private readonly Queue<DamagePopup> _pool = new Queue<DamagePopup>(16);

        private void Awake()
        {
            ResolveFallbacks();
            Prewarm();
        }

        public void Spawn(CombatPopupPlayRequest request)
        {
            ResolveFallbacks();
            var popup = GetFromPool();
            if (popup == null)
                return;

            CombatPopupVisualProfileSO profile = request.profileOverride;
            if (profile == null && visualCatalog != null)
                profile = visualCatalog.ResolveProfile(request.category);

            PreparePopupForPlay(popup);
            popup.Play(request, profile, ResolveRenderCamera(), ReturnToPool);
        }

        public void Spawn(Vector3 worldPosition, int amount, bool isHeal, bool isCrit = false, string impactText = null)
        {
            CombatPopupCategory category = CombatPopupLegacyMapper.ResolveLegacyCategory(amount, isHeal, isCrit, impactText);
            CombatPopupPlayRequest request = string.IsNullOrEmpty(impactText)
                ? CombatPopupPlayRequest.ForAmount(category, worldPosition, amount, isHeal)
                : CombatPopupPlayRequest.ForLabel(category, worldPosition, impactText);

            if (visualCatalog == null)
            {
                ResolveFallbacks();
                var popup = GetFromPool();
                if (popup == null)
                    return;
                PreparePopupForPlay(popup);
                popup.Play(ApplyYOffset(worldPosition), amount, isHeal, isCrit, impactText, uiAnimTuning, ResolveRenderCamera(), ReturnToPool);
                return;
            }

            Spawn(request);
        }

        public void SpawnImpact(Vector3 worldPosition, string impactText)
        {
            if (string.IsNullOrEmpty(impactText))
                return;

            Spawn(worldPosition, 0, false, false, impactText);
        }

        /// <summary>Spawn a label popup at an explicit world anchor (e.g. barrier anchor).</summary>
        public void SpawnAtAnchor(Transform anchor, string impactText, int amount = 0, bool isHeal = false, bool isCrit = false)
        {
            if (anchor == null)
                return;

            Spawn(anchor.position, amount, isHeal, isCrit, impactText);
        }

        public void SpawnAtAnchor(Transform anchor, CombatPopupPlayRequest request)
        {
            if (anchor == null)
                return;

            request.worldPosition = anchor.position;
            Spawn(request);
        }

        private Vector3 ApplyYOffset(Vector3 worldPosition)
        {
            float yOffset = uiAnimTuning != null ? Mathf.Max(0f, uiAnimTuning.popupWorldYOffset) : 1.15f;
            return worldPosition + Vector3.up * yOffset;
        }

        private void Prewarm()
        {
            int count = Mathf.Max(1, initialPoolSize);
            for (int i = _pool.Count; i < count; i++)
            {
                var popup = CreatePopup();
                if (popup != null)
                    _pool.Enqueue(popup);
            }
        }

        private DamagePopup GetFromPool()
        {
            if (_pool.Count == 0)
            {
                var created = CreatePopup();
                if (created == null)
                    return null;
                _pool.Enqueue(created);
            }

            var popup = _pool.Dequeue();
            if (popup == null)
                return null;

            popup.gameObject.SetActive(true);
            return popup;
        }

        private void ReturnToPool(DamagePopup popup)
        {
            if (popup == null)
                return;

            popup.gameObject.SetActive(false);
            Transform poolParent = popupRoot != null ? popupRoot : transform;
            popup.transform.SetParent(poolParent, false);
            popup.transform.localPosition = Vector3.zero;
            popup.transform.localRotation = Quaternion.identity;
            _pool.Enqueue(popup);
        }

        private void PreparePopupForPlay(DamagePopup popup)
        {
            if (popup == null)
                return;

            Transform poolParent = popupRoot != null ? popupRoot : transform;
            if (popup.transform.parent == poolParent)
                popup.transform.SetParent(worldPlayRoot, true);
        }

        public void Configure(Camera camera, CombatPopupVisualCatalogSO catalogOverride = null)
        {
            if (camera != null)
                renderCamera = camera;
            if (catalogOverride != null)
                visualCatalog = catalogOverride;
        }

        private Camera ResolveRenderCamera()
        {
            if (renderCamera != null)
                return renderCamera;

            renderCamera = Camera.main;
            return renderCamera;
        }

        private DamagePopup CreatePopup()
        {
            var parent = popupRoot != null ? popupRoot : transform;
            DamagePopup popup;

            if (popupPrefab != null)
            {
                popup = Instantiate(popupPrefab, parent);
            }
            else
            {
                var go = new GameObject("DamagePopup", typeof(DamagePopup));
                go.transform.SetParent(parent, false);

                var textGo = new GameObject("Text", typeof(TextMeshPro));
                textGo.transform.SetParent(go.transform, false);
                var text = textGo.GetComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = 2.8f;
                text.outlineWidth = 0.2f;

                var spriteGo = new GameObject("Sprite", typeof(SpriteRenderer));
                spriteGo.transform.SetParent(go.transform, false);
                spriteGo.SetActive(false);

                popup = go.GetComponent<DamagePopup>();
            }

            if (popup != null)
            {
                ApplyCombatPopupLayer(popup.gameObject);
                popup.gameObject.SetActive(false);
            }
            return popup;
        }

        private static void ApplyCombatPopupLayer(GameObject root)
        {
            if (root == null)
                return;

            int layer = ResolveCombatPopupLayer();
            if (layer < 0)
                return;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in transforms)
                child.gameObject.layer = layer;
        }

        private static int ResolveCombatPopupLayer()
        {
            if (s_combatPopupLayer != int.MinValue)
                return s_combatPopupLayer;

            s_combatPopupLayer = LayerMask.NameToLayer(CombatPopupLayerName);
            if (s_combatPopupLayer < 0 && !s_missingCombatPopupLayerWarningLogged)
            {
                Debug.LogWarning($"[DamagePopupSpawner] Missing layer '{CombatPopupLayerName}'. Popups will render on their prefab layer until the layer exists.");
                s_missingCombatPopupLayerWarningLogged = true;
            }

            return s_combatPopupLayer;
        }

        private void ResolveFallbacks()
        {
            if (popupRoot == null)
                popupRoot = transform;
            if (worldPlayRoot == popupRoot)
                worldPlayRoot = null;
            if (uiAnimTuning == null)
                uiAnimTuning = GetComponentInParent<CombatUiAnimTuning>(true);
            if (uiAnimTuning == null)
                uiAnimTuning = FindObjectOfType<CombatUiAnimTuning>(true);
            if (visualCatalog == null)
                visualCatalog = CombatPopupRuntimeDefaults.TryLoadVisualCatalog();
            if (popupPrefab == null)
                popupPrefab = CombatPopupRuntimeDefaults.TryLoadPopupPrefab();
            if (renderCamera == null)
                renderCamera = Camera.main;
        }
    }
}
