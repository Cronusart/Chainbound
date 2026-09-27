using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

namespace Chainbound.Combat
{
    /// <summary>
    /// Swaps two scanline textures at a fixed rate to make the wallet screen feel alive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalletScanlinesAnimator : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private string scanlinesElementName = "Scanlines";
        [SerializeField] private Texture2D frameA;
        [SerializeField] private Texture2D frameB;
        [SerializeField] private float fps = 2f;
        [SerializeField] private int bindRetryWarningAfterFrames = 180;
        [SerializeField] private bool logBindingWarnings = true;

        private Image _scanlinesImage;
        private IVisualElementScheduledItem _scheduledSwap;
        private Coroutine _bindRoutine;
        private bool _showA = true;

        private void OnEnable()
        {
            StopBindRoutine();
            _bindRoutine = StartCoroutine(BindAndStartRoutine());
        }

        private void OnDisable()
        {
            StopBindRoutine();
            StopAnimation();
        }

        private void OnValidate()
        {
            fps = Mathf.Max(0f, fps);
            bindRetryWarningAfterFrames = Mathf.Max(1, bindRetryWarningAfterFrames);
        }

        private void CacheScanlinesImage()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            var root = document != null ? document.rootVisualElement : null;
            _scanlinesImage = root?.Q<Image>(scanlinesElementName);
        }

        private IEnumerator BindAndStartRoutine()
        {
            int attempts = 0;
            bool warned = false;

            while (isActiveAndEnabled)
            {
                CacheScanlinesImage();
                if (_scanlinesImage != null)
                {
                    StartAnimation();
                    _bindRoutine = null;
                    yield break;
                }

                attempts++;
                if (!warned && logBindingWarnings && attempts >= bindRetryWarningAfterFrames)
                {
                    string docName = document != null ? document.name : "null";
                    Debug.LogWarning(
                        $"[WalletScanlinesAnimator] Could not find scanlines element '{scanlinesElementName}' " +
                        $"after {bindRetryWarningAfterFrames} frames on UIDocument '{docName}'. " +
                        "Will keep retrying.");
                    warned = true;
                }

                yield return null;
            }

            _bindRoutine = null;
        }

        private void StartAnimation()
        {
            StopAnimation();

            if (_scanlinesImage == null)
                return;

            if (frameA == null && frameB == null)
                return;

            _showA = true;
            ApplyCurrentFrame();

            if (fps <= 0f || frameA == null || frameB == null)
                return;

            int intervalMs = Mathf.Max(1, Mathf.RoundToInt(1000f / fps));
            _scheduledSwap = _scanlinesImage.schedule.Execute(SwapFrame).Every(intervalMs);
        }

        private void StopAnimation()
        {
            _scheduledSwap?.Pause();
            _scheduledSwap = null;
        }

        private void StopBindRoutine()
        {
            if (_bindRoutine == null)
                return;

            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        private void SwapFrame()
        {
            _showA = !_showA;
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            if (_scanlinesImage == null)
                return;

            if (frameA != null && frameB != null)
            {
                _scanlinesImage.image = _showA ? frameA : frameB;
                return;
            }

            _scanlinesImage.image = frameA != null ? frameA : frameB;
        }
    }
}
