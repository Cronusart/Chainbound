using System.Net;
using Chainbound.BattleService;
using Chainbound.Combat.Core;
using Xunit;

namespace Chainbound.BattleService.Tests;

public sealed class SupabaseAuthorityGatewayTests
{
    [Fact]
    public async Task Narrative_reward_history_is_not_read_when_rewards_are_off()
    {
        var handler = new RecordingHandler();
        var gateway = new SupabaseAuthorityGateway(
            new HttpClient(handler),
            new ServiceOptions
            {
                SupabaseUrl = "https://example.supabase.co",
                SupabaseServiceRoleKey = "service-role",
                BattleRewardMode = "off",
            });

        bool eligible = await gateway.IsNarrativeRewardEligibleAsync(
            Guid.NewGuid(),
            new ManifestEncounter
            {
                EncounterId = "intro_first_creature",
                BattleKind = AuthoritativeBattleKind.Scripted,
                RewardEligibilityQuestId = "chainbound_intro",
            },
            CancellationToken.None);

        Assert.False(eligible);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task Narrative_reward_history_is_checked_when_rewards_are_enabled()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"quest_id\":\"chainbound_intro\"}]"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]"),
            });
        var gateway = new SupabaseAuthorityGateway(
            new HttpClient(handler),
            new ServiceOptions
            {
                SupabaseUrl = "https://example.supabase.co",
                SupabaseServiceRoleKey = "service-role",
                BattleRewardMode = "grant",
            });
        Guid profileId = Guid.Parse("11111111-1111-4111-8111-111111111111");

        bool eligible = await gateway.IsNarrativeRewardEligibleAsync(
            profileId,
            new ManifestEncounter
            {
                EncounterId = "intro_first_creature",
                BattleKind = AuthoritativeBattleKind.Scripted,
                RewardEligibilityQuestId = "chainbound_intro",
            },
            CancellationToken.None);

        Assert.True(eligible);
        Assert.Equal(2, handler.RequestedUrls.Count);
        Assert.Contains("/rest/v1/profile_quest_states?", handler.RequestedUrls[0]);
        Assert.Contains("/rest/v1/battle_reward_settlements?", handler.RequestedUrls[1]);
    }

    [Fact]
    public async Task Begin_uses_the_live_function_arguments_when_rewards_are_off()
    {
        string body = await CaptureBeginBody("off");

        Assert.Contains("\"p_battle_id\"", body);
        Assert.Contains("\"p_state_json\"", body);
        Assert.DoesNotContain("p_encounter_id", body);
        Assert.DoesNotContain("p_reward_tier", body);
        Assert.DoesNotContain("p_reward_config_version", body);
        Assert.DoesNotContain("p_reward_config_hash", body);
    }

    [Fact]
    public async Task Begin_sends_reward_arguments_when_rewards_are_enabled()
    {
        string body = await CaptureBeginBody("grant");

        Assert.Contains("\"p_encounter_id\"", body);
        Assert.Contains("\"p_reward_tier\"", body);
        Assert.Contains("\"p_reward_config_version\"", body);
        Assert.Contains("\"p_reward_config_hash\"", body);
    }

    [Fact]
    public async Task Settle_and_commit_omit_unapplied_terminal_arguments_when_rewards_are_off()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") });
        var gateway = Gateway(handler, "off");
        var session = SampleSession();

        await gateway.SettleBattleAsync(
            session, 1, 0, 5000, "Abandoned", true, "abandon", "Abandoned", CancellationToken.None);
        await gateway.CommitActionStepAsync(
            session,
            new TeamCommandApiRequest { CommandId = "cmd" },
            session.State,
            "active",
            1,
            1,
            "victory",
            "Victory",
            CancellationToken.None);

        Assert.Equal(2, handler.Bodies.Count);
        Assert.All(handler.Bodies, body =>
        {
            Assert.DoesNotContain("p_terminal_outcome", body);
            Assert.DoesNotContain("p_terminal_reason", body);
        });
    }

    [Fact]
    public async Task Party_vitals_treat_null_numeric_columns_as_unpersisted_values()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"id\":\"starter\",\"current_hp\":null,\"party_slot\":null}]"),
        });
        var gateway = Gateway(handler, "off");

        List<CombatVitalSnapshot> vitals = await gateway.GetPartyVitalsAsync(Guid.NewGuid(), CancellationToken.None);

        CombatVitalSnapshot starter = Assert.Single(vitals);
        Assert.Equal("starter", starter.EntityId);
        Assert.Equal(0, starter.RosterIndex);
        Assert.Equal(-1, starter.CurrentHp);
        Assert.True(starter.IsActive);
    }

    [Fact]
    public async Task Loadout_treats_null_numeric_effects_as_zero()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"gear_effects\":{\"scan\":null,\"stability_bp\":null},\"boost_effects\":{},\"active_boosts\":[{\"item_id\":\"test\",\"expires_at_unix_milliseconds\":null,\"effects\":{}}],\"authority_unix_milliseconds\":null}"),
        });
        var gateway = Gateway(handler, "off");

        AuthorityLoadoutSnapshot loadout = await gateway.GetLoadoutAsync(Guid.NewGuid(), 1, CancellationToken.None);

        Assert.Equal(0, loadout.GearEffects.ScanRating);
        Assert.Equal(0, loadout.GearEffects.StabilityBasisPoints);
        ActiveCaptureBoost boost = Assert.Single(loadout.ActiveBoosts);
        Assert.Equal(0, boost.ExpiresAtUnixMilliseconds);
        Assert.Equal(0, loadout.AuthorityUnixMilliseconds);
    }

    private static async Task<string> CaptureBeginBody(string rewardMode)
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}"),
        });
        var gateway = Gateway(handler, rewardMode);
        await gateway.BeginBattleAsync(SampleSession(), CancellationToken.None);
        return handler.Bodies[0];
    }

    private static SupabaseAuthorityGateway Gateway(RecordingHandler handler, string rewardMode)
    {
        return new SupabaseAuthorityGateway(
            new HttpClient(handler),
            new ServiceOptions
            {
                SupabaseUrl = "https://example.supabase.co",
                SupabaseServiceRoleKey = "service-role",
                BattleRewardMode = rewardMode,
            });
    }

    private static BattleSessionRecord SampleSession()
    {
        return new BattleSessionRecord
        {
            BattleId = Guid.Parse("22222222-2222-4222-8222-222222222222"),
            ProfileId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            StartCommandId = "cmd",
            Seed = 7,
            ManifestHash = "abc",
            ConfigHash = "def",
            EncounterId = "intro_first_creature",
            RewardTier = BattleRewardTier.Elite,
            RewardConfigVersion = 3,
            RewardConfigHash = new string('a', 64),
            ExpiresAtUnixMilliseconds = 1,
            State = new AuthoritativeBattleState { BattleKind = AuthoritativeBattleKind.Scripted },
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<string> RequestedUrls { get; } = new();
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri?.ToString() ?? "");
            Bodies.Add(request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken));
            if (responses.Count == 0)
                throw new InvalidOperationException("Rewards-off path made an unexpected HTTP request.");
            return responses.Dequeue();
        }
    }
}
