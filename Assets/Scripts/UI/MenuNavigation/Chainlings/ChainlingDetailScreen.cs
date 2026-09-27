using System;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingDetailScreen : MenuScreen
    {
        private enum DetailPanel
        {
            Overview,
            Moves,
            Style,
            Progression,
        }

        [Header("Data Source")]
        [SerializeField] private ChainlingsDataSourceMode dataSourceMode = ChainlingsDataSourceMode.Production;

        [Header("Services")]
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private MonoBehaviour cosmeticInventoryServiceBehaviour;
        [SerializeField] private MonoBehaviour moveInventoryProviderBehaviour;
        [SerializeField] private MonoBehaviour moveEquipServiceBehaviour;
        [SerializeField] private MonoBehaviour visualIdentityServiceBehaviour;
        [SerializeField] private MonoBehaviour rebirthServiceBehaviour;

        [Header("Data")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private ChainlingVisualIdentityDatabaseSO identityDatabase;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [FormerlySerializedAs("rewardConfig")]
        [SerializeField] private ProgressionConfigSO progressionConfig;

        [Header("UI")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text metaLabel;
        [SerializeField] private Image portraitImage;
        [SerializeField] private RectTransform segmentRoot;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private ChainlingOverviewPanel overviewPanel;
        [SerializeField] private ChainlingMoveEquipPanel movePanel;
        [SerializeField] private ChainlingStylePanel stylePanel;
        [SerializeField] private ChainlingProgressionPanel progressionPanel;

        private readonly Button[] segmentButtons = new Button[4];
        private PlayerRosterEntry entry;
        private PlayerRosterSnapshot rosterCache;
        private SpeciesDefinitionSO species;
        private DetailPanel currentPanel;
        private bool eventsBound;

        public bool IsPrefabAuthored =>
            overviewPanel != null &&
            movePanel != null &&
            stylePanel != null &&
            progressionPanel != null &&
            panelRoot != null &&
            segmentRoot != null;

        public ChainlingsDataSourceMode DataSourceMode => dataSourceMode;

        protected override void Awake()
        {
            base.Awake();
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
        }

        public override void Show()
        {
            EnsureBuilt();
            BindPanelEvents();
            base.Show();
        }

        public void SetDataSourceMode(ChainlingsDataSourceMode mode)
        {
            dataSourceMode = mode;
            cosmeticInventoryServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, cosmeticInventoryServiceBehaviour);
            moveInventoryProviderBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, moveInventoryProviderBehaviour);
            moveEquipServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, moveEquipServiceBehaviour);
            visualIdentityServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, visualIdentityServiceBehaviour);
            rebirthServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, rebirthServiceBehaviour);
        }

        public void Configure(
            SupabaseConfigSO config,
            SpeciesDatabaseSO speciesDb,
            CombatVisualDatabaseSO combatVisualDb,
            ChainlingVisualIdentityDatabaseSO chainlingIdentityDb,
            MoveDatabaseSO moveDb,
            ProgressionConfigSO progression,
            MonoBehaviour cosmeticInventoryServiceSource,
            MonoBehaviour moveInventoryProviderSource,
            MonoBehaviour moveEquipServiceSource,
            MonoBehaviour visualIdentityServiceSource,
            MonoBehaviour rebirthServiceSource)
        {
            if (config != null)
                supabaseConfig = config;
            if (speciesDb != null)
                speciesDatabase = speciesDb;
            if (combatVisualDb != null)
                visualDatabase = combatVisualDb;
            if (chainlingIdentityDb != null)
                identityDatabase = chainlingIdentityDb;
            if (moveDb != null)
                moveDatabase = moveDb;
            if (progression != null)
                progressionConfig = progression;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, cosmeticInventoryServiceSource))
                cosmeticInventoryServiceBehaviour = cosmeticInventoryServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, moveInventoryProviderSource))
                moveInventoryProviderBehaviour = moveInventoryProviderSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, moveEquipServiceSource))
                moveEquipServiceBehaviour = moveEquipServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, visualIdentityServiceSource))
                visualIdentityServiceBehaviour = visualIdentityServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, rebirthServiceSource))
                rebirthServiceBehaviour = rebirthServiceSource;

            ConfigurePanels();
            EnsureBuilt();
            BindPanelEvents();
        }

        public void Open(PlayerRosterEntry rosterEntry, PlayerRosterSnapshot cache)
        {
            EnsureBuilt();
            BindPanelEvents();
            entry = rosterEntry;
            rosterCache = cache;
            species = ResolveSpecies(entry);
            ConfigurePanels();
            RefreshHeader();
            BindPanels();
            ShowPanel(DetailPanel.Overview);
            ApplyPreviewIdentity(entry != null && entry.snapshot != null ? entry.snapshot.visualIdentity : null);
        }

        public override void Refresh()
        {
            RefreshHeader();
            BindPanels();
        }

        private void BindPanelEvents()
        {
            if (eventsBound || stylePanel == null || progressionPanel == null)
                return;

            stylePanel.PreviewIdentityChanged += ApplyPreviewIdentity;
            stylePanel.Saved += HandleStyleSaved;
            progressionPanel.Reborn += HandleReborn;
            eventsBound = true;
        }

        private void ConfigurePanels()
        {
            if (stylePanel != null)
                stylePanel.Configure(
                    supabaseConfig,
                    visualDatabase,
                    identityDatabase,
                    cosmeticInventoryServiceBehaviour,
                    visualIdentityServiceBehaviour);
            if (movePanel != null)
                movePanel.Configure(speciesDatabase, moveDatabase, moveInventoryProviderBehaviour, moveEquipServiceBehaviour);
            if (progressionPanel != null)
                progressionPanel.Configure(progressionConfig, rebirthServiceBehaviour);
        }

        private void BindPanels()
        {
            if (overviewPanel != null)
                overviewPanel.Bind(entry, species);
            if (movePanel != null)
                movePanel.Bind(entry);
            if (stylePanel != null)
                stylePanel.Bind(entry, rosterCache);
            if (progressionPanel != null)
                progressionPanel.Bind(entry, species, rosterCache);
        }

        private void RefreshHeader()
        {
            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            string name = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.displayName)
                ? snapshot.displayName
                : species != null && !string.IsNullOrWhiteSpace(species.displayName)
                    ? species.displayName
                    : snapshot != null ? snapshot.speciesId : "Chainling";

            if (nameLabel != null)
                nameLabel.text = name;

            if (metaLabel != null && snapshot != null)
            {
                string typeText = species != null
                    ? species.primaryType.ToString()
                    : "Unknown type";
                metaLabel.text =
                    $"{typeText} | Stars {snapshot.stars}/{CombatConstants.STAR_MAX} | " +
                    $"{ChainlingPlayerTerms.FormatCycleLabel(snapshot.rebirthCount)} | Evo {snapshot.evolutionStage}";
            }
            else if (metaLabel != null)
            {
                metaLabel.text = "";
            }
        }

        private void HandleStyleSaved(ChainlingVisualIdentity identity)
        {
            if (entry != null && entry.snapshot != null)
                entry.snapshot.visualIdentity = ChainlingVisualIdentity.CloneForPlayerSave(identity);
            ApplyPreviewIdentity(identity);
        }

        private void HandleReborn(PlayerRosterEntry updatedEntry)
        {
            if (updatedEntry != null)
                entry = updatedEntry;
            species = ResolveSpecies(entry);
            RefreshHeader();
            ApplyPreviewIdentity(entry != null && entry.snapshot != null ? entry.snapshot.visualIdentity : null);
        }

        private void ApplyPreviewIdentity(ChainlingVisualIdentity identity)
        {
            if (portraitImage == null || entry == null || entry.snapshot == null)
                return;

            ChainlingVisualIdentity normalized = ChainlingVisualIdentity.CloneOrDefault(identity);
            if (normalized == null)
                normalized = ChainlingVisualIdentity.CloneOrDefault(entry.snapshot.visualIdentity);
            ChainlingType entangledType = entry.snapshot.isEntangled
                ? entry.snapshot.entangledType
                : ChainlingType.None;
            CombatVisualProfileSO profile = visualDatabase != null
                ? visualDatabase.GetProfile(entry.snapshot.speciesId, normalized.visualVariantId, entangledType)
                : null;

            Sprite sprite = ResolvePortrait(profile);
            portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
            portraitImage.preserveAspect = true;
        }

        private static Sprite ResolvePortrait(CombatVisualProfileSO profile)
        {
            if (profile == null)
                return null;
            if (profile.portrait != null)
                return profile.portrait;
            if (profile.idleSequence != null && profile.idleSequence.Length > 0)
                return profile.idleSequence[0];
            return null;
        }

        private SpeciesDefinitionSO ResolveSpecies(PlayerRosterEntry rosterEntry)
        {
            if (speciesDatabase == null || rosterEntry == null || rosterEntry.snapshot == null)
                return null;
            return speciesDatabase.GetById(rosterEntry.snapshot.speciesId);
        }

        private void ShowPanel(DetailPanel panel)
        {
            currentPanel = panel;
            SetPanelActive(overviewPanel, panel == DetailPanel.Overview);
            SetPanelActive(movePanel, panel == DetailPanel.Moves);
            SetPanelActive(stylePanel, panel == DetailPanel.Style);
            SetPanelActive(progressionPanel, panel == DetailPanel.Progression);
            RefreshSegments();
        }

        private static void SetPanelActive(Component panel, bool active)
        {
            if (panel != null)
                panel.gameObject.SetActive(active);
        }

        private void RefreshSegments()
        {
            BindSegment(0, DetailPanel.Overview, "Overview");
            BindSegment(1, DetailPanel.Moves, "Moves");
            BindSegment(2, DetailPanel.Style, "Style");
            BindSegment(3, DetailPanel.Progression, "Progress");
        }

        private void BindSegment(int index, DetailPanel panel, string label)
        {
            Button button = index >= 0 && index < segmentButtons.Length ? segmentButtons[index] : null;
            if (button == null)
                return;

            Image image = button.targetGraphic as Image;
            if (image != null)
                image.color = currentPanel == panel ? PlayerMenuUiFactory.Accent : PlayerMenuUiFactory.PanelAlt;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowPanel(panel));
        }

        private void EnsureBuilt()
        {
            if (IsPrefabAuthored)
            {
                CacheSegmentButtonsFromRoot();
                AttachSegmentFocusVisuals();
                return;
            }

            if (overviewPanel != null && movePanel != null && stylePanel != null && progressionPanel != null)
                return;

            PlayerMenuUiFactory.Stretch((RectTransform)transform, 0f, 0f, 0f, 0f);

            var root = PlayerMenuUiFactory.GetOrAdd<VerticalLayoutGroup>(gameObject);
            root.padding = new RectOffset(22, 22, 18, 18);
            root.spacing = 12f;
            root.childControlWidth = true;
            root.childControlHeight = false;
            root.childForceExpandWidth = true;
            root.childForceExpandHeight = false;

            var header = PlayerMenuUiFactory.CreateRect("DetailHeader", transform);
            PlayerMenuUiFactory.LayoutElement(header.gameObject, preferredHeight: 150f);
            var headerLayout = PlayerMenuUiFactory.HorizontalLayout(header.gameObject, 16f);
            headerLayout.childForceExpandWidth = false;

            var portraitBack = PlayerMenuUiFactory.CreateImage("PortraitBack", header, PlayerMenuUiFactory.PanelAlt);
            PlayerMenuUiFactory.LayoutElement(portraitBack.gameObject, preferredWidth: 150f, preferredHeight: 150f);
            portraitImage = portraitImage != null
                ? portraitImage
                : PlayerMenuUiFactory.CreateImage("Portrait", portraitBack.transform, Color.clear);
            PlayerMenuUiFactory.Stretch(portraitImage.rectTransform, 10f, 10f, 10f, 10f);

            var textColumn = PlayerMenuUiFactory.CreateRect("HeaderText", header);
            PlayerMenuUiFactory.VerticalLayout(textColumn.gameObject, 6f);
            PlayerMenuUiFactory.LayoutElement(textColumn.gameObject, flexibleWidth: 1f);
            nameLabel = nameLabel != null
                ? nameLabel
                : PlayerMenuUiFactory.CreateText("Name", textColumn, "Chainling", 34f, TextAlignmentOptions.MidlineLeft, false);
            PlayerMenuUiFactory.LayoutElement(nameLabel.gameObject, preferredHeight: 50f);
            metaLabel = metaLabel != null
                ? metaLabel
                : PlayerMenuUiFactory.CreateText("Meta", textColumn, "", 20f, TextAlignmentOptions.TopLeft, true);
            metaLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(metaLabel.gameObject, preferredHeight: 78f);

            segmentRoot = segmentRoot != null ? segmentRoot : PlayerMenuUiFactory.CreateRect("Segments", transform);
            PlayerMenuUiFactory.LayoutElement(segmentRoot.gameObject, preferredHeight: 56f);
            PlayerMenuUiFactory.HorizontalLayout(segmentRoot.gameObject, 8f);
            for (int i = 0; i < segmentButtons.Length; i++)
            {
                if (segmentButtons[i] == null)
                {
                    segmentButtons[i] = PlayerMenuUiFactory.CreateButton($"Segment_{i:D2}", segmentRoot, "", PlayerMenuUiFactory.PanelAlt);
                    PlayerMenuUiFactory.LayoutElement(segmentButtons[i].gameObject, preferredHeight: 52f, flexibleWidth: 1f);
                }
            }

            panelRoot = panelRoot != null ? panelRoot : PlayerMenuUiFactory.CreateRect("PanelRoot", transform);
            PlayerMenuUiFactory.LayoutElement(panelRoot.gameObject, preferredHeight: 560f, flexibleWidth: 1f);

            overviewPanel = overviewPanel != null ? overviewPanel : CreatePanel<ChainlingOverviewPanel>("OverviewPanel");
            movePanel = movePanel != null ? movePanel : CreatePanel<ChainlingMoveEquipPanel>("MovePanel");
            stylePanel = stylePanel != null ? stylePanel : CreatePanel<ChainlingStylePanel>("StylePanel");
            progressionPanel = progressionPanel != null ? progressionPanel : CreatePanel<ChainlingProgressionPanel>("ProgressionPanel");

            RefreshSegments();
            AttachSegmentFocusVisuals();
        }

        private void AttachSegmentFocusVisuals()
        {
            for (int i = 0; i < segmentButtons.Length; i++)
            {
                if (segmentButtons[i] != null)
                    MenuSelectableFocusVisual.EnsureOn(segmentButtons[i]);
            }
        }

        private void CacheSegmentButtonsFromRoot()
        {
            if (segmentRoot == null)
                return;

            for (int i = 0; i < segmentButtons.Length && i < segmentRoot.childCount; i++)
            {
                if (segmentButtons[i] != null)
                    continue;

                segmentButtons[i] = segmentRoot.GetChild(i).GetComponent<Button>();
            }
        }

        private T CreatePanel<T>(string name) where T : Component
        {
            var background = PlayerMenuUiFactory.CreateImage(name, panelRoot, PlayerMenuUiFactory.Panel);
            PlayerMenuUiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);
            return background.gameObject.AddComponent<T>();
        }
    }
}
