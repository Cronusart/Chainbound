using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    public sealed partial class ChainlingPcTerminalScreen
    {
        private bool IsActionSheetOpen => actionSheet != null && actionSheet.gameObject.activeSelf;

        private void LateUpdate()
        {
            if (!entranceSettled || closing || partyRows == null || IsActionSheetOpen)
                return;
            if (Time.unscaledTime < partyRevealUntil)
                return;

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int index = -1;
            if (selected != null)
            {
                for (int i = 0; i < partyRows.Length; i++)
                {
                    InventoryItemUseTargetSlot row = partyRows[i];
                    if (row == null)
                        continue;
                    if (selected == row.gameObject || selected.transform.IsChildOf(row.transform))
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index == focusedPartyRow)
                return;

            for (int i = 0; i < partyRows.Length; i++)
            {
                if (partyRows[i] != null)
                    partyRows[i].SetSelected(i == index, true);
            }

            focusedPartyRow = index;
        }

        private void BindPartyRows(List<PlayerRosterEntry> party)
        {
            if (partyRows == null)
                return;

            for (int slot = 0; slot < partyRows.Length && slot < SlotNames.Length; slot++)
            {
                InventoryItemUseTargetSlot row = partyRows[slot];
                if (row == null)
                    continue;

                PlayerRosterEntry entry = party != null && slot < party.Count ? party[slot] : null;
                string captionName = entry != null ? ResolveName(entry) : "EMPTY";
                row.Bind(
                    entry,
                    ResolveSpecies(entry),
                    typeIconPalette,
                    starArt,
                    cyclePalette,
                    motionProfile,
                    $"{SlotNames[slot]} · {captionName}");
                if (row.Button != null)
                    row.Button.interactable = true;
                if (entranceSettled && Application.isPlaying)
                    row.PlayProgressReveal(0.2f);
            }
        }

        private void PlayPartyRowReveal()
        {
            if (partyRows == null || !Application.isPlaying)
                return;

            partyRevealUntil = Time.unscaledTime + 0.52f;
            for (int i = 0; i < partyRows.Length; i++)
            {
                InventoryItemUseTargetSlot row = partyRows[i];
                if (row == null)
                    continue;

                row.KillMotion();
                row.transform.localScale = new Vector3(0.94f, 0.94f, 1f);
                float at = 0.08f * i;
                int captured = i;
                Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
                sequence.Insert(at, row.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true));
                sequence.InsertCallback(at, () =>
                {
                    if (partyRows != null && captured < partyRows.Length && partyRows[captured] != null)
                        partyRows[captured].PlayProgressReveal(0.36f);
                });
            }
        }

        private void PlayPartyRowClose(Action onComplete)
        {
            if (partyRows == null || !Application.isPlaying)
            {
                onComplete?.Invoke();
                return;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            bool any = false;
            for (int i = partyRows.Length - 1; i >= 0; i--)
            {
                InventoryItemUseTargetSlot row = partyRows[i];
                if (row == null)
                    continue;

                any = true;
                float at = 0.05f * (partyRows.Length - 1 - i);
                sequence.Insert(at, row.transform.DOScale(0.9f, 0.14f).SetEase(Ease.InQuad).SetUpdate(true));
            }

            if (!any)
            {
                onComplete?.Invoke();
                return;
            }

            sequence.OnComplete(() => onComplete?.Invoke());
        }

        private void KillPartyMotion()
        {
            DOTween.Kill(this);
            if (partyRows == null)
                return;

            for (int i = 0; i < partyRows.Length; i++)
            {
                if (partyRows[i] == null)
                    continue;
                partyRows[i].KillMotion();
                DOTween.Kill(partyRows[i].transform);
            }
        }

        private void BuildActionSheet(Transform parent)
        {
            GameObject sheetObject = new GameObject("ActionSheet", typeof(RectTransform), typeof(CanvasGroup));
            sheetObject.transform.SetParent(parent, false);
            PlayerMenuUiFactory.Stretch(sheetObject.GetComponent<RectTransform>());
            actionSheet = sheetObject.GetComponent<CanvasGroup>();
            actionSheet.alpha = 0f;
            actionSheet.interactable = false;
            actionSheet.blocksRaycasts = true;

            Image blocker = PlayerMenuUiFactory.CreateImage("Blocker", sheetObject.transform, new Color(0.02f, 0.01f, 0.02f, 0.55f));
            PlayerMenuUiFactory.Stretch(blocker.rectTransform);
            blocker.raycastTarget = true;

            RectTransform panel = PlayerMenuUiFactory.CreateRect("Panel", sheetObject.transform);
            panel.anchorMin = new Vector2(0.30f, 0.28f);
            panel.anchorMax = new Vector2(0.70f, 0.74f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            Image plate = panel.gameObject.AddComponent<Image>();
            plate.color = new Color(0.10f, 0.045f, 0.04f, 0.98f);
            plate.raycastTarget = true;

            VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(panel.gameObject, 10f);
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            actionTitle = PlayerMenuUiFactory.CreateText("Title", panel, "CHAINLING", 28f, TextAlignmentOptions.MidlineLeft, false);
            actionTitle.fontStyle = FontStyles.Bold;
            actionTitle.color = PlayerMenuUiFactory.Accent;
            PlayerMenuUiFactory.LayoutElement(actionTitle.gameObject, preferredHeight: 40f, flexibleWidth: 1f);

            actionButtonGroup = new GameObject("Actions", typeof(RectTransform), typeof(VerticalLayoutGroup));
            actionButtonGroup.transform.SetParent(panel, false);
            VerticalLayoutGroup actions = actionButtonGroup.GetComponent<VerticalLayoutGroup>();
            actions.spacing = 10f;
            actions.childControlWidth = true;
            actions.childControlHeight = true;
            actions.childForceExpandWidth = true;
            actions.childForceExpandHeight = false;
            PlayerMenuUiFactory.LayoutElement(actionButtonGroup, preferredHeight: 360f, flexibleWidth: 1f);

            moveButton = CreateSheetButton(actionButtonGroup.transform, "MoveToParty", "MOVE TO PARTY");
            storageButton = CreateSheetButton(actionButtonGroup.transform, "SendToStorage", "SEND TO STORAGE");
            manageButton = CreateSheetButton(actionButtonGroup.transform, "Manage", "MANAGE CHAINLING");
            releaseButton = CreateSheetButton(actionButtonGroup.transform, "Release", "RELEASE");
            actionCloseButton = CreateSheetButton(actionButtonGroup.transform, "Close", "CLOSE");

            confirmGroup = new GameObject("Confirm", typeof(RectTransform), typeof(VerticalLayoutGroup));
            confirmGroup.transform.SetParent(panel, false);
            VerticalLayoutGroup confirmLayout = confirmGroup.GetComponent<VerticalLayoutGroup>();
            confirmLayout.spacing = 10f;
            confirmLayout.childControlWidth = true;
            confirmLayout.childControlHeight = true;
            confirmLayout.childForceExpandWidth = true;
            confirmLayout.childForceExpandHeight = false;
            PlayerMenuUiFactory.LayoutElement(confirmGroup, preferredHeight: 220f, flexibleWidth: 1f);
            confirmBody = PlayerMenuUiFactory.CreateText(
                "Body",
                confirmGroup.transform,
                "Release this Chainling?",
                22f,
                TextAlignmentOptions.TopLeft,
                true);
            PlayerMenuUiFactory.LayoutElement(confirmBody.gameObject, preferredHeight: 96f, flexibleWidth: 1f);
            confirmButton = CreateSheetButton(confirmGroup.transform, "Confirm", "RELEASE");
            confirmCancelButton = CreateSheetButton(confirmGroup.transform, "Cancel", "CANCEL");
            confirmGroup.SetActive(false);
            sheetObject.SetActive(false);
        }

        private static Button CreateSheetButton(Transform parent, string name, string label)
        {
            Button button = PlayerMenuUiFactory.CreateButton(name, parent, label, PlayerMenuUiFactory.PanelAlt);
            PlayerMenuUiFactory.LayoutElement(button.gameObject, preferredHeight: 64f, flexibleWidth: 1f);
            MenuSelectableFocusVisual.EnsureOn(button, new Color(1f, 0.62f, 0.22f, 0.95f));
            return button;
        }

        private void WireActionSheet()
        {
            WireSheetButton(moveButton, BeginChooseSlot);
            WireSheetButton(storageButton, SendSelectedToStorage);
            WireSheetButton(manageButton, ManageSelectedChainling);
            WireSheetButton(releaseButton, BeginRelease);
            WireSheetButton(actionCloseButton, Close);
            WireSheetButton(confirmButton, ConfirmRelease);
            WireSheetButton(confirmCancelButton, Close);
        }

        private static void WireSheetButton(Button button, UnityAction action)
        {
            if (button == null || action == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void AppendPartyNavigation(List<Selectable> order)
        {
            if (partyRows == null)
                return;

            for (int i = 0; i < partyRows.Length; i++)
            {
                Button button = partyRows[i] != null ? partyRows[i].Button : null;
                if (button != null)
                    order.Add(button);
            }
        }

        private void AppendSheetNavigation(List<Selectable> order)
        {
            if (confirmingRelease)
            {
                if (confirmButton != null)
                    order.Add(confirmButton);
                if (confirmCancelButton != null)
                    order.Add(confirmCancelButton);
                return;
            }

            if (moveButton != null)
                order.Add(moveButton);
            if (storageButton != null && storageButton.gameObject.activeInHierarchy)
                order.Add(storageButton);
            if (manageButton != null && manageButton.gameObject.activeInHierarchy)
                order.Add(manageButton);
            if (releaseButton != null && releaseButton.gameObject.activeInHierarchy)
                order.Add(releaseButton);
            if (actionCloseButton != null)
                order.Add(actionCloseButton);
        }

        private bool TryDismissRosterOverlay()
        {
            if (confirmingRelease)
            {
                confirmingRelease = false;
                ShowActionButtons();
                SetStatus("RELEASE CANCELLED.");
                if (entranceSettled)
                    RefreshNavigation();
                return true;
            }

            if (IsActionSheetOpen)
            {
                HideActionSheetImmediate();
                SetStatus("SELECT A STORED CHAINLING OR A PARTY SLOT.");
                if (entranceSettled)
                    RefreshNavigation();
                return true;
            }

            if (choosingSlot)
            {
                choosingSlot = false;
                SetStatus("SELECT A STORED CHAINLING OR A PARTY SLOT.");
                if (entranceSettled)
                    RefreshNavigation();
                return true;
            }

            return false;
        }

        private void OpenActionSheet(PlayerRosterEntry entry, bool inParty)
        {
            if (entry == null || actionSheet == null || closing)
                return;

            actionTarget = entry;
            actionTargetInParty = inParty;
            choosingSlot = false;
            confirmingRelease = false;
            if (actionTitle != null)
                actionTitle.text = ResolveName(entry).ToUpperInvariant();

            SetSheetLabel(moveButton, inParty ? "CHANGE SLOT" : "MOVE TO PARTY");
            if (storageButton != null)
                storageButton.gameObject.SetActive(inParty);
            if (releaseButton != null)
                releaseButton.gameObject.SetActive(!inParty);

            ShowActionButtons();
            actionSheet.gameObject.SetActive(true);
            actionSheet.alpha = 0f;
            actionSheet.interactable = true;
            actionSheet.blocksRaycasts = true;
            actionSheet.DOFade(1f, 0.16f).SetUpdate(true).SetTarget(actionSheet);
            if (entranceSettled)
                RefreshNavigation();
        }

        private static void SetSheetLabel(Button button, string label)
        {
            if (button == null)
                return;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;
        }

        private void ShowActionButtons()
        {
            if (actionButtonGroup != null)
                actionButtonGroup.SetActive(true);
            if (confirmGroup != null)
                confirmGroup.SetActive(false);
        }

        private void HideActionSheetImmediate()
        {
            confirmingRelease = false;
            if (actionSheet == null)
                return;

            actionSheet.DOKill();
            actionSheet.alpha = 0f;
            actionSheet.interactable = false;
            actionSheet.gameObject.SetActive(false);
            ShowActionButtons();
        }

        private void BeginChooseSlot()
        {
            if (actionTarget == null)
                return;

            choosingSlot = true;
            HideActionSheetImmediate();
            SetStatus(actionTargetInParty
                ? "CHOOSE THE NEW SLOT."
                : "CHOOSE LEAD, PARTNER, OR BENCH.");
            PlayCue(PlayerMenuSfxCue.Confirm);
            if (entranceSettled)
                RefreshNavigation();
        }

        private async void SendSelectedToStorage()
        {
            if (actionTarget == null || actionTarget.snapshot == null || !actionTargetInParty)
                return;

            List<PlayerRosterEntry> party = GetParty();
            if (!CanSendPartyMemberToStorage(party.Count))
            {
                SetStatus("THE LAST PARTY MEMBER HAS TO STAY EQUIPPED.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            if (!TryBuildPartyOrder(party, actionTarget.snapshot.backendChainlingId, 0, true, out string[] orderedIds))
            {
                SetStatus("THE LAST PARTY MEMBER HAS TO STAY EQUIPPED.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            await SavePartyOrderAsync(orderedIds);
        }

        private void ManageSelectedChainling()
        {
            PlayerRosterEntry entry = actionTarget;
            if (entry == null)
                return;

            if (detailPrefab == null)
            {
                SetStatus("CHAINLING DETAILS ARE UNAVAILABLE.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            HideActionSheetImmediate();
            if (openDetail != null)
            {
                openDetail.Closed -= HandleDetailClosed;
                Destroy(openDetail.gameObject);
                openDetail = null;
            }

            openDetail = Instantiate(detailPrefab, transform);
            openDetail.gameObject.name = "ChainlingDetailView";
            openDetail.transform.SetAsLastSibling();
            RectTransform rect = openDetail.transform as RectTransform;
            if (rect != null)
                PlayerMenuUiFactory.Stretch(rect);

            openDetail.Configure(
                ResolveSupabaseConfig(),
                speciesDatabase,
                visualDatabase,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            openDetail.Closed += HandleDetailClosed;
            openDetail.OpenForManagement(entry, roster);
            openDetail.Show();
            if (root != null)
                root.interactable = false;
        }

        private async void HandleDetailClosed()
        {
            if (openDetail != null)
            {
                openDetail.Closed -= HandleDetailClosed;
                ChainlingDetailScreen closingDetail = openDetail;
                openDetail = null;
                if (closingDetail != null)
                    Destroy(closingDetail.gameObject);
            }

            if (root != null && !closing)
                root.interactable = entranceSettled;

            await RefreshAsync();
        }

        private void BeginRelease()
        {
            if (actionTarget == null)
                return;

            if (actionTargetInParty || !CanReleaseChainling(actionTarget.isPartyMember))
            {
                SetStatus("SEND THIS CHAINLING TO STORAGE BEFORE RELEASING THEM.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            confirmingRelease = true;
            if (actionTitle != null)
                actionTitle.text = "RELEASE";
            if (confirmBody != null)
            {
                confirmBody.text =
                    $"Release {ResolveName(actionTarget)}? This deletes them from storage. Known moves stay on your profile.";
            }

            if (actionButtonGroup != null)
                actionButtonGroup.SetActive(false);
            if (confirmGroup != null)
                confirmGroup.SetActive(true);
            if (entranceSettled)
                RefreshNavigation();
        }

        private async void ConfirmRelease()
        {
            PlayerRosterEntry target = actionTarget;
            if (target == null || target.snapshot == null || !CanReleaseChainling(target.isPartyMember))
            {
                SetStatus("SEND THIS CHAINLING TO STORAGE BEFORE RELEASING THEM.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            confirmingRelease = false;
            HideActionSheetImmediate();
            SetInteractable(false);
            SetStatus("RELEASING...");
            IMenuPartyLineupService service = ResolvePartyService();
            GameSessionContext session = GameSessionContext.Instance;
            MenuMutationResult result = service != null && session != null
                ? await service.ReleaseChainlingAsync(
                    session.PlayerProfileId,
                    session.WalletAddress,
                    session.AuthUserId,
                    target.snapshot.backendChainlingId)
                : MenuMutationResult.Failed("Party service unavailable.");

            if (result == null || !result.success)
            {
                SetStatus(DescribeReleaseError(result));
                PlayCue(PlayerMenuSfxCue.Invalid);
                SetInteractable(true);
                return;
            }

            PlayCue(PlayerMenuSfxCue.Confirm);
            actionTarget = null;
            selectedStorage = null;
            await RefreshAsync();
            SetStatus($"{ResolveName(target)} RELEASED.");
            SetInteractable(true);
        }

        private static string DescribeReleaseError(MenuMutationResult result)
        {
            string error = result != null ? result.error : null;
            if (string.IsNullOrWhiteSpace(error))
                return "THIS CHAINLING COULD NOT BE RELEASED.";
            if (error.IndexOf("still_in_party", StringComparison.OrdinalIgnoreCase) >= 0)
                return "SEND THIS CHAINLING TO STORAGE BEFORE RELEASING THEM.";
            if (error.IndexOf("bound_chainling", StringComparison.OrdinalIgnoreCase) >= 0)
                return "THIS CHAINLING IS BOUND AND CANNOT BE RELEASED.";
            return error;
        }

        private async Task SavePartyOrderAsync(string[] orderedIds)
        {
            choosingSlot = false;
            HideActionSheetImmediate();
            SetInteractable(false);
            SetStatus("SAVING PARTY...");
            IMenuPartyLineupService service = ResolvePartyService();
            GameSessionContext session = GameSessionContext.Instance;
            MenuMutationResult result = service != null && session != null
                ? await service.SavePartyLineupAsync(
                    session.PlayerProfileId,
                    session.WalletAddress,
                    session.AuthUserId,
                    orderedIds)
                : MenuMutationResult.Failed("Party service unavailable.");

            if (result == null || !result.success)
            {
                SetStatus(result != null && !string.IsNullOrWhiteSpace(result.error)
                    ? result.error
                    : "PARTY COULD NOT BE SAVED.");
                PlayCue(PlayerMenuSfxCue.Invalid);
                SetInteractable(true);
                return;
            }

            PlayCue(PlayerMenuSfxCue.Confirm);
            actionTarget = null;
            await RefreshAsync();
            SetInteractable(true);
        }

        private static bool TryBuildPartyOrder(
            List<PlayerRosterEntry> party,
            string selectedId,
            int targetSlot,
            bool removeSelected,
            out string[] orderedIds)
        {
            orderedIds = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(selectedId))
                return false;

            var ordered = new List<string>(CombatConstants.ROSTER_SIZE);
            if (party != null)
            {
                for (int i = 0; i < party.Count; i++)
                {
                    string id = party[i] != null && party[i].snapshot != null
                        ? party[i].snapshot.backendChainlingId
                        : null;
                    if (!string.IsNullOrWhiteSpace(id))
                        ordered.Add(id);
                }
            }

            if (removeSelected)
            {
                if (!CanSendPartyMemberToStorage(ordered.Count))
                    return false;

                ordered.RemoveAll(id => id == selectedId);
                if (ordered.Count == 0 || ordered.Count > CombatConstants.ROSTER_SIZE)
                    return false;

                orderedIds = ordered.ToArray();
                return true;
            }

            int existing = ordered.IndexOf(selectedId);
            if (existing >= 0)
            {
                if (targetSlot < ordered.Count && existing != targetSlot)
                {
                    string displaced = ordered[targetSlot];
                    ordered[targetSlot] = selectedId;
                    ordered[existing] = displaced;
                }
                else if (targetSlot == ordered.Count)
                {
                    ordered.RemoveAt(existing);
                    ordered.Add(selectedId);
                }
            }
            else if (targetSlot >= ordered.Count)
            {
                if (ordered.Count >= CombatConstants.ROSTER_SIZE)
                    return false;
                ordered.Add(selectedId);
            }
            else
            {
                ordered[targetSlot] = selectedId;
            }

            if (ordered.Count == 0 || ordered.Count > CombatConstants.ROSTER_SIZE)
                return false;

            orderedIds = ordered.ToArray();
            return true;
        }
    }
}
