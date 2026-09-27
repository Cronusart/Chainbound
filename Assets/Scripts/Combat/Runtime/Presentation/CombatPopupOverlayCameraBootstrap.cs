using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatPopupOverlayCameraBootstrap : MonoBehaviour
    {
        private const string CombatPopupLayerName = "CombatPopup";
        private const string OverlayCameraName = "CombatPopupOverlayCamera";

        [SerializeField] private Camera baseCamera;
        [SerializeField] private bool initializeOnEnable = true;
        [SerializeField] private bool logWarnings = true;

        private UniversalAdditionalCameraData _baseCameraData;
        private Camera _configuredBaseCamera;
        private Camera _overlayCamera;
        private int _popupLayer = -1;
        private int _originalBaseCullingMask;
        private bool _hasOriginalBaseCullingMask;
        private bool _stackRegistered;
        private bool _missingLayerWarningLogged;
        private bool _missingCameraWarningLogged;
        private bool _missingUrpWarningLogged;

        public Camera OverlayCamera => _overlayCamera;

        private void OnEnable()
        {
            if (initializeOnEnable && Application.isPlaying)
                TryInitialize(false);
        }

        private void LateUpdate()
        {
            if (_overlayCamera != null && _configuredBaseCamera != null)
                SyncOverlayCameraSettings();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        public void Initialize(Camera camera)
        {
            if (camera != null)
                baseCamera = camera;

            TryInitialize(true);
        }

        private bool TryInitialize(bool warnIfUnavailable)
        {
            Camera resolvedBaseCamera = baseCamera != null ? baseCamera : Camera.main;
            if (resolvedBaseCamera == null)
            {
                LogMissingCameraWarning(warnIfUnavailable);
                return false;
            }

            if (_overlayCamera != null && _configuredBaseCamera == resolvedBaseCamera)
            {
                SyncOverlayCameraSettings();
                return true;
            }

            Cleanup();

            _popupLayer = LayerMask.NameToLayer(CombatPopupLayerName);
            if (_popupLayer < 0)
            {
                LogMissingLayerWarning(warnIfUnavailable);
                return false;
            }

            _baseCameraData = resolvedBaseCamera.GetComponent<UniversalAdditionalCameraData>();
            if (_baseCameraData == null)
            {
                LogMissingUrpWarning(warnIfUnavailable, resolvedBaseCamera);
                return false;
            }

            _configuredBaseCamera = resolvedBaseCamera;
            baseCamera = resolvedBaseCamera;
            _originalBaseCullingMask = resolvedBaseCamera.cullingMask;
            _hasOriginalBaseCullingMask = true;

            CreateOverlayCamera();
            RegisterOverlayCamera();
            resolvedBaseCamera.cullingMask &= ~(1 << _popupLayer);
            return true;
        }

        private void CreateOverlayCamera()
        {
            var overlayObject = new GameObject(OverlayCameraName);
            overlayObject.transform.SetParent(_configuredBaseCamera.transform, false);
            overlayObject.transform.localPosition = Vector3.zero;
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;

            _overlayCamera = overlayObject.AddComponent<Camera>();
            SyncOverlayCameraSettings();

            var overlayData = overlayObject.AddComponent<UniversalAdditionalCameraData>();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = false;
            overlayData.renderShadows = false;
        }

        private void RegisterOverlayCamera()
        {
            if (_baseCameraData == null || _overlayCamera == null)
                return;

            if (!_baseCameraData.cameraStack.Contains(_overlayCamera))
                _baseCameraData.cameraStack.Add(_overlayCamera);

            _stackRegistered = true;
        }

        private void SyncOverlayCameraSettings()
        {
            if (_overlayCamera == null || _configuredBaseCamera == null)
                return;

            _overlayCamera.CopyFrom(_configuredBaseCamera);
            _overlayCamera.cullingMask = 1 << _popupLayer;
            _overlayCamera.clearFlags = CameraClearFlags.Nothing;
            _overlayCamera.depth = _configuredBaseCamera.depth;
            _overlayCamera.enabled = true;
        }

        private void Cleanup()
        {
            if (_stackRegistered && _baseCameraData != null && _overlayCamera != null)
                _baseCameraData.cameraStack.Remove(_overlayCamera);

            if (_hasOriginalBaseCullingMask && _configuredBaseCamera != null)
                _configuredBaseCamera.cullingMask = _originalBaseCullingMask;

            if (_overlayCamera != null)
            {
                GameObject overlayObject = _overlayCamera.gameObject;
                _overlayCamera.enabled = false;
                overlayObject.SetActive(false);
                _overlayCamera = null;

                if (Application.isPlaying)
                    Destroy(overlayObject);
                else
                    DestroyImmediate(overlayObject);
            }

            _baseCameraData = null;
            _configuredBaseCamera = null;
            _hasOriginalBaseCullingMask = false;
            _stackRegistered = false;
        }

        private void LogMissingCameraWarning(bool shouldWarn)
        {
            if (!shouldWarn || !logWarnings || _missingCameraWarningLogged)
                return;

            Debug.LogWarning("[CombatPopupOverlayCameraBootstrap] No combat camera is available. Popup overlay camera was not created.");
            _missingCameraWarningLogged = true;
        }

        private void LogMissingLayerWarning(bool shouldWarn)
        {
            if (!shouldWarn || !logWarnings || _missingLayerWarningLogged)
                return;

            Debug.LogWarning($"[CombatPopupOverlayCameraBootstrap] Missing layer '{CombatPopupLayerName}'. Popup overlay camera was not created.");
            _missingLayerWarningLogged = true;
        }

        private void LogMissingUrpWarning(bool shouldWarn, Camera resolvedBaseCamera)
        {
            if (!shouldWarn || !logWarnings || _missingUrpWarningLogged)
                return;

            Debug.LogWarning($"[CombatPopupOverlayCameraBootstrap] Camera '{resolvedBaseCamera.name}' is missing UniversalAdditionalCameraData. Popup overlay camera was not created.");
            _missingUrpWarningLogged = true;
        }
    }
}
