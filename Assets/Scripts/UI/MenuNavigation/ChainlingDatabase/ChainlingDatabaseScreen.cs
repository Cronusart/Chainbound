using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Zone signal log. Unseen slots stay unregistered behind a blank mark.
    /// A seen or captured row opens the center sheet. Capture is what fills the rest of the file.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChainlingDatabaseScreen : MenuScreen
    {
        enum RowFilter
        {
            All = 0,
            Seen = 1,
            Caught = 2,
        }

        [SerializeField] private EncounterCatalogSourceSO encounterCatalog;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private Sprite idleButtonSprite;
        [SerializeField] private Sprite selectedButtonSprite;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private Texture scanlineTexture;

        public EncounterCatalogSourceSO EncounterCatalog => encounterCatalog;
        public Sprite IdleButtonSprite => idleButtonSprite;
        public Sprite SelectedButtonSprite => selectedButtonSprite;
        public TMP_FontAsset LabelFont => labelFont;

        EncounterCatalogIndex catalog = EncounterCatalogIndex.Build((EncounterCatalogSourceSO)null);
        SpeciesDatabaseSO speciesDatabase;
        ChainlingDexSnapshot dexSnapshot = new ChainlingDexSnapshot();
        readonly HashSet<string> owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly List<EncounterCatalogSpecies> visibleSpecies = new List<EncounterCatalogSpecies>();
        ChainlingDatabaseWidgets widgets;
        RowFilter filter = RowFilter.All;
        int zoneIndex;
        int speciesIndex;
        int requestId;
        bool sheetOpen;
        bool liveFetchArmed;
        ChainlingDatabaseEntryView currentView = new ChainlingDatabaseEntryView();

        public void EnsureLayoutForEditor()
        {
            ChainlingDatabaseLayout.Ensure(transform, Chrome());
            CacheWidgets();
        }

        public void BindForTests(
            IReadOnlyList<EncounterZoneSO> zones,
            SpeciesDatabaseSO species,
            ChainlingDexSnapshot dex,
            ISet<string> ownedSpeciesIds)
        {
            liveFetchArmed = false;
            EnsureLayoutForEditor();
            speciesDatabase = species;
            catalog = EncounterCatalogIndex.Build(zones, species);
            dexSnapshot = dex ?? new ChainlingDexSnapshot();
            owned.Clear();
            if (ownedSpeciesIds != null)
            {
                foreach (string id in ownedSpeciesIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        owned.Add(id.Trim());
                }
            }

            zoneIndex = 0;
            speciesIndex = 0;
            filter = RowFilter.All;
            BindColumns(reopenSheet: false);
        }

        public override void Refresh()
        {
            liveFetchArmed = true;
            int request = ++requestId;
            EnsureLayoutForEditor();
            speciesDatabase = encounterCatalog != null ? encounterCatalog.speciesDatabase : null;
            catalog = EncounterCatalogIndex.Build(encounterCatalog);
            dexSnapshot = LoadDex(request);
            owned.Clear();
            CollectOwned(owned);
            if (catalog.Zones.Count == 0)
                zoneIndex = 0;
            else
                zoneIndex = Mathf.Clamp(zoneIndex, 0, catalog.Zones.Count - 1);
            BindColumns(reopenSheet: sheetOpen);
            if (Application.isPlaying)
                RefreshRosterAsync(request);
        }

        public override void Hide()
        {
            CloseSheet(restoreSelection: false);
            base.Hide();
        }

        public override bool TryHandleCancel()
        {
            if (!sheetOpen)
                return false;

            CloseSheet(restoreSelection: true);
            return true;
        }

        void Update()
        {
            if (!IsVisible)
                return;

            Gamepad pad = Gamepad.current;
            if (pad == null)
                return;

            if (pad.leftShoulder.wasPressedThisFrame)
                CycleZone(-1);
            else if (pad.rightShoulder.wasPressedThisFrame)
                CycleZone(1);
        }

        ChainlingDatabaseChrome Chrome()
        {
            return new ChainlingDatabaseChrome
            {
                idleSprite = idleButtonSprite,
                selectedSprite = selectedButtonSprite,
                font = labelFont,
                scanlineTexture = scanlineTexture,
            };
        }

        void CacheWidgets()
        {
            widgets = new ChainlingDatabaseWidgets
            {
                zoneContent = transform.Find(ChainlingDatabaseLayout.ZoneContentPath),
                speciesContent = transform.Find(ChainlingDatabaseLayout.SpeciesContentPath),
                filterButton = transform.Find("TopCenter/Header/FilterButton")?.GetComponent<Button>(),
                filterLabel = transform.Find("TopCenter/Header/FilterButton/Label")?.GetComponent<TMP_Text>(),
                emptyLabel = transform.Find("TopCenter/EmptyLabel")?.GetComponent<TMP_Text>(),
                unknownMark = transform.Find("TopRight/UnknownMark")?.GetComponent<Image>(),
                portraitWell = transform.Find("TopRight/PortraitWell")?.GetComponent<Image>(),
                portrait = transform.Find("TopRight/Portrait")?.GetComponent<Image>(),
                nameLabel = transform.Find("TopRight/Name")?.GetComponent<TMP_Text>(),
                fact1 = transform.Find("TopRight/Fact1")?.GetComponent<TMP_Text>(),
                fact2 = transform.Find("TopRight/Fact2")?.GetComponent<TMP_Text>(),
                fact3 = transform.Find("TopRight/Fact3")?.GetComponent<TMP_Text>(),
                dialogueTitle = transform.Find("DialogueLowerCenter/Title")?.GetComponent<TMP_Text>(),
                dialogueLine1 = transform.Find("DialogueLowerCenter/Line1")?.GetComponent<TMP_Text>(),
                dialogueLine2 = transform.Find("DialogueLowerCenter/Line2")?.GetComponent<TMP_Text>(),
                dialoguePrompt = transform.Find("DialogueLowerCenter/Prompt")?.GetComponent<TMP_Text>(),
                sheet = transform.Find("DatabaseSheet")?.gameObject,
                sheetTitle = transform.Find("DatabaseSheet/Title")?.GetComponent<TMP_Text>(),
                sheetBody = transform.Find("DatabaseSheet/Body")?.GetComponent<TMP_Text>(),
                dossierButton = transform.Find("DatabaseSheet/DossierButton")?.GetComponent<Button>(),
            };

            if (widgets.filterButton != null)
            {
                widgets.filterButton.onClick.RemoveAllListeners();
                widgets.filterButton.onClick.AddListener(CycleFilter);
            }

            if (widgets.dossierButton != null)
            {
                widgets.dossierButton.onClick.RemoveAllListeners();
                widgets.dossierButton.onClick.AddListener(OpenDossier);
            }
        }

        void BindColumns(bool reopenSheet)
        {
            BindZoneRail();
            BindSpeciesColumn();
            WireNavigation();
            ShowCurrent(reopenSheet);
        }

        void BindZoneRail()
        {
            if (widgets == null || widgets.zoneContent == null)
                return;

            List<Button> buttons = DirectButtons(widgets.zoneContent);
            EnsureCount(widgets.zoneContent, buttons, catalog.Zones.Count, "ZoneRow_");
            for (int i = 0; i < buttons.Count; i++)
            {
                bool active = i < catalog.Zones.Count;
                buttons[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                EncounterCatalogZone zone = catalog.Zones[i];
                SetLabel(buttons[i], ZoneLabel(zone));
                int captured = i;
                buttons[i].onClick.RemoveAllListeners();
                ChainlingDatabaseRowSignal signal = buttons[i].GetComponent<ChainlingDatabaseRowSignal>();
                if (signal != null)
                {
                    signal.index = captured;
                    signal.selected = OnZoneSelected;
                }
            }
        }

        void BindSpeciesColumn()
        {
            visibleSpecies.Clear();
            if (widgets == null || widgets.speciesContent == null)
                return;

            EncounterCatalogZone zone = CurrentZone();
            if (zone != null && zone.species != null)
            {
                for (int i = 0; i < zone.species.Count; i++)
                {
                    EncounterCatalogSpecies slot = zone.species[i];
                    if (slot != null && PassesFilter(slot))
                        visibleSpecies.Add(slot);
                }
            }

            if (visibleSpecies.Count == 0)
                speciesIndex = 0;
            else
                speciesIndex = Mathf.Clamp(speciesIndex, 0, visibleSpecies.Count - 1);

            List<Button> buttons = DirectButtons(widgets.speciesContent);
            EnsureCount(widgets.speciesContent, buttons, visibleSpecies.Count, "SpeciesRow_");
            for (int i = 0; i < buttons.Count; i++)
            {
                bool active = i < visibleSpecies.Count;
                buttons[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                EncounterCatalogSpecies slot = visibleSpecies[i];
                ChainlingDatabaseEntryView view = BuildView(slot);
                SetLabel(buttons[i], view.rowLabel);
                int captured = i;
                buttons[i].onClick.RemoveAllListeners();
                buttons[i].onClick.AddListener(() => OnSpeciesSubmitted(captured));
                ChainlingDatabaseRowSignal signal = buttons[i].GetComponent<ChainlingDatabaseRowSignal>();
                if (signal != null)
                {
                    signal.index = captured;
                    signal.selected = OnSpeciesSelected;
                }
            }

            if (widgets.emptyLabel != null)
                widgets.emptyLabel.gameObject.SetActive(visibleSpecies.Count == 0);
            if (widgets.filterLabel != null)
                widgets.filterLabel.text = filter.ToString();
        }

        void ShowCurrent(bool reopenSheet)
        {
            EncounterCatalogSpecies slot = CurrentSpecies();
            if (slot == null)
            {
                EncounterCatalogZone zone = CurrentZone();
                currentView = new ChainlingDatabaseEntryView
                {
                    lowerTitle = zone != null ? zone.displayName : "Species",
                    lowerLine1 = "None logged",
                    prompt = ChainlingDatabaseEntryBuilder.PromptBack,
                    sheetPrompt = ChainlingDatabaseEntryBuilder.PromptBack,
                };
                ApplyNote(currentView, false);
                return;
            }

            currentView = BuildView(slot);
            ApplyNote(currentView, reopenSheet && currentView.canOpenSheet);
        }

        ChainlingDatabaseEntryView BuildView(EncounterCatalogSpecies slot)
        {
            SpeciesDefinitionSO species = speciesDatabase != null ? speciesDatabase.GetById(slot.speciesId) : null;
            ChainlingDatabaseTier tier = ChainlingDatabaseEntryBuilder.Resolve(slot.speciesId, dexSnapshot, owned);
            return ChainlingDatabaseEntryBuilder.Build(
                slot,
                species,
                tier,
                catalog.ZoneDisplayNamesFor(slot.speciesId),
                owned.Contains(slot.speciesId));
        }

        void ApplyNote(ChainlingDatabaseEntryView view, bool openSheet)
        {
            if (view == null)
                view = new ChainlingDatabaseEntryView();

            SetText(widgets != null ? widgets.nameLabel : null, string.IsNullOrEmpty(view.name) ? view.rowLabel : view.name);
            SetText(widgets != null ? widgets.fact1 : null, view.typeLabel);
            SetText(widgets != null ? widgets.fact2 : null, view.zoneLabel);
            SetText(widgets != null ? widgets.fact3 : null, JoinFact(view.timeLabel, view.frequencyLabel));
            SetText(widgets != null ? widgets.dialogueTitle : null, view.lowerTitle);
            SetText(widgets != null ? widgets.dialogueLine1 : null, view.lowerLine1);
            SetText(widgets != null ? widgets.dialogueLine2 : null, view.lowerLine2);
            ApplyPortrait(view);

            sheetOpen = openSheet && view.canOpenSheet;
            if (widgets != null && widgets.sheet != null)
                widgets.sheet.SetActive(sheetOpen);
            if (sheetOpen)
            {
                SetText(widgets.sheetTitle, view.sheetTitle);
                SetText(widgets.sheetBody, view.sheetBody);
                if (widgets.dossierButton != null)
                    widgets.dossierButton.gameObject.SetActive(view.canOpenDossier);
            }

            SetText(widgets != null ? widgets.dialoguePrompt : null, sheetOpen ? view.sheetPrompt : view.prompt);
            if (sheetOpen && view.canOpenDossier && widgets != null && widgets.dossierButton != null && EventSystem.current != null)
                widgets.dossierButton.Select();
        }

        void ApplyPortrait(ChainlingDatabaseEntryView view)
        {
            bool show = view != null && view.showPortrait;
            Sprite sprite = show ? ResolvePortrait(CurrentSpecies()) : null;
            if (widgets == null)
                return;

            if (widgets.unknownMark != null)
                widgets.unknownMark.gameObject.SetActive(!show);
            if (widgets.portraitWell != null)
                widgets.portraitWell.gameObject.SetActive(show);
            if (widgets.portrait == null)
                return;

            widgets.portrait.sprite = show ? sprite : null;
            widgets.portrait.enabled = show && sprite != null;
            widgets.portrait.color = Color.white;
        }

        Sprite ResolvePortrait(EncounterCatalogSpecies slot)
        {
            if (visualDatabase == null || slot == null)
                return null;

            CombatVisualProfileSO profile = visualDatabase.GetProfile(slot.speciesId);
            return profile != null ? profile.portrait : null;
        }

        void OnZoneSelected(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.Zones.Count)
                return;
            if (index == zoneIndex && !sheetOpen)
                return;

            zoneIndex = index;
            speciesIndex = 0;
            CloseSheet(restoreSelection: false);
            BindSpeciesColumn();
            WireNavigation();
            ShowCurrent(reopenSheet: false);
        }

        void OnSpeciesSelected(int index)
        {
            if (index < 0 || index >= visibleSpecies.Count)
                return;

            speciesIndex = index;
            if (sheetOpen)
                CloseSheet(restoreSelection: false);
            ShowCurrent(reopenSheet: false);
        }

        void OnSpeciesSubmitted(int index)
        {
            if (index < 0 || index >= visibleSpecies.Count)
                return;

            bool sameOpenRow = sheetOpen && index == speciesIndex;
            speciesIndex = index;
            ChainlingDatabaseEntryView view = BuildView(visibleSpecies[index]);
            currentView = view;
            if (!view.canOpenSheet)
            {
                ApplyNote(view, false);
                return;
            }

            if (sameOpenRow && view.canOpenDossier)
            {
                OpenDossier();
                return;
            }

            ApplyNote(view, true);
        }

        void OpenDossier()
        {
            EncounterCatalogSpecies slot = CurrentSpecies();
            if (slot == null || currentView == null || !currentView.canOpenDossier)
                return;

            ChainlingsPartyScreen party = FindFirstObjectByType<ChainlingsPartyScreen>(FindObjectsInactive.Include);
            if (party != null && party.TryOpenOwnedSpeciesDossier(slot.speciesId))
                return;

            SetText(widgets != null ? widgets.dialogueLine1 : null, ChainlingDatabaseEntryBuilder.FileComplete);
        }

        void CycleFilter()
        {
            filter = (RowFilter)(((int)filter + 1) % 3);
            speciesIndex = 0;
            CloseSheet(restoreSelection: false);
            BindSpeciesColumn();
            WireNavigation();
            ShowCurrent(reopenSheet: false);
        }

        void CycleZone(int direction)
        {
            if (catalog == null || catalog.Zones.Count == 0)
                return;

            bool keepSpecies = IsSpeciesFocused();
            int keepIndex = speciesIndex;
            zoneIndex = (zoneIndex + direction + catalog.Zones.Count) % catalog.Zones.Count;
            speciesIndex = keepIndex;
            CloseSheet(restoreSelection: false);
            BindZoneRail();
            BindSpeciesColumn();
            WireNavigation();
            ShowCurrent(reopenSheet: false);
            if (EventSystem.current == null)
                return;
            if (keepSpecies && ActiveButtons(widgets != null ? widgets.speciesContent : null).Count > 0)
                SelectButton(widgets.speciesContent, speciesIndex);
            else
                SelectButton(widgets != null ? widgets.zoneContent : null, zoneIndex);
        }

        void CloseSheet(bool restoreSelection)
        {
            sheetOpen = false;
            if (widgets != null && widgets.sheet != null)
                widgets.sheet.SetActive(false);
            if (currentView != null)
                SetText(widgets != null ? widgets.dialoguePrompt : null, currentView.prompt);
            if (restoreSelection)
                SelectButton(widgets != null ? widgets.speciesContent : null, speciesIndex);
        }

        void WireNavigation()
        {
            List<Button> zones = ActiveButtons(widgets != null ? widgets.zoneContent : null);
            List<Button> species = ActiveButtons(widgets != null ? widgets.speciesContent : null);
            Button filterButton = widgets != null ? widgets.filterButton : null;
            LinkColumn(zones, null, species.Count > 0 ? species[0] : filterButton);
            if (filterButton != null)
            {
                Navigation navigation = filterButton.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = null;
                navigation.selectOnDown = species.Count > 0 ? species[0] : null;
                navigation.selectOnLeft = zones.Count > 0 ? zones[Mathf.Clamp(zoneIndex, 0, zones.Count - 1)] : null;
                navigation.selectOnRight = null;
                filterButton.navigation = navigation;
            }

            Button back = zones.Count > 0 ? zones[Mathf.Clamp(zoneIndex, 0, zones.Count - 1)] : null;
            LinkColumn(species, filterButton, null);
            for (int i = 0; i < species.Count; i++)
            {
                Navigation navigation = species[i].navigation;
                navigation.selectOnLeft = back;
                navigation.selectOnRight = null;
                if (i == 0)
                    navigation.selectOnUp = filterButton;
                species[i].navigation = navigation;
            }

            if (widgets != null && widgets.dossierButton != null)
            {
                Navigation navigation = widgets.dossierButton.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = null;
                navigation.selectOnDown = null;
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                widgets.dossierButton.navigation = navigation;
            }
        }

        static void LinkColumn(List<Button> column, Selectable upFromFirst, Selectable right)
        {
            for (int i = 0; i < column.Count; i++)
            {
                Navigation navigation = column[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = i > 0 ? column[i - 1] : upFromFirst;
                navigation.selectOnDown = i < column.Count - 1 ? column[i + 1] : null;
                navigation.selectOnLeft = null;
                navigation.selectOnRight = right;
                column[i].navigation = navigation;
            }
        }

        string ZoneLabel(EncounterCatalogZone zone)
        {
            if (zone == null)
                return "";
            if (zone.species == null || zone.species.Count == 0)
                return zone.displayName + "\nUnlogged";

            for (int i = 0; i < zone.species.Count; i++)
            {
                EncounterCatalogSpecies slot = zone.species[i];
                if (slot == null)
                    continue;
                ChainlingDatabaseTier tier = ChainlingDatabaseEntryBuilder.Resolve(slot.speciesId, dexSnapshot, owned);
                if (tier != ChainlingDatabaseTier.Unregistered)
                    return zone.displayName;
            }

            return zone.displayName + "\nUnlogged";
        }

        bool PassesFilter(EncounterCatalogSpecies slot)
        {
            ChainlingDatabaseTier tier = ChainlingDatabaseEntryBuilder.Resolve(slot.speciesId, dexSnapshot, owned);
            if (filter == RowFilter.Seen)
                return tier != ChainlingDatabaseTier.Unregistered;
            if (filter == RowFilter.Caught)
                return tier == ChainlingDatabaseTier.Captured;
            return true;
        }

        EncounterCatalogZone CurrentZone()
        {
            if (catalog == null || zoneIndex < 0 || zoneIndex >= catalog.Zones.Count)
                return null;
            return catalog.Zones[zoneIndex];
        }

        EncounterCatalogSpecies CurrentSpecies()
        {
            if (speciesIndex < 0 || speciesIndex >= visibleSpecies.Count)
                return null;
            return visibleSpecies[speciesIndex];
        }

        ChainlingDexSnapshot LoadDex(int request)
        {
            var snapshot = new ChainlingDexSnapshot();
            IChainlingDexService service = FindService<IChainlingDexService>();
            if (service != null)
            {
                Task<ChainlingDexSnapshot> task = service.FetchDexAsync(SessionProfile(), SessionWallet());
                if (task != null && task.IsCompletedSuccessfully && task.Result != null)
                    snapshot = task.Result;
                else if (task != null && !task.IsCompleted)
                    ContinueDex(task, request);
            }

            ChainlingDexRecords.UnionInto(snapshot);
            return snapshot;
        }

        async void ContinueDex(Task<ChainlingDexSnapshot> task, int request)
        {
            try
            {
                ChainlingDexSnapshot loaded = await task;
                if (request != requestId || this == null || loaded == null)
                    return;

                dexSnapshot = loaded;
                ChainlingDexRecords.UnionInto(dexSnapshot);
                BindColumns(reopenSheet: sheetOpen);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ChainlingDatabase] Dex fetch failed: " + exception.Message);
            }
        }

        async void RefreshRosterAsync(int request)
        {
            if (!liveFetchArmed)
                return;

            try
            {
                GameSessionContext session = GameSessionContext.Instance;
                IPlayerRosterProvider provider = FindService<IPlayerRosterProvider>();
                if (session == null || provider == null)
                    return;

                PlayerRosterSnapshot roster = await PlayerProfileReadStore.ResolveOrCreate().RefreshRosterAsync(
                    provider,
                    session.PlayerProfileId,
                    session.WalletAddress);
                if (request != requestId || this == null || roster == null)
                    return;

                AddRoster(owned, roster);
                BindColumns(reopenSheet: sheetOpen);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ChainlingDatabase] Roster fetch failed: " + exception.Message);
            }
        }

        void CollectOwned(HashSet<string> destination)
        {
            ChainlingsPartyScreen party = FindFirstObjectByType<ChainlingsPartyScreen>(FindObjectsInactive.Include);
            if (party != null)
                party.CopyOwnedSpeciesIds(destination);

            GameSessionContext session = GameSessionContext.Instance;
            if (session == null)
                return;
            if (PlayerProfileReadStore.ResolveOrCreate().TryGetRoster(
                    session.PlayerProfileId,
                    session.WalletAddress,
                    out PlayerRosterSnapshot roster))
                AddRoster(destination, roster);
        }

        static void AddRoster(ISet<string> destination, PlayerRosterSnapshot roster)
        {
            if (destination == null || roster == null || roster.chainlings == null)
                return;

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                string id = roster.chainlings[i]?.snapshot?.speciesId;
                if (!string.IsNullOrWhiteSpace(id))
                    destination.Add(id.Trim());
            }
        }

        static T FindService<T>() where T : class
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is T service)
                    return service;
            }

            return null;
        }

        static string SessionProfile()
        {
            GameSessionContext session = GameSessionContext.Instance;
            return session != null ? session.PlayerProfileId : "";
        }

        static string SessionWallet()
        {
            GameSessionContext session = GameSessionContext.Instance;
            return session != null ? session.WalletAddress : "";
        }

        static void EnsureCount(Transform content, List<Button> buttons, int needed, string prefix)
        {
            if (content == null || buttons.Count == 0 || buttons.Count >= needed)
                return;

            Button template = buttons[0];
            while (buttons.Count < needed)
            {
                Button clone = Instantiate(template, content);
                clone.name = prefix + buttons.Count.ToString("00");
                clone.onClick.RemoveAllListeners();
                buttons.Add(clone);
            }
        }

        static List<Button> DirectButtons(Transform content)
        {
            var buttons = new List<Button>();
            if (content == null)
                return buttons;

            for (int i = 0; i < content.childCount; i++)
            {
                Button button = content.GetChild(i).GetComponent<Button>();
                if (button != null)
                    buttons.Add(button);
            }

            return buttons;
        }

        static List<Button> ActiveButtons(Transform content)
        {
            List<Button> buttons = DirectButtons(content);
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                if (!buttons[i].gameObject.activeSelf)
                    buttons.RemoveAt(i);
            }

            return buttons;
        }

        static void SetLabel(Button button, string value)
        {
            TMP_Text label = button != null ? button.transform.Find("Label")?.GetComponent<TMP_Text>() : null;
            SetText(label, value);
        }

        static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value ?? "";
        }

        static string JoinFact(string time, string frequency)
        {
            if (string.IsNullOrWhiteSpace(time))
                return frequency ?? "";
            if (string.IsNullOrWhiteSpace(frequency))
                return time;
            return time + " · " + frequency;
        }

        bool IsSpeciesFocused()
        {
            if (EventSystem.current == null || widgets == null || widgets.speciesContent == null)
                return false;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            return selected != null && selected.transform.IsChildOf(widgets.speciesContent);
        }

        static void SelectButton(Transform content, int index)
        {
            if (EventSystem.current == null)
                return;

            List<Button> buttons = ActiveButtons(content);
            if (buttons.Count == 0)
                return;

            buttons[Mathf.Clamp(index, 0, buttons.Count - 1)].Select();
        }
    }

    sealed class ChainlingDatabaseWidgets
    {
        public Transform zoneContent;
        public Transform speciesContent;
        public Button filterButton;
        public TMP_Text filterLabel;
        public TMP_Text emptyLabel;
        public Image unknownMark;
        public Image portraitWell;
        public Image portrait;
        public TMP_Text nameLabel;
        public TMP_Text fact1;
        public TMP_Text fact2;
        public TMP_Text fact3;
        public TMP_Text dialogueTitle;
        public TMP_Text dialogueLine1;
        public TMP_Text dialogueLine2;
        public TMP_Text dialoguePrompt;
        public GameObject sheet;
        public TMP_Text sheetTitle;
        public TMP_Text sheetBody;
        public Button dossierButton;
    }
}
