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
    /// Landscape player ID card for the Profile tab. Dog art is a serialized sprite;
    /// stats bind from <see cref="IPlayerStatsProvider"/> and <see cref="GameSessionContext"/>.
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
        [SerializeField] private Image dogImage;
        [SerializeField] private Sprite dogSprite;
        [SerializeField] private GameObject hatSlot;
        [SerializeField] private GameObject outfitSlot;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text linkChipLabel;
        [SerializeField] private TMP_Text cardTitleLabel;
        [SerializeField] private TMP_Text uniqueSpeciesLabel;
        [SerializeField] private TMP_Text capturesLabel;
        [SerializeField] private TMP_Text stampedMovesLabel;
        [SerializeField] private TMP_Text recordLabel;
        [SerializeField] private TMP_Text cosmeticHintLabel;
        [SerializeField] private Button shareButton;
        [SerializeField] private string defaultTitle = "New Signal";

        [Header("Capture Utility")]
        [SerializeField] private TMP_Text headUtilityLabel;
        [SerializeField] private TMP_Text bodyUtilityLabel;
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
            ApplyStaticArt();
            BindSessionIdentity();
            base.Show();
            Refresh();
        }

        public override void Refresh()
        {
            ResolveAuthoredReferencesFromHierarchy();
            ApplyStaticArt();
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

            if (captureRecordButton != null && MenuSelectableNavigation.IsUsable(captureRecordButton))
                order.Add(captureRecordButton);
            if (shareButton != null && MenuSelectableNavigation.IsUsable(shareButton) && !order.Contains(shareButton))
                order.Add(shareButton);
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

        private void ApplyStaticArt()
        {
            if (dogImage != null && dogSprite != null)
                dogImage.sprite = dogSprite;

            EnsureCaptureUtilityUi();
            BindUtilitySlot(hatSlot, headUtilityLabel, "HEAD", lastLoadout != null ? lastLoadout.HeadItemId : "");
            BindUtilitySlot(outfitSlot, bodyUtilityLabel, "BODY", lastLoadout != null ? lastLoadout.BodyItemId : "");
        }

        private void BindSessionIdentity()
        {
            GameSessionContext session = GameSessionContext.Instance;
            string sessionName = session != null ? session.DisplayName : "";
            bool guest = session == null || session.IsGuestSession;

            if (nameLabel != null)
                nameLabel.text = ProfileCardFormatter.FormatDisplayName(sessionName);
            BindLinkChip(guest);
            if (cardTitleLabel != null)
                cardTitleLabel.text = string.IsNullOrWhiteSpace(defaultTitle) ? "New Signal" : defaultTitle;
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

            int unique = stats != null ? stats.uniqueSpeciesCaptured : 0;
            int captures = stats != null ? stats.totalCaptures : 0;
            int entangled = stats != null ? stats.entangledCaptures : 0;
            int moves = stats != null ? stats.totalStampedMovesCreated : 0;
            int won = stats != null ? stats.battlesWon : 0;
            int lost = stats != null ? stats.battlesLost : 0;

            if (uniqueSpeciesLabel != null)
                uniqueSpeciesLabel.text = ProfileCardFormatter.FormatUniqueSpecies(unique);
            if (capturesLabel != null)
                capturesLabel.text = ProfileCardFormatter.FormatCaptures(captures, entangled) + "   [VIEW RECORD]";
            if (stampedMovesLabel != null)
                stampedMovesLabel.text = ProfileCardFormatter.FormatStampedMoves(moves);

            string record = ProfileCardFormatter.FormatRecord(won, lost);
            if (recordLabel != null)
            {
                recordLabel.text = record;
                recordLabel.gameObject.SetActive(!string.IsNullOrEmpty(record));
            }

            RefreshCaptureRecordCopy();
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
            BindUtilitySlot(hatSlot, headUtilityLabel, "HEAD", snapshot != null ? snapshot.HeadItemId : "");
            BindUtilitySlot(outfitSlot, bodyUtilityLabel, "BODY", snapshot != null ? snapshot.BodyItemId : "");
            RefreshCaptureRecordCopy();
        }

        private static void BindUtilitySlot(GameObject slot, TMP_Text label, string slotName, string itemId)
        {
            if (slot == null || label == null)
                return;

            slot.SetActive(true);
            string itemName = "EMPTY";
            if (!string.IsNullOrWhiteSpace(itemId) && PlayerItemCatalogSeed.TryGet(itemId, out PlayerItemDefinition definition))
                itemName = definition.displayName;
            else if (!string.IsNullOrWhiteSpace(itemId))
                itemName = itemId.Replace('_', ' ').ToUpperInvariant();
            label.text = $"{slotName}\n{itemName}";
        }

        private void EnsureCaptureUtilityUi()
        {
            if (hatSlot != null)
                headUtilityLabel = EnsureUtilitySlotLabel(hatSlot, headUtilityLabel, "HeadUtilityLabel");
            if (outfitSlot != null)
                bodyUtilityLabel = EnsureUtilitySlotLabel(outfitSlot, bodyUtilityLabel, "BodyUtilityLabel");

            if (captureRecordButton == null && capturesLabel != null)
            {
                capturesLabel.raycastTarget = true;
                captureRecordButton = capturesLabel.GetComponent<Button>();
                if (captureRecordButton == null)
                    captureRecordButton = capturesLabel.gameObject.AddComponent<Button>();
                captureRecordButton.targetGraphic = capturesLabel;
            }

            EnsureCaptureRecordDrawer();
            if (!captureUiWired)
            {
                captureRecordButton?.onClick.AddListener(() => SetCaptureRecordVisible(true));
                captureRecordCloseButton?.onClick.AddListener(() => SetCaptureRecordVisible(false));
                captureUiWired = true;
            }
        }

        private static TMP_Text EnsureUtilitySlotLabel(GameObject slot, TMP_Text current, string labelName)
        {
            if (current != null)
                return current;

            Image background = slot.GetComponent<Image>();
            if (background == null)
                background = slot.AddComponent<Image>();
            background.color = new Color(0.03f, 0.08f, 0.12f, 0.82f);
            background.raycastTarget = false;
            TMP_Text label = PlayerMenuUiFactory.CreateText(labelName, slot.transform, "", 15f, TextAlignmentOptions.Center, true);
            PlayerMenuUiFactory.Stretch(label.rectTransform, 4f, 4f, 4f, 4f);
            label.fontStyle = FontStyles.Bold;
            label.color = PlayerMenuUiFactory.Accent;
            return label;
        }

        private void EnsureCaptureRecordDrawer()
        {
            if (captureRecordDrawer != null)
                return;

            Image panel = PlayerMenuUiFactory.CreateImage("CaptureRecordDrawer", transform, new Color(0.025f, 0.055f, 0.09f, 0.98f));
            captureRecordDrawer = panel.gameObject;
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.2f, 0.08f);
            rect.anchorMax = new Vector2(0.8f, 0.92f);
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
                Transform card = transform.Find("ProfileCardRoot");
                if (card != null)
                    profileCardRoot = card as RectTransform;
            }

            if (dogImage == null)
                dogImage = FindChildComponent<Image>("ProfileCardRoot/LeftStage/DogImage");
            if (hatSlot == null)
            {
                Transform hat = transform.Find("ProfileCardRoot/LeftStage/HatSlot");
                if (hat != null)
                    hatSlot = hat.gameObject;
            }

            if (outfitSlot == null)
            {
                Transform outfit = transform.Find("ProfileCardRoot/LeftStage/OutfitSlot");
                if (outfit != null)
                    outfitSlot = outfit.gameObject;
            }

            if (nameLabel == null)
                nameLabel = FindChildComponent<TMP_Text>("ProfileCardRoot/RightIdentity/NameLabel");
            if (linkChipLabel == null)
                linkChipLabel = FindChildComponent<TMP_Text>("ProfileCardRoot/RightIdentity/ChipLabel");
            if (cardTitleLabel == null)
                cardTitleLabel = FindChildComponent<TMP_Text>("ProfileCardRoot/RightIdentity/TitleLabel");
            if (uniqueSpeciesLabel == null)
                uniqueSpeciesLabel = FindChildComponent<TMP_Text>("ProfileCardRoot/RightIdentity/UniqueSpeciesLabel");
            if (capturesLabel == null)
                capturesLabel = FindChildComponent<TMP_Text>("ProfileCardRoot/RightIdentity/CapturesLabel");
            if (stampedMovesLabel == null)
                stampedMovesLabel = FindChildComponent<TMP_Text>("ProfileCardRoot/RightIdentity/StampedMovesLabel");
            if (recordLabel == null)
                recordLabel = FindChildComponent<TMP_Text>("ProfileCardRoot/RightIdentity/RecordLabel");
            if (cosmeticHintLabel == null)
                cosmeticHintLabel = FindChildComponent<TMP_Text>("CosmeticHintLabel");
            if (shareButton == null)
            {
                Transform share = transform.Find("ShareButton");
                if (share != null)
                    shareButton = share.GetComponent<Button>();
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
