using Chainbound.BattleService;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Reflection;
using Xunit;

namespace Chainbound.BattleService.Tests;

public sealed class RequestRateLimiterTests
{
    [Fact]
    public void Enforces_profile_ip_and_auth_failure_limits()
    {
        var limiter = new RequestRateLimiter();
        Guid profileId = Guid.NewGuid();

        for (int i = 0; i < 30; i++) Assert.True(limiter.TryAcquireProfile(profileId, out _));
        Assert.False(limiter.TryAcquireProfile(profileId, out int profileRetry));
        Assert.InRange(profileRetry, 1, 60);

        for (int i = 0; i < 120; i++) Assert.True(limiter.TryAcquireIp("127.0.0.1", out _));
        Assert.False(limiter.TryAcquireIp("127.0.0.1", out _));

        for (int i = 0; i < 10; i++) limiter.RegisterAuthFailure("127.0.0.2");
        Assert.True(limiter.IsAuthFailureBlocked("127.0.0.2", out _));
    }

    [Fact]
    public void Resolves_rightmost_forwarded_hop_only_behind_a_trusted_frontend()
    {
        var cloudRun = new DefaultHttpContext();
        cloudRun.Connection.RemoteIpAddress = IPAddress.Parse("169.254.1.1");
        cloudRun.Request.Headers["X-Forwarded-For"] = "203.0.113.9, 198.51.100.2";
        Assert.Equal("198.51.100.2", ClientAddressResolver.Resolve(cloudRun));

        var direct = new DefaultHttpContext();
        direct.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");
        direct.Request.Headers["X-Forwarded-For"] = "203.0.113.9";
        Assert.Equal("198.51.100.7", ClientAddressResolver.Resolve(direct));
    }

    [Fact]
    public void Rotating_spoofed_forwarded_entries_share_the_trusted_frontend_bucket()
    {
        var limiter = new RequestRateLimiter();
        for (int i = 0; i < 120; i++)
        {
            var request = new DefaultHttpContext();
            request.Connection.RemoteIpAddress = IPAddress.Parse("169.254.1.1");
            request.Request.Headers["X-Forwarded-For"] = $"203.0.113.{i}, 198.51.100.44";
            Assert.True(limiter.TryAcquireIp(ClientAddressResolver.Resolve(request), out _));
        }

        var blocked = new DefaultHttpContext();
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("169.254.1.1");
        blocked.Request.Headers["X-Forwarded-For"] = "192.0.2.99, 198.51.100.44";
        Assert.False(limiter.TryAcquireIp(ClientAddressResolver.Resolve(blocked), out _));
    }

    [Fact]
    public void Junk_authorization_does_not_change_the_ip_bucket()
    {
        var limiter = new RequestRateLimiter();
        var request = new DefaultHttpContext();
        request.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.77");
        request.Request.Headers.Authorization = "Bearer definitely-not-a-jwt";

        for (int i = 0; i < 120; i++)
            Assert.True(limiter.TryAcquireIp(ClientAddressResolver.Resolve(request), out _));

        Assert.False(limiter.TryAcquireIp(ClientAddressResolver.Resolve(request), out _));
    }

    [Fact]
    public void Tracked_key_cap_evicts_oldest_entries()
    {
        var limiter = new RequestRateLimiter(maxTrackedKeys: 32);
        for (int i = 0; i < 100; i++)
            Assert.True(limiter.TryAcquireIp($"192.0.2.{i}", out _));

        FieldInfo windowsField = typeof(RequestRateLimiter).GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object windows = windowsField.GetValue(limiter)!;
        int count = (int)windows.GetType().GetProperty("Count")!.GetValue(windows)!;
        Assert.InRange(count, 1, 32);
    }

    [Fact]
    public void Evicts_stale_windows_during_bounded_periodic_cleanup()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.FromUnixTimeSeconds(0));
        var limiter = new RequestRateLimiter(clock);
        for (int i = 0; i < 300; i++)
            Assert.True(limiter.TryAcquireIp($"192.0.2.{i}", out _));

        clock.Advance(TimeSpan.FromMinutes(2));
        for (int i = 0; i < 212; i++)
            Assert.True(limiter.TryAcquireIp($"198.51.100.{i}", out _));

        FieldInfo windowsField = typeof(RequestRateLimiter).GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object windows = windowsField.GetValue(limiter)!;
        int count = (int)windows.GetType().GetProperty("Count")!.GetValue(windows)!;
        Assert.InRange(count, 1, 256);
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
