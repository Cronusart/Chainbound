using System.Collections;
using System.Collections.Generic;
using System;
using Chainbound.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Menu-navigation-local sprite frame animator (same API surface as global UIImageFrameAnimator).
    /// Lives in this assembly so Party Tab code does not depend on Assembly-CSharp UI helpers.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class PartyTabFrameAnimator : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private float fps = 12f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool useUnscaledTime = true;

        private readonly List<Sprite> currentFrames = new List<Sprite>();
        private Coroutine animationCoroutine;
        private bool isPlaying;
        private Action oneShotComplete;

        public event Action LoopCompleted;
        public bool IsPlaying => isPlaying;

        private void Awake()
        {
            EnsureTargetImage();
        }

        public void SetTargetImage(Image image)
        {
            targetImage = image != null ? image : GetComponent<Image>();
        }

        public void Play()
        {
            Play(0f);
        }

        public void Play(float delaySeconds)
        {
            EnsureTargetImage();

            if (currentFrames.Count == 0)
                return;

            Stop();

            // Inactive objects cannot run coroutines — don't leave IsPlaying stuck true.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            isPlaying = true;
            oneShotComplete = null;
            animationCoroutine = StartCoroutine(AnimateCoroutine(Mathf.Max(0f, delaySeconds)));
        }

        public void PlayOneShot(Action onComplete = null, float delaySeconds = 0f)
        {
            EnsureTargetImage();

            if (currentFrames.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            Stop();
            loop = false;
            isPlaying = true;
            oneShotComplete = onComplete;
            animationCoroutine = StartCoroutine(AnimateCoroutine(Mathf.Max(0f, delaySeconds)));
        }

        public void Stop()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            isPlaying = false;
            oneShotComplete = null;
        }

        public void SetFrames(IReadOnlyList<Sprite> frames)
        {
            EnsureTargetImage();
            currentFrames.Clear();
            if (frames == null || frames.Count == 0)
                return;

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] != null)
                    currentFrames.Add(frames[i]);
            }

            if (targetImage != null && currentFrames.Count > 0)
                targetImage.sprite = currentFrames[0];
        }

        public void SetFrames(SpriteAnimationSequence sequence)
        {
            EnsureTargetImage();
            currentFrames.Clear();
            if (sequence == null || sequence.Length == 0)
                return;

            for (int i = 0; i < sequence.Length; i++)
            {
                Sprite sprite = sequence[i];
                if (sprite != null)
                    currentFrames.Add(sprite);
            }

            if (targetImage != null && currentFrames.Count > 0)
                targetImage.sprite = currentFrames[0];
        }

        public void SetFPS(float newFps)
        {
            fps = Mathf.Max(1f, newFps);
        }

        public void SetLoop(bool shouldLoop)
        {
            loop = shouldLoop;
        }

        private void EnsureTargetImage()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();
        }

        private IEnumerator AnimateCoroutine(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(delaySeconds);
                else
                    yield return new WaitForSeconds(delaySeconds);
            }

            if (currentFrames.Count <= 1)
            {
                isPlaying = false;
                animationCoroutine = null;
                oneShotComplete?.Invoke();
                oneShotComplete = null;
                yield break;
            }

            float frameDuration = 1f / fps;
            int currentFrameIndex = 0;

            while (isPlaying)
            {
                if (targetImage != null)
                    targetImage.sprite = currentFrames[currentFrameIndex];

                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(frameDuration);
                else
                    yield return new WaitForSeconds(frameDuration);

                currentFrameIndex++;
                if (currentFrameIndex >= currentFrames.Count)
                {
                    if (loop)
                    {
                        currentFrameIndex = 0;
                        LoopCompleted?.Invoke();
                    }
                    else
                        break;
                }
            }

            isPlaying = false;
            animationCoroutine = null;
            oneShotComplete?.Invoke();
            oneShotComplete = null;
        }

        private void OnDisable()
        {
            Stop();
        }
    }
}
