using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace Chainbound.BattleService;

public sealed class RequestRateLimiter
{
    public const int DefaultMaxTrackedKeys = 4096;

    private readonly ConcurrentDictionary<string, WindowCounter> _windows = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly int _maxTrackedKeys;
    private long _operationCount;
    private long _accessSequence;
    private readonly object _trimGate = new();

    public RequestRateLimiter(TimeProvider? timeProvider = null, int maxTrackedKeys = DefaultMaxTrackedKeys)
    {
        if (maxTrackedKeys < 1) throw new ArgumentOutOfRangeException(nameof(maxTrackedKeys));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxTrackedKeys = maxTrackedKeys;
    }

    public bool TryAcquireIp(string ip, out int retryAfterSeconds) =>
        TryAcquire($"ip:{ip}", 120, out retryAfterSeconds);

    public bool TryAcquireProfile(Guid profileId, out int retryAfterSeconds) =>
        TryAcquire($"profile:{profileId:D}", 30, out retryAfterSeconds);

    public bool IsAuthFailureBlocked(string ip, out int retryAfterSeconds) =>
        IsAtLimit($"auth:{ip}", 10, out retryAfterSeconds);

    public void RegisterAuthFailure(string ip) => TryAcquire($"auth:{ip}", 10, out _);

    private bool TryAcquire(string key, int limit, out int retryAfterSeconds)
    {
        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        long window = now / 60;
        CleanupStaleWindows(window);
        while (true)
        {
            WindowCounter counter = _windows.GetOrAdd(
                key,
                _ => new WindowCounter(window, Interlocked.Increment(ref _accessSequence)));
            lock (counter)
            {
                if (!_windows.TryGetValue(key, out WindowCounter? current) || !ReferenceEquals(current, counter))
                    continue;
                if (counter.Window != window)
                {
                    counter.Window = window;
                    counter.Count = 0;
                }
                counter.LastAccessSequence = Interlocked.Increment(ref _accessSequence);
                EnforceKeyCap(counter);
                retryAfterSeconds = (int)(60 - now % 60);
                if (counter.Count >= limit) return false;
                counter.Count++;
                return true;
            }
        }
    }

    private bool IsAtLimit(string key, int limit, out int retryAfterSeconds)
    {
        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        long window = now / 60;
        CleanupStaleWindows(window);
        retryAfterSeconds = (int)(60 - now % 60);
        if (!_windows.TryGetValue(key, out WindowCounter? counter)) return false;
        lock (counter)
            return counter.Window == window && counter.Count >= limit;
    }

    private void CleanupStaleWindows(long currentWindow)
    {
        if ((Interlocked.Increment(ref _operationCount) & 0xff) != 0)
            return;
        long oldestRetainedWindow = currentWindow - 1;
        foreach ((string key, WindowCounter counter) in _windows)
        {
            lock (counter)
            {
                if (counter.Window < oldestRetainedWindow)
                    _windows.TryRemove(key, out _);
            }
        }
    }

    private void EnforceKeyCap(WindowCounter protectedCounter)
    {
        if (_windows.Count <= _maxTrackedKeys)
            return;

        lock (_trimGate)
        {
            int excess = _windows.Count - _maxTrackedKeys;
            if (excess <= 0) return;

            foreach (KeyValuePair<string, WindowCounter> candidate in _windows
                         .Where(pair => !ReferenceEquals(pair.Value, protectedCounter))
                         .OrderBy(pair => Volatile.Read(ref pair.Value.LastAccessSequence))
                         .Take(excess))
            {
                _windows.TryRemove(candidate.Key, out _);
            }
        }
    }

    private sealed class WindowCounter(long window, long lastAccessSequence)
    {
        public long Window = window;
        public int Count;
        public long LastAccessSequence = lastAccessSequence;
    }
}

public static class ClientAddressResolver
{
    public static string Resolve(HttpContext context)
    {
        IPAddress? remote = context.Connection.RemoteIpAddress;
        if (remote != null && IsTrustedFrontend(remote) &&
            context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            string[] hops = forwarded.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string rightmostHop = hops.Length == 0 ? "" : hops[^1];
            if (IPAddress.TryParse(rightmostHop, out IPAddress? client))
                return Normalize(client);
        }
        return remote == null ? "unknown" : Normalize(remote);
    }

    private static string Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();

    private static bool IsTrustedFrontend(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal)
            return true;
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return (address.GetAddressBytes()[0] & 0xfe) == 0xfc;
        byte[] bytes = address.MapToIPv4().GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168) ||
               (bytes[0] == 169 && bytes[1] == 254);
    }
}
