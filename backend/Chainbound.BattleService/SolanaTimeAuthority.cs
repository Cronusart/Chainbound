using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Chainbound.BattleService;

/// <summary>
/// Calibrates a monotonic process clock from finalized Solana block time.
/// Expiration-sensitive mutations stop after ten minutes without a refresh.
/// </summary>
public sealed class SolanaTimeAuthority : BackgroundService
{
    private readonly HttpClient _http;
    private readonly ServiceOptions _options;
    private readonly ILogger<SolanaTimeAuthority> _logger;
    private readonly object _gate = new();
    private long _calibratedUnixMilliseconds;
    private long _calibratedStopwatchTicks;
    private long _lastFinalizedSlot;
    private bool _hasCalibration;

    public SolanaTimeAuthority(HttpClient http, ServiceOptions options, ILogger<SolanaTimeAuthority> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public long LastFinalizedSlot
    {
        get { lock (_gate) return _lastFinalizedSlot; }
    }

    public bool TryGetUnixMilliseconds(out long unixMilliseconds)
    {
        lock (_gate)
        {
            if (!_hasCalibration)
            {
                unixMilliseconds = 0;
                return false;
            }

            long elapsedTicks = Stopwatch.GetTimestamp() - _calibratedStopwatchTicks;
            double elapsedMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds < 0 || elapsedMilliseconds > _options.MaximumTimeExtrapolation.TotalMilliseconds)
            {
                unixMilliseconds = 0;
                return false;
            }

            unixMilliseconds = _calibratedUnixMilliseconds + (long)elapsedMilliseconds;
            return true;
        }
    }

    public long RequireUnixMilliseconds()
    {
        if (TryGetUnixMilliseconds(out long value))
            return value;
        throw new AuthorityException(
            "time_authority_stale",
            "Finalized Solana time is stale; expiration-sensitive operations are temporarily unavailable.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(90));
        do
        {
            try
            {
                await CalibrateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Finalized Solana time calibration failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CalibrateAsync(CancellationToken cancellationToken)
    {
        long slot = await RpcLongAsync(
            "getSlot",
            new object[] { new { commitment = "finalized" } },
            cancellationToken);
        long unixSeconds = await RpcLongAsync(
            "getBlockTime",
            new object[] { slot },
            cancellationToken);
        long stopwatchTicks = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            _calibratedUnixMilliseconds = checked(unixSeconds * 1000L);
            _calibratedStopwatchTicks = stopwatchTicks;
            _lastFinalizedSlot = slot;
            _hasCalibration = true;
        }
    }

    private async Task<long> RpcLongAsync(string method, object[] parameters, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            _options.SolanaRpcEndpoint,
            new { jsonrpc = "2.0", id = 1, method, @params = parameters },
            JsonDefaults.Options,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("result", out JsonElement result) ||
            result.ValueKind != JsonValueKind.Number ||
            !result.TryGetInt64(out long value))
        {
            throw new InvalidOperationException($"Solana {method} returned no numeric result.");
        }
        return value;
    }
}
