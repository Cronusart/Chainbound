using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Loading screen UI controller with fade, animated graphics, progress bar, and tips.
/// Supports both FullLoading and QuickFade transition styles.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Fade panel (full-screen black overlay)")]
    [SerializeField] private CanvasGroup fadePanel;
    
    [Tooltip("Loading container (center panel with spinner/progress/tips)")]
    [SerializeField] private GameObject loadingContainer;
    
    [Tooltip("Optional CanvasGroup for loading container fade (auto-adds if missing)")]
    [SerializeField] private CanvasGroup loadingContainerCanvasGroup;
    
    [Tooltip("Animated loading graphic image")]
    [SerializeField] private Image loadingImage;
    
    [Tooltip("Frame animator component (auto-finds if null)")]
    [SerializeField] private UIImageFrameAnimator frameAnimator;
    
    [Tooltip("Progress bar slider (optional, only shown for FullLoading)")]
    [SerializeField] private Slider progressBar;
    
    [Tooltip("Progress bar fill image (alternative to slider)")]
    [SerializeField] private Image progressFillImage;
    
    [Tooltip("Tips text (TMP_Text)")]
    [SerializeField] private TextMeshProUGUI tipsText;
    
    [Header("Animation Sets")]
    [Tooltip("Available loading animation sets")]
    [SerializeField] private List<LoadingAnimationSet> animationSets = new List<LoadingAnimationSet>();
    
    public enum AnimationSelectionMode
    {
        RandomEachLoad,
        Sequential
    }
    
    [Tooltip("How to select animation sets")]
    [SerializeField] private AnimationSelectionMode selectionMode = AnimationSelectionMode.RandomEachLoad;
    
    [Header("Tips")]
    [Tooltip("List of loading tips")]
    [SerializeField] private List<string> tips = new List<string>();
    
    [Tooltip("Randomize tip on each load")]
    [SerializeField] private bool randomTipEachLoad = true;
    
    [Tooltip("Rotate tips while loading")]
    [SerializeField] private bool rotateTipsWhileLoading = true;
    
    [Tooltip("Time between tip rotations (seconds)")]
    [Range(1f, 10f)]
    [SerializeField] private float tipRotateIntervalSeconds = 3f;
    
    [Tooltip("Don't repeat tips until all have been shown")]
    [SerializeField] private bool noRepeatUntilAllUsed = true;
    
    [Header("Timing")]
    [Tooltip("Fade in duration")]
    [Range(0.1f, 2f)]
    [SerializeField] private float fadeInDuration = 0.3f;
    
    [Tooltip("Fade out duration")]
    [Range(0.1f, 2f)]
    [SerializeField] private float fadeOutDuration = 0.3f;
    
    [Tooltip("Small delay after scene load before fade-out begins")]
    [Range(0f, 1f)]
    [SerializeField] private float postLoadFadeOutDelay = 0.1f;

    [Header("Startup")]
    [Tooltip("Keep the first Bootstrap frame covered. Disable only for isolated UI preview scenes.")]
    [SerializeField] private bool startCoveredOnAwake = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // State
    private Canvas canvas;
    private RectTransform loadingImageRect;
    private int lastAnimationIndex = -1;
    private int currentAnimationIndex = -1;
    private List<int> usedTipIndices = new List<int>();
    private Coroutine tipRotationCoroutine;
    private Coroutine fadeCoroutine;
    private bool isVisible = false;
    private RectTransform safeAreaRoot;
    private Rect lastSafeArea;
    private Vector2 lastScreenSize;
    private GameObject recoveryPanel;
    private TextMeshProUGUI recoveryMessage;
    private Button recoveryRetryButton;
    private Button recoverySecondaryButton;
    private TextMeshProUGUI recoverySecondaryLabel;
    private string statusOverride = string.Empty;
    
    private void Awake()
    {
        // A zero-scale root makes an otherwise-correct overlay impossible to render.
        // Normalize defensively as well as fixing the authored prefab.
        transform.localScale = Vector3.one;

        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LoadingScreenUI] No Canvas component found!");
            return;
        }
        
        // Ensure canvas is on top
        canvas.sortingOrder = 32000;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        // Auto-find components if not assigned
        if (fadePanel == null)
        {
            fadePanel = GetComponentInChildren<CanvasGroup>();
        }
        
        if (frameAnimator == null && loadingImage != null)
        {
            frameAnimator = loadingImage.GetComponent<UIImageFrameAnimator>();
            if (frameAnimator == null)
            {
                frameAnimator = loadingImage.gameObject.AddComponent<UIImageFrameAnimator>();
            }
        }
        
        if (loadingContainer != null && loadingContainerCanvasGroup == null)
        {
            loadingContainerCanvasGroup = loadingContainer.GetComponent<CanvasGroup>();
            if (loadingContainerCanvasGroup == null)
            {
                loadingContainerCanvasGroup = loadingContainer.AddComponent<CanvasGroup>();
            }
        }
        
        if (loadingImage != null)
        {
            loadingImageRect = loadingImage.GetComponent<RectTransform>();
        }

        EnsureSafeAreaRoot();
        
        if (startCoveredOnAwake)
            ShowImmediate(TransitionStyle.FullLoading);
        else
            SetHiddenImmediate();
        
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(false);
        }
        
        if (progressFillImage != null)
        {
            progressFillImage.gameObject.SetActive(false);
        }
        
        if (tipsText != null)
        {
            tipsText.gameObject.SetActive(startCoveredOnAwake);
        }
    }

    private void Update()
    {
        ApplySafeAreaIfNeeded();
    }

    public void ConfigureTimings(float fadeIn, float fadeOut, float postLoadDelay)
    {
        fadeInDuration = Mathf.Max(0f, fadeIn);
        fadeOutDuration = Mathf.Max(0f, fadeOut);
        postLoadFadeOutDelay = Mathf.Max(0f, postLoadDelay);
    }

    /// <summary>Immediately places an opaque, raycast-blocking cover on screen.</summary>
    public void ShowImmediate(TransitionStyle style)
    {
        StopActiveFade();
        PrepareForShow(style);
        if (fadePanel != null)
        {
            fadePanel.alpha = 1f;
            fadePanel.blocksRaycasts = true;
        }
        if (loadingContainerCanvasGroup != null)
        {
            loadingContainerCanvasGroup.alpha = 1f;
            loadingContainerCanvasGroup.blocksRaycasts = true;
            loadingContainerCanvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// Shows and fully covers the source scene. SceneLoader waits for this routine
    /// and then one rendered end-of-frame before beginning scene IO.
    /// </summary>
    public IEnumerator ShowAndWaitForCover(TransitionStyle style)
    {
        StopActiveFade();
        PrepareForShow(style);

        float startAlpha = fadePanel != null ? fadePanel.alpha : 0f;
        float duration = startAlpha >= 0.999f ? 0f : fadeInDuration;
        yield return FadeCoroutine(startAlpha, 1f, duration, () =>
        {
            SpriteCinematicPlayer.Instance?.ReleaseLoadCover();
        });
    }
    
    /// <summary>
    /// Show loading screen with specified transition style.
    /// </summary>
    public void Show(TransitionStyle style)
    {
        StopActiveFade();
        PrepareForShow(style);
        float startAlpha = fadePanel != null ? fadePanel.alpha : 0f;
        fadeCoroutine = StartCoroutine(FadeCoroutine(startAlpha, 1f, fadeInDuration, () =>
        {
            SpriteCinematicPlayer.Instance?.ReleaseLoadCover();
        }));
    }
    
    /// <summary>
    /// Hide loading screen with fade out.
    /// </summary>
    public void Hide()
    {
        if (!isVisible)
        {
            return;
        }
        
        isVisible = false;
        
        if (enableDebugLogs)
        {
            Debug.Log("[LoadingScreenUI] Hiding loading screen");
        }
        
        // Stop tip rotation
        if (tipRotationCoroutine != null)
        {
            StopCoroutine(tipRotationCoroutine);
            tipRotationCoroutine = null;
        }
        
        // Stop animation
        if (frameAnimator != null)
        {
            frameAnimator.Stop();
        }
        
        // Fade out
        StopActiveFade();
        fadeCoroutine = StartCoroutine(FadeCoroutine(1f, 0f, fadeOutDuration, () =>
        {
            // Hide container after fade
            if (loadingContainer != null)
            {
                loadingContainer.SetActive(false);
            }
        }, postLoadFadeOutDelay));
    }

    public void SetStatus(string message)
    {
        statusOverride = message?.Trim() ?? string.Empty;
        if (tipsText == null || string.IsNullOrEmpty(statusOverride))
            return;

        tipsText.gameObject.SetActive(true);
        tipsText.text = statusOverride;
    }

    public void ClearStatus()
    {
        statusOverride = string.Empty;
    }

    public void ShowRecovery(
        string message,
        System.Action retry,
        string secondaryLabel,
        System.Action secondary)
    {
        StopActiveFade();
        EnsureRecoveryPanel();
        isVisible = true;

        if (loadingContainer != null)
            loadingContainer.SetActive(false);
        if (fadePanel != null)
        {
            fadePanel.alpha = 1f;
            fadePanel.blocksRaycasts = true;
        }

        recoveryPanel.SetActive(true);
        recoveryMessage.text = string.IsNullOrWhiteSpace(message)
            ? "The destination could not be prepared."
            : message;

        recoveryRetryButton.onClick.RemoveAllListeners();
        recoveryRetryButton.onClick.AddListener(() => retry?.Invoke());
        recoverySecondaryButton.onClick.RemoveAllListeners();
        recoverySecondaryButton.onClick.AddListener(() => secondary?.Invoke());
        recoverySecondaryLabel.text = string.IsNullOrWhiteSpace(secondaryLabel) ? "Cancel" : secondaryLabel;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(recoveryRetryButton.gameObject);
    }

    public void HideRecoveryImmediate()
    {
        if (recoveryPanel != null)
            recoveryPanel.SetActive(false);
    }
    
    /// <summary>
    /// Set loading progress (0-1).
    /// </summary>
    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        
        if (progressBar != null && progressBar.gameObject.activeSelf)
        {
            progressBar.value = progress;
        }
        
        if (progressFillImage != null && progressFillImage.gameObject.activeSelf)
        {
            if (progressFillImage.type == Image.Type.Filled)
            {
                progressFillImage.fillAmount = progress;
            }
            else
            {
                // For non-filled images, adjust alpha or use a mask
                Color c = progressFillImage.color;
                c.a = progress;
                progressFillImage.color = c;
            }
        }
    }
    
    /// <summary>
    /// Select random animation set and start playing it.
    /// </summary>
    public void SelectRandomAnimationSetAndPlay()
    {
        if (animationSets == null || animationSets.Count == 0)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[LoadingScreenUI] No animation sets configured!");
            }
            return;
        }
        
        if (frameAnimator == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[LoadingScreenUI] No frame animator found!");
            }
            return;
        }
        
        // Select animation set
        int selectedIndex = 0;
        
        if (selectionMode == AnimationSelectionMode.RandomEachLoad)
        {
            // Random selection with no immediate repeat
            if (animationSets.Count > 1 && lastAnimationIndex >= 0)
            {
                // Reroll until different
                do
                {
                    selectedIndex = Random.Range(0, animationSets.Count);
                } while (selectedIndex == lastAnimationIndex && animationSets.Count > 1);
            }
            else
            {
                selectedIndex = Random.Range(0, animationSets.Count);
            }
        }
        else // Sequential
        {
            selectedIndex = (lastAnimationIndex + 1) % animationSets.Count;
        }
        
        lastAnimationIndex = selectedIndex;
        currentAnimationIndex = selectedIndex;
        
        LoadingAnimationSet selectedSet = animationSets[selectedIndex];
        
        if (enableDebugLogs)
        {
            Debug.Log($"[LoadingScreenUI] Selected animation set '{selectedSet.id}' (index {selectedIndex})");
        }
        
        // Configure animator
        frameAnimator.SetFPS(selectedSet.fps);
        frameAnimator.SetFrames(selectedSet.frames);
        
        // Configure image size if needed
        if (loadingImageRect != null && selectedSet.sizeDelta != Vector2.zero)
        {
            loadingImageRect.sizeDelta = selectedSet.sizeDelta;
        }
        
        // Reset rotation
        if (loadingImageRect != null)
        {
            loadingImageRect.localRotation = Quaternion.identity;
        }
        
        // Start rotation if enabled
        if (selectedSet.enableRotation && loadingImageRect != null)
        {
            StartCoroutine(RotateImageCoroutine(selectedSet.rotationSpeedDegreesPerSecond));
        }
        
        // Play animation
        frameAnimator.Play();
    }
    
    /// <summary>
    /// Select random tip and start rotation coroutine.
    /// </summary>
    public void SelectRandomTipAndStartRotation()
    {
        if (tips == null || tips.Count == 0 || tipsText == null)
        {
            return;
        }
        
        // Select initial tip
        int tipIndex = SelectRandomTipIndex();
        if (tipIndex >= 0 && tipIndex < tips.Count)
        {
            tipsText.text = tips[tipIndex];
        }
        
        // Start rotation if enabled
        if (rotateTipsWhileLoading)
        {
            if (tipRotationCoroutine != null)
            {
                StopCoroutine(tipRotationCoroutine);
            }
            tipRotationCoroutine = StartCoroutine(TipRotationCoroutine());
        }
    }
    
    private int SelectRandomTipIndex()
    {
        if (tips == null || tips.Count == 0)
        {
            return -1;
        }
        
        if (!noRepeatUntilAllUsed)
        {
            return Random.Range(0, tips.Count);
        }
        
        // Track used tips
        if (usedTipIndices.Count >= tips.Count)
        {
            // Reset when all tips used
            usedTipIndices.Clear();
        }
        
        // Find available tips
        List<int> available = new List<int>();
        for (int i = 0; i < tips.Count; i++)
        {
            if (!usedTipIndices.Contains(i))
            {
                available.Add(i);
            }
        }
        
        if (available.Count == 0)
        {
            // Fallback: use all tips
            available.AddRange(new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            for (int i = 0; i < tips.Count; i++)
            {
                if (!available.Contains(i))
                {
                    available.Add(i);
                }
            }
        }
        
        int selected = available[Random.Range(0, available.Count)];
        usedTipIndices.Add(selected);
        
        return selected;
    }
    
    private IEnumerator TipRotationCoroutine()
    {
        while (isVisible && tips != null && tips.Count > 0)
        {
            yield return new WaitForSecondsRealtime(tipRotateIntervalSeconds);
            
            if (!isVisible) break;
            
            if (!string.IsNullOrEmpty(statusOverride))
                continue;

            int tipIndex = SelectRandomTipIndex();
            if (tipIndex >= 0 && tipIndex < tips.Count && tipsText != null)
            {
                tipsText.text = tips[tipIndex];
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[LoadingScreenUI] Rotated tip to index {tipIndex}");
                }
            }
        }
    }
    
    private IEnumerator RotateImageCoroutine(float degreesPerSecond)
    {
        if (loadingImageRect == null) yield break;
        
        while (isVisible && frameAnimator != null && frameAnimator.IsPlaying)
        {
            float deltaTime = Time.unscaledDeltaTime;
            float rotationDelta = degreesPerSecond * deltaTime;
            
            loadingImageRect.Rotate(0f, 0f, rotationDelta);
            
            yield return null;
        }
    }
    
    private IEnumerator FadeCoroutine(float startAlpha, float endAlpha, float duration, System.Action onComplete = null, float delayBeforeFade = 0f)
    {
        if (fadePanel == null) yield break;
        
        bool shouldBlockRaycasts = (Mathf.Max(startAlpha, endAlpha) > 0.5f);
        fadePanel.blocksRaycasts = shouldBlockRaycasts;
        if (loadingContainerCanvasGroup != null)
        {
            loadingContainerCanvasGroup.blocksRaycasts = shouldBlockRaycasts;
            loadingContainerCanvasGroup.interactable = false;
        }
        
        fadePanel.alpha = startAlpha;
        if (loadingContainerCanvasGroup != null)
        {
            loadingContainerCanvasGroup.alpha = startAlpha;
        }
        
        if (delayBeforeFade > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBeforeFade);
        }
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            fadePanel.alpha = alpha;
            if (loadingContainerCanvasGroup != null)
            {
                loadingContainerCanvasGroup.alpha = alpha;
            }
            yield return null;
        }
        
        fadePanel.alpha = endAlpha;
        fadePanel.blocksRaycasts = (endAlpha > 0.5f);
        
        if (loadingContainerCanvasGroup != null)
        {
            loadingContainerCanvasGroup.alpha = endAlpha;
            loadingContainerCanvasGroup.blocksRaycasts = (endAlpha > 0.5f);
            loadingContainerCanvasGroup.interactable = false;
        }
        
        onComplete?.Invoke();
        fadeCoroutine = null;
    }

    private void PrepareForShow(TransitionStyle style)
    {
        isVisible = true;
        statusOverride = string.Empty;
        HideRecoveryImmediate();

        if (canvas == null)
            canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;
        }

        if (loadingContainer != null)
            loadingContainer.SetActive(true);

        bool showDetails = style == TransitionStyle.FullLoading;
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(showDetails);
            progressBar.value = 0f;
        }
        if (progressFillImage != null)
        {
            progressFillImage.gameObject.SetActive(showDetails);
            if (progressFillImage.type == Image.Type.Filled)
                progressFillImage.fillAmount = 0f;
        }
        if (tipsText != null)
        {
            tipsText.gameObject.SetActive(showDetails);
            if (showDetails)
                SelectRandomTipAndStartRotation();
        }

        SelectRandomAnimationSetAndPlay();
    }

    private void SetHiddenImmediate()
    {
        isVisible = false;
        if (fadePanel != null)
        {
            fadePanel.alpha = 0f;
            fadePanel.blocksRaycasts = false;
        }
        if (loadingContainer != null)
            loadingContainer.SetActive(false);
        if (loadingContainerCanvasGroup != null)
        {
            loadingContainerCanvasGroup.alpha = 0f;
            loadingContainerCanvasGroup.blocksRaycasts = false;
            loadingContainerCanvasGroup.interactable = false;
        }
    }

    private void StopActiveFade()
    {
        if (fadeCoroutine == null)
            return;
        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }

    private void EnsureSafeAreaRoot()
    {
        if (safeAreaRoot != null || loadingContainer == null)
            return;

        GameObject safeObject = new GameObject("SafeAreaRoot", typeof(RectTransform));
        safeAreaRoot = safeObject.GetComponent<RectTransform>();
        safeAreaRoot.SetParent(transform, false);
        safeAreaRoot.anchorMin = Vector2.zero;
        safeAreaRoot.anchorMax = Vector2.one;
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;

        loadingContainer.transform.SetParent(safeAreaRoot, false);
        ApplySafeAreaIfNeeded(true);
    }

    private void ApplySafeAreaIfNeeded(bool force = false)
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Vector2 size = new Vector2(Screen.width, Screen.height);
        Rect area = Screen.safeArea;
        if (!force && area == lastSafeArea && size == lastScreenSize)
            return;

        lastSafeArea = area;
        lastScreenSize = size;
        safeAreaRoot.anchorMin = area.position / size;
        safeAreaRoot.anchorMax = (area.position + area.size) / size;
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }

    private void EnsureRecoveryPanel()
    {
        if (recoveryPanel != null)
            return;

        RectTransform parent = safeAreaRoot != null ? safeAreaRoot : transform as RectTransform;
        recoveryPanel = new GameObject("LoadingRecoveryPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = recoveryPanel.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 360f);
        recoveryPanel.GetComponent<Image>().color = new Color(0.035f, 0.04f, 0.06f, 0.97f);

        recoveryMessage = CreateRecoveryText(panelRect, "Message", new Vector2(0f, 70f), new Vector2(680f, 150f), 30f);
        recoveryRetryButton = CreateRecoveryButton(panelRect, "RetryButton", "Retry", new Vector2(-130f, -105f), out _);
        recoverySecondaryButton = CreateRecoveryButton(panelRect, "SecondaryButton", "Cancel", new Vector2(130f, -105f), out recoverySecondaryLabel);
        recoveryPanel.SetActive(false);
    }

    private static TextMeshProUGUI CreateRecoveryText(
        RectTransform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateRecoveryButton(
        RectTransform parent,
        string name,
        string label,
        Vector2 position,
        out TextMeshProUGUI labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(220f, 64f);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.22f, 0.32f, 1f);
        Button button = go.GetComponent<Button>();
        labelText = CreateRecoveryText(rect, "Label", Vector2.zero, rect.sizeDelta, 26f);
        labelText.text = label;
        return button;
    }
    
    /// <summary>
    /// Check if loading screen is currently visible.
    /// </summary>
    public bool IsVisible => isVisible;

    /// <summary>
    /// Total time from <see cref="Hide"/> until the fade-out coroutine should finish
    /// (<see cref="postLoadFadeOutDelay"/> + <see cref="fadeOutDuration"/>).
    /// SceneLoader uses this to wait for the overlay to clear before unlocking input.
    /// </summary>
    public float TotalFadeOutDurationSeconds => postLoadFadeOutDelay + fadeOutDuration;
}
