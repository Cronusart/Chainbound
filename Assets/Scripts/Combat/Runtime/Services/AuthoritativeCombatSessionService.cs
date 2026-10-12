using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chainbound.Combat.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Chainbound.Combat
{
    public interface IAuthoritativeCombatAvailability
    {
        bool IsAuthorityConfigured { get; }
        bool IsAuthorityReachable { get; }
        string LastAuthorityError { get; }
        event Action<bool, string> AvailabilityChanged;
        Task<bool> RefreshAvailabilityAsync(CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>
    /// GameServices client for the containerized PvE authority. The bearer token is
    /// process-memory only and the client never calls service-role RPCs directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthoritativeCombatSessionService : MonoBehaviour,
        ICombatSessionService,
        ICaptureLoadoutService,
        IAuthoritativeCombatAvailability
    {
        [SerializeField] private AuthoritativeCombatServiceConfigSO config;
        [SerializeField] private bool enableDebugLogs;

        private bool isReachable;
        private string lastError = "";

        public bool IsAuthorityConfigured => config != null && config.IsConfigured;
        public bool IsRolloutEnabled => config != null && config.authoritativePveCaptureEnabled;
        public bool AllowsLocalPractice => config == null || config.allowLocalPracticeInEditor;
        public bool IsAuthorityReachable => isReachable;
        public string LastAuthorityError => lastError;
        public event Action<bool, string> AvailabilityChanged;

        public void Configure(AuthoritativeCombatServiceConfigSO value)
        {
            if (value != null)
                config = value;
        }

        public async Task<bool> RefreshAvailabilityAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!IsAuthorityConfigured)
            {
                SetAvailability(false, "Authoritative PvE capture is not configured.");
                return false;
            }

            try
            {
                HttpResponse response = await SendAsync("GET", "/health/ready", null, false, cancellationToken);
                bool ok = response.statusCode >= 200 && response.statusCode < 300;
                if (!ok)
                {
                    string detail = DescribeTransportFailure(response);
                    SetAvailability(false, detail);
                    Debug.LogWarning($"[AuthoritativeCombatSessionService] Battle service is not ready: {detail}", this);
                }
                else
                {
                    SetAvailability(true, "");
                }
                return ok;
            }
            catch (Exception exception)
            {
                SetAvailability(false, exception.Message);
                Debug.LogWarning($"[AuthoritativeCombatSessionService] Battle service is not ready: {exception.Message}", this);
                return false;
            }
        }

        public async Task<CombatSessionEnvelope> StartEncounterAsync(
            StartCombatSessionRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            var dto = new StartBattleRequestDto
            {
                commandId = request != null ? request.CommandId : "",
                zoneId = request != null ? request.ZoneId : "",
                narrativeEncounterId = request != null ? request.NarrativeEncounterId : "",
                clientManifestHash = config.gameplayManifestHash,
            };
            HttpResponse response = await SendAsync("POST", "/v1/pve/battles", JsonUtility.ToJson(dto), true, cancellationToken);
            return ParseEnvelopeOrThrow(response);
        }

        public async Task<CombatSessionEnvelope> ResumeEncounterAsync(
            string battleId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            HttpResponse response = await SendAsync("GET", $"/v1/pve/battles/{Uri.EscapeDataString(battleId ?? "")}", null, true, cancellationToken);
            return ParseEnvelopeOrThrow(response);
        }

        public async Task<CombatSessionEnvelope> GetCurrentEncounterAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            HttpResponse response = await SendAsync("GET", "/v1/pve/battles/current", null, true, cancellationToken);
            if (response.statusCode == 204)
                return null;
            return ParseEnvelopeOrThrow(response);
        }

        public async Task<CombatSessionEnvelope> ForfeitEncounterAsync(
            string battleId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            HttpResponse response = await SendAsync(
                "POST",
                $"/v1/pve/battles/{Uri.EscapeDataString(battleId ?? "")}/forfeit",
                "{}",
                true,
                cancellationToken);
            return ParseEnvelopeOrThrow(response);
        }

        public async Task<CombatSessionEnvelope> SubmitTeamCommandAsync(
            string battleId,
            CombatTeamCommand command,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            var dto = new TeamCommandDto
            {
                commandId = command != null ? command.CommandId : "",
                expectedVersion = command != null ? command.ExpectedVersion : 0,
                kind = command != null ? command.Kind : "",
                targetId = command != null ? command.TargetId : "",
                orbId = command != null ? command.OrbId : "",
                actorId = command != null ? command.ActorId : "",
                actionId = command != null ? command.ActionId : "",
                turnNumber = command != null ? command.TurnNumber : 0,
                stepIndex = command != null ? command.StepIndex : 0,
                battleActive = command == null || command.BattleActive,
                winnerTeamIndex = command != null ? command.WinnerTeamIndex : -1,
                endReason = command != null ? command.EndReason : "",
                vitals = command != null && command.Vitals != null
                    ? command.Vitals.ConvertAll(ToDto).ToArray()
                    : Array.Empty<VitalDto>(),
            };
            HttpResponse response = await SendAsync(
                "POST",
                $"/v1/pve/battles/{Uri.EscapeDataString(battleId ?? "")}/commands",
                JsonUtility.ToJson(dto),
                true,
                cancellationToken);
            return ParseEnvelopeOrThrow(response);
        }

        public async Task<BattleRewardSettlement> GetBattleRewardAsync(
            string battleId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            HttpResponse response = await SendAsync(
                "GET",
                $"/v1/pve/battles/{Uri.EscapeDataString(battleId ?? "")}/reward",
                null,
                true,
                cancellationToken);
            if (response.statusCode < 200 || response.statusCode >= 300)
                throw ParseServiceException(response);
            BattleRewardSettlementDto dto = JsonUtility.FromJson<BattleRewardSettlementDto>(response.body);
            if (dto == null || string.IsNullOrWhiteSpace(dto.battleId) || string.IsNullOrWhiteSpace(dto.status))
                throw new InvalidOperationException("Battle authority returned an invalid reward receipt.");
            return dto.ToCore();
        }

        public async Task<ICaptureSession> OpenCaptureSessionAsync(
            string battleId,
            string attemptId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            var session = new AuthoritativeCaptureSession(
                BuildWebSocketUri(battleId),
                SupabaseAuthSessionContext.AccessToken,
                attemptId,
                enableDebugLogs);
            await session.ConnectAsync(cancellationToken);
            return session;
        }

        public async Task<CaptureLoadoutSnapshot> FetchAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            HttpResponse response = await SendAsync("GET", "/v1/capture/loadout", null, true, cancellationToken);
            return ParseLoadoutOrThrow(response);
        }

        public async Task<CaptureLoadoutSnapshot> EquipAsync(
            string slot,
            string itemId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            var dto = new EquipGearDto { slot = slot ?? "", itemId = itemId ?? "" };
            HttpResponse response = await SendAsync("PUT", "/v1/capture/loadout", JsonUtility.ToJson(dto), true, cancellationToken);
            return ParseLoadoutOrThrow(response);
        }

        public async Task<CaptureLoadoutSnapshot> ActivateBoostAsync(
            string itemId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireConfigured();
            HttpResponse response = await SendAsync(
                "POST",
                $"/v1/capture/boosts/{Uri.EscapeDataString(itemId ?? "")}",
                "{}",
                true,
                cancellationToken);
            return ParseLoadoutOrThrow(response);
        }

        private async Task<HttpResponse> SendAsync(
            string method,
            string relativePath,
            string json,
            bool authenticated,
            CancellationToken cancellationToken)
        {
            string url = config.serviceBaseUrl.TrimEnd('/') + relativePath;
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (json != null)
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Content-Type", "application/json");
                if (authenticated)
                {
                    string token = SupabaseAuthSessionContext.AccessToken;
                    if (string.IsNullOrWhiteSpace(token))
                        throw new InvalidOperationException("An authenticated Supabase profile session is required.");
                    request.SetRequestHeader("Authorization", $"Bearer {token.Trim()}");
                }
                request.timeout = Mathf.Max(1, config.requestTimeoutSeconds);
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }

                var result = new HttpResponse
                {
                    statusCode = (int)request.responseCode,
                    body = request.downloadHandler != null ? request.downloadHandler.text : "",
                    transportError = request.error ?? "",
                };
                bool ok = result.statusCode >= 200 && result.statusCode < 300;
                if (authenticated)
                    SetAvailability(ok || result.statusCode == 401 || result.statusCode == 403, ok ? "" : lastError);
                if (enableDebugLogs)
                    Debug.Log($"[AuthoritativeCombatSessionService] {method} {relativePath} -> {result.statusCode}", this);
                return result;
            }
        }

        private CombatSessionEnvelope ParseEnvelopeOrThrow(HttpResponse response)
        {
            if (response.statusCode < 200 || response.statusCode >= 300)
                throw ParseServiceException(response);
            CombatSessionEnvelopeDto dto = JsonUtility.FromJson<CombatSessionEnvelopeDto>(response.body);
            if (dto == null || string.IsNullOrWhiteSpace(dto.battleId))
                throw new InvalidOperationException("Battle authority returned an invalid session envelope.");
            SetAvailability(true, "");
            return new CombatSessionEnvelope
            {
                BattleId = dto.battleId,
                Version = dto.version,
                ManifestHash = dto.manifestHash,
                StateJson = dto.stateJson,
                ErrorCode = dto.errorCode,
                Message = dto.message,
                IsResumable = dto.isResumable,
                ExpiresAtUnixMilliseconds = dto.expiresAtUnixMilliseconds,
                RequiresResumeChoice = dto.requiresResumeChoice,
                RequiresWipe = dto.requiresWipe,
                EndReason = dto.endReason,
                Settlement = dto.settlement != null ? dto.settlement.ToCore() : null,
            };
        }

        private static VitalDto ToDto(CombatVitalSnapshot source)
        {
            return new VitalDto
            {
                entityId = source != null ? source.EntityId : "",
                teamIndex = source != null ? source.TeamIndex : 0,
                rosterIndex = source != null ? source.RosterIndex : 0,
                currentHp = source != null ? source.CurrentHp : 0,
                currentAp = source != null ? source.CurrentAp : 0,
                isActive = source != null && source.IsActive,
            };
        }

        private CaptureLoadoutSnapshot ParseLoadoutOrThrow(HttpResponse response)
        {
            if (response.statusCode < 200 || response.statusCode >= 300)
                throw ParseServiceException(response);
            CaptureLoadoutDto dto = JsonUtility.FromJson<CaptureLoadoutDto>(response.body);
            var result = new CaptureLoadoutSnapshot
            {
                HeadItemId = dto != null ? dto.headItemId : "",
                BodyItemId = dto != null ? dto.bodyItemId : "",
                AuthorityUnixMilliseconds = dto != null ? dto.authorityUnixMilliseconds : 0,
                TimeAuthorityFresh = dto != null && dto.timeAuthorityFresh,
                ResolvedEffects = dto != null ? dto.resolvedEffects.ToCore() : CaptureEffectVector.None,
                ActiveBoosts = new List<ActiveCaptureBoost>(),
            };
            if (dto != null && dto.activeBoosts != null)
            {
                for (int i = 0; i < dto.activeBoosts.Length; i++)
                {
                    CaptureBoostDto boost = dto.activeBoosts[i];
                    result.ActiveBoosts.Add(new ActiveCaptureBoost
                    {
                        ItemId = boost.itemId,
                        ExpiresAtUnixMilliseconds = boost.expiresAtUnixMilliseconds,
                        Effects = boost.effects.ToCore(),
                    });
                }
            }
            return result;
        }

        private Exception ParseServiceException(HttpResponse response)
        {
            ErrorDto error = null;
            try { error = JsonUtility.FromJson<ErrorDto>(response.body); }
            catch (ArgumentException) { }
            string code = error != null && !string.IsNullOrWhiteSpace(error.code) ? error.code : "authority_request_failed";
            string message = error != null && !string.IsNullOrWhiteSpace(error.message)
                ? error.message
                : "SIGNAL SYNC UNAVAILABLE — RETRYING";
            if (response.statusCode == 0 || response.statusCode >= 500)
                SetAvailability(false, "SIGNAL SYNC UNAVAILABLE — RETRYING");
            return new AuthoritativeCombatServiceException(code, message, response.statusCode);
        }

        private Uri BuildWebSocketUri(string battleId)
        {
            var builder = new UriBuilder(config.serviceBaseUrl);
            builder.Scheme = builder.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            builder.Path = $"/v1/pve/battles/{Uri.EscapeDataString(battleId ?? "")}/stream";
            builder.Query = "";
            return builder.Uri;
        }

        private void RequireConfigured()
        {
            if (!IsAuthorityConfigured)
                throw new InvalidOperationException("Authoritative PvE capture is not configured or enabled.");
        }

        private void SetAvailability(bool reachable, string error)
        {
            bool changed = isReachable != reachable || !string.Equals(lastError, error ?? "", StringComparison.Ordinal);
            isReachable = reachable;
            lastError = error ?? "";
            if (changed)
                AvailabilityChanged?.Invoke(isReachable, lastError);
        }

        private static string DescribeTransportFailure(HttpResponse response)
        {
            if (!string.IsNullOrWhiteSpace(response.transportError))
                return response.transportError.Trim();
            if (response.statusCode <= 0)
                return "The battle service did not respond.";
            return $"The battle service returned HTTP {response.statusCode}.";
        }

        private struct HttpResponse { public int statusCode; public string body; public string transportError; }
        [Serializable] private sealed class StartBattleRequestDto { public string commandId; public string zoneId; public string narrativeEncounterId; public string clientManifestHash; }
        [Serializable] private sealed class TeamCommandDto
        {
            public string commandId; public int expectedVersion; public string kind; public string targetId; public string orbId;
            public string actorId; public string actionId; public int turnNumber; public int stepIndex;
            public bool battleActive; public int winnerTeamIndex; public string endReason; public VitalDto[] vitals;
        }
        [Serializable] private sealed class VitalDto
        {
            public string entityId; public int teamIndex; public int rosterIndex; public int currentHp; public int currentAp; public bool isActive;
        }
        [Serializable] private sealed class EquipGearDto { public string slot; public string itemId; }
        [Serializable] private sealed class ErrorDto { public string code; public string message; public int stateVersion; }
        [Serializable] private sealed class CombatSessionEnvelopeDto
        {
            public string battleId; public int version; public string manifestHash; public string stateJson;
            public string errorCode; public string message; public bool isResumable; public long expiresAtUnixMilliseconds;
            public bool requiresResumeChoice; public bool requiresWipe; public string endReason;
            public BattleRewardSettlementDto settlement;
        }
        [Serializable] private sealed class CaptureLoadoutDto
        {
            public string headItemId; public string bodyItemId; public CaptureEffectDto resolvedEffects;
            public CaptureBoostDto[] activeBoosts; public long authorityUnixMilliseconds; public bool timeAuthorityFresh;
        }
        [Serializable] private struct CaptureBoostDto { public string itemId; public CaptureEffectDto effects; public long expiresAtUnixMilliseconds; }
        [Serializable] internal struct CaptureEffectDto
        {
            public int scanRating; public int stabilityBasisPoints; public int bandWidthBasisPoints;
            public int lockRateBasisPoints; public int captureTimeBasisPoints;
            public CaptureEffectVector ToCore() => new CaptureEffectVector(scanRating, stabilityBasisPoints, bandWidthBasisPoints, lockRateBasisPoints, captureTimeBasisPoints);
        }
    }

    public sealed class AuthoritativeCombatServiceException : Exception
    {
        public AuthoritativeCombatServiceException(string code, string message, int statusCode) : base(message)
        {
            Code = code ?? "";
            StatusCode = statusCode;
        }
        public string Code { get; }
        public int StatusCode { get; }
    }

    internal sealed class AuthoritativeCaptureSession : ICaptureSession
    {
        private readonly ClientWebSocket socket = new ClientWebSocket();
        private readonly Uri uri;
        private readonly string expectedAttemptId;
        private readonly bool debug;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly TaskCompletionSource<CaptureChallengeSnapshot> challengeReady = new TaskCompletionSource<CaptureChallengeSnapshot>();
        private readonly TaskCompletionSource<CaptureOutcome> completion = new TaskCompletionSource<CaptureOutcome>();
        private readonly TaskCompletionSource<long> armedReady = new TaskCompletionSource<long>();
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private CaptureChallengeSnapshot challenge;
        private CaptureAuthoritativeState latestState;
        private Task receiveLoop;
        private long nextInputSequence;
        private long lastSentInputSequence = -1;
        private long lastAcknowledgedInputSequence = -1;
        private long lastResendStopwatchTicks;
        private int desiredHeld;
        private int disposing;
        private int disposed;

        public AuthoritativeCaptureSession(Uri uri, string accessToken, string attemptId, bool debug)
        {
            this.uri = uri;
            expectedAttemptId = attemptId ?? "";
            this.debug = debug;
            socket.Options.SetRequestHeader("Authorization", $"Bearer {(accessToken ?? "").Trim()}");
        }

        public CaptureChallengeSnapshot Challenge => challenge;
        public CaptureAuthoritativeState LatestState => latestState;
        public Task<CaptureOutcome> Completion => completion.Task;
        public event Action<CaptureAuthoritativeState> SnapshotReceived;

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            await socket.ConnectAsync(uri, cancellationToken);
            receiveLoop = ReceiveLoopAsync(lifetime.Token);
            using (cancellationToken.Register(() => challengeReady.TrySetCanceled()))
                challenge = await challengeReady.Task;
            if (!string.IsNullOrWhiteSpace(expectedAttemptId) && !string.Equals(challenge.AttemptId, expectedAttemptId, StringComparison.Ordinal))
                throw new InvalidOperationException("Capture stream attempt id did not match the requested attempt.");
        }

        public async Task ArmAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (challenge != null && challenge.DeadlineUnixMilliseconds > 0)
                return;
            if (completion.Task.IsCompleted)
                return;
            if (socket.State != WebSocketState.Open)
                throw new InvalidOperationException("Capture stream closed before the attempt could start.");

            string json = JsonUtility.ToJson(new InputDto { sequence = 0, held = false, arm = true });
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await sendGate.WaitAsync(cancellationToken);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                sendGate.Release();
            }

            await Task.WhenAny(armedReady.Task, completion.Task);
        }

        public async Task SetHeldAsync(bool held, long inputSequence, CancellationToken cancellationToken = default(CancellationToken))
        {
            // The session, rather than any UI surface, owns the wire sequence.
            // Clear/focus events otherwise race controller-local counters and can
            // leave every later hold message stale from the server's perspective.
            _ = inputSequence;
            Volatile.Write(ref desiredHeld, held ? 1 : 0);
            await SendHeldAsync(held, cancellationToken);
        }

        private async Task SendHeldAsync(bool held, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref disposed) != 0 || socket.State != WebSocketState.Open || completion.Task.IsCompleted)
                return;
            long sequence = Interlocked.Increment(ref nextInputSequence);
            Interlocked.Exchange(ref lastSentInputSequence, sequence);
            string json = JsonUtility.ToJson(new InputDto { sequence = sequence, held = held });
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await sendGate.WaitAsync(cancellationToken);
            try
            {
                if (Volatile.Read(ref disposed) != 0 || socket.State != WebSocketState.Open || completion.Task.IsCompleted)
                    return;
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                sendGate.Release();
            }
        }

        public void ClearHeldInput()
        {
            _ = SetHeldAsync(false, 0, CancellationToken.None);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposing, 1) != 0)
                return;
            ClearHeldInput();
            Interlocked.Exchange(ref disposed, 1);
            lifetime.Cancel();
            socket.Dispose();
            lifetime.Dispose();
            sendGate.Dispose();
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[16384];
            try
            {
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var segment = new ArraySegment<byte>(buffer);
                    WebSocketReceiveResult result = await socket.ReceiveAsync(segment, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    if (result.MessageType != WebSocketMessageType.Text || !result.EndOfMessage)
                        continue;
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    StreamMessageDto message = JsonUtility.FromJson<StreamMessageDto>(json);
                    if (message == null)
                        continue;
                    if (string.Equals(message.type, "challenge", StringComparison.Ordinal))
                    {
                        CaptureChallengeSnapshot mapped = message.challenge.ToCore();
                        challengeReady.TrySetResult(mapped);
                    }
                    else if (string.Equals(message.type, "armed", StringComparison.Ordinal))
                    {
                        if (challenge != null && message.deadlineUnixMilliseconds > 0)
                            challenge.DeadlineUnixMilliseconds = message.deadlineUnixMilliseconds;
                        armedReady.TrySetResult(message.deadlineUnixMilliseconds);
                    }
                    else if (string.Equals(message.type, "snapshot", StringComparison.Ordinal))
                    {
                        latestState = message.state.ToCore();
                        Interlocked.Exchange(ref lastAcknowledgedInputSequence, latestState.LastInputSequence);
                        TryResendUnacknowledgedHeldState(latestState);
                        SnapshotReceived?.Invoke(latestState);
                    }
                    else if (string.Equals(message.type, "outcome", StringComparison.Ordinal))
                    {
                        completion.TrySetResult(message.outcome.ToCore());
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (debug)
                    Debug.LogWarning($"[AuthoritativeCaptureSession] Stream interrupted: {exception.Message}");
                challengeReady.TrySetException(exception);
                completion.TrySetResult(new CaptureOutcome
                {
                    AttemptId = expectedAttemptId,
                    Kind = CaptureOutcomeKind.Interrupted,
                    ErrorCode = "capture_stream_interrupted",
                    Message = "Capture stream interrupted; the battle remains resumable.",
                    BattleContinues = true,
                });
            }
        }

        private void TryResendUnacknowledgedHeldState(CaptureAuthoritativeState state)
        {
            if (state == null || completion.Task.IsCompleted || socket.State != WebSocketState.Open)
                return;

            long sent = Interlocked.Read(ref lastSentInputSequence);
            long acknowledged = Interlocked.Read(ref lastAcknowledgedInputSequence);
            bool held = Volatile.Read(ref desiredHeld) != 0;
            if (sent <= acknowledged || state.IsHeld == held)
                return;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            long previous = Interlocked.Read(ref lastResendStopwatchTicks);
            if (previous != 0 && (now - previous) * 1000L < System.Diagnostics.Stopwatch.Frequency / 4)
                return;
            Interlocked.Exchange(ref lastResendStopwatchTicks, now);
            if (debug)
                Debug.LogWarning($"[AuthoritativeCaptureSession] Re-sending capture input; server acknowledged {acknowledged}, latest sent {sent}, held={held}.");
            _ = SetHeldAsync(held, 0, CancellationToken.None);
        }

        [Serializable] private sealed class InputDto { public long sequence; public bool held; public bool arm; }
        [Serializable] private sealed class StreamMessageDto
        {
            public string type;
            public long deadlineUnixMilliseconds;
            public ChallengeDto challenge;
            public StateDto state;
            public OutcomeDto outcome;
        }
        [Serializable] private sealed class ChallengeDto
        {
            public string attemptId; public uint seed; public long deadlineUnixMilliseconds; public string battleId;
            public string targetId; public string speciesId; public string profileId; public string orbId; public string configHash;
            public int difficulty; public AuthoritativeCombatSessionService.CaptureEffectDto effectiveEffects;
            public ParametersDto parameters; public bool lowHpMarkerRevealed; public bool isLowHp; public ScannerDto[] scannerIntel;
            public CaptureChallengeSnapshot ToCore()
            {
                var value = new CaptureChallengeSnapshot
                {
                    AttemptId = attemptId, Seed = seed, DeadlineUnixMilliseconds = deadlineUnixMilliseconds,
                    BattleId = battleId, TargetId = targetId, SpeciesId = speciesId, ProfileId = profileId,
                    OrbId = orbId, ConfigHash = configHash, Difficulty = (CaptureVolatilityProfile)difficulty,
                    EffectiveEffects = effectiveEffects.ToCore(), Parameters = parameters.ToCore(),
                    LowHpMarkerRevealed = lowHpMarkerRevealed, IsLowHp = isLowHp,
                    ScannerIntel = new List<CaptureScannerIntel>(),
                };
                if (scannerIntel != null)
                {
                    for (int i = 0; i < scannerIntel.Length; i++)
                        value.ScannerIntel.Add(new CaptureScannerIntel { Stat = (CaptureIvStat)scannerIntel[i].stat, Grade = scannerIntel[i].grade });
                }
                return value;
            }
        }
        [Serializable] private struct ScannerDto { public int stat; public int grade; }
        [Serializable] private struct ParametersDto
        {
            public int tickRate, durationTicks, graphUnits, bandHeightUnits, lockRateBasisPointsPerSecond,
                escapeRateBasisPointsPerSecond, bandLiftAccelerationUnitsPerSecondSquared,
                bandGravityUnitsPerSecondSquared, bandDragBasisPointsPerSecond, bandMaximumSpeedUnitsPerSecond,
                bandEdgeBounceBasisPoints, traceAccelerationUnitsPerSecondSquared, traceMaximumSpeedUnitsPerSecond,
                traceMeanReversionUnitsPerSecondSquared, traceEdgeBounceBasisPoints, spikeChanceBasisPointsPerTick,
                spikeVelocityImpulseUnitsPerSecond;
            public CaptureSimulationParameters ToCore() => new CaptureSimulationParameters
            {
                TickRate = tickRate, DurationTicks = durationTicks, GraphUnits = graphUnits, BandHeightUnits = bandHeightUnits,
                LockRateBasisPointsPerSecond = lockRateBasisPointsPerSecond, EscapeRateBasisPointsPerSecond = escapeRateBasisPointsPerSecond,
                BandLiftAccelerationUnitsPerSecondSquared = bandLiftAccelerationUnitsPerSecondSquared,
                BandGravityUnitsPerSecondSquared = bandGravityUnitsPerSecondSquared,
                BandDragBasisPointsPerSecond = bandDragBasisPointsPerSecond, BandMaximumSpeedUnitsPerSecond = bandMaximumSpeedUnitsPerSecond,
                BandEdgeBounceBasisPoints = bandEdgeBounceBasisPoints, TraceAccelerationUnitsPerSecondSquared = traceAccelerationUnitsPerSecondSquared,
                TraceMaximumSpeedUnitsPerSecond = traceMaximumSpeedUnitsPerSecond,
                TraceMeanReversionUnitsPerSecondSquared = traceMeanReversionUnitsPerSecondSquared,
                TraceEdgeBounceBasisPoints = traceEdgeBounceBasisPoints, SpikeChanceBasisPointsPerTick = spikeChanceBasisPointsPerTick,
                SpikeVelocityImpulseUnitsPerSecond = spikeVelocityImpulseUnitsPerSecond,
            };
        }
        [Serializable] private struct StateDto
        {
            public string attemptId; public int version, tick, targetPositionUnits, bandPositionUnits,
                meterBasisPoints, remainingTicks, outcome;
            public long lastInputSequence;
            public bool isHeld, isInsideBand, spikeOccurred;
            public CaptureAuthoritativeState ToCore() => new CaptureAuthoritativeState
            {
                AttemptId = attemptId, Version = version, Tick = tick, TargetPositionUnits = targetPositionUnits,
                BandPositionUnits = bandPositionUnits, MeterBasisPoints = meterBasisPoints, RemainingTicks = remainingTicks,
                LastInputSequence = lastInputSequence,
                IsHeld = isHeld, IsInsideBand = isInsideBand, SpikeOccurred = spikeOccurred, Outcome = (CaptureOutcomeKind)outcome,
            };
        }
        [Serializable] private struct OutcomeDto
        {
            public int kind; public string attemptId, errorCode, message, grantedChainlingId, grantedEntangledTypeId;
            public bool addedToParty, battleContinues; public int finalMeterBasisPoints, elapsedTicks;
            public string[] unlockedAchievementIds;
            public BattleRewardSettlementDto settlement;
            public CaptureOutcome ToCore() => new CaptureOutcome
            {
                Kind = (CaptureOutcomeKind)kind, AttemptId = attemptId, ErrorCode = errorCode, Message = message,
                GrantedChainlingId = grantedChainlingId, GrantedEntangledTypeId = grantedEntangledTypeId,
                AddedToParty = addedToParty, BattleContinues = battleContinues,
                FinalMeterBasisPoints = finalMeterBasisPoints, ElapsedTicks = elapsedTicks,
                Settlement = settlement != null ? settlement.ToCore() : null,
                UnlockedAchievementIds = unlockedAchievementIds != null
                    ? new List<string>(unlockedAchievementIds)
                    : new List<string>(),
            };
        }
    }

    [Serializable]
    internal sealed class BattleRewardSettlementDto
    {
        public string status;
        public string battleId;
        public string settlementId;
        public string terminalOutcome;
        public long requestedRam;
        public long appliedRam;
        public string reason;
        public string rewardTier;
        public int rewardConfigVersion;
        public string rewardConfigHash;
        public long dailyEarned;
        public long dailyCap;
        public long balanceAfter;
        public long balanceBefore;
        public long receiptSequence;
        public string settledAt;
        public bool alreadyApplied;

        public BattleRewardSettlement ToCore()
        {
            bool hasLedgerResult = string.Equals(status, BattleRewardStatus.Settled, StringComparison.Ordinal) ||
                                   string.Equals(status, BattleRewardStatus.Zero, StringComparison.Ordinal);
            return new BattleRewardSettlement
            {
                Status = status ?? BattleRewardStatus.Unavailable,
                BattleId = battleId ?? "",
                SettlementId = settlementId ?? "",
                TerminalOutcome = terminalOutcome ?? "",
                RequestedRam = requestedRam,
                AppliedRam = appliedRam,
                Reason = reason ?? "",
                RewardTier = rewardTier ?? BattleRewardTier.Standard,
                RewardConfigVersion = rewardConfigVersion,
                RewardConfigHash = rewardConfigHash ?? "",
                DailyEarned = hasLedgerResult ? dailyEarned : (long?)null,
                DailyCap = hasLedgerResult ? dailyCap : (long?)null,
                BalanceAfter = hasLedgerResult ? balanceAfter : (long?)null,
                BalanceBefore = hasLedgerResult ? balanceBefore : (long?)null,
                ReceiptSequence = receiptSequence,
                SettledAt = settledAt ?? "",
                AlreadyApplied = alreadyApplied,
            };
        }
    }
}
