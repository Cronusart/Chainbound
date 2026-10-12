using System.Text;
using System.Text.Json;
using Chainbound.Combat.Core;

namespace Chainbound.BattleService;

public sealed class BattleRewardClient
{
    private readonly HttpClient _http;
    private readonly ServiceOptions _options;

    public BattleRewardClient(HttpClient http, ServiceOptions options)
    {
        _http = http;
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _options = options;
    }

    public bool IsReady => _options.HasBattleRewardCoordinator && _options.BattleRewardMode != "invalid";

    public async Task<BattleRewardSettlement> SettleAsync(Guid battleId, CancellationToken cancellationToken)
    {
        if (!IsReady) return BattleRewardSettlement.Unavailable(battleId.ToString("D"), "reward_coordinator_unavailable");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.BattleRewardTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ResolvedBattleRewardSettleUrl);
        request.Headers.Add("apikey", _options.SupabaseServiceRoleKey);
        request.Headers.Add("x-battle-reward-secret", _options.BattleRewardServiceSecret);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { battle_id = battleId }, JsonDefaults.Options),
            Encoding.UTF8,
            "application/json");
        using HttpResponseMessage response = await _http.SendAsync(request, timeout.Token);
        string body = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new AuthorityException("reward_settlement_failed", $"Reward coordinator returned {(int)response.StatusCode}.", 503);
        using JsonDocument document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("success", out JsonElement success) || success.ValueKind != JsonValueKind.True)
            throw new AuthorityException("reward_settlement_failed", "Reward coordinator rejected the settlement.", 503);
        BattleRewardSettlement? settlement = JsonSerializer.Deserialize<BattleRewardSettlement>(body, JsonDefaults.Options);
        if (settlement == null || !BattleRewardReceiptValidator.IsValid(settlement, battleId))
            throw new AuthorityException("reward_settlement_invalid", "Reward coordinator returned an invalid receipt.", 503);
        return settlement!;
    }
}
