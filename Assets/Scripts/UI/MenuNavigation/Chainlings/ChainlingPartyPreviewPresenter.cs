using System.Collections;
using Chainbound.Combat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingPartyPreviewPresenter : MonoBehaviour
    {
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private RectTransform portraitStage;
        [SerializeField] private CanvasGroup portraitGroup;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private float debounceSeconds = 0.2f;

        private PlayerRosterEntry pendingEntry;
        private SpeciesDefinitionSO pendingSpecies;
        private CombatVisualProfileSO pendingProfile;
        private int pendingSlotIndex;
        private bool pendingAnimate;
        private string currentIdentityKey = "";
        private Coroutine debounceRoutine;
        private Tween fadeTween;
        private Tween scaleTween;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnDisable()
        {
            KillTweens();
            if (debounceRoutine != null)
            {
                StopCoroutine(debounceRoutine);
                debounceRoutine = null;
            }
        }

        public void Configure(CombatVisualDatabaseSO combatVisualDatabase)
        {
            if (combatVisualDatabase != null)
                visualDatabase = combatVisualDatabase;
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
        }

        public void Bind(
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            CombatVisualProfileSO profile,
            int slotIndex,
            bool animate)
        {
            EnsureBuilt();
            pendingEntry = entry;
            pendingSpecies = species;
            pendingProfile = profile != null ? profile : ResolveProfile(entry);
            pendingSlotIndex = slotIndex;
            pendingAnimate = animate;

            if (!Application.isPlaying || !isActiveAndEnabled || debounceSeconds <= 0f)
            {
                ApplyPending();
                return;
            }

            if (debounceRoutine != null)
                StopCoroutine(debounceRoutine);
            debounceRoutine = StartCoroutine(ApplyPendingDebounced());
        }

        private IEnumerator ApplyPendingDebounced()
        {
            yield return new WaitForSecondsRealtime(debounceSeconds);
            debounceRoutine = null;
            ApplyPending();
        }

        private void ApplyPending()
        {
            ChainlingSnapshot snapshot = pendingEntry != null ? pendingEntry.snapshot : null;
            string identityKey = BuildIdentityKey(snapshot, pendingSlotIndex);
            bool identityChanged = !string.Equals(identityKey, currentIdentityKey, System.StringComparison.Ordinal);
            currentIdentityKey = identityKey;

            if (nameLabel != null)
                nameLabel.text = snapshot != null ? ResolveDisplayName(snapshot, pendingSpecies) : $"Slot {ChainlingsPartyScreen.SlotLabel(pendingSlotIndex)}";

            Sprite sprite = ResolvePortrait(pendingProfile);
            if (portraitImage != null)
            {
                portraitImage.sprite = sprite;
                portraitImage.enabled = sprite != null;
                portraitImage.preserveAspect = true;
            }

            if (emptyLabel != null)
            {
                emptyLabel.enabled = sprite == null;
                emptyLabel.text = snapshot == null ? "Empty slot" : "Portrait unavailable";
            }

            if (pendingAnimate)
                PlayFeedback(identityChanged);
        }

        private CombatVisualProfileSO ResolveProfile(PlayerRosterEntry entry)
        {
            if (visualDatabase == null || entry == null || entry.snapshot == null)
                return null;

            ChainlingVisualIdentity identity = ChainlingVisualIdentity.CloneOrDefault(entry.snapshot.visualIdentity);
            ChainlingType entangledType = entry.snapshot.isEntangled
                ? entry.snapshot.entangledType
                : ChainlingType.None;
            return visualDatabase.GetProfile(entry.snapshot.speciesId, identity.visualVariantId, entangledType);
        }

        private void PlayFeedback(bool identityChanged)
        {
            KillTweens();

            if (portraitGroup != null)
            {
                if (identityChanged)
                {
                    portraitGroup.alpha = 0f;
                    fadeTween = portraitGroup
                        .DOFade(1f, 0.16f)
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true);
                }
                else
                {
                    portraitGroup.alpha = 1f;
                }
            }

            if (portraitStage != null)
            {
                portraitStage.localScale = Vector3.one * 1.02f;
                scaleTween = portraitStage
                    .DOScale(Vector3.one, 0.16f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        private void KillTweens()
        {
            if (fadeTween != null)
            {
                fadeTween.Kill();
                fadeTween = null;
            }

            if (scaleTween != null)
            {
                scaleTween.Kill();
                scaleTween = null;
            }
        }

        private void EnsureBuilt()
        {
            if (portraitStage != null && portraitGroup != null && portraitImage != null && nameLabel != null && emptyLabel != null)
                return;

            VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(gameObject, 14f);
            layout.padding = new RectOffset(34, 34, 30, 30);
            layout.childForceExpandHeight = false;

            if (nameLabel == null)
            {
                nameLabel = PlayerMenuUiFactory.CreateText("PreviewName", transform, "Party preview", 34f, TextAlignmentOptions.TopLeft, false);
                PlayerMenuUiFactory.LayoutElement(nameLabel.gameObject, preferredHeight: 52f);
            }

            if (portraitStage == null)
            {
                Image stage = PlayerMenuUiFactory.CreateImage("PortraitStage", transform, new Color(0.035f, 0.05f, 0.075f, 0.92f));
                portraitStage = stage.rectTransform;
                PlayerMenuUiFactory.LayoutElement(stage.gameObject, preferredHeight: 410f, flexibleWidth: 1f);
            }

            if (portraitGroup == null)
                portraitGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(portraitStage.gameObject);

            if (portraitImage == null)
            {
                portraitImage = PlayerMenuUiFactory.CreateImage("Portrait", portraitStage, Color.clear);
                PlayerMenuUiFactory.Stretch(portraitImage.rectTransform, 28f, 24f, 28f, 24f);
                portraitImage.preserveAspect = true;
            }

            if (emptyLabel == null)
            {
                emptyLabel = PlayerMenuUiFactory.CreateText("EmptyLabel", portraitStage, "Empty slot", 26f, TextAlignmentOptions.Center, true);
                emptyLabel.color = PlayerMenuUiFactory.Muted;
                PlayerMenuUiFactory.Stretch(emptyLabel.rectTransform, 24f, 24f, 24f, 24f);
            }
        }

        private static string BuildIdentityKey(ChainlingSnapshot snapshot, int slotIndex)
        {
            if (snapshot == null)
                return $"empty:{slotIndex}";

            ChainlingVisualIdentity identity = ChainlingVisualIdentity.CloneOrDefault(snapshot.visualIdentity);
            return $"{snapshot.backendChainlingId}:{snapshot.speciesId}:{identity.visualVariantId}";
        }

        private static string ResolveDisplayName(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.displayName))
                return snapshot.displayName;
            if (species != null && !string.IsNullOrWhiteSpace(species.displayName))
                return species.displayName;
            return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.speciesId) ? snapshot.speciesId : "Chainling";
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
    }
}
