using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dotsider.Website.Tests;

public class DemoGuardTests
{
    private static readonly IPAddress TestIp = IPAddress.Parse("203.0.113.1");
    private static readonly IPAddress TestIp2 = IPAddress.Parse("203.0.113.2");

    private static (DemoGuard guard, FakeTimeProvider time) CreateGuard(Action<DemoGuardOptions>? configure = null)
    {
        var options = new DemoGuardOptions();
        configure?.Invoke(options);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var guard = new DemoGuard(NullLogger<DemoGuard>.Instance, options, time);
        return (guard, time);
    }

    // ── Basic allow/deny ─────────────────────────────────────────

    [Fact]
    public void FirstConnection_IsAllowed()
    {
        var (guard, _) = CreateGuard();
        Assert.Null(guard.TryAllow(TestIp, "Mozilla/5.0"));
    }

    [Fact]
    public void MultipleIps_AreIndependent()
    {
        var (guard, _) = CreateGuard(o => o.MaxConnectionsPerIpPerWindow = 2);

        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));

        // Second IP is unaffected
        Assert.Null(guard.TryAllow(TestIp2, null));
    }

    // ── Per-IP rate limiting ─────────────────────────────────────

    [Fact]
    public void ExceedingRateLimit_BansIp()
    {
        var (guard, _) = CreateGuard(o =>
        {
            o.MaxConnectionsPerIpPerWindow = 3;
            o.RateWindow = TimeSpan.FromMinutes(1);
        });

        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));
    }

    [Fact]
    public void BannedIp_IsRejected()
    {
        var (guard, _) = CreateGuard(o =>
        {
            o.MaxConnectionsPerIpPerWindow = 1;
            o.BanDuration = TimeSpan.FromMinutes(15);
        });

        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));

        // Subsequent attempts are rejected as "banned"
        Assert.Equal("banned", guard.TryAllow(TestIp, null));
        Assert.Equal("banned", guard.TryAllow(TestIp, null));
    }

    [Fact]
    public void Ban_ExpiresAfterDuration()
    {
        var (guard, time) = CreateGuard(o =>
        {
            o.MaxConnectionsPerIpPerWindow = 1;
            o.BanDuration = TimeSpan.FromMinutes(15);
            o.RateWindow = TimeSpan.FromMinutes(1);
        });

        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));
        Assert.Equal("banned", guard.TryAllow(TestIp, null));

        // Advance past both ban (15min) and rate window (1min)
        time.Advance(TimeSpan.FromMinutes(16));

        // Ban expired, rate window expired — allowed again
        Assert.Null(guard.TryAllow(TestIp, null));
    }

    // ── Max concurrent sessions ──────────────────────────────────

    [Fact]
    public void ExceedingConcurrentSessions_IsBlocked()
    {
        var (guard, _) = CreateGuard(o => o.MaxConcurrentPerIp = 2);

        Assert.Null(guard.TryAllow(TestIp, null));
        guard.SessionStarted(TestIp, "s1", null);

        Assert.Null(guard.TryAllow(TestIp, null));
        guard.SessionStarted(TestIp, "s2", null);

        // Third concurrent session is blocked
        Assert.Equal("max-concurrent", guard.TryAllow(TestIp, null));
    }

    [Fact]
    public void EndingSession_AllowsNewConnection()
    {
        var (guard, _) = CreateGuard(o => o.MaxConcurrentPerIp = 1);

        Assert.Null(guard.TryAllow(TestIp, null));
        guard.SessionStarted(TestIp, "s1", null);

        Assert.Equal("max-concurrent", guard.TryAllow(TestIp, null));

        // End the session
        guard.SessionEnded(TestIp, "s1", TimeSpan.FromSeconds(30));
        guard.ReleaseSlot(TestIp);

        // Now a new connection is allowed
        Assert.Null(guard.TryAllow(TestIp, null));
    }

    // ── Rapid disconnect detection ───────────────────────────────

    [Fact]
    public void RapidDisconnects_BanIp()
    {
        var (guard, _) = CreateGuard(o =>
        {
            o.MaxRapidDisconnects = 3;
            o.SuspiciousSessionDuration = TimeSpan.FromSeconds(2);
            o.MaxConnectionsPerIpPerWindow = 100; // don't trigger rate limit
        });

        for (var i = 0; i < 3; i++)
        {
            Assert.Null(guard.TryAllow(TestIp, null));
            guard.SessionStarted(TestIp, $"s{i}", null);
            guard.SessionEnded(TestIp, $"s{i}", TimeSpan.FromMilliseconds(500)); // < 2s
            guard.ReleaseSlot(TestIp);
        }

        // Should be banned now
        Assert.Equal("banned", guard.TryAllow(TestIp, null));
    }

    [Fact]
    public void NormalSessionDuration_ResetsRapidDisconnectCounter()
    {
        var (guard, _) = CreateGuard(o =>
        {
            o.MaxRapidDisconnects = 3;
            o.SuspiciousSessionDuration = TimeSpan.FromSeconds(2);
            o.MaxConnectionsPerIpPerWindow = 100;
        });

        // Two rapid disconnects
        for (var i = 0; i < 2; i++)
        {
            Assert.Null(guard.TryAllow(TestIp, null));
            guard.SessionStarted(TestIp, $"s{i}", null);
            guard.SessionEnded(TestIp, $"s{i}", TimeSpan.FromMilliseconds(500));
            guard.ReleaseSlot(TestIp);
        }

        // One normal session resets the counter
        Assert.Null(guard.TryAllow(TestIp, null));
        guard.SessionStarted(TestIp, "normal", null);
        guard.SessionEnded(TestIp, "normal", TimeSpan.FromSeconds(30));
        guard.ReleaseSlot(TestIp);

        // Two more rapid disconnects — should NOT be banned (counter was reset)
        for (var i = 0; i < 2; i++)
        {
            Assert.Null(guard.TryAllow(TestIp, null));
            guard.SessionStarted(TestIp, $"r{i}", null);
            guard.SessionEnded(TestIp, $"r{i}", TimeSpan.FromMilliseconds(500));
            guard.ReleaseSlot(TestIp);
        }

        Assert.Null(guard.TryAllow(TestIp, null)); // still allowed
    }

    // ── Circuit breaker ──────────────────────────────────────────

    [Fact]
    public void CircuitBreaker_TripsOnGlobalFlood()
    {
        var (guard, _) = CreateGuard(o =>
        {
            o.CircuitThreshold = 5;
            o.CircuitWindow = TimeSpan.FromMinutes(1);
            o.CircuitCooldown = TimeSpan.FromMinutes(5);
            o.MaxConnectionsPerIpPerWindow = 100;
        });

        Assert.False(guard.IsCircuitOpen);

        // 5 connections from different IPs trips the circuit
        for (var i = 0; i < 5; i++)
        {
            var ip = IPAddress.Parse($"10.0.0.{i + 1}");
            guard.TryAllow(ip, null);
        }

        Assert.True(guard.IsCircuitOpen);
    }

    [Fact]
    public void CircuitOpen_BlocksAllConnections()
    {
        var (guard, _) = CreateGuard(o =>
        {
            o.CircuitThreshold = 2;
            o.CircuitCooldown = TimeSpan.FromMinutes(5);
            o.MaxConnectionsPerIpPerWindow = 100;
        });

        // Trip the circuit
        guard.TryAllow(IPAddress.Parse("10.0.0.1"), null);
        guard.TryAllow(IPAddress.Parse("10.0.0.2"), null);

        Assert.True(guard.IsCircuitOpen);

        // Any IP, even a new one, is blocked
        Assert.Equal("circuit-open", guard.TryAllow(IPAddress.Parse("10.0.0.99"), null));
    }

    [Fact]
    public void CircuitBreaker_ResetsAfterCooldown()
    {
        var (guard, time) = CreateGuard(o =>
        {
            o.CircuitThreshold = 2;
            o.CircuitCooldown = TimeSpan.FromMinutes(5);
            o.CircuitWindow = TimeSpan.FromMinutes(1);
            o.MaxConnectionsPerIpPerWindow = 100;
        });

        guard.TryAllow(IPAddress.Parse("10.0.0.1"), null);
        guard.TryAllow(IPAddress.Parse("10.0.0.2"), null);

        Assert.True(guard.IsCircuitOpen);
        Assert.Equal("circuit-open", guard.TryAllow(IPAddress.Parse("10.0.0.3"), null));

        // Advance past cooldown
        time.Advance(TimeSpan.FromMinutes(6));

        // Circuit should reset on next call
        Assert.Null(guard.TryAllow(IPAddress.Parse("10.0.0.4"), null));
        Assert.False(guard.IsCircuitOpen);
    }

    // ── Escalating bans ──────────────────────────────────────────

    [Fact]
    public void RepeatedViolations_EscalateBanDuration()
    {
        var (guard, time) = CreateGuard(o =>
        {
            o.MaxConnectionsPerIpPerWindow = 1;
            o.RateWindow = TimeSpan.FromMinutes(1);
            o.BanDuration = TimeSpan.FromMinutes(15);
            o.MaxBanDuration = TimeSpan.FromHours(24);
            o.MaxConcurrentPerIp = 100; // don't trigger concurrent limit
        });

        // First offense — 15 min ban
        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));
        Assert.Equal("banned", guard.TryAllow(TestIp, null));

        // Wait out the first ban
        time.Advance(TimeSpan.FromMinutes(16));
        Assert.Null(guard.TryAllow(TestIp, null));

        // Second offense — 30 min ban (doubled)
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));

        // 16 min is not enough now
        time.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal("banned", guard.TryAllow(TestIp, null));

        // 31 min total is enough
        time.Advance(TimeSpan.FromMinutes(15));
        Assert.Null(guard.TryAllow(TestIp, null));
    }

    [Fact]
    public void BanDuration_NeverExceeds24Hours()
    {
        var (guard, time) = CreateGuard(o =>
        {
            o.MaxConnectionsPerIpPerWindow = 1;
            o.RateWindow = TimeSpan.FromMinutes(1);
            o.BanDuration = TimeSpan.FromHours(12);
            o.MaxBanDuration = TimeSpan.FromHours(24);
            o.MaxConcurrentPerIp = 100; // don't trigger concurrent limit
        });

        // First offense: 12h ban
        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));
        time.Advance(TimeSpan.FromHours(13));

        // Second offense: would be 24h (12h * 2), exactly at cap
        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));
        time.Advance(TimeSpan.FromHours(25));

        // Third offense: would be 48h (12h * 4), but capped at 24h
        Assert.Null(guard.TryAllow(TestIp, null));
        Assert.Equal("rate-limit", guard.TryAllow(TestIp, null));

        // After 24h, should be allowed
        time.Advance(TimeSpan.FromHours(25));
        Assert.Null(guard.TryAllow(TestIp, null));
    }

    // ── Health endpoint integration ──────────────────────────────

    [Fact]
    public void IsCircuitOpen_ReflectsState()
    {
        var (guard, _) = CreateGuard(o =>
        {
            o.CircuitThreshold = 1;
            o.CircuitCooldown = TimeSpan.FromHours(1);
            o.MaxConnectionsPerIpPerWindow = 100;
        });

        Assert.False(guard.IsCircuitOpen);

        guard.TryAllow(TestIp, null);

        Assert.True(guard.IsCircuitOpen);
    }

    // ── Session tracking accuracy ────────────────────────────────

    [Fact]
    public void ReleaseSlot_NeverGoesNegative()
    {
        var (guard, _) = CreateGuard();

        Assert.Null(guard.TryAllow(TestIp, null));
        guard.SessionStarted(TestIp, "s1", null);

        // Release the slot twice (e.g. race condition)
        guard.ReleaseSlot(TestIp);
        guard.ReleaseSlot(TestIp);

        // Should still allow connections — active sessions can't go below 0
        Assert.Null(guard.TryAllow(TestIp, null));
    }

    [Fact]
    public void ReleaseSlot_ForUnknownIp_DoesNotThrow()
    {
        var (guard, _) = CreateGuard();
        var unknownIp = IPAddress.Parse("192.168.1.1");

        // Should not throw — just a no-op for unknown IP
        guard.ReleaseSlot(unknownIp);
    }

    [Fact]
    public void SessionEnded_ForUnknownIp_DoesNotThrow()
    {
        var (guard, _) = CreateGuard();
        var unknownIp = IPAddress.Parse("192.168.1.1");

        // Should not throw — just a no-op for the IP record
        guard.SessionEnded(unknownIp, "ghost", TimeSpan.FromSeconds(5));
    }

    // ── Post-TryAllow failure rollback ─────────────────────────

    [Fact]
    public void ReleaseSlot_WithoutSession_FreesSlot()
    {
        var (guard, _) = CreateGuard(o => o.MaxConcurrentPerIp = 1);

        // TryAllow succeeds (reserves slot), but session never starts
        // (simulates 503 global cap or AcceptWebSocketAsync fault)
        Assert.Null(guard.TryAllow(TestIp, null));

        // Slot is occupied — second attempt blocked
        Assert.Equal("max-concurrent", guard.TryAllow(TestIp, null));

        // Release without SessionEnded (no real session happened)
        guard.ReleaseSlot(TestIp);

        // Slot is free again
        Assert.Null(guard.TryAllow(TestIp, null));
    }

    [Fact]
    public void ReleaseSlot_WithoutSession_DoesNotTriggerRapidDisconnect()
    {
        var (guard, _) = CreateGuard(o =>
        {
            o.MaxRapidDisconnects = 3;
            o.SuspiciousSessionDuration = TimeSpan.FromSeconds(2);
            o.MaxConnectionsPerIpPerWindow = 100;
            o.MaxConcurrentPerIp = 100;
        });

        // Simulate 5 failed post-TryAllow paths (503s) — only ReleaseSlot, no SessionEnded
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(guard.TryAllow(TestIp, null));
            guard.ReleaseSlot(TestIp);
        }

        // Should NOT be banned — ReleaseSlot alone doesn't count as rapid disconnect
        Assert.Null(guard.TryAllow(TestIp, null));
    }
}
