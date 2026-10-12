using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using Chainbound.Combat.Core;
using UnityEngine;

public enum PendingBattleResumeOutcome
{
    None,
    Rejoin,
    Wipe,
    Failed,
}

public enum PendingBattleResumeChoice
{
    Unavailable,
    Rejoin,
    Forfeit,
    Continue,
}

public readonly struct PendingBattleResumeResult
{
    public PendingBattleResumeResult(PendingBattleResumeOutcome outcome, CombatSessionEnvelope session = null, string message = "")
    {
        Outcome = outcome;
        Session = session;
        Message = message ?? string.Empty;
    }

    public PendingBattleResumeOutcome Outcome { get; }
    public CombatSessionEnvelope Session { get; }
    public string Message { get; }
}

/// <summary>
/// Shared authority decision point used after menu hydration and when a grass or
/// narrative encounter discovers an already-open battle.
/// </summary>
public sealed class PendingBattleResumeCoordinator : MonoBehaviour
{
    [SerializeField] private PendingBattleResumePresenter presenter;

    public static PendingBattleResumeCoordinator Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        if (presenter == null)
            presenter = GetComponent<PendingBattleResumePresenter>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public async Task<PendingBattleResumeResult> ResolveCurrentAsync()
    {
        ICombatSessionService service = ResolveService();
        if (service == null || presenter == null)
            return new PendingBattleResumeResult(PendingBattleResumeOutcome.None);
        if (service is AuthoritativeCombatSessionService authority && !authority.IsRolloutEnabled)
            return new PendingBattleResumeResult(PendingBattleResumeOutcome.None);

        try
        {
            CombatSessionEnvelope current = await service.GetCurrentEncounterAsync();
            return current == null
                ? new PendingBattleResumeResult(PendingBattleResumeOutcome.None)
                : await ResolveEnvelopeAsync(current, service);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[PendingBattleResumeCoordinator] Could not check the current battle: {exception.Message}", this);
            return new PendingBattleResumeResult(PendingBattleResumeOutcome.Failed, message: exception.Message);
        }
    }

    public Task<PendingBattleResumeResult> ResolveEnvelopeAsync(CombatSessionEnvelope envelope)
    {
        return ResolveEnvelopeAsync(envelope, ResolveService());
    }

    private async Task<PendingBattleResumeResult> ResolveEnvelopeAsync(
        CombatSessionEnvelope envelope,
        ICombatSessionService service)
    {
        if (envelope == null)
            return new PendingBattleResumeResult(PendingBattleResumeOutcome.None);
        if (presenter == null)
            return new PendingBattleResumeResult(PendingBattleResumeOutcome.Failed, envelope, "Pending battle prompt is unavailable.");

        if (envelope.RequiresWipe || !envelope.IsResumable)
        {
            await presenter.ShowExpiredAsync();
            AuthoritativeBattleLaunchContext.Clear();
            await RefreshLocalConsequencesAfterWipeAsync();
            return new PendingBattleResumeResult(PendingBattleResumeOutcome.Wipe, envelope);
        }

        if (service == null)
            return new PendingBattleResumeResult(PendingBattleResumeOutcome.Failed, envelope, "Battle authority is unavailable.");

        string status = string.Empty;
        while (true)
        {
            PendingBattleResumeChoice choice = await presenter.ShowOpenAsync(status);
            if (choice == PendingBattleResumeChoice.Unavailable)
            {
                return new PendingBattleResumeResult(
                    PendingBattleResumeOutcome.Failed,
                    envelope,
                    "Pending battle prompt closed before a choice was made.");
            }

            try
            {
                CombatSessionEnvelope resolved;
                if (choice == PendingBattleResumeChoice.Forfeit)
                {
                    resolved = await service.ForfeitEncounterAsync(envelope.BattleId);
                    AuthoritativeBattleLaunchContext.Clear();
                    await RefreshLocalConsequencesAfterWipeAsync();
                    return new PendingBattleResumeResult(PendingBattleResumeOutcome.Wipe, resolved);
                }

                resolved = await service.ResumeEncounterAsync(envelope.BattleId);
                if (resolved.RequiresWipe || !resolved.IsResumable)
                {
                    await presenter.ShowExpiredAsync();
                    AuthoritativeBattleLaunchContext.Clear();
                    await RefreshLocalConsequencesAfterWipeAsync();
                    return new PendingBattleResumeResult(PendingBattleResumeOutcome.Wipe, resolved);
                }

                AuthoritativeBattleLaunchContext.Publish(resolved);
                return new PendingBattleResumeResult(PendingBattleResumeOutcome.Rejoin, resolved);
            }
            catch (Exception exception)
            {
                status = "Signal sync failed. Choose again to retry.";
                Debug.LogWarning($"[PendingBattleResumeCoordinator] Resume decision failed: {exception.Message}", this);
            }
        }
    }

