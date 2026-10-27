using System.Collections;
using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Hides menu chrome and HUD, captures <see cref="cardRoot"/> at 1920x1080, then shares the PNG.
    /// Chrome and the HUD token are always restored, including on failure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProfileCardShareService : MonoBehaviour
    {
        public const int CaptureWidth = 1920;
        public const int CaptureHeight = 1080;
        public const string HudTokenReason = "profile_share";

        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private ProfilePortraitTurntable portrait;
        [SerializeField] private Button shareButton;
        [SerializeField] private GameObject[] hideDuringCapture;
        [SerializeField] private ProfileShareCopySO shareCopy;
        [SerializeField] private int captureWidth = CaptureWidth;
        [SerializeField] private int captureHeight = CaptureHeight;

        private bool sharing;
        private Coroutine shareRoutine;

        public void Configure(RectTransform root, Button button, ProfileShareCopySO copy, params GameObject[] extraHide)
        {
            if (root != null)
                cardRoot = root;
            if (button != null)
                shareButton = button;
            if (copy != null)
                shareCopy = copy;
            if (extraHide != null && extraHide.Length > 0)
                hideDuringCapture = extraHide;
        }

        public void Share()
        {
            if (sharing)
                return;

            if (shareRoutine != null)
                StopCoroutine(shareRoutine);

            shareRoutine = StartCoroutine(ShareRoutine());
        }

        private IEnumerator ShareRoutine()
        {
            sharing = true;
            bool shareInteractable = shareButton != null && shareButton.interactable;
            if (shareButton != null)
                shareButton.interactable = false;

            ChromeSnapshot chrome = default;
            bool[] extraHideWasActive = null;
            MobileHudToken hudToken = default;
            Texture2D captured = null;

            try
            {
                chrome = HideMenuChrome();
                extraHideWasActive = HideExtra();
                if (portrait == null)
                    portrait = GetComponentInChildren<ProfilePortraitTurntable>(true);
                portrait?.FreezeOnDown();

                MobileHudService hud = MobileHudService.Instance;
                if (hud != null)
                    hudToken = hud.AcquireContext(MobileHudContext.Hidden, HudTokenReason);

                yield return new WaitForEndOfFrame();

                captured = CaptureCard();
                if (captured == null)
                {
                    Debug.LogWarning("[ProfileCardShareService] Card capture failed.", this);
                    yield break;
                }

                byte[] png = captured.EncodeToPNG();
                string path = ProfileCardNativeShare.WritePng(png);
                string text = ProfileCardFormatter.FormatShareCopy(
                    shareCopy != null ? shareCopy.Template : ProfileCardFormatter.DefaultShareTemplate,
                    ResolveShareName());

                if (!ProfileCardNativeShare.TrySharePng(path, text, out string error) &&
                    !string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning($"[ProfileCardShareService] Share failed: {error}", this);
                }
            }
            finally
            {
                if (captured != null)
                    Destroy(captured);

                RestoreExtra(extraHideWasActive);
                RestoreMenuChrome(chrome);

                if (hudToken.IsValid)
                    hudToken.Release();

                if (shareButton != null)
                    shareButton.interactable = shareInteractable;

                portrait?.Resume();
                sharing = false;
                shareRoutine = null;
            }
        }

        private string ResolveShareName()
        {
            GameSessionContext session = GameSessionContext.Instance;
            return session != null ? session.DisplayName : "";
        }

        private ChromeSnapshot HideMenuChrome()
        {
            PlayerMenuController menu = GetComponentInParent<PlayerMenuController>();
            ChromeSnapshot snapshot = default;
            if (menu == null)
                return snapshot;

            menu.ResolveShareCaptureChrome(
                out snapshot.closeButton,
                out snapshot.footerHints,
                out snapshot.hub,
                out snapshot.background);

            snapshot.closeWasActive = snapshot.closeButton != null && snapshot.closeButton.activeSelf;
            snapshot.footerWasActive = snapshot.footerHints != null && snapshot.footerHints.activeSelf;
            snapshot.hubWasActive = snapshot.hub != null && snapshot.hub.activeSelf;
            snapshot.backgroundWasActive = snapshot.background != null && snapshot.background.activeSelf;
            menu.SetShareCaptureChromeVisible(false);
            return snapshot;
        }

        private static void RestoreMenuChrome(ChromeSnapshot snapshot)
        {
            SetActive(snapshot.closeButton, snapshot.closeWasActive);
            SetActive(snapshot.footerHints, snapshot.footerWasActive);
            SetActive(snapshot.hub, snapshot.hubWasActive);
            SetActive(snapshot.background, snapshot.backgroundWasActive);
        }

        private bool[] HideExtra()
        {
            if (hideDuringCapture == null || hideDuringCapture.Length == 0)
                return null;

            bool[] wasActive = new bool[hideDuringCapture.Length];
            for (int i = 0; i < hideDuringCapture.Length; i++)
            {
                GameObject target = hideDuringCapture[i];
                wasActive[i] = target != null && target.activeSelf;
                if (target != null)
                    target.SetActive(false);
            }

            return wasActive;
        }

        private void RestoreExtra(bool[] wasActive)
        {
            if (hideDuringCapture == null || wasActive == null)
                return;

            int count = Mathf.Min(hideDuringCapture.Length, wasActive.Length);
            for (int i = 0; i < count; i++)
            {
                if (hideDuringCapture[i] != null)
                    hideDuringCapture[i].SetActive(wasActive[i]);
            }
        }

        private Texture2D CaptureCard()
        {
            if (cardRoot == null)
                return null;

            Canvas.ForceUpdateCanvases();
            Canvas canvas = cardRoot.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector3[] corners = new Vector3[4];
            cardRoot.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);

            int x = Mathf.FloorToInt(bottomLeft.x);
            int y = Mathf.FloorToInt(bottomLeft.y);
            int width = Mathf.CeilToInt(topRight.x - bottomLeft.x);
            int height = Mathf.CeilToInt(topRight.y - bottomLeft.y);
            if (width <= 0 || height <= 0)
                return null;

            x = Mathf.Clamp(x, 0, Screen.width - 1);
            y = Mathf.Clamp(y, 0, Screen.height - 1);
            if (x + width > Screen.width)
                width = Screen.width - x;
            if (y + height > Screen.height)
                height = Screen.height - y;
            if (width <= 0 || height <= 0)
                return null;

            Texture2D source = new Texture2D(width, height, TextureFormat.RGBA32, false);
            source.ReadPixels(new Rect(x, y, width, height), 0, 0);
            source.Apply();

            int outWidth = Mathf.Max(1, captureWidth);
            int outHeight = Mathf.Max(1, captureHeight);
            if (source.width == outWidth && source.height == outHeight)
                return source;

            Texture2D scaled = ScaleTexture(source, outWidth, outHeight);
            Destroy(source);
            return scaled;
        }

        private static Texture2D ScaleTexture(Texture2D source, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private struct ChromeSnapshot
        {
            public GameObject closeButton;
            public GameObject footerHints;
            public GameObject hub;
            public GameObject background;
            public bool closeWasActive;
            public bool footerWasActive;
            public bool hubWasActive;
            public bool backgroundWasActive;
        }
    }
}
