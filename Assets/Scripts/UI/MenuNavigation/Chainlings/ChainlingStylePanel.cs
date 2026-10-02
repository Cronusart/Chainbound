using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingStylePanel : MonoBehaviour
    {
        private enum StyleSlot
        {
            Variant,
            Aura,
            HudCard,
            PartyColumn,
            IdleLoop,
            IdleFlourish,
            Melee,
            Ranged,
            Hit,
            SwapOut,
            Ko,
            Victory,
            SwapIn,
        }

        private struct StyleOption
        {
            public string id;
            public string label;
            public bool free;
        }

        [Header("Services")]
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private MonoBehaviour cosmeticInventoryServiceBehaviour;
        [SerializeField] private MonoBehaviour visualIdentityServiceBehaviour;

        [Header("Data")]
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private ChainlingVisualIdentityDatabaseSO identityDatabase;

        [Header("UI")]
        [SerializeField] private RectTransform slotListRoot;
        [SerializeField] private RectTransform optionListRoot;
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button saveButton;
        [Tooltip("Prefab wireframe. Skips the two-column slot builder and does not persist Save.")]
        [SerializeField] private bool authoredWireframe;

        private readonly List<Button> slotButtons = new List<Button>();
        private readonly List<Button> optionButtons = new List<Button>();
        private readonly List<string> ownedCosmeticIds = new List<string>();
        private PlayerRosterSnapshot rosterCache;
        private PlayerRosterEntry entry;
        private ChainlingVisualIdentity editingIdentity;
        private StyleSlot currentSlot;
        private bool isSaving;
        private ICosmeticInventoryService cosmeticInventoryService;
        private IMenuVisualIdentityService visualIdentityService;

        public event Action<ChainlingVisualIdentity> PreviewIdentityChanged;
        public event Action<ChainlingVisualIdentity> Saved;

        private string CurrentSpeciesId => entry != null && entry.snapshot != null ? entry.snapshot.speciesId : string.Empty;

        private void Awake()
        {
            EnsureBuilt();
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
        }

        private bool UsesAuthoredWireframe => authoredWireframe || transform.Find("Content") != null;

        private void OnEnable()
        {
            if (UsesAuthoredWireframe || saveButton == null)
                return;
            saveButton.onClick.AddListener(HandleSaveClicked);
        }

        private void OnDisable()
        {
            if (saveButton != null)
                saveButton.onClick.RemoveListener(HandleSaveClicked);
        }

        public void Configure(
            SupabaseConfigSO config,
            CombatVisualDatabaseSO combatVisualDatabase,
            ChainlingVisualIdentityDatabaseSO chainlingIdentityDatabase,
            MonoBehaviour cosmeticInventoryServiceSource,
            MonoBehaviour visualIdentityServiceSource = null)
        {
            if (config != null)
                supabaseConfig = config;
            if (combatVisualDatabase != null)
                visualDatabase = combatVisualDatabase;
            if (chainlingIdentityDatabase != null)
                identityDatabase = chainlingIdentityDatabase;
            if (cosmeticInventoryServiceSource != null)
                cosmeticInventoryServiceBehaviour = cosmeticInventoryServiceSource;
            if (visualIdentityServiceSource != null)
            {
                visualIdentityServiceBehaviour = visualIdentityServiceSource;
                visualIdentityService = null;
            }
        }

        public void Bind(PlayerRosterEntry rosterEntry, PlayerRosterSnapshot cache)
        {
            EnsureBuilt();
            if (UsesAuthoredWireframe)
                return;

            entry = rosterEntry;
            rosterCache = cache;
            editingIdentity = ChainlingVisualIdentity.CloneForPlayerSave(entry != null && entry.snapshot != null
                ? entry.snapshot.visualIdentity
                : null);
            currentSlot = StyleSlot.Variant;
            _ = RefreshOwnershipAsync();
            RefreshSlots();
            RefreshOptions();
            EmitPreview();
        }

        public void RefreshCosmeticOwnership()
        {
            if (UsesAuthoredWireframe)
                return;

            cosmeticInventoryService = ResolveCosmeticInventoryService();
            if (cosmeticInventoryService is ICosmeticInventoryProvenanceService provenanceService)
                provenanceService.ClearCosmeticCache();

            _ = RefreshOwnershipAsync();
        }

        private async Task RefreshOwnershipAsync()
        {
            ownedCosmeticIds.Clear();
            cosmeticInventoryService = ResolveCosmeticInventoryService();
            if (cosmeticInventoryService == null)
                return;

            var session = GameSessionContext.Instance;
            IReadOnlyList<string> owned = await cosmeticInventoryService.FetchOwnedCosmeticIdsAsync(
                session != null ? session.PlayerProfileId : "",
                session != null ? session.WalletAddress : "");

            if (owned != null)
                ownedCosmeticIds.AddRange(owned);

            RefreshOptions();
        }

        private void RefreshSlots()
        {
            EnsureSlotButtonCount(13);
            BindSlotButton(0, StyleSlot.Variant, "Appearance", "Variant");
            BindSlotButton(1, StyleSlot.Aura, "Appearance", "Aura");
            BindSlotButton(2, StyleSlot.HudCard, "Appearance", "HUD Card");
            BindSlotButton(3, StyleSlot.PartyColumn, "Appearance", "Party Column");
            BindSlotButton(4, StyleSlot.IdleLoop, "Battle Animations", "Idle Loop");
            BindSlotButton(5, StyleSlot.IdleFlourish, "Battle Animations", "Idle Flourish");
            BindSlotButton(6, StyleSlot.Melee, "Battle Animations", "Melee Strike");
            BindSlotButton(7, StyleSlot.Ranged, "Battle Animations", "Ranged Strike");
            BindSlotButton(8, StyleSlot.Hit, "Battle Animations", "Hit Reaction");
            BindSlotButton(9, StyleSlot.SwapIn, "Battle Animations", "Swap In");
            BindSlotButton(10, StyleSlot.SwapOut, "Battle Animations", "Swap Out");
            BindSlotButton(11, StyleSlot.Ko, "Battle Animations", "KO");
            BindSlotButton(12, StyleSlot.Victory, "Presentation", "Victory Pose");
        }

        private void BindSlotButton(int index, StyleSlot slot, string group, string label)
        {
            Button button = slotButtons[index];
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = $"{group}\n{label}";

            Image image = button.targetGraphic as Image;
            if (image != null)
                image.color = slot == currentSlot ? PlayerMenuUiFactory.Accent : PlayerMenuUiFactory.PanelAlt;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                currentSlot = slot;
                RefreshSlots();
                RefreshOptions();
            });
        }

        private void RefreshOptions()
        {
            if (editingIdentity == null)
                editingIdentity = ChainlingVisualIdentity.CreateDefault();

            List<StyleOption> options = BuildOptions(currentSlot);
            EnsureOptionButtonCount(options.Count);

            for (int i = 0; i < optionButtons.Count; i++)
            {
                Button button = optionButtons[i];
                bool active = i < options.Count;
                button.gameObject.SetActive(active);
                if (!active)
                    continue;

                StyleOption option = options[i];
                bool owned = IsOptionAvailable(option);
                bool equipped = string.Equals(GetCurrentId(currentSlot), option.id, StringComparison.Ordinal);
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                    text.text = $"{option.label}{(equipped ? " [Equipped]" : "")}{(owned ? "" : " [Locked]")}";

                Image image = button.targetGraphic as Image;
                if (image != null)
                    image.color = equipped ? PlayerMenuUiFactory.Accent : PlayerMenuUiFactory.PanelAlt;

                button.interactable = owned;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectOption(option));
            }

            if (headingLabel != null)
                headingLabel.text = SlotHeading(currentSlot);
            if (statusLabel != null && !isSaving)
                statusLabel.text = "Preview changes here, then save to wallet/profile data.";
        }

        private void SelectOption(StyleOption option)
        {
            SetCurrentId(currentSlot, option.id);
            editingIdentity = ChainlingVisualIdentity.CloneForPlayerSave(editingIdentity);
            RefreshOptions();
            EmitPreview();
        }

        private async void HandleSaveClicked()
        {
            if (isSaving || entry == null || entry.snapshot == null)
                return;

            isSaving = true;
            SetStatus("Saving style...");
            if (saveButton != null)
                saveButton.interactable = false;

            IMenuVisualIdentityService service = ResolveVisualIdentityService();
            if (service == null)
            {
                isSaving = false;
                if (saveButton != null)
                    saveButton.interactable = true;
                SetStatus("No visual identity service was found.");
                return;
            }

            var session = GameSessionContext.Instance;
            IReadOnlyDictionary<string, string> cosmeticAssetIdsByCosmeticId = null;
            cosmeticInventoryService = ResolveCosmeticInventoryService();
            if (cosmeticInventoryService is ICosmeticInventoryProvenanceService provenanceService)
                cosmeticAssetIdsByCosmeticId = provenanceService.GetAssetIdsByCosmeticId();

            VisualIdentityMutationResult result = await service.SaveVisualIdentityAsync(
                session != null ? session.PlayerProfileId : "",
                session != null ? session.WalletAddress : "",
                session != null ? session.AuthUserId : "",
                entry.snapshot.backendChainlingId,
                editingIdentity,
                ownedCosmeticIds,
                rosterCache,
                identityDatabase,
                entry.snapshot.speciesId,
                cosmeticAssetIdsByCosmeticId);

            isSaving = false;
            if (saveButton != null)
                saveButton.interactable = true;

            if (result == null || !result.success)
            {
                SetStatus(result != null ? result.error : "Style save failed.");
                return;
            }

            editingIdentity = ChainlingVisualIdentity.CloneForPlayerSave(result.visualIdentity);
            if (entry.snapshot != null)
                entry.snapshot.visualIdentity = ChainlingVisualIdentity.CloneForPlayerSave(editingIdentity);
            SetStatus("Style saved.");
            RefreshOptions();
            EmitPreview();
            Saved?.Invoke(editingIdentity);
            RefreshCosmeticOwnership();
        }

        private List<StyleOption> BuildOptions(StyleSlot slot)
        {
            var options = new List<StyleOption>();
            switch (slot)
            {
                case StyleSlot.Variant:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.BaseVariantId, label = "Base Variant", free = true });
                    if (visualDatabase != null && entry != null && entry.snapshot != null)
                    {
                        var entries = visualDatabase.GetEntriesForSpecies(entry.snapshot.speciesId);
                        for (int i = 0; i < entries.Count; i++)
                        {
                            var visual = entries[i];
                            string id = ChainlingVisualIdentity.NormalizeId(visual.visualVariantId, ChainlingVisualIdentity.BaseVariantId);
                            AddUnique(options, id, visual.profile != null && !string.IsNullOrWhiteSpace(visual.profile.displayName)
                                ? visual.profile.displayName
                                : id, IsFreeId(id));
                        }
                    }
                    break;
                case StyleSlot.Aura:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.NoneId, label = "No Aura", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.Auras : null);
                    break;
                case StyleSlot.HudCard:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.DefaultId, label = "Default HUD Card", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.HudCardVariants : null);
                    break;
                case StyleSlot.PartyColumn:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.DefaultId, label = "Default Party Column", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.PartyColumnVariants : null);
                    break;
                case StyleSlot.IdleLoop:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.NoneId, label = "Default Idle Loop", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.IdleStyles : null);
                    break;
                case StyleSlot.IdleFlourish:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.NoneId, label = "No Idle Flourish", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.IdleFlourishes : null);
                    break;
                case StyleSlot.Melee:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.NoneId, label = "Default Melee", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.MeleeAttackStyles : null);
                    break;
                case StyleSlot.Ranged:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.NoneId, label = "Default Ranged", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.RangedAttackStyles : null);
                    break;
                case StyleSlot.Hit:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.NoneId, label = "Default Hit Reaction", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.HitReactionStyles : null);
                    break;
                case StyleSlot.SwapOut:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.DefaultId, label = "Default Swap Out", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.SwapOutFx : null);
                    break;
                case StyleSlot.Ko:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.NoneId, label = "Default KO", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.KoFx : null);
                    break;
                case StyleSlot.Victory:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.DefaultId, label = "Default Victory", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.VictoryPoses : null);
                    break;
                case StyleSlot.SwapIn:
                    options.Add(new StyleOption { id = ChainlingVisualIdentity.DefaultId, label = "Default Swap In", free = true });
                    AddDefinitions(options, identityDatabase != null ? identityDatabase.SwapInFx : null);
                    break;
            }

            return options;
        }

        private void AddDefinitions<T>(List<StyleOption> options, IReadOnlyList<T> definitions)
            where T : ChainlingVisualDefinitionSO
        {
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Count; i++)
            {
                T def = definitions[i];
                if (def == null)
                    continue;
                if (!def.IsCompatibleWith(CurrentSpeciesId, editingIdentity != null ? editingIdentity.visualVariantId : ChainlingVisualIdentity.BaseVariantId))
                    continue;

                AddUnique(options, def.Id, def.DisplayName, IsFreeId(def.Id));
            }
        }

        private static void AddUnique(List<StyleOption> options, string id, string label, bool free)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].id, id, StringComparison.Ordinal))
                    return;
            }

            options.Add(new StyleOption { id = id, label = string.IsNullOrWhiteSpace(label) ? id : label, free = free });
        }

        private bool IsOptionAvailable(StyleOption option)
        {
            if (option.free || IsFreeId(option.id))
                return true;
            if (ownedCosmeticIds.Count == 0)
                return false;
            for (int i = 0; i < ownedCosmeticIds.Count; i++)
            {
                if (string.Equals(ownedCosmeticIds[i], option.id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private string GetCurrentId(StyleSlot slot)
        {
            switch (slot)
            {
                case StyleSlot.Variant: return editingIdentity.visualVariantId;
                case StyleSlot.Aura: return editingIdentity.auraId;
                case StyleSlot.HudCard: return editingIdentity.hudCardVariantId;
                case StyleSlot.PartyColumn: return editingIdentity.partyColumnVariantId;
                case StyleSlot.IdleLoop: return editingIdentity.idleStyleId;
                case StyleSlot.IdleFlourish: return editingIdentity.idleFlourishId;
                case StyleSlot.Melee: return editingIdentity.meleeAttackStyleId;
                case StyleSlot.Ranged: return editingIdentity.rangedAttackStyleId;
                case StyleSlot.Hit: return editingIdentity.hitReactionStyleId;
                case StyleSlot.SwapOut: return editingIdentity.swapOutFxId;
                case StyleSlot.Ko: return editingIdentity.koFxId;
                case StyleSlot.Victory: return editingIdentity.victoryPoseId;
                case StyleSlot.SwapIn: return editingIdentity.swapInFxId;
                default: return "";
            }
        }

        private void SetCurrentId(StyleSlot slot, string id)
        {
            switch (slot)
            {
                case StyleSlot.Variant:
                    editingIdentity.visualVariantId = id;
                    ResetVariantIncompatibleAnimationCosmetics();
                    break;
                case StyleSlot.Aura: editingIdentity.auraId = id; break;
                case StyleSlot.HudCard: editingIdentity.hudCardVariantId = id; break;
                case StyleSlot.PartyColumn: editingIdentity.partyColumnVariantId = id; break;
                case StyleSlot.IdleLoop: editingIdentity.idleStyleId = id; break;
                case StyleSlot.IdleFlourish: editingIdentity.idleFlourishId = id; break;
                case StyleSlot.Melee: editingIdentity.meleeAttackStyleId = id; break;
                case StyleSlot.Ranged: editingIdentity.rangedAttackStyleId = id; break;
                case StyleSlot.Hit: editingIdentity.hitReactionStyleId = id; break;
                case StyleSlot.SwapOut: editingIdentity.swapOutFxId = id; break;
                case StyleSlot.Ko: editingIdentity.koFxId = id; break;
                case StyleSlot.Victory: editingIdentity.victoryPoseId = id; break;
                case StyleSlot.SwapIn: editingIdentity.swapInFxId = id; break;
            }
        }

        private static string SlotHeading(StyleSlot slot)
        {
            switch (slot)
            {
                case StyleSlot.Variant: return "Variant";
                case StyleSlot.Aura: return "Aura";
                case StyleSlot.HudCard: return "HUD Card Variant";
                case StyleSlot.PartyColumn: return "Party Column Card";
                case StyleSlot.IdleLoop: return "Idle Loop";
                case StyleSlot.IdleFlourish: return "Idle Flourish";
                case StyleSlot.Melee: return "Melee Strike Variant";
                case StyleSlot.Ranged: return "Ranged Strike Variant";
                case StyleSlot.Hit: return "Hit Reaction";
                case StyleSlot.SwapOut: return "Swap Out Variant";
                case StyleSlot.Ko: return "KO";
                case StyleSlot.Victory: return "Victory Pose";
                case StyleSlot.SwapIn: return "Swap In Variant";
                default: return "Style";
            }
        }

        private void ResetVariantIncompatibleAnimationCosmetics()
        {
            if (editingIdentity == null || identityDatabase == null)
                return;

            string speciesId = CurrentSpeciesId;
            string variantId = editingIdentity.visualVariantId;

            if (!IsCompatible(identityDatabase.GetSwapInFx(editingIdentity.swapInFxId), speciesId, variantId))
                editingIdentity.swapInFxId = ChainlingVisualIdentity.DefaultId;
            if (!IsCompatible(identityDatabase.GetSwapOutFx(editingIdentity.swapOutFxId), speciesId, variantId))
                editingIdentity.swapOutFxId = ChainlingVisualIdentity.DefaultId;
            if (!IsCompatible(identityDatabase.GetKoFx(editingIdentity.koFxId), speciesId, variantId))
                editingIdentity.koFxId = ChainlingVisualIdentity.NoneId;
            if (!IsCompatible(identityDatabase.GetIdleStyle(editingIdentity.idleStyleId), speciesId, variantId))
                editingIdentity.idleStyleId = ChainlingVisualIdentity.NoneId;
            if (!IsCompatible(identityDatabase.GetIdleFlourish(editingIdentity.idleFlourishId), speciesId, variantId))
                editingIdentity.idleFlourishId = ChainlingVisualIdentity.NoneId;
            if (!IsCompatible(identityDatabase.GetMeleeAttackStyle(editingIdentity.meleeAttackStyleId), speciesId, variantId))
                editingIdentity.meleeAttackStyleId = ChainlingVisualIdentity.NoneId;
            if (!IsCompatible(identityDatabase.GetRangedAttackStyle(editingIdentity.rangedAttackStyleId), speciesId, variantId))
                editingIdentity.rangedAttackStyleId = ChainlingVisualIdentity.NoneId;
            if (!IsCompatible(identityDatabase.GetHitReactionStyle(editingIdentity.hitReactionStyleId), speciesId, variantId))
                editingIdentity.hitReactionStyleId = ChainlingVisualIdentity.NoneId;
            if (!IsCompatible(identityDatabase.GetAttackerKoStyle(editingIdentity.attackerKoStyleId), speciesId, variantId))
                editingIdentity.attackerKoStyleId = ChainlingVisualIdentity.NoneId;
        }

        private static bool IsCompatible(ChainlingVisualDefinitionSO definition, string speciesId, string visualVariantId)
        {
            return definition == null || definition.IsCompatibleWith(speciesId, visualVariantId);
        }

        private void EmitPreview()
        {
            PreviewIdentityChanged?.Invoke(ChainlingVisualIdentity.CloneForPlayerSave(editingIdentity));
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null)
                statusLabel.text = value ?? "";
        }

        private void EnsureBuilt()
        {
            if (UsesAuthoredWireframe)
                return;

            if (slotListRoot != null && optionListRoot != null && saveButton != null)
                return;

            var rootLayout = PlayerMenuUiFactory.VerticalLayout(gameObject, 12f);
            rootLayout.padding = new RectOffset(14, 14, 14, 14);

            headingLabel = headingLabel != null
                ? headingLabel
                : PlayerMenuUiFactory.CreateText("Heading", transform, "Style", 26f, TextAlignmentOptions.MidlineLeft, false);
            PlayerMenuUiFactory.LayoutElement(headingLabel.gameObject, preferredHeight: 34f);

            var body = PlayerMenuUiFactory.CreateRect("Body", transform);
            PlayerMenuUiFactory.LayoutElement(body.gameObject, preferredHeight: 360f, flexibleWidth: 1f);
            var horizontal = PlayerMenuUiFactory.HorizontalLayout(body.gameObject, 12f);
            horizontal.childForceExpandWidth = true;

            slotListRoot = slotListRoot != null ? slotListRoot : PlayerMenuUiFactory.CreateRect("SlotList", body);
            PlayerMenuUiFactory.VerticalLayout(slotListRoot.gameObject, 8f);
            PlayerMenuUiFactory.LayoutElement(slotListRoot.gameObject, preferredWidth: 230f, flexibleWidth: 0f);

            optionListRoot = optionListRoot != null ? optionListRoot : PlayerMenuUiFactory.CreateRect("OptionList", body);
            PlayerMenuUiFactory.VerticalLayout(optionListRoot.gameObject, 8f);
            PlayerMenuUiFactory.LayoutElement(optionListRoot.gameObject, flexibleWidth: 1f);

            statusLabel = statusLabel != null
                ? statusLabel
                : PlayerMenuUiFactory.CreateText("Status", transform, "", 20f, TextAlignmentOptions.MidlineLeft, true);
            statusLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(statusLabel.gameObject, preferredHeight: 54f);

            saveButton = saveButton != null
                ? saveButton
                : PlayerMenuUiFactory.CreateButton("SaveStyleButton", transform, "Save Style", PlayerMenuUiFactory.Accent);
            PlayerMenuUiFactory.LayoutElement(saveButton.gameObject, preferredHeight: 58f);
        }

        private void EnsureSlotButtonCount(int count)
        {
            while (slotButtons.Count < count)
            {
                Button button = PlayerMenuUiFactory.CreateButton($"Slot_{slotButtons.Count:D2}", slotListRoot, "", PlayerMenuUiFactory.PanelAlt);
                PlayerMenuUiFactory.LayoutElement(button.gameObject, preferredHeight: 56f, flexibleWidth: 1f);
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                    text.fontSize = 17f;
                slotButtons.Add(button);
            }
        }

        private void EnsureOptionButtonCount(int count)
        {
            while (optionButtons.Count < count)
            {
                Button button = PlayerMenuUiFactory.CreateButton($"Option_{optionButtons.Count:D2}", optionListRoot, "", PlayerMenuUiFactory.PanelAlt);
                PlayerMenuUiFactory.LayoutElement(button.gameObject, preferredHeight: 52f, flexibleWidth: 1f);
                optionButtons.Add(button);
            }
        }

        private ICosmeticInventoryService ResolveCosmeticInventoryService()
        {
            if (cosmeticInventoryService != null)
                return cosmeticInventoryService;
            if (cosmeticInventoryServiceBehaviour is ICosmeticInventoryService assigned)
                return assigned;

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICosmeticInventoryService service)
                    return service;
            }

            return null;
        }

        private IMenuVisualIdentityService ResolveVisualIdentityService()
        {
            if (visualIdentityService != null)
                return visualIdentityService;
            if (visualIdentityServiceBehaviour is IMenuVisualIdentityService assigned)
            {
                visualIdentityService = assigned;
                return visualIdentityService;
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMenuVisualIdentityService service)
                {
                    visualIdentityService = service;
                    return visualIdentityService;
                }
            }

            return null;
        }

        private static bool IsFreeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ||
                   string.Equals(id, ChainlingVisualIdentity.NoneId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.DefaultId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.EntangledVariantId, StringComparison.Ordinal);
        }
    }
}
