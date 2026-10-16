using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// World-PC roster storage and party assignment surface.
    /// Layout is the handheld Profile B frame: storage, party, selection, and a shared status line.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ChainlingPcTerminalScreen : MonoBehaviour
    {
        private const string PlayerRoomTerminalId = "player_room_pc";

        [Header("Catalogs")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private ChainlingVisualIdentityDatabaseSO identityDatabase;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [SerializeField] private ChainlingCardWidget cardPrefab;
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        [Header("Services")]
        [SerializeField] private MonoBehaviour rosterProviderBehaviour;
        [SerializeField] private MonoBehaviour partyLineupServiceBehaviour;

        [Header("Party rows")]
        [SerializeField] private InventoryItemUseTargetSlot partyRowPrefab;
        [SerializeField] private InventoryItemUseTargetSlot[] partyRows = new InventoryItemUseTargetSlot[CombatConstants.ROSTER_SIZE];
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [SerializeField] private CompactStarRatingArtSO starArt;
        [SerializeField] private CycleIconPaletteSO cyclePalette;
        [SerializeField] private SignalJam.UI.MenuNavigation.PartyTab.PartyTabMotionProfileSO motionProfile;
        [SerializeField] private ChainlingDetailScreen detailPrefab;

        [Header("Action sheet")]
        [SerializeField] private CanvasGroup actionSheet;
        [SerializeField] private GameObject actionButtonGroup;
        [SerializeField] private GameObject confirmGroup;
        [SerializeField] private TMP_Text actionTitle;
        [SerializeField] private TMP_Text confirmBody;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button storageButton;
        [SerializeField] private Button manageButton;
        [SerializeField] private Button releaseButton;
        [SerializeField] private Button actionCloseButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button confirmCancelButton;

        [Header("View")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform storageRoot;
        [SerializeField] private TMP_Text emptyStorageLabel;
        [SerializeField] private TMP_Text status;
        [SerializeField] private MenuInputContextController inputContext;
        [SerializeField] private Selectable defaultSelection;
        [SerializeField] private PlayerMenuBootSequenceController bootSequence;
        [SerializeField] private CanvasGroup[] revealColumns = Array.Empty<CanvasGroup>();
        [SerializeField] private Image previewPortrait;
        [SerializeField] private TMP_Text previewName;
        [SerializeField] private TMP_Text previewMeta;
        [SerializeField] private TMP_Text previewBody;
        [SerializeField] private Image previewHpFill;
        [SerializeField] private MonoBehaviour sfxPlayerBehaviour;

        private readonly List<GameObject> spawnedCards = new List<GameObject>();
        private PlayerRosterSnapshot roster;
        private PlayerRosterEntry selectedStorage;
        private PlayerRosterEntry actionTarget;
        private bool actionTargetInParty;
        private bool choosingSlot;
        private bool confirmingRelease;
        private int focusedPartyRow = -1;
        private float partyRevealUntil;
        private int lastCloseFrame = -1;
        private ChainlingDetailScreen openDetail;
        private int actionSheetRequest;
        private InputLockHandle inputLock;
        private bool hasInputLock;
        private string terminalId;
        private long lastRosterRefreshSequence;
        private bool entranceSettled;
        private bool closing;
        private MonoBehaviour cachedSfxPlayerBehaviour;

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

        public void ConfigurePartyPresentation(
            InventoryItemUseTargetSlot rowPrefab,
            TypeIconPaletteSO types,
            CompactStarRatingArtSO stars,
            CycleIconPaletteSO cycles,
            SignalJam.UI.MenuNavigation.PartyTab.PartyTabMotionProfileSO motion,
            ChainlingDetailScreen detail)
        {
            if (rowPrefab != null)
                partyRowPrefab = rowPrefab;
            if (types != null)
                typeIconPalette = types;
            if (stars != null)
                starArt = stars;
            if (cycles != null)
                cyclePalette = cycles;
            if (motion != null)
                motionProfile = motion;
            if (detail != null)
                detailPrefab = detail;
        }

        public static bool CanSendPartyMemberToStorage(int partyCount)
        {
            return partyCount > 1;
        }

        public static bool CanReleaseChainling(bool isPartyMember)
        {
            return !isPartyMember;
        }

        public void BindView(
            CanvasGroup canvasGroup,
            TMP_Text titleLabel,
            Button close,
            RectTransform storage,
            TMP_Text emptyLabel,
            TMP_Text statusLabel,
            MenuInputContextController context,
            Selectable firstSelection)
        {
            root = canvasGroup;
            title = titleLabel;
            closeButton = close;
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
            closing = false;
            entranceSettled = false;
            choosingSlot = false;
            confirmingRelease = false;
            actionTarget = null;
            actionTargetInParty = false;
            HideActionSheetImmediate();
            root.gameObject.SetActive(true);
            root.alpha = 1f;
            root.blocksRaycasts = true;
            root.interactable = false;
            ArmTerminalInput();
            SetStatus("LOADING CHAINLING STORAGE...");
            PlayCue(PlayerMenuSfxCue.MenuOpen);
            PlayerMenuSfxResolver.SuppressNavigationCues(0.6f);
            BeginOpenMotion();
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                SetStatus("CHAINLING STORAGE COULD NOT BE LOADED.");
                if (entranceSettled)
                    SetInteractable(true);
            }
        }

        public void Close()
        {
            if (Time.frameCount == lastCloseFrame)
                return;
            lastCloseFrame = Time.frameCount;

            if (closing)
                return;

            if (openDetail != null && openDetail.IsVisible)
            {
                openDetail.CloseFromHost();
                return;
            }

            if (TryDismissRosterOverlay())
                return;

            if (root == null || !root.gameObject.activeSelf)
            {
                ReleaseTerminalInput();
                ReleaseInputLock();
                return;
            }

            closing = true;
            entranceSettled = false;
            HideActionSheetImmediate();
            choosingSlot = false;
            selectedStorage = null;
            actionTarget = null;
            root.interactable = false;
            ReleaseTerminalInput();
            KillPartyMotion();
            PlayCue(PlayerMenuSfxCue.MenuClose);
            PlayPartyRowClose(() =>
            {
                if (bootSequence != null && bootSequence.HasExitFrames)
                    bootSequence.PlayCloseSequence(FinishClose);
                else
                    FinishClose();
            });
        }

        private void Awake()
        {
            WireView();
            if (root != null)
                root.gameObject.SetActive(false);
            ReleaseTerminalInput();
        }

        private void OnDisable()
        {
            PlayerRosterRefreshSignal.ResolveOrCreate().Changed -= HandleRosterChanged;
            KillPartyMotion();
            closing = false;
            entranceSettled = false;
            ReleaseTerminalInput();
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

            roster = await PlayerProfileReadStore.ResolveOrCreate().RefreshRosterAsync(
                provider,
                session.PlayerProfileId,
                session.WalletAddress);
            if (root != null && !root.gameObject.activeSelf && !closing)
            {
                root.gameObject.SetActive(true);
                root.alpha = 1f;
                root.blocksRaycasts = true;
                root.interactable = entranceSettled;
            }

            if (roster == null)
            {
                SetStatus("CHAINLING STORAGE COULD NOT BE LOADED.");
                return;
            }

            selectedStorage = null;
            choosingSlot = false;
            confirmingRelease = false;
            actionTarget = null;
            HideActionSheetImmediate();
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

            SetStatus("SELECT A STORED CHAINLING OR A PARTY SLOT.");
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

            BindPartyRows(GetParty());

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

            BindPreview();
            if (entranceSettled && !closing)
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
            if (entry == null)
                return;

            selectedStorage = entry;
            BindPreview();
            PlayCue(PlayerMenuSfxCue.ChainlingSelect);
            actionSheetRequest++;
            OpenActionSheet(entry, false);
        }

        private void SelectPartyRow(int slot)
        {
            if (choosingSlot)
            {
                AssignSelectedToSlot(slot);
                return;
            }

            List<PlayerRosterEntry> party = GetParty();
            if (slot < 0 || slot >= party.Count || party[slot] == null)
            {
                SetStatus("THAT SLOT IS EMPTY.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            PlayerRosterEntry entry = party[slot];
            selectedStorage = entry;
            BindPreview();
            InventoryItemUseTargetSlot row = slot < partyRows.Length ? partyRows[slot] : null;
            if (row != null)
                row.PlaySelectPulse();
            PlayCue(PlayerMenuSfxCue.ChainlingSelect);
            int request = ++actionSheetRequest;
            DOVirtual.DelayedCall(0.16f, () =>
            {
                if (request != actionSheetRequest || closing)
                    return;
                OpenActionSheet(entry, true);
            }, false)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private async void AssignSelectedToSlot(int slot)
        {
            PlayerRosterEntry selected = actionTarget != null ? actionTarget : selectedStorage;
            if (selected == null || selected.snapshot == null)
            {
                SetStatus("SELECT A CHAINLING FIRST.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            List<PlayerRosterEntry> party = GetParty();
            if (slot > party.Count)
            {
                SetStatus("FILL THE PREVIOUS PARTY SLOT FIRST.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            string selectedId = selected.snapshot.backendChainlingId;
            if (!TryBuildPartyOrder(party, selectedId, slot, false, out string[] orderedIds))
            {
                SetStatus("THAT PARTY CHANGE IS NOT AVAILABLE.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            await SavePartyOrderAsync(orderedIds);
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

            PlayerMenuUiFactory.LayoutElement(card.gameObject, preferredHeight: 188f, flexibleWidth: 1f);
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
            if (root != null &&
                storageRoot != null &&
                actionSheet != null &&
                partyRows != null &&
                partyRows.Length >= CombatConstants.ROSTER_SIZE &&
                partyRows[0] != null)
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
            ClearGeneratedShell();

            GameObject shell = new GameObject("SafeArea", typeof(RectTransform), typeof(CanvasGroup));
            shell.transform.SetParent(transform, false);
            PlayerMenuUiFactory.Stretch(shell.GetComponent<RectTransform>());
            root = shell.GetComponent<CanvasGroup>();

            Image dimmer = PlayerMenuUiFactory.CreateImage("Dimmer", shell.transform, new Color(0.03f, 0.012f, 0.02f, 0.58f));
            PlayerMenuUiFactory.Stretch(dimmer.rectTransform);
            dimmer.raycastTarget = true;

            GameObject bootObject = new GameObject("TerminalBoot", typeof(RectTransform), typeof(PlayerMenuBootSequenceController));
            bootObject.transform.SetParent(shell.transform, false);
            PlayerMenuUiFactory.Stretch(bootObject.GetComponent<RectTransform>());
            bootSequence = bootObject.GetComponent<PlayerMenuBootSequenceController>();

            GameObject crt = new GameObject("CrtBackground", typeof(RectTransform), typeof(PlayerMenuBackgroundController));
            crt.transform.SetParent(bootObject.transform, false);
            PlayerMenuUiFactory.Stretch(crt.GetComponent<RectTransform>());
            PlayerMenuBackgroundController background = crt.GetComponent<PlayerMenuBackgroundController>();
            background.BuildDefaultLayoutForEditor();
            if (background.BaseTintImage != null)
            {
                background.BaseTintImage.color = new Color(0.07f, 0.025f, 0.035f, 0.78f);
                background.BaseTintImage.raycastTarget = false;
            }

            GameObject menuPanel = new GameObject("MenuPanel", typeof(RectTransform), typeof(CanvasGroup));
            menuPanel.transform.SetParent(bootObject.transform, false);
            PlayerMenuUiFactory.Stretch(menuPanel.GetComponent<RectTransform>());
            CanvasGroup menuGroup = menuPanel.GetComponent<CanvasGroup>();
            bootSequence.ConfigureTargets(crt.GetComponent<RectTransform>(), menuPanel.GetComponent<RectTransform>());
            bootSequence.BuildDefaultLayoutForEditor();

            RectTransform storageSlot = CreateHudSlot(menuPanel.transform, "TopLeft", new Vector2(0.019f, 0.438f), new Vector2(0.316f, 0.951f));
            RectTransform partySlot = CreateHudSlot(menuPanel.transform, "TopCenter", new Vector2(0.334f, 0.438f), new Vector2(0.666f, 0.868f));
            RectTransform previewSlot = CreateHudSlot(menuPanel.transform, "TopRight", new Vector2(0.684f, 0.438f), new Vector2(0.981f, 0.951f));
            RectTransform dialogueSlot = CreateHudSlot(menuPanel.transform, "DialogueLowerCenter", new Vector2(0.206f, 0.014f), new Vector2(0.794f, 0.382f));
            revealColumns = new[]
            {
                partySlot.GetComponent<CanvasGroup>(),
                storageSlot.GetComponent<CanvasGroup>(),
                previewSlot.GetComponent<CanvasGroup>(),
                dialogueSlot.GetComponent<CanvasGroup>()
            };

            VerticalLayoutGroup storageLayout = FillSlot(storageSlot.gameObject);
            title = CreateSlotHeader(storageSlot, "Title", "STORAGE");
            BuildStorageList(storageSlot, storageLayout);

            VerticalLayoutGroup partyLayout = FillSlot(partySlot.gameObject);
            CreateSlotHeader(partySlot, "PartyHeader", "PARTY");
            RectTransform partyRoot = PlayerMenuUiFactory.CreateRect("PartySlots", partySlot);
            PlayerMenuUiFactory.LayoutElement(partyRoot.gameObject, preferredHeight: 280f, flexibleWidth: 1f, flexibleHeight: 1f);
            VerticalLayoutGroup partyRowLayout = PlayerMenuUiFactory.VerticalLayout(partyRoot.gameObject, 8f);
            partyRowLayout.childControlHeight = true;
            partyRowLayout.childForceExpandHeight = false;
            partyRowLayout.childAlignment = TextAnchor.UpperCenter;
            partyRows = new InventoryItemUseTargetSlot[CombatConstants.ROSTER_SIZE];
            for (int slot = 0; slot < partyRows.Length; slot++)
            {
                if (partyRowPrefab == null)
                    break;

                InventoryItemUseTargetSlot row = Instantiate(partyRowPrefab, partyRoot);
                row.gameObject.name = $"PartySlot_{slot + 1}";
                row.gameObject.SetActive(true);
                LayoutElement element = row.GetComponent<LayoutElement>();
                if (element == null)
                    element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 96f;
                element.minHeight = 72f;
                element.flexibleWidth = 1f;
                if (row.Button != null)
                    MenuSelectableFocusVisual.EnsureOn(row.Button, new Color(1f, 0.62f, 0.22f, 0.95f));
                partyRows[slot] = row;
            }

            partyLayout.childAlignment = TextAnchor.UpperCenter;

            VerticalLayoutGroup previewLayout = FillSlot(previewSlot.gameObject);
            CreateSlotHeader(previewSlot, "PreviewHeader", "SELECTED");
            Image portraitBack = PlayerMenuUiFactory.CreateImage("PortraitBack", previewSlot, new Color(0.12f, 0.06f, 0.05f, 0.9f));
            PlayerMenuUiFactory.LayoutElement(portraitBack.gameObject, preferredHeight: 132f, flexibleWidth: 1f);
            previewPortrait = PlayerMenuUiFactory.CreateImage("Portrait", portraitBack.transform, Color.white);
            PlayerMenuUiFactory.Stretch(previewPortrait.rectTransform, 10f, 10f, 10f, 10f);
            previewPortrait.preserveAspect = true;
            previewPortrait.enabled = false;
            previewName = CreatePreviewLine(previewSlot, "PreviewName", "NO CHAINLING", 28f);
            previewMeta = CreatePreviewLine(previewSlot, "PreviewMeta", "Choose a stored Chainling.", 18f);
            previewMeta.color = PlayerMenuUiFactory.Muted;
            previewBody = CreatePreviewLine(previewSlot, "PreviewBody", "Deposited Chainlings recover while they stay in storage.", 18f);
            previewBody.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(previewBody.gameObject, preferredHeight: 72f, flexibleWidth: 1f, flexibleHeight: 1f);
            Image hpTrack = PlayerMenuUiFactory.CreateImage("HpTrack", previewSlot, new Color(0.15f, 0.08f, 0.08f, 0.95f));
            PlayerMenuUiFactory.LayoutElement(hpTrack.gameObject, preferredHeight: 16f, flexibleWidth: 1f);
            previewHpFill = PlayerMenuUiFactory.CreateImage("HpFill", hpTrack.transform, new Color(0.45f, 0.85f, 0.38f, 1f));
            PlayerMenuUiFactory.Stretch(previewHpFill.rectTransform);
            previewHpFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            previewHpFill.raycastTarget = false;
            closeButton = PlayerMenuUiFactory.CreateButton("Close", previewSlot, "B  CLOSE", PlayerMenuUiFactory.PanelAlt);
            PlayerMenuUiFactory.LayoutElement(closeButton.gameObject, preferredHeight: 64f, flexibleWidth: 1f);
            MenuSelectableFocusVisual.EnsureOn(closeButton, new Color(1f, 0.62f, 0.22f, 0.95f));
            previewLayout.childAlignment = TextAnchor.UpperCenter;

            VerticalLayoutGroup dialogueLayout = FillSlot(dialogueSlot.gameObject);
            TMP_Text kicker = CreateSlotHeader(dialogueSlot, "DialogueKicker", "CHAINLING PC");
            kicker.fontSize = 16f;
            status = PlayerMenuUiFactory.CreateText(
                "Status",
                dialogueSlot,
                "SELECT A STORED CHAINLING OR A PARTY SLOT.",
                24f,
                TextAlignmentOptions.TopLeft,
                true);
            PlayerMenuUiFactory.LayoutElement(status.gameObject, preferredHeight: 96f, flexibleWidth: 1f, flexibleHeight: 1f);
            dialogueLayout.childAlignment = TextAnchor.UpperLeft;

            BuildActionSheet(shell.transform);

            if (inputContext == null)
            {
                inputContext = PlayerMenuUiFactory.GetOrAdd<MenuInputContextController>(gameObject);
                inputContext.OnCancel.RemoveListener(Close);
                inputContext.OnCancel.AddListener(Close);
            }

            defaultSelection = partyRows != null && partyRows.Length > 0 && partyRows[0] != null && partyRows[0].Button != null
                ? partyRows[0].Button
                : closeButton;
            menuGroup.alpha = 1f;
            root.alpha = 1f;
            root.interactable = false;
            root.blocksRaycasts = false;
            root.gameObject.SetActive(true);
        }

        private void ClearGeneratedShell()
        {
            Transform existing = transform.Find("SafeArea");
            if (existing == null)
                return;

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        private static RectTransform CreateHudSlot(Transform parent, string slotName, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform slot = PlayerMenuUiFactory.CreateRect(slotName, parent);
            slot.anchorMin = anchorMin;
            slot.anchorMax = anchorMax;
            slot.offsetMin = Vector2.zero;
            slot.offsetMax = Vector2.zero;
            slot.pivot = new Vector2(0.5f, 0.5f);
            Image plate = slot.gameObject.AddComponent<Image>();
            plate.color = new Color(0.22f, 0.1f, 0.08f, 0.94f);
            plate.raycastTarget = false;
            slot.gameObject.AddComponent<CanvasGroup>();
            return slot;
        }

        private static VerticalLayoutGroup FillSlot(GameObject slot)
        {
            VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(slot, 8f);
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static TMP_Text CreateSlotHeader(Transform parent, string objectName, string value)
        {
            TMP_Text header = PlayerMenuUiFactory.CreateText(objectName, parent, value, 20f, TextAlignmentOptions.MidlineLeft, false);
            header.color = PlayerMenuUiFactory.Accent;
            header.fontStyle = FontStyles.Bold;
            PlayerMenuUiFactory.LayoutElement(header.gameObject, preferredHeight: 28f, flexibleWidth: 1f);
            return header;
        }

        private static TMP_Text CreatePreviewLine(Transform parent, string objectName, string value, float size)
        {
            TMP_Text line = PlayerMenuUiFactory.CreateText(objectName, parent, value, size, TextAlignmentOptions.TopLeft, true);
            line.overflowMode = TextOverflowModes.Ellipsis;
            PlayerMenuUiFactory.LayoutElement(line.gameObject, preferredHeight: size + 10f, flexibleWidth: 1f);
            return line;
        }

        private void BuildStorageList(RectTransform slot, VerticalLayoutGroup slotLayout)
        {
            slotLayout.childAlignment = TextAnchor.UpperLeft;
            GameObject scrollGo = new GameObject("StorageScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(slot, false);
            PlayerMenuUiFactory.LayoutElement(scrollGo, preferredHeight: 220f, flexibleWidth: 1f, flexibleHeight: 1f);
            Image scrollImage = scrollGo.GetComponent<Image>();
            scrollImage.color = new Color(0.05f, 0.025f, 0.03f, 0.72f);
            scrollImage.raycastTarget = true;

            RectTransform viewport = PlayerMenuUiFactory.CreateRect("Viewport", scrollGo.transform);
            PlayerMenuUiFactory.Stretch(viewport, 8f, 8f, 22f, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            storageRoot = PlayerMenuUiFactory.CreateRect("Cards", viewport);
            storageRoot.anchorMin = new Vector2(0f, 1f);
            storageRoot.anchorMax = new Vector2(1f, 1f);
            storageRoot.pivot = new Vector2(0.5f, 1f);
            storageRoot.sizeDelta = Vector2.zero;
            VerticalLayoutGroup cards = PlayerMenuUiFactory.VerticalLayout(storageRoot.gameObject, 8f);
            cards.padding = new RectOffset(4, 4, 4, 4);
            cards.childControlHeight = true;
            ContentSizeFitter fitter = storageRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            emptyStorageLabel = PlayerMenuUiFactory.CreateText(
                "EmptyStorage",
                storageRoot,
                "NO STORED CHAINLINGS\nCaptured Chainlings appear here when your party is full.",
                18f,
                TextAlignmentOptions.Center);
            PlayerMenuUiFactory.LayoutElement(emptyStorageLabel.gameObject, preferredHeight: 88f, flexibleWidth: 1f);
            emptyStorageLabel.gameObject.SetActive(false);

            Scrollbar scrollbar = CreateVerticalScrollbar(scrollGo.transform);
            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = storageRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static Scrollbar CreateVerticalScrollbar(Transform parent)
        {
            GameObject barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(parent, false);
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(14f, -16f);
            barRect.anchoredPosition = new Vector2(-4f, 0f);
            Image track = barObject.GetComponent<Image>();
            track.color = new Color(0.2f, 0.1f, 0.08f, 0.85f);
            track.raycastTarget = true;

            RectTransform sliding = PlayerMenuUiFactory.CreateRect("Sliding Area", barObject.transform);
            PlayerMenuUiFactory.Stretch(sliding, 2f, 2f, 2f, 2f);
            Image handle = PlayerMenuUiFactory.CreateImage("Handle", sliding, new Color(0.92f, 0.55f, 0.24f, 0.95f));
            PlayerMenuUiFactory.Stretch(handle.rectTransform);
            handle.raycastTarget = true;

            Scrollbar scrollbar = barObject.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private void WireView()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            if (partyRows != null)
            {
                for (int slot = 0; slot < partyRows.Length; slot++)
                {
                    if (partyRows[slot] == null || partyRows[slot].Button == null)
                        continue;

                    int capturedSlot = slot;
                    partyRows[slot].Button.onClick.RemoveAllListeners();
                    partyRows[slot].Button.onClick.AddListener(() => SelectPartyRow(capturedSlot));
                }
            }

            WireActionSheet();

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
            if (IsActionSheetOpen)
            {
                AppendSheetNavigation(order);
            }
            else
            {
                AppendPartyNavigation(order);
                for (int i = 0; i < spawnedCards.Count; i++)
                {
                    Button button = spawnedCards[i] != null ? spawnedCards[i].GetComponent<Button>() : null;
                    if (button != null)
                        order.Add(button);
                }

                if (closeButton != null)
                    order.Add(closeButton);
            }

            Selectable first = defaultSelection;
            if (IsActionSheetOpen)
            {
                first = confirmingRelease && confirmButton != null ? confirmButton : moveButton;
            }
            else if (choosingSlot)
            {
                first = partyRows != null && partyRows.Length > 0 && partyRows[0] != null
                    ? partyRows[0].Button
                    : defaultSelection;
            }
            else
            {
                for (int i = 0; i < spawnedCards.Count; i++)
                {
                    ChainlingCardWidget card = spawnedCards[i] != null ? spawnedCards[i].GetComponent<ChainlingCardWidget>() : null;
                    Button button = card != null ? card.Button : null;
                    if (button == null || !button.interactable)
                        continue;

                    if (first == defaultSelection)
                        first = button;
                    if (selectedStorage != null && card.BoundEntry == selectedStorage && !actionTargetInParty)
                    {
                        first = button;
                        break;
                    }
                }
            }

            inputContext.SetContentNavigation(order, first, transform);
            inputContext.ConfigureNavigation();
        }

        private void BeginOpenMotion()
        {
            if (bootSequence != null && bootSequence.HasBootFrames)
                bootSequence.PlayOpenSequence(FinishEntrance, PlayColumnReveal);
            else
            {
                PlayColumnReveal();
                FinishEntrance();
            }
        }

        private void FinishEntrance()
        {
            if (closing || root == null || !root.gameObject.activeSelf)
                return;

            entranceSettled = true;
            root.interactable = true;
            if (inputContext != null)
            {
                inputContext.SetAnimationHold(false);
                inputContext.SetInteractionEnabled(true);
            }
            PlayPartyRowReveal();
            RefreshNavigation();
        }

        private void FinishClose()
        {
            KillPartyMotion();
            if (root != null)
            {
                root.interactable = false;
                root.blocksRaycasts = false;
                root.gameObject.SetActive(false);
            }

            ReleaseTerminalInput();
            closing = false;
            ReleaseInputLock();
        }

        private void PlayColumnReveal()
        {
            DOTween.Kill(this);
            if (revealColumns == null || revealColumns.Length == 0)
                return;

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            for (int i = 0; i < revealColumns.Length; i++)
            {
                CanvasGroup column = revealColumns[i];
                if (column == null)
                    continue;

                Transform columnTransform = column.transform;
                column.alpha = 0f;
                columnTransform.localScale = new Vector3(0.92f, 0.92f, 1f);
                float at = 0.06f * i;
                sequence.Insert(at, columnTransform.DOScale(1f, 0.28f).SetEase(Ease.OutBack, 1.4f).SetUpdate(true));
                sequence.Insert(at, column.DOFade(1f, 0.16f).SetEase(Ease.OutQuad).SetUpdate(true));
            }
        }

        private void BindPreview()
        {
            SpeciesDefinitionSO species = ResolveSpecies(selectedStorage);
            CombatVisualProfileSO visual = ResolveVisual(selectedStorage);
            if (previewPortrait != null)
            {
                previewPortrait.sprite = visual != null ? visual.portrait : null;
                previewPortrait.enabled = previewPortrait.sprite != null;
                previewPortrait.color = Color.white;
            }

            if (selectedStorage == null || selectedStorage.snapshot == null)
            {
                if (previewName != null)
                    previewName.text = "NO CHAINLING";
                if (previewMeta != null)
                    previewMeta.text = "Choose a stored Chainling.";
                if (previewBody != null)
                    previewBody.text = "Deposited Chainlings recover while they stay in storage.";
                SetHpFill(0f, false);
                return;
            }

            ChainlingSnapshot snapshot = selectedStorage.snapshot;
            ChainlingVitalsDisplay vitals = ChainlingVitalsUtility.BuildDisplay(snapshot, species);
            if (previewName != null)
                previewName.text = ResolveName(selectedStorage);
            if (previewMeta != null)
            {
                string speciesName = species != null && !string.IsNullOrWhiteSpace(species.displayName)
                    ? species.displayName
                    : snapshot.speciesId;
                previewMeta.text = $"{speciesName}  ·  {Mathf.Max(0, snapshot.stars)}★";
            }

            if (previewBody != null)
            {
                previewBody.text = vitals.IsFainted
                    ? "FAINTED. Storage will not revive a downed Chainling."
                    : $"HP {vitals.Label}. Recovers while this Chainling stays deposited.";
            }

            SetHpFill(vitals.Fill01, vitals.IsFainted);
        }

        private void SetHpFill(float fill01, bool fainted)
        {
            if (previewHpFill == null)
                return;

            previewHpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
            previewHpFill.color = fainted
                ? new Color(0.55f, 0.22f, 0.22f, 1f)
                : new Color(0.45f, 0.85f, 0.38f, 1f);
        }

        private void PlayCue(PlayerMenuSfxCue cue)
        {
            IPlayerMenuSfxPlayer player = PlayerMenuSfxResolver.Resolve(sfxPlayerBehaviour, ref cachedSfxPlayerBehaviour);
            player?.PlayCue(cue);
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

        private ChainlingDetailScreen ResolveDetailPrefab()
        {
            if (detailPrefab != null)
                return detailPrefab;

            ChainlingsPartyScreen party = FindObjectOfType<ChainlingsPartyScreen>(true);
            if (party != null && party.DetailViewPrefab != null)
                detailPrefab = party.DetailViewPrefab;

            return detailPrefab;
        }

        private MonoBehaviour ResolveCosmeticInventory()
        {
            return FindObjectOfType<CoreCosmeticInventoryService>(true);
        }

        private MonoBehaviour ResolveOrAddMenuService<TService, TFallback>()
            where TFallback : MonoBehaviour, TService
        {
            TFallback existing = FindObjectOfType<TFallback>(true);
            if (existing != null)
                return existing;

            TFallback created = GetComponent<TFallback>();
            if (created == null)
                created = gameObject.AddComponent<TFallback>();

            SupabaseConfigSO config = ResolveSupabaseConfig();
            if (created is SupabaseMoveInventoryProvider moveInventory)
                moveInventory.Configure(config);
            else if (created is SupabaseMenuMoveEquipService moveEquip)
                moveEquip.Configure(config);
            else if (created is SupabaseMenuVisualIdentityService visualIdentity)
                visualIdentity.Configure(config);
            else if (created is SupabaseRebirthService rebirth)
                rebirth.Configure(config);

            return created;
        }

        private SupabaseConfigSO ResolveSupabaseConfig()
        {
            if (supabaseConfig == null)
                supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
            return supabaseConfig;
        }

        private void ArmTerminalInput()
        {
            if (inputContext == null)
                return;

            inputContext.SetInteractionEnabled(false);
            inputContext.SetAnimationHold(true);
            inputContext.enabled = true;
        }

        private void ReleaseTerminalInput()
        {
            if (EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(transform))
                    EventSystem.current.SetSelectedGameObject(null);
            }

            if (inputContext != null)
                inputContext.enabled = false;
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
            if (root != null && !closing)
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

            if (partyRows != null && partyRows.Length > 0 && partyRows[0] != null && partyRows[0].Button != null)
                EventSystem.current.SetSelectedGameObject(partyRows[0].Button.gameObject);
            else if (closeButton != null)
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }
    }
}
