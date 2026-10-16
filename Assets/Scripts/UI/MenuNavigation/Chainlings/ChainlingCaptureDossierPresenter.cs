using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Battle-side adapter that hydrates an authority-created Chainling and presents
    /// the same dossier prefab used by the Player Menu detail view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingCaptureDossierPresenter : MonoBehaviour, ICaptureDossierPresenter
    {
        private const string LockReason = "CaptureDossier";

        [SerializeField] private ChainlingDetailScreen dossierPrefab;
        [SerializeField] private RectTransform overlayParent;
        [SerializeField] private MonoBehaviour rosterProviderBehaviour;
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] [Min(1)] private int hydrateAttempts = 4;
        [SerializeField] [Min(0.05f)] private float retryDelaySeconds = 0.25f;

        private TaskCompletionSource<bool> activeCompletion;
        private ChainlingDetailScreen activeDossier;
        private InputLockHandle inputLock;
        private MobileHudToken hudToken;

        public async Task PresentAsync(CaptureDossierRequest request)
        {
            if (request == null)
                return;
            bool useLocal = request.localSnapshot != null && !string.IsNullOrWhiteSpace(request.localSnapshot.speciesId);
            if (!useLocal && string.IsNullOrWhiteSpace(request.chainlingId))
                return;

            if (activeCompletion != null)
            {
                await activeCompletion.Task;
                return;
            }

            activeCompletion = new TaskCompletionSource<bool>();
            try
            {
                IPlayerRosterProvider provider = ResolveRosterProvider();
                PlayerRosterEntry captured = null;
                PlayerRosterSnapshot roster = null;
                string wallet = GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : string.Empty;

                if (useLocal)
                {
                    // Practice / pre-grant reveal: show the captured chainling on the local
                    // snapshot. That snapshot carries the one equipped move, not the wild
                    // fight kit. The cached roster only informs the storage count line.
                    roster = provider is SupabasePlayerRosterProvider cached ? cached.CachedRoster : null;
                    captured = new PlayerRosterEntry
                    {
                        snapshot = request.localSnapshot,
                        isPartyMember = request.addedToParty,
                        partySlot = request.addedToParty && roster != null ? roster.PartyCount : -1,
                    };
                    if (string.IsNullOrWhiteSpace(captured.snapshot.displayName))
                        captured.snapshot.displayName = request.fallbackDisplayName;
                }
                else
                {
                    for (int attempt = 0; attempt < Mathf.Max(1, hydrateAttempts); attempt++)
                    {
                        roster = provider != null
                            ? await PlayerProfileReadStore.ResolveOrCreate().RefreshRosterAsync(
                                provider,
                                request.playerProfileId,
                                wallet)
                            : null;
                        captured = FindByBackendId(roster, request.chainlingId);
                        if (captured != null)
                            break;
                        if (attempt + 1 < hydrateAttempts)
                            await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                    }

                    if (captured == null && request.fallbackSnapshot != null &&
                        !string.IsNullOrWhiteSpace(request.fallbackSnapshot.speciesId))
                    {
                        captured = BuildProvisionalEntry(request, roster);

                        Debug.LogWarning(
                            $"[ChainlingCaptureDossierPresenter] Showing provisional dossier for '{request.chainlingId}' while server hydration is unavailable.",
                            this);
                    }
                }

                if (captured == null || dossierPrefab == null)
                {
                    Debug.LogWarning(
                        $"[ChainlingCaptureDossierPresenter] Capture is safe, but dossier hydration failed for '{request.chainlingId}'.",
                        this);
                    CompletePresentation();
                    return;
                }

                AcquirePresentationTokens();
                Transform parent = overlayParent != null ? overlayParent : ResolveFallbackParent();
                activeDossier = Instantiate(dossierPrefab, parent, false);
                activeDossier.gameObject.name = "CapturedChainlingDossier";
                activeDossier.gameObject.SetActive(false);
                activeDossier.Open(captured, roster);
                activeDossier.SetCapturePresentation(CompletePresentation);
                activeDossier.Show();
                await activeDossier.WaitForPageLandedAsync();
                if (activeDossier == null)
                    return;

                if (request.presentNamePrompt != null)
                    await request.presentNamePrompt(activeDossier.SetLiveDisplayName);
                if (activeDossier != null)
                    activeDossier.UnlockCaptureIndex();

                await activeCompletion.Task;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[ChainlingCaptureDossierPresenter] Capture is safe; dossier presentation was skipped: {exception.Message}",
                    this);
                CompletePresentation();
            }
        }

        private void OnDisable()
        {
            CompletePresentation();
        }

        private void OnDestroy()
        {
            CompletePresentation();
        }

        private void CompletePresentation()
        {
            if (activeDossier != null)
            {
                Destroy(activeDossier.gameObject);
                activeDossier = null;
            }

            if (inputLock.IsValid)
                inputLock.Release();
            inputLock = default;
            hudToken.Release();
            hudToken = default;

            TaskCompletionSource<bool> completion = activeCompletion;
            activeCompletion = null;
            completion?.TrySetResult(true);
        }

        private void AcquirePresentationTokens()
        {
            if (inputLock.IsValid)
                inputLock.Release();
            inputLock = InputLockService.Instance != null
                ? InputLockService.Instance.AcquireLock(LockReason, InputLockChannel.All, this)
                : default;
            hudToken.Release();
            hudToken = MobileHudService.Instance != null
                ? MobileHudService.Instance.AcquireContext(MobileHudContext.Menu, LockReason)
                : default;
        }

        private IPlayerRosterProvider ResolveRosterProvider()
        {
            if (rosterProviderBehaviour is IPlayerRosterProvider assigned)
                return assigned;

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerRosterProvider found)
                {
                    rosterProviderBehaviour = behaviours[i];
                    return found;
                }
            }

            SupabaseConfigSO config = supabaseConfig != null
                ? supabaseConfig
                : Resources.Load<SupabaseConfigSO>("SupabaseConfig");
            if (config == null)
                return null;

            var provider = gameObject.AddComponent<SupabasePlayerRosterProvider>();
            provider.Configure(config);
            rosterProviderBehaviour = provider;
            return provider;
        }

        private Transform ResolveFallbackParent()
        {
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>(true);
            return canvas != null ? canvas.transform : transform;
        }

        private static PlayerRosterEntry FindByBackendId(PlayerRosterSnapshot roster, string chainlingId)
        {
            if (roster?.chainlings == null)
                return null;
            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry candidate = roster.chainlings[i];
                if (candidate?.snapshot != null &&
                    string.Equals(candidate.snapshot.backendChainlingId, chainlingId, StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        private static PlayerRosterEntry BuildProvisionalEntry(
            CaptureDossierRequest request,
            PlayerRosterSnapshot roster)
        {
            request.fallbackSnapshot.backendChainlingId = request.chainlingId;
            var captured = new PlayerRosterEntry
            {
                snapshot = request.fallbackSnapshot,
                isPartyMember = request.addedToParty,
                partySlot = request.addedToParty && roster != null ? roster.PartyCount : -1,
            };
            if (string.IsNullOrWhiteSpace(captured.snapshot.displayName))
                captured.snapshot.displayName = request.fallbackDisplayName;
            return captured;
        }
    }
}
