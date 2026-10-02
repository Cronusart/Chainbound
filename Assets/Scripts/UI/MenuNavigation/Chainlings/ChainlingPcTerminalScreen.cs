using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// World-PC roster storage and party assignment surface.
    /// Layout is owned by the authored prefab; this component binds data and input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingPcTerminalScreen : MonoBehaviour
    {
        private const string PlayerRoomTerminalId = "player_room_pc";
        private static readonly string[] SlotNames = { "LEAD", "PARTNER", "BENCH" };

        [Header("Catalogs")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private ChainlingCardWidget cardPrefab;
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        [Header("Services")]
        [SerializeField] private MonoBehaviour rosterProviderBehaviour;
        [SerializeField] private MonoBehaviour partyLineupServiceBehaviour;

        [Header("View")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] partyButtons = new Button[CombatConstants.ROSTER_SIZE];
        [SerializeField] private RectTransform storageRoot;
        [SerializeField] private TMP_Text emptyStorageLabel;
        [SerializeField] private TMP_Text status;
        [SerializeField] private MenuInputContextController inputContext;
        [SerializeField] private Selectable defaultSelection;

        private readonly List<GameObject> spawnedCards = new List<GameObject>();
        private PlayerRosterSnapshot roster;
        private PlayerRosterEntry selectedStorage;
        private InputLockHandle inputLock;
        private bool hasInputLock;
        private string terminalId;
        private long lastRosterRefreshSequence;

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        public void Configure(
            SpeciesDatabaseSO species,
            CombatVisualDatabaseSO visuals,
            ChainlingCardWidget cards,
            SupabaseConfigSO config)
        {
            speciesDatabase = species;
            visualDatabase = visuals;
            cardPrefab = cards;
            supabaseConfig = config;
        }

        public void BindView(
            CanvasGroup canvasGroup,
            TMP_Text titleLabel,
            Button close,
            Button[] party,
            RectTransform storage,
            TMP_Text emptyLabel,
            TMP_Text statusLabel,
            MenuInputContextController context,
            Selectable firstSelection)
        {
            root = canvasGroup;
            title = titleLabel;
            closeButton = close;
            partyButtons = party ?? Array.Empty<Button>();
            storageRoot = storage;
            emptyStorageLabel = emptyLabel;
            status = statusLabel;
            inputContext = context;
            defaultSelection = firstSelection;
        }

        public async void Open(string sourceTerminalId)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            terminalId = string.IsNullOrWhiteSpace(sourceTerminalId)
                ? PlayerRoomTerminalId
                : sourceTerminalId.Trim();
            EnsureUi();
            ConfigureOverlayCanvas();
            if (root == null)
            {
                Debug.LogWarning("[ChainlingPcTerminalScreen] Authored view is missing.", this);
                return;
            }

            if (title != null)
            {
                title.text = terminalId == PlayerRoomTerminalId
                    ? "YOUR PC · CHAINLING STORAGE"
                    : "CHAINLING STORAGE";
            }

            UpdateTitle();

            AcquireInputLock();
            root.gameObject.SetActive(true);
            root.alpha = 1f;
            root.interactable = true;
            root.blocksRaycasts = true;
            SetStatus("LOADING CHAINLING STORAGE...");
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                SetStatus("CHAINLING STORAGE COULD NOT BE LOADED.");
                SetInteractable(true);
            }
        }

        public void Close()
        {
            if (root != null)
            {
                root.interactable = false;
                root.blocksRaycasts = false;
                root.gameObject.SetActive(false);
            }

            selectedStorage = null;
            ReleaseInputLock();
        }

        private void Awake()
        {
            WireView();
            if (root != null)
                root.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            PlayerRosterRefreshSignal.ResolveOrCreate().Changed -= HandleRosterChanged;
            ReleaseInputLock();
        }

        private void OnEnable()
        {
            PlayerRosterRefreshSignal.ResolveOrCreate().Changed -= HandleRosterChanged;
            PlayerRosterRefreshSignal.ResolveOrCreate().Changed += HandleRosterChanged;
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureUi();
            WireView();
        }

        private async Task RefreshAsync()
        {
            IPlayerRosterProvider provider = ResolveRosterProvider();
            GameSessionContext session = GameSessionContext.Instance;
            if (provider == null || session == null)
            {
                SetStatus("PROFILE STORAGE IS UNAVAILABLE.");
                return;
            }

            roster = await provider.FetchRosterAsync(session.PlayerProfileId, session.WalletAddress);
            if (root != null && !root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
                root.alpha = 1f;
                root.interactable = true;
                root.blocksRaycasts = true;
            }

            if (roster == null)
            {
                SetStatus("CHAINLING STORAGE COULD NOT BE LOADED.");
                return;
            }

            selectedStorage = null;
            try
            {
                Rebuild();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                SetStatus("CHAINLING STORAGE COULD NOT BE DISPLAYED.");
                return;
            }

            SetStatus("SELECT A STORED CHAINLING, THEN CHOOSE A PARTY SLOT.");
            UpdateTitle();
        }

        private void Rebuild()
        {
            for (int i = 0; i < spawnedCards.Count; i++)
            {
                if (spawnedCards[i] != null)
                    Destroy(spawnedCards[i]);
            }
            spawnedCards.Clear();

            List<PlayerRosterEntry> party = GetParty();
            for (int slot = 0; slot < partyButtons.Length && slot < SlotNames.Length; slot++)
            {
                if (partyButtons[slot] == null)
                    continue;

                PlayerRosterEntry entry = slot < party.Count ? party[slot] : null;
                TMP_Text label = partyButtons[slot].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = entry != null
                        ? $"{SlotNames[slot]}\n{ResolveName(entry)}"
                        : $"{SlotNames[slot]}\nEMPTY";
                }

                partyButtons[slot].interactable = entry != null || slot <= party.Count;
            }

            int storageCount = 0;
            if (roster != null && roster.chainlings != null)
            {
                for (int i = 0; i < roster.chainlings.Count; i++)
                {
                    PlayerRosterEntry entry = roster.chainlings[i];
                    if (entry == null || entry.isPartyMember)
                        continue;
                    storageCount++;
                    ChainlingCardWidget card = CreateCard();
                    card.Bind(
                        entry,
                        ResolveSpecies(entry),
                        ResolveVisual(entry),
                        entry == selectedStorage,
                        SelectStored);
                    if (card.Button != null)
                        MenuSelectableFocusVisual.EnsureOn(card.Button);
                    spawnedCards.Add(card.gameObject);
                }
            }

            if (emptyStorageLabel != null)
            {
                emptyStorageLabel.gameObject.SetActive(storageCount == 0);
                if (storageCount == 0)
                {
                    emptyStorageLabel.text =
                        "NO STORED CHAINLINGS\nCaptured Chainlings appear here when your party is full.";
                }
            }

            SelectFirstAvailable();
            RefreshNavigation();
        }

        private void HandleRosterChanged(PlayerRosterRefreshSignal.Notice notice)
        {
            if (!MatchesActiveProfile(notice.PlayerProfileId) || notice.Sequence <= lastRosterRefreshSequence)
                return;

            lastRosterRefreshSequence = notice.Sequence;
            if (IsOpen)
                _ = RefreshAsync();
        }

        private static bool MatchesActiveProfile(string playerProfileId)
        {
            GameSessionContext session = GameSessionContext.Instance;
            return session != null &&
                   !string.IsNullOrWhiteSpace(session.PlayerProfileId) &&
                   string.Equals(session.PlayerProfileId, playerProfileId, StringComparison.Ordinal);
        }

        private void UpdateTitle()
        {
            if (title == null)
                return;

            string prefix = terminalId == PlayerRoomTerminalId ? "YOUR PC · " : "";
            title.text = roster != null
                ? $"{prefix}STORAGE {roster.StorageCount} / {roster.StorageCapacity}"
                : $"{prefix}CHAINLING STORAGE";
        }

        private void SelectStored(PlayerRosterEntry entry)
        {
            selectedStorage = entry;
            Rebuild();
            SetStatus($"{ResolveName(entry)} SELECTED. CHOOSE LEAD, PARTNER, OR BENCH.");
        }

        private async void AssignSelectedToSlot(int slot)
        {
            if (selectedStorage == null)
            {
                SetStatus("SELECT A CHAINLING FROM STORAGE FIRST.");
                return;
            }

            List<PlayerRosterEntry> party = GetParty();
            if (slot > party.Count)
            {
                SetStatus("FILL THE PREVIOUS PARTY SLOT FIRST.");
                return;
            }

            var orderedIds = new List<string>(CombatConstants.ROSTER_SIZE);
            for (int i = 0; i < party.Count; i++)
                orderedIds.Add(party[i].snapshot.backendChainlingId);

            string selectedId = selectedStorage.snapshot.backendChainlingId;
            if (slot == orderedIds.Count)
                orderedIds.Add(selectedId);
            else
                orderedIds[slot] = selectedId;

            SetInteractable(false);
            SetStatus("SAVING PARTY...");
            IMenuPartyLineupService service = ResolvePartyService();
            GameSessionContext session = GameSessionContext.Instance;
            MenuMutationResult result = service != null && session != null
                ? await service.SavePartyLineupAsync(
                    session.PlayerProfileId,
                    session.WalletAddress,
                    session.AuthUserId,
                    orderedIds.ToArray())
                : MenuMutationResult.Failed("Party service unavailable.");

            if (result == null || !result.success)
            {
                SetStatus(result != null && !string.IsNullOrWhiteSpace(result.error)
                    ? result.error
                    : "PARTY COULD NOT BE SAVED.");
                SetInteractable(true);
                return;
            }

            await RefreshAsync();
            SetInteractable(true);
        }

        private List<PlayerRosterEntry> GetParty()
        {
            var party = new List<PlayerRosterEntry>(CombatConstants.ROSTER_SIZE);
            if (roster != null && roster.chainlings != null)
            {
                for (int i = 0; i < roster.chainlings.Count; i++)
                {
                    PlayerRosterEntry entry = roster.chainlings[i];
                    if (entry != null && entry.isPartyMember)
                        party.Add(entry);
                }
            }

            party.Sort((left, right) => left.partySlot.CompareTo(right.partySlot));
            return party;
        }

        private ChainlingCardWidget CreateCard()
        {
            ChainlingCardWidget card;
            if (cardPrefab != null)
            {
                card = Instantiate(cardPrefab, storageRoot);
            }
            else
            {
                var go = new GameObject("ChainlingCard", typeof(RectTransform), typeof(ChainlingCardWidget));
                go.transform.SetParent(storageRoot, false);
                card = go.GetComponent<ChainlingCardWidget>();
            }

            PlayerMenuUiFactory.LayoutElement(card.gameObject, 220f, 260f);
            return card;
        }

        private SpeciesDefinitionSO ResolveSpecies(PlayerRosterEntry entry)
        {
            return speciesDatabase != null && entry != null && entry.snapshot != null
                ? speciesDatabase.GetById(entry.snapshot.speciesId)
                : null;
        }

        private CombatVisualProfileSO ResolveVisual(PlayerRosterEntry entry)
        {
            if (visualDatabase == null || entry == null || entry.snapshot == null)
                return null;
            ChainlingSnapshot snapshot = entry.snapshot;
            return visualDatabase.GetProfile(
                snapshot.speciesId,
                snapshot.visualIdentity != null ? snapshot.visualIdentity.visualVariantId : "base",
                snapshot.isEntangled ? snapshot.entangledType : ChainlingType.None);
        }

        private string ResolveName(PlayerRosterEntry entry)
        {
            if (entry != null && entry.snapshot != null && !string.IsNullOrWhiteSpace(entry.snapshot.displayName))
                return entry.snapshot.displayName;
            SpeciesDefinitionSO species = ResolveSpecies(entry);
            if (species != null && !string.IsNullOrWhiteSpace(species.displayName))
                return species.displayName;
            return entry != null && entry.snapshot != null ? entry.snapshot.speciesId : "Chainling";
        }

        private void EnsureUi()
        {
            if (root != null && storageRoot != null && partyButtons != null && partyButtons.Length >= CombatConstants.ROSTER_SIZE)
                return;

            BuildAuthoredHierarchy();
        }

        private void ConfigureOverlayCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 860;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            RectTransform rect = transform as RectTransform;
            if (rect != null)
            {
                PlayerMenuUiFactory.Stretch(rect);
                rect.localScale = Vector3.one;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildAuthoredHierarchy()
        {
            ConfigureOverlayCanvas();

            Transform safe = transform.Find("SafeArea");
            GameObject panel = safe != null ? safe.gameObject : new GameObject("SafeArea", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panel.transform.SetParent(transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            PlayerMenuUiFactory.Stretch(panelRect, 0f, 0f, 0f, 0f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.025f, 0.045f, 0.075f, 0.985f);
            if (panel.GetComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>() == null)
                panel.AddComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>();
            root = panel.GetComponent<CanvasGroup>();

            if (title == null)
            {
                title = PlayerMenuUiFactory.CreateText(
                    "Title",
                    panel.transform,
                    "CHAINLING STORAGE",
                    38f,
                    TextAlignmentOptions.MidlineLeft,
                    false);
                PlayerMenuUiFactory.Top(title.rectTransform, 70f, 32f, 190f, 18f);
            }

            if (closeButton == null)
            {
                closeButton = PlayerMenuUiFactory.CreateButton("Close", panel.transform, "B  CLOSE", PlayerMenuUiFactory.PanelAlt);
                PlayerMenuUiFactory.Top(closeButton.transform as RectTransform, 58f, 0f, 28f, 22f);
                RectTransform closeRect = closeButton.transform as RectTransform;
                closeRect.anchorMin = new Vector2(0.84f, 1f);
                MenuSelectableFocusVisual.EnsureOn(closeButton);
            }

            RectTransform partyRoot = panel.transform.Find("PartySlots") as RectTransform;
            if (partyRoot == null)
            {
                partyRoot = PlayerMenuUiFactory.CreateRect("PartySlots", panel.transform);
                partyRoot.anchorMin = new Vector2(0.03f, 0.64f);
                partyRoot.anchorMax = new Vector2(0.97f, 0.84f);
                partyRoot.offsetMin = Vector2.zero;
                partyRoot.offsetMax = Vector2.zero;
                HorizontalLayoutGroup partyLayout = PlayerMenuUiFactory.HorizontalLayout(partyRoot.gameObject, 18f);
                partyLayout.childForceExpandWidth = true;
            }

            if (partyButtons == null || partyButtons.Length != CombatConstants.ROSTER_SIZE)
                partyButtons = new Button[CombatConstants.ROSTER_SIZE];

            for (int slot = 0; slot < partyButtons.Length; slot++)
            {
                if (partyButtons[slot] != null)
                    continue;

                partyButtons[slot] = PlayerMenuUiFactory.CreateButton(
                    $"Party_{SlotNames[slot]}",
                    partyRoot,
                    SlotNames[slot],
                    PlayerMenuUiFactory.PanelAlt);
                PlayerMenuUiFactory.LayoutElement(partyButtons[slot].gameObject, 300f, 150f, 1f);
                MenuSelectableFocusVisual.EnsureOn(partyButtons[slot]);
            }

            if (panel.transform.Find("StorageLabel") == null)
            {
                TMP_Text storageLabel = PlayerMenuUiFactory.CreateText(
                    "StorageLabel",
                    panel.transform,
                    "STORAGE",
                    25f,
                    TextAlignmentOptions.MidlineLeft,
                    false);
                storageLabel.color = PlayerMenuUiFactory.Accent;
                storageLabel.rectTransform.anchorMin = new Vector2(0.03f, 0.56f);
                storageLabel.rectTransform.anchorMax = new Vector2(0.97f, 0.62f);
                storageLabel.rectTransform.offsetMin = Vector2.zero;
                storageLabel.rectTransform.offsetMax = Vector2.zero;
            }

            Transform scrollTransform = panel.transform.Find("StorageScroll");
            GameObject scrollGo = scrollTransform != null
                ? scrollTransform.gameObject
                : new GameObject("StorageScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel.transform, false);
            RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.03f, 0.16f);
            scrollRect.anchorMax = new Vector2(0.97f, 0.56f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0.04f, 0.065f, 0.1f, 0.9f);

            if (storageRoot == null)
            {
                storageRoot = PlayerMenuUiFactory.CreateRect("Cards", scrollGo.transform);
                storageRoot.anchorMin = new Vector2(0f, 0f);
                storageRoot.anchorMax = new Vector2(0f, 1f);
                storageRoot.pivot = new Vector2(0f, 0.5f);
                storageRoot.sizeDelta = new Vector2(0f, 0f);
                HorizontalLayoutGroup storageLayout = PlayerMenuUiFactory.HorizontalLayout(storageRoot.gameObject, 18f);
                storageLayout.padding = new RectOffset(18, 18, 18, 18);
                ContentSizeFitter fitter = storageRoot.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = storageRoot;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            if (emptyStorageLabel == null)
            {
                emptyStorageLabel = PlayerMenuUiFactory.CreateText(
                    "EmptyStorage",
                    storageRoot,
                    "NO STORED CHAINLINGS\nCaptured Chainlings appear here when your party is full.",
                    22f,
                    TextAlignmentOptions.Center);
                PlayerMenuUiFactory.LayoutElement(emptyStorageLabel.gameObject, 520f, 100f);
                emptyStorageLabel.gameObject.SetActive(false);
            }

            if (status == null)
            {
                status = PlayerMenuUiFactory.CreateText("Status", panel.transform, "", 20f, TextAlignmentOptions.Center, true);
                PlayerMenuUiFactory.Bottom(status.rectTransform, 82f, 30f, 30f, 20f);
            }

            if (inputContext == null)
            {
                inputContext = PlayerMenuUiFactory.GetOrAdd<MenuInputContextController>(gameObject);
                inputContext.OnCancel.RemoveListener(Close);
                inputContext.OnCancel.AddListener(Close);
            }

            defaultSelection = closeButton;
            root.gameObject.SetActive(false);
        }

        private void WireView()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            for (int slot = 0; slot < partyButtons.Length; slot++)
            {
                if (partyButtons[slot] == null)
                    continue;

                int capturedSlot = slot;
                partyButtons[slot].onClick.RemoveAllListeners();
                partyButtons[slot].onClick.AddListener(() => AssignSelectedToSlot(capturedSlot));
            }

            if (inputContext != null)
            {
                inputContext.OnCancel.RemoveListener(Close);
                inputContext.OnCancel.AddListener(Close);
            }
        }

        private void RefreshNavigation()
        {
            if (inputContext == null)
                return;

            var order = new List<Selectable>();
            if (closeButton != null)
                order.Add(closeButton);
            for (int i = 0; i < partyButtons.Length; i++)
            {
                if (partyButtons[i] != null)
                    order.Add(partyButtons[i]);
            }

            for (int i = 0; i < spawnedCards.Count; i++)
            {
                Button button = spawnedCards[i] != null ? spawnedCards[i].GetComponent<Button>() : null;
                if (button != null)
                    order.Add(button);
            }

            Selectable first = defaultSelection;
            for (int i = 0; i < spawnedCards.Count; i++)
            {
                Button button = spawnedCards[i] != null ? spawnedCards[i].GetComponent<Button>() : null;
                if (button != null && button.interactable)
                {
                    first = button;
                    break;
                }
            }

            inputContext.SetContentNavigation(order, first, transform);
        }

        private IPlayerRosterProvider ResolveRosterProvider()
        {
            IPlayerRosterProvider existing = rosterProviderBehaviour as IPlayerRosterProvider;
            if (existing != null)
                return existing;

            SupabasePlayerRosterProvider provider = GetComponent<SupabasePlayerRosterProvider>();
            if (provider == null)
                provider = gameObject.AddComponent<SupabasePlayerRosterProvider>();
            provider.Configure(ResolveSupabaseConfig());
            rosterProviderBehaviour = provider;
            return provider;
        }

        private IMenuPartyLineupService ResolvePartyService()
        {
            IMenuPartyLineupService existing = partyLineupServiceBehaviour as IMenuPartyLineupService;
            if (existing != null)
                return existing;

            SupabaseMenuPartyLineupService service = GetComponent<SupabaseMenuPartyLineupService>();
            if (service == null)
                service = gameObject.AddComponent<SupabaseMenuPartyLineupService>();
            service.Configure(ResolveSupabaseConfig());
            partyLineupServiceBehaviour = service;
            return service;
        }

        private SupabaseConfigSO ResolveSupabaseConfig()
        {
            if (supabaseConfig == null)
                supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
            return supabaseConfig;
        }

        private void AcquireInputLock()
        {
            if (hasInputLock || InputLockService.Instance == null)
                return;
            inputLock = InputLockService.Instance.AcquireLock($"ChainlingPcTerminal:{terminalId}", InputLockChannel.All, this);
            hasInputLock = inputLock.IsValid;
        }

        private void ReleaseInputLock()
        {
            if (!hasInputLock)
                return;
            inputLock.Release();
            inputLock = default;
            hasInputLock = false;
        }

        private void SetStatus(string value)
        {
            if (status != null)
                status.text = value ?? "";
        }

        private void SetInteractable(bool value)
        {
            if (root != null)
                root.interactable = value;
        }

        private void SelectFirstAvailable()
        {
            if (EventSystem.current == null)
                return;
            for (int i = 0; i < spawnedCards.Count; i++)
            {
                Button button = spawnedCards[i] != null ? spawnedCards[i].GetComponent<Button>() : null;
                if (button != null && button.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    return;
                }
            }

            if (partyButtons != null && partyButtons.Length > 0 && partyButtons[0] != null)
                EventSystem.current.SetSelectedGameObject(partyButtons[0].gameObject);
            else if (closeButton != null)
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }
    }
}
