using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Combat;
using Chainbound.Combat.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Profile career sheet. The portrait turntable owns the dog sprites.
    /// Stats bind from <see cref="IPlayerStatsProvider"/> and <see cref="GameSessionContext"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProfileMenuScreen : MenuScreen
    {
        public const string ProfileScreenId = "profile";

        [Header("Services")]
        [Tooltip("Optional MonoBehaviour implementing IPlayerStatsProvider.")]
        [SerializeField] private MonoBehaviour playerStatsProviderBehaviour;
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private ProfileShareCopySO shareCopy;
        [SerializeField] private ProfileCardShareService shareService;

        [Header("Card")]
        [SerializeField] private RectTransform profileCardRoot;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text linkChipLabel;
        [SerializeField] private TMP_Text uniqueSpeciesValue;
        [SerializeField] private TMP_Text capturesValue;
        [SerializeField] private TMP_Text entangledValue;
        [SerializeField] private TMP_Text winsValue;
        [SerializeField] private TMP_Text lossesValue;
        [SerializeField] private TMP_Text kosValue;
        [SerializeField] private TMP_Text kodValue;
        [SerializeField] private TMP_Text ramEarnedValue;
        [SerializeField] private TMP_Text ramSpentValue;
        [SerializeField] private TMP_Text rebirthsValue;
        [SerializeField] private TMP_Text starUpsValue;
        [SerializeField] private TMP_Text focusLine;
        [SerializeField] private TMP_Text cosmeticHintLabel;
        [SerializeField] private Button shareButton;

        [Header("Capture Utility")]
        [SerializeField] private GameObject captureRecordDrawer;
        [SerializeField] private TMP_Text captureRecordLabel;
        [SerializeField] private Button captureRecordButton;
        [SerializeField] private Button captureRecordCloseButton;

        private IPlayerStatsProvider statsProvider;
        private int statsRequestVersion;
        private int loadoutRequestVersion;
        private bool shareWired;
        private bool captureUiWired;
        private PlayerStatsSnapshot lastStats;
        private CaptureLoadoutSnapshot lastLoadout;

        public bool HasEmbeddedLayout
        {
            get
            {
                ResolveAuthoredReferencesFromHierarchy();
                return profileCardRoot != null;
            }
        }

        public void ConfigureServices(
            SupabaseConfigSO config,
            MonoBehaviour statsProviderSource = null,
            ProfileShareCopySO copy = null)
        {
            if (config != null)
                supabaseConfig = config;
            if (statsProviderSource != null)
                playerStatsProviderBehaviour = statsProviderSource;
            if (copy != null)
                shareCopy = copy;

            statsProvider = null;
        }

        public override void Show()
        {
            ResolveAuthoredReferencesFromHierarchy();
            WireShareButton();
            EnsureCaptureUtilityUi();
            BindSessionIdentity();
            base.Show();
            Refresh();
        }

        public override void Refresh()
        {
            ResolveAuthoredReferencesFromHierarchy();
            BindSessionIdentity();
            _ = RefreshStatsAsync(++statsRequestVersion);
            _ = RefreshCaptureLoadoutAsync(++loadoutRequestVersion);
        }

        protected override void CollectNavigationSelectables(List<Selectable> order)
        {
            if (captureRecordDrawer != null && captureRecordDrawer.activeSelf)
            {
                if (captureRecordCloseButton != null && MenuSelectableNavigation.IsUsable(captureRecordCloseButton))
                    order.Add(captureRecordCloseButton);
                return;
            }

            ProfileCareerRow[] rows = GetComponentsInChildren<ProfileCareerRow>(false);
            for (int i = 0; i < rows.Length; i++)
            {
                Button button = rows[i] != null ? rows[i].GetComponent<Button>() : null;
                if (button != null && MenuSelectableNavigation.IsUsable(button) && !order.Contains(button))
                    order.Add(button);
            }
        }

        protected override Selectable ResolveDefaultNavigationSelection(List<Selectable> order)
        {
            if (shareButton != null && MenuSelectableNavigation.IsUsable(shareButton))
                return shareButton;

            return base.ResolveDefaultNavigationSelection(order);
        }

        public override bool TryHandleCancel()
        {
            if (captureRecordDrawer == null || !captureRecordDrawer.activeSelf)
                return false;

            SetCaptureRecordVisible(false);
            return true;
        }

        public void ShareCard()
        {
            if (shareService == null)
                shareService = GetComponent<ProfileCardShareService>();

            if (shareService == null)
            {
                Debug.LogWarning("[ProfileMenuScreen] ProfileCardShareService is missing.", this);
                return;
            }

            shareService.Share();
        }

        public void ShowFocusLine(string line)
        {
            if (focusLine == null)
                return;

            focusLine.text = string.IsNullOrWhiteSpace(line) ? "CAREER" : line;
        }

        private void BindSessionIdentity()
        {
            GameSessionContext session = GameSessionContext.Instance;
            string sessionName = session != null ? session.DisplayName : "";
            bool guest = session == null || session.IsGuestSession;

            if (nameLabel != null)
                nameLabel.text = ProfileCardFormatter.FormatDisplayName(sessionName);
            BindLinkChip(guest);
        }

        private void BindLinkChip(bool guest)
        {
            if (linkChipLabel == null)
                return;

            linkChipLabel.text = ProfileCardFormatter.FormatLinkChip(guest);
            linkChipLabel.color = guest ? PlayerMenuUiFactory.Muted : PlayerMenuUiFactory.Accent;
        }

        private void BindStats(PlayerStatsSnapshot stats)
        {
            lastStats = stats;
            GameSessionContext session = GameSessionContext.Instance;
            string sessionName = session != null ? session.DisplayName : "";
            string statsName = stats != null ? stats.displayName : "";
            bool guest = session == null || session.IsGuestSession;

            if (nameLabel != null)
                nameLabel.text = ProfileCardFormatter.FormatDisplayName(sessionName, statsName);
            BindLinkChip(guest);

            SetCount(uniqueSpeciesValue, stats != null ? stats.uniqueSpeciesCaptured : 0);
            SetCount(capturesValue, stats != null ? stats.totalCaptures : 0);
            SetCount(entangledValue, stats != null ? stats.entangledCaptures : 0);
            SetCount(winsValue, stats != null ? stats.battlesWon : 0);
            SetCount(lossesValue, stats != null ? stats.battlesLost : 0);
            SetCount(kosValue, stats != null ? stats.totalKosScored : 0);
            SetCount(kodValue, stats != null ? stats.totalKosTaken : 0);
            SetRam(ramEarnedValue, stats != null ? stats.totalRamEarned : 0L);
            SetRam(ramSpentValue, stats != null ? stats.totalRamSpent : 0L);
            SetCount(rebirthsValue, stats != null ? stats.totalRebirths : 0);
            SetCount(starUpsValue, stats != null ? stats.totalStarUps : 0);

            RefreshCaptureRecordCopy();
        }

        private static void SetCount(TMP_Text label, int value)
        {
            if (label != null)
                label.text = ProfileCardFormatter.FormatCount(value);
        }

        private static void SetRam(TMP_Text label, long value)
        {
            if (label != null)
                label.text = ProfileCardFormatter.FormatRam(value);
        }

        private async Task RefreshCaptureLoadoutAsync(int requestVersion)
        {
            EnsureCaptureUtilityUi();
            AuthoritativeCombatSessionService service = FindFirstObjectByType<AuthoritativeCombatSessionService>(FindObjectsInactive.Include);
            if (service == null || !service.IsRolloutEnabled)
            {
                if (requestVersion == loadoutRequestVersion)
                    BindCaptureLoadout(null);
                return;
            }

            try
            {
                CaptureLoadoutSnapshot snapshot = await service.FetchAsync();
                if (requestVersion == loadoutRequestVersion)
                    BindCaptureLoadout(snapshot);
            }
            catch (Exception exception)
            {
                if (requestVersion != loadoutRequestVersion)
                    return;
                Debug.LogWarning($"[ProfileMenuScreen] Failed to refresh capture loadout: {exception.Message}", this);
                BindCaptureLoadout(null);
            }
        }

        private void BindCaptureLoadout(CaptureLoadoutSnapshot snapshot)
        {
            lastLoadout = snapshot;
            RefreshCaptureRecordCopy();
        }

        private void EnsureCaptureUtilityUi()
        {
            EnsureCaptureRecordDrawer();
            if (captureUiWired)
                return;

            captureRecordButton?.onClick.AddListener(() => SetCaptureRecordVisible(true));
            captureRecordCloseButton?.onClick.AddListener(() => SetCaptureRecordVisible(false));
            captureUiWired = true;
        }

        private void EnsureCaptureRecordDrawer()
        {
            if (captureRecordDrawer != null)
                return;

            Image panel = PlayerMenuUiFactory.CreateImage("CaptureRecordDrawer", transform, new Color(0.025f, 0.055f, 0.09f, 0.98f));
            captureRecordDrawer = panel.gameObject;
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.206f, 0.12f);
            rect.anchorMax = new Vector2(0.794f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.raycastTarget = true;

            captureRecordLabel = PlayerMenuUiFactory.CreateText("CaptureRecordCopy", rect, "", 20f, TextAlignmentOptions.TopLeft, true);
            PlayerMenuUiFactory.Stretch(captureRecordLabel.rectTransform, 28f, 76f, 28f, 28f);

            captureRecordCloseButton = PlayerMenuUiFactory.CreateButton("CloseCaptureRecord", rect, "CLOSE", PlayerMenuUiFactory.PanelAlt);
            PlayerMenuUiFactory.Bottom(captureRecordCloseButton.transform as RectTransform, 48f, 28f, 28f, 18f);
            captureRecordDrawer.SetActive(false);
        }

        private void SetCaptureRecordVisible(bool visible)
        {
            EnsureCaptureUtilityUi();
            if (captureRecordDrawer == null)
                return;
            RefreshCaptureRecordCopy();
            captureRecordDrawer.SetActive(visible);
            if (InputContext != null)
                ConfigureInputContext(InputContext);
            (visible ? captureRecordCloseButton : captureRecordButton)?.Select();
        }

        private void RefreshCaptureRecordCopy()
        {
            if (captureRecordLabel == null)
                return;

            PlayerStatsSnapshot stats = lastStats;
            int attempts = stats != null ? stats.captureAttempts : 0;
            int captures = stats != null ? stats.totalCaptures : 0;
            float rate = attempts > 0 ? captures * 100f / attempts : 0f;
            var copy = new StringBuilder(320);
            copy.AppendLine("CAPTURE RECORD");
            copy.AppendLine($"Captures  {captures} / {attempts} attempts  ({rate:0.0}%)");
            copy.AppendLine($"Orbs used  {(stats != null ? stats.captureOrbsUsed : 0)}    Timeouts  {(stats != null ? stats.captureTimeouts : 0)}    Escaped  {(stats != null ? stats.captureTargetsEscaped : 0)}");
            copy.AppendLine($"Perfect locks  {(stats != null ? stats.capturePerfectLocks : 0)}    Above low HP  {(stats != null ? stats.capturesAboveLowHp : 0)}");
            copy.AppendLine($"Extreme  {(stats != null ? stats.extremeProfileCaptures : 0)}    Entangle Orb  {(stats != null ? stats.entangleOrbCaptures : 0)}");
            int fastest = stats != null ? stats.fastestCaptureMilliseconds : 0;
            copy.AppendLine($"Fastest lock  {(fastest > 0 ? $"{fastest / 1000f:0.00}s" : "—")}");
            copy.AppendLine();
            copy.AppendLine("ACHIEVEMENTS");
            if (stats == null || stats.captureAchievementIds == null || stats.captureAchievementIds.Count == 0)
                copy.AppendLine("No capture achievements yet.");
            else
                for (int i = 0; i < stats.captureAchievementIds.Count; i++)
                    copy.AppendLine("• " + FormatAchievement(stats.captureAchievementIds[i]));

            if (lastLoadout != null && lastLoadout.ActiveBoosts != null && lastLoadout.ActiveBoosts.Count > 0)
            {
                copy.AppendLine();
                copy.AppendLine("ACTIVE BOOSTS");
                for (int i = 0; i < lastLoadout.ActiveBoosts.Count; i++)
                {
                    ActiveCaptureBoost boost = lastLoadout.ActiveBoosts[i];
                    string name = PlayerItemCatalogSeed.TryGet(boost.ItemId, out PlayerItemDefinition definition)
                        ? definition.displayName
                        : boost.ItemId.Replace('_', ' ');
                    copy.AppendLine($"• {name} — until {DateTimeOffset.FromUnixTimeMilliseconds(boost.ExpiresAtUnixMilliseconds).ToLocalTime():t}");
                }
            }
            captureRecordLabel.text = copy.ToString();
        }

        private static string FormatAchievement(string id)
        {
            switch ((id ?? "").Trim().ToLowerInvariant())
            {
                case "first_lock": return "First Lock";
                case "clean_signal": return "Clean Signal";
                case "against_the_noise": return "Against the Noise";
                case "extreme_hunter": return "Extreme Hunter";
                case "entangler": return "Entangler";
                default: return (id ?? "").Replace('_', ' ');
            }
        }

        private async Task RefreshStatsAsync(int requestVersion)
        {
            IPlayerStatsProvider provider = ResolveStatsProvider();
            if (provider == null)
            {
                BindStats(null);
                return;
            }

            try
            {
                GameSessionContext session = GameSessionContext.Instance;
                PlayerStatsSnapshot stats = await provider.FetchStatsAsync(
                    session != null ? session.PlayerProfileId : "",
                    session != null ? session.WalletAddress : "");

                if (requestVersion != statsRequestVersion)
                    return;

                BindStats(stats);
            }
            catch (Exception ex)
            {
                if (requestVersion != statsRequestVersion)
                    return;

                Debug.LogWarning($"[ProfileMenuScreen] Failed to refresh profile stats: {ex.Message}", this);
                BindStats(null);
            }
        }

        private IPlayerStatsProvider ResolveStatsProvider()
        {
            if (statsProvider != null)
                return statsProvider;

            if (playerStatsProviderBehaviour is IPlayerStatsProvider assigned)
                return statsProvider = assigned;

            MonoBehaviour[] localBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < localBehaviours.Length; i++)
            {
                if (localBehaviours[i] is IPlayerStatsProvider localProvider)
                    return statsProvider = localProvider;
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerStatsProvider provider)
                    return statsProvider = provider;
            }

            if (supabaseConfig == null)
                return null;

            SupabasePlayerStatsProvider supabaseProvider = gameObject.AddComponent<SupabasePlayerStatsProvider>();
            supabaseProvider.Configure(supabaseConfig);
            playerStatsProviderBehaviour = supabaseProvider;
            return statsProvider = supabaseProvider;
        }

        private void WireShareButton()
        {
            if (shareWired || shareButton == null)
                return;

            shareButton.onClick.RemoveListener(ShareCard);
            shareButton.onClick.AddListener(ShareCard);
            shareWired = true;
        }

        private void ResolveAuthoredReferencesFromHierarchy()
        {
            if (profileCardRoot == null)
            {
                Transform sheet = transform.Find("ProfileSheet");
                if (sheet != null)
                    profileCardRoot = sheet as RectTransform;
            }

            if (nameLabel == null)
                nameLabel = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/NameLabel");
            if (linkChipLabel == null)
                linkChipLabel = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/ChipLabel");
            if (uniqueSpeciesValue == null)
                uniqueSpeciesValue = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/UniqueSpecies/Value");
            if (capturesValue == null)
                capturesValue = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/Captures/Value");
            if (entangledValue == null)
                entangledValue = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/Entangled/Value");
            if (winsValue == null)
                winsValue = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/Wins/Value");
            if (lossesValue == null)
                lossesValue = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/Losses/Value");
            if (kosValue == null)
                kosValue = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/Kos/Value");
            if (kodValue == null)
                kodValue = FindChildComponent<TMP_Text>("ProfileSheet/TopCenter/Kod/Value");
            if (ramEarnedValue == null)
                ramEarnedValue = FindChildComponent<TMP_Text>("ProfileSheet/TopRight/RamEarned/Value");
            if (ramSpentValue == null)
                ramSpentValue = FindChildComponent<TMP_Text>("ProfileSheet/TopRight/RamSpent/Value");
            if (rebirthsValue == null)
                rebirthsValue = FindChildComponent<TMP_Text>("ProfileSheet/TopRight/Rebirths/Value");
            if (starUpsValue == null)
                starUpsValue = FindChildComponent<TMP_Text>("ProfileSheet/TopRight/StarUps/Value");
            if (focusLine == null)
                focusLine = FindChildComponent<TMP_Text>("ProfileSheet/DialogueLowerCenter/FocusLine");
            if (cosmeticHintLabel == null)
                cosmeticHintLabel = FindChildComponent<TMP_Text>("ProfileSheet/TopLeft/CosmeticHintLabel");
            if (shareButton == null)
            {
                Transform share = transform.Find("ProfileSheet/DialogueLowerCenter/ShareButton");
                if (share != null)
                    shareButton = share.GetComponent<Button>();
            }

            if (captureRecordButton == null)
            {
                Transform captures = transform.Find("ProfileSheet/TopCenter/Captures");
                if (captures != null)
                    captureRecordButton = captures.GetComponent<Button>();
            }

            if (shareService == null)
                shareService = GetComponent<ProfileCardShareService>();
        }

        private T FindChildComponent<T>(string path) where T : Component
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }
    }
}
