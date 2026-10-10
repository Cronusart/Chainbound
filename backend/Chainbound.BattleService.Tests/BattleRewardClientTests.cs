using System.Net;
using System.Text;
using Chainbound.BattleService;
using Chainbound.Combat.Core;
using Xunit;

namespace Chainbound.BattleService.Tests;

public sealed class BattleRewardClientTests
{
    [Fact]
    public async Task Settle_sends_service_credentials_and_parses_authoritative_receipt()
    {
        Guid battleId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"success":true,"status":"settled","battleId":"11111111-1111-4111-8111-111111111111","settlementId":"22222222-2222-4222-8222-222222222222","terminalOutcome":"victory","requestedRam":15,"appliedRam":5,"reason":"granted","rewardTier":"elite","rewardConfigVersion":3,"rewardConfigHash":"hash","dailyEarned":100,"dailyCap":100,"balanceBefore":37,"balanceAfter":42,"balanceVersion":101,"receiptSequence":7,"settledAt":"2026-10-09T12:00:00.000Z","alreadyApplied":false}""",
                Encoding.UTF8,
                "application/json"),
        });
        var client = new BattleRewardClient(new HttpClient(handler), Options());

        BattleRewardSettlement result = await client.SettleAsync(battleId, CancellationToken.None);

        Assert.Equal(BattleRewardStatus.Settled, result.Status);
        Assert.Equal(5, result.AppliedRam);
        Assert.Equal(42, result.BalanceAfter);
        Assert.Equal(" ", handler.Authorization);
        Assert.Equal("service-role", handler.ApiKey);
        Assert.Equal("private-secret", handler.PrivateSecret);
        Assert.Contains(battleId.ToString("D"), handler.Body);
    }

    [Fact]
    public async Task Settle_rejects_receipt_with_invalid_balance_equation()
    {
        Guid battleId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"success":true,"status":"settled","battleId":"11111111-1111-4111-8111-111111111111","settlementId":"22222222-2222-4222-8222-222222222222","terminalOutcome":"victory","requestedRam":5,"appliedRam":5,"reason":"granted","rewardTier":"standard","rewardConfigVersion":3,"rewardConfigHash":"hash","balanceBefore":10,"balanceAfter":20,"receiptSequence":8,"settledAt":"2026-10-09T12:00:00.000Z"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var client = new BattleRewardClient(new HttpClient(handler), Options());

        AuthorityException error = await Assert.ThrowsAsync<AuthorityException>(() =>
            client.SettleAsync(battleId, CancellationToken.None));

        Assert.Equal("reward_settlement_invalid", error.Code);
    }

    [Fact]
    public async Task Settle_mode_off_does_not_call_the_reward_coordinator()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true}"),
        });
        var client = new BattleRewardClient(new HttpClient(handler), new ServiceOptions
        {
            SupabaseUrl = "https://example.supabase.co",
            SupabaseAnonKey = "anon",
            SupabaseServiceRoleKey = "service-role",
            BattleRewardMode = "off",
            BattleRewardTimeout = TimeSpan.FromSeconds(2),
        });

        BattleRewardSettlement result = await client.SettleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(BattleRewardStatus.Unavailable, result.Status);
        Assert.Equal("reward_coordinator_unavailable", result.Reason);
        Assert.Equal("", handler.Body);
    }

    [Fact]
    public async Task Settle_fails_closed_when_coordinator_rejects_request()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{\"success\":false,\"error\":\"temporary\"}"),
        });
        var client = new BattleRewardClient(new HttpClient(handler), Options());

        AuthorityException error = await Assert.ThrowsAsync<AuthorityException>(() =>
            client.SettleAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("reward_settlement_failed", error.Code);
    }

    [Fact]
    public async Task Settle_rejects_receipt_for_a_different_battle()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"success":true,"status":"zero","battleId":"33333333-3333-4333-8333-333333333333","settlementId":"22222222-2222-4222-8222-222222222222","requestedRam":5,"appliedRam":0,"reason":"daily_cap","rewardTier":"standard","rewardConfigVersion":3,"rewardConfigHash":"hash","dailyEarned":100,"dailyCap":100,"balanceAfter":42,"alreadyApplied":true}""",
                Encoding.UTF8,
                "application/json"),
        });
        var client = new BattleRewardClient(new HttpClient(handler), Options());

        AuthorityException error = await Assert.ThrowsAsync<AuthorityException>(() =>
            client.SettleAsync(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                CancellationToken.None));

        Assert.Equal("reward_settlement_invalid", error.Code);
    }

    private static ServiceOptions Options() => new()
    {
        SupabaseUrl = "https://example.supabase.co",
        SupabaseAnonKey = "anon",
        SupabaseServiceRoleKey = "service-role",
        BattleRewardServiceSecret = "private-secret",
        BattleRewardSettleUrl = "https://example.supabase.co/functions/v1/battle_reward_settle",
        BattleRewardMode = "grant",
        BattleRewardTimeout = TimeSpan.FromSeconds(2),
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage response;

        public RecordingHandler(HttpResponseMessage response)
        {
            this.response = response;
        }

        public string Authorization { get; private set; } = "";
        public string ApiKey { get; private set; } = "";
        public string PrivateSecret { get; private set; } = "";
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = $"{request.Headers.Authorization?.Scheme} {request.Headers.Authorization?.Parameter}";
            ApiKey = request.Headers.TryGetValues("apikey", out IEnumerable<string>? apiKeys)
                ? apiKeys.Single()
                : "";
            PrivateSecret = request.Headers.TryGetValues("x-battle-reward-secret", out IEnumerable<string>? secrets)
                ? secrets.Single()
                : "";
            Body = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
