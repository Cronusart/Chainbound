using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Keeps <see cref="MobileHudCanvas"/> in <see cref="RenderMode.ScreenSpaceCamera"/> with a valid
    /// plane distance / sorting order and binds <see cref="Canvas.worldCamera"/> once the gameplay
    /// camera exists (DDOL UI often wakes before scene cameras; rig cameras may be untagged).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class MobileHudScreenSpaceCameraSetup : MonoBehaviour
    {
        [Header("Canvas (Screen Space — Camera)")]
        [Tooltip("Distance from the camera along its forward axis. Must be between near and far clip planes.")]
        [SerializeField] private float planeDistance = 100f;

        [Tooltip("Draw above typical gameplay UI layers.")]
        [SerializeField] private int sortingOrder = 5000;

        [Header("Optional")]
        [Tooltip("If set, used instead of auto-detection.")]
        [SerializeField] private Camera overrideCamera;

        private Canvas targetCanvas;
        private bool boundCamera;

        private void Awake()
        {
            targetCanvas = GetComponent<Canvas>();
            EnsureUiLayerWhenUsingGameplayCamera();
            ApplyCanvasDefaults();
        }

        /// <summary>
        /// Main gameplay cameras often omit layer 0 (Default) from the culling mask; HUD must use a
        /// rendered layer (e.g. UI) or it will not appear in Screen Space — Camera mode.
        /// </summary>
        private void EnsureUiLayerWhenUsingGameplayCamera()
        {
            if (targetCanvas == null || targetCanvas.renderMode != RenderMode.ScreenSpaceCamera)
                return;

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0)
                return;

            if (gameObject.layer == 0)
                gameObject.layer = uiLayer;
        }

        private void OnEnable()
        {
            TryBindCamera();
        }

        private void LateUpdate()
        {
            if (boundCamera && targetCanvas != null && targetCanvas.worldCamera != null)
                return;
            TryBindCamera();
        }

        private void ApplyCanvasDefaults()
        {
            if (targetCanvas == null)
                return;

            if (targetCanvas.renderMode != RenderMode.ScreenSpaceCamera)
                return;

            if (planeDistance > 0f && targetCanvas.planeDistance <= 0f)
                targetCanvas.planeDistance = planeDistance;

            if (targetCanvas.sortingOrder < 0)
                targetCanvas.sortingOrder = sortingOrder;
        }

        private void TryBindCamera()
        {
            if (targetCanvas == null || targetCanvas.renderMode != RenderMode.ScreenSpaceCamera)
                return;

            ApplyCanvasDefaults();

            if (overrideCamera != null)
            {
                targetCanvas.worldCamera = overrideCamera;
                boundCamera = true;
                return;
            }

            if (targetCanvas.worldCamera != null)
            {
                boundCamera = true;
                return;
            }

            Camera resolved = ResolveGameplayCamera();
            if (resolved != null)
            {
                targetCanvas.worldCamera = resolved;
                boundCamera = true;
            }
        }

        private static Camera ResolveGameplayCamera()
        {
            if (Camera.main != null)
                return Camera.main;

            var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera c = cameras[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                if (c.gameObject.name == "MainCamera")
                    return c;
            }

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera c = cameras[i];
                if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                    return c;
            }

            return null;
        }
    }
}
