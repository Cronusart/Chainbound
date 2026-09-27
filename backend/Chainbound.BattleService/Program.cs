using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Chainbound.BattleService;
using Chainbound.Combat.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddSingleton<ServiceOptions>();
builder.Services.AddHttpClient<SupabaseAuthorityGateway>();
builder.Services.AddHttpClient<SolanaTimeAuthority>();
builder.Services.AddSingleton<SolanaTimeAuthority>(sp =>
{
    IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
    return new SolanaTimeAuthority(
        factory.CreateClient(nameof(SolanaTimeAuthority)),
        sp.GetRequiredService<ServiceOptions>(),
        sp.GetRequiredService<ILogger<SolanaTimeAuthority>>());
});
builder.Services.AddHostedService(sp => sp.GetRequiredService<SolanaTimeAuthority>());
builder.Services.AddSingleton<ManifestCatalog>();
builder.Services.AddSingleton<BattleSessionManager>();

WebApplication app = builder.Build();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (AuthorityException exception)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = exception.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ApiError { Code = exception.Code, Message = exception.Message });
        }
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Unhandled battle authority request failure.");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ApiError
            {
                Code = "authority_internal_error",
                Message = "The battle authority could not complete the request.",
            });
        }
    }
});

app.MapGet("/health/live", () => Results.Ok(new { ok = true }));
app.MapGet("/health/ready", (BattleSessionManager manager, ManifestCatalog catalog, SolanaTimeAuthority time) =>
{
    bool clockFresh = time.TryGetUnixMilliseconds(out long now);
    return manager.IsReady
        ? Results.Ok(new { ok = true, manifestHash = catalog.ManifestHash, configHash = catalog.ConfigHash, authorityUnixMilliseconds = now, finalizedSlot = time.LastFinalizedSlot })
        : Results.Json(new
        {
            ok = false,
            code = "signal_sync_unavailable",
            manifestReady = catalog.IsReady,
            clockFresh,
            authorityUnixMilliseconds = now,
            finalizedSlot = time.LastFinalizedSlot,
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapPost("/v1/pve/battles", async (
    HttpContext context,
    StartBattleApiRequest request,
    SupabaseAuthorityGateway gateway,
    BattleSessionManager manager,
    CancellationToken cancellationToken) =>
{
    AuthenticatedProfile owner = await Authenticate(context, gateway, cancellationToken);
    CombatSessionEnvelope envelope = await manager.StartAsync(owner, request, cancellationToken);
    return Results.Ok(envelope);
});

app.MapGet("/v1/pve/battles/{id:guid}", async (
    HttpContext context,
    Guid id,
    SupabaseAuthorityGateway gateway,
    BattleSessionManager manager,
    CancellationToken cancellationToken) =>
{
    AuthenticatedProfile owner = await Authenticate(context, gateway, cancellationToken);
    return Results.Ok(await manager.ResumeAsync(owner, id, cancellationToken));
});

app.MapPost("/v1/pve/battles/{id:guid}/commands", async (
    HttpContext context,
    Guid id,
    TeamCommandApiRequest command,
    SupabaseAuthorityGateway gateway,
    BattleSessionManager manager,
    CancellationToken cancellationToken) =>
{
    AuthenticatedProfile owner = await Authenticate(context, gateway, cancellationToken);
    CommandServiceResult result = await manager.SubmitAsync(owner, id, command, cancellationToken);
    if (!result.Ok)
    {
        int status = result.Code == "state_version_conflict" ? 409 : 422;
        return Results.Json(new ApiError
        {
            Code = result.Code,
            Message = result.Message,
            StateVersion = result.Session?.StateVersion,
        }, statusCode: status);
    }
    return Results.Ok(manager.BuildEnvelope(result.Session!));
});

app.MapGet("/v1/capture/loadout", async (
    HttpContext context,
    SupabaseAuthorityGateway gateway,
    SolanaTimeAuthority time,
    CancellationToken cancellationToken) =>
{
    AuthenticatedProfile owner = await Authenticate(context, gateway, cancellationToken);
    long now = time.RequireUnixMilliseconds();
    AuthorityLoadoutSnapshot loadout = await gateway.GetLoadoutAsync(owner.ProfileId, now, cancellationToken);
    return Results.Ok(ToClientLoadout(loadout, now));
});

app.MapPut("/v1/capture/loadout", async (
    HttpContext context,
    EquipCaptureGearApiRequest request,
    SupabaseAuthorityGateway gateway,
    SolanaTimeAuthority time,
    CancellationToken cancellationToken) =>
{
    AuthenticatedProfile owner = await Authenticate(context, gateway, cancellationToken);
    long now = time.RequireUnixMilliseconds();
    AuthorityLoadoutSnapshot loadout = await gateway.EquipAsync(owner.ProfileId, request.Slot, request.ItemId, now, cancellationToken);
    return Results.Ok(ToClientLoadout(loadout, now));
});

app.MapPost("/v1/capture/boosts/{itemId}", async (
    HttpContext context,
    string itemId,
    SupabaseAuthorityGateway gateway,
    SolanaTimeAuthority time,
    CancellationToken cancellationToken) =>
{
    AuthenticatedProfile owner = await Authenticate(context, gateway, cancellationToken);
    long now = time.RequireUnixMilliseconds();
    AuthorityLoadoutSnapshot loadout = await gateway.ActivateBoostAsync(owner.ProfileId, itemId, now, cancellationToken);
    return Results.Ok(ToClientLoadout(loadout, now));
});

app.Map("/v1/pve/battles/{id:guid}/stream", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    Guid battleId = Guid.Parse((string)context.Request.RouteValues["id"]!);
    SupabaseAuthorityGateway gateway = context.RequestServices.GetRequiredService<SupabaseAuthorityGateway>();
    BattleSessionManager manager = context.RequestServices.GetRequiredService<BattleSessionManager>();
    AuthenticatedProfile owner = await Authenticate(context, gateway, context.RequestAborted);
    CaptureAttemptRuntime attempt = await manager.GetAttemptAsync(owner, battleId, context.RequestAborted);
    using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
    attempt.Connect();
    try
    {
        await SendJson(socket, new { type = "challenge", challenge = attempt.Challenge }, context.RequestAborted);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        Task receive = ReceiveInputs(socket, attempt, linked.Token);
        Task send = SendSnapshots(socket, attempt, linked.Token);
        await Task.WhenAny(receive, send);
        linked.Cancel();
        try { await Task.WhenAll(receive, send); } catch (OperationCanceledException) { }
    }
    finally
    {
        attempt.Disconnect();
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "capture stream closed", CancellationToken.None);
    }
});

app.Run();

static async Task<AuthenticatedProfile> Authenticate(HttpContext context, SupabaseAuthorityGateway gateway, CancellationToken cancellationToken)
{
    return await gateway.AuthenticateAsync(context.Request.Headers.Authorization.ToString(), cancellationToken);
}

static CaptureLoadoutSnapshot ToClientLoadout(AuthorityLoadoutSnapshot source, long now)
{
    return new CaptureLoadoutSnapshot
    {
        HeadItemId = source.HeadItemId,
        BodyItemId = source.BodyItemId,
        ActiveBoosts = source.ActiveBoosts,
        ResolvedEffects = source.GearEffects.Add(source.BoostEffects),
        AuthorityUnixMilliseconds = now,
        TimeAuthorityFresh = true,
    };
}

static async Task ReceiveInputs(WebSocket socket, CaptureAttemptRuntime attempt, CancellationToken cancellationToken)
{
    byte[] buffer = new byte[4096];
    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
        WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancellationToken);
        if (result.MessageType == WebSocketMessageType.Close)
            break;
        if (result.MessageType != WebSocketMessageType.Text || !result.EndOfMessage)
            continue;
        CaptureStreamInput? input = JsonSerializer.Deserialize<CaptureStreamInput>(buffer.AsSpan(0, result.Count), JsonDefaults.Options);
        if (input != null)
            attempt.SetInput(input.Sequence, input.Held);
    }
}

static async Task SendSnapshots(WebSocket socket, CaptureAttemptRuntime attempt, CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
    while (socket.State == WebSocketState.Open && !attempt.Completion.IsCompleted && await timer.WaitForNextTickAsync(cancellationToken))
        await SendJson(socket, new { type = "snapshot", state = attempt.Latest }, cancellationToken);
    CaptureOutcome outcome = await attempt.Completion;
    if (socket.State == WebSocketState.Open)
        await SendJson(socket, new { type = "outcome", outcome }, cancellationToken);
}

static async Task SendJson(WebSocket socket, object payload, CancellationToken cancellationToken)
{
    byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonDefaults.Options));
    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
}

public partial class Program { }