    private static ICombatSessionService ResolveService()
    {
        return SolanaServiceBridge.Instance != null
            ? SolanaServiceBridge.Instance.CombatSessionService
            : null;
    }

    private static async Task RefreshLocalConsequencesAfterWipeAsync()
    {
        GameSessionContext session = GameSessionContext.Instance;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerProfileId))
            return;

        try
        {
            SupabasePlayerRosterProvider[] rosters = UnityEngine.Object.FindObjectsByType<SupabasePlayerRosterProvider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < rosters.Length; i++)
            {
                if (rosters[i] == null)
                    continue;
                await PlayerProfileReadStore.ResolveOrCreate().RefreshRosterAsync(
                    rosters[i],
                    session.PlayerProfileId,
                    session.WalletAddress);
                break;
            }

            CoreBagItemInventoryService[] bags = UnityEngine.Object.FindObjectsByType<CoreBagItemInventoryService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < bags.Length; i++)
            {
                if (bags[i] == null)
                    continue;
                await PlayerProfileReadStore.ResolveOrCreate().RefreshInventoryAsync(
                    bags[i],
                    session.PlayerProfileId,
                    session.WalletAddress);
                break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[PendingBattleResumeCoordinator] Could not refresh party or RAM after the wipe: {exception.Message}");
        }
    }
}

public static class PendingBattleRouteUtility
{
    public const string BattleScene = "Combat2_TestHarness";

    /// <summary>
    /// True when the current-battle probe found nothing, including when the service
    /// never answered. A failure that already has a session still stops the route.
    /// </summary>
    public static bool ShouldContinueAfterProbe(PendingBattleResumeResult result)
    {
        if (result.Outcome == PendingBattleResumeOutcome.None)
            return true;

        return result.Outcome == PendingBattleResumeOutcome.Failed && result.Session == null;
    }

    public static bool RequestRejoin(TransitionStyle style, string sourceId)
    {
        SceneLoader loader = SceneLoader.Instance;
        EncounterTransitionBridge bridge = EncounterTransitionBridge.Instance;
        if (loader == null || bridge == null)
            return false;

        CombatStageRequest.SetPending(string.Empty);
        bridge.ArmBattleSceneLaunchTicket(BattleScene, EncounterTransitionBridge.EncounterLaunchSource.AuthoritativeResume);
        SanctuarySpawnDirector.ClearPendingPresentation();
        return loader.RequestTransition(new SceneTransitionRequest(
            BattleScene,
            string.Empty,
            style,
            sourceId));
    }

    public static bool RequestWipe(TransitionStyle style, string sourceId)
    {
        SceneLoader loader = SceneLoader.Instance;
        SanctuaryStateService sanctuary = SanctuaryStateService.Instance;
        if (loader == null || sanctuary == null)
            return false;

        sanctuary.GetWipeDestination(
            out string sceneId,
            out string spawnId,
            out bool _,
            out string[] residentChunkSceneIds);
        SanctuarySpawnDirector.ArmPendingPresentation(sceneId, spawnId, SanctuaryWakeKind.CombatDefeat);
        return loader.RequestTransition(new SceneTransitionRequest(
            sceneId,
            spawnId,
            style,
            sourceId,
            preferredOverworldChunkSceneId: residentChunkSceneIds != null && residentChunkSceneIds.Length > 0
                ? residentChunkSceneIds[0]
                : string.Empty,
            preferredOverworldChunkSceneIds: residentChunkSceneIds));
    }
}
