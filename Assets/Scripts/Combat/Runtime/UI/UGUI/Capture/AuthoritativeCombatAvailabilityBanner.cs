using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>Persistent, non-blocking player feedback when an online battle start is gated.</summary>
    [DisallowMultipleComponent]
    public sealed class AuthoritativeCombatAvailabilityBanner : MonoBehaviour
    {
        private CanvasGroup group;
        private TMP_Text label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCreated()
        {
            if (FindFirstObjectByType<AuthoritativeCombatAvailabilityBanner>(FindObjectsInactive.Include) != null)
                return;
            var go = new GameObject("Authoritative Combat Availability Banner", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AuthoritativeCombatAvailabilityBanner));
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.17f, 0.88f);
            rect.anchorMax = new Vector2(0.83f, 0.97f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.22f, 0.035f, 0.04f, 0.96f);
            group = panel.GetComponent<CanvasGroup>();

            var textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 8f);
            textRect.offsetMax = new Vector2(-24f, -8f);
            label = textObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 25f;
            label.color = Color.white;
            label.raycastTarget = false;
            Show(string.Empty);
        }

        private void OnEnable()
        {
            AuthoritativeBattleLaunchContext.GateMessageChanged += Show;
        }

        private void OnDisable()
        {
            AuthoritativeBattleLaunchContext.GateMessageChanged -= Show;
        }

        private void Show(string message)
        {
            bool visible = !string.IsNullOrWhiteSpace(message);
            if (label != null)
                label.text = message ?? string.Empty;
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }
    }
}
