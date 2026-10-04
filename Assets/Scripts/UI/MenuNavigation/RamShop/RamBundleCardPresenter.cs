using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Plays an authored RAM bundle sheet and a slight unscaled scale pulse.
    /// The pulse lives on a child so focus and press motion can scale the parent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RamBundleCardPresenter : MonoBehaviour
    {
        [SerializeField] private Image artImage;
        [SerializeField] private RectTransform pulseRoot;
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] private int ramAmount;
        [SerializeField] private float framesPerSecond = 8f;
        [SerializeField] private float pulseScale = 1.03f;
        [SerializeField] private float pulseHalfSeconds = 0.9f;
        [SerializeField] private float pulseDelay;

        private Coroutine playback;

        public int RamAmount => ramAmount;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ShowFrame(0);
            playback = StartCoroutine(PlayFrames());
            StartPulse();
        }

        private void OnDisable()
        {
            if (playback != null)
            {
                StopCoroutine(playback);
                playback = null;
            }

            DOTween.Kill(this, false);
            if (pulseRoot != null)
                pulseRoot.localScale = Vector3.one;
        }

        private void ShowFrame(int index)
        {
            if (artImage == null || frames == null || frames.Length == 0)
                return;

            Sprite sprite = frames[index % frames.Length];
            if (sprite != null)
                artImage.sprite = sprite;
        }

        private IEnumerator PlayFrames()
        {
            if (frames == null || frames.Length <= 1)
                yield break;

            float wait = 1f / Mathf.Max(1f, framesPerSecond);
            int index = 0;
            while (isActiveAndEnabled)
            {
                ShowFrame(index);
                yield return new WaitForSecondsRealtime(wait);
                index = (index + 1) % frames.Length;
            }
        }

        private void StartPulse()
        {
            if (pulseRoot == null)
                return;

            pulseRoot.localScale = Vector3.one;
            pulseRoot
                .DOScale(Mathf.Max(1f, pulseScale), Mathf.Max(0.2f, pulseHalfSeconds))
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(Mathf.Max(0f, pulseDelay))
                .SetUpdate(true)
                .SetTarget(this);
        }
    }
}
