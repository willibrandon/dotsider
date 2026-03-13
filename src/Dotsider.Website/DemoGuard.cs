using System.Collections.Concurrent;
using System.Net;

namespace Dotsider.Website;

/// <summary>
/// Protects the live demo from abuse — per-IP rate limiting, global circuit
/// breaker, auto-banning, and full audit logging.
/// </summary>
/// <param name="logger">Logger for audit and guard events.</param>
/// <param name="options">Guard configuration thresholds.</param>
/// <param name="timeProvider">Time provider for testability.</param>
internal sealed class DemoGuard(ILogger<DemoGuard> logger, DemoGuardOptions options, TimeProvider timeProvider)
{
    // Per-IP tracking
    private readonly ConcurrentDictionary<IPAddress, IpRecord> _ipRecords = new();
    private readonly ConcurrentDictionary<IPAddress, BanRecord> _banned = new();
    private readonly ConcurrentDictionary<IPAddress, int> _strikes = new();

    // Global circuit breaker
    private readonly ConcurrentQueue<DateTimeOffset> _globalConnections = new();
    private volatile bool _circuitOpen;
    private DateTimeOffset _circuitOpenedAt;

    /// <summary>
    /// Gets a value indicating whether the global circuit breaker is open (demo disabled).
    /// </summary>
    public bool IsCircuitOpen => _circuitOpen;

    /// <summary>
    /// Check whether a connection from this IP should be allowed.
    /// Returns a rejection reason, or null if allowed.
    /// </summary>
    public string? TryAllow(IPAddress ip, string? userAgent)
    {
        var now = timeProvider.GetUtcNow();

        PurgeStaleBans(now);

        // Check ban list
        if (_banned.TryGetValue(ip, out var ban))
        {
            if (now < ban.ExpiresAt)
            {
                Log.ConnectionBlocked(logger, ip.ToString(), "banned", userAgent);
                return "banned";
            }

            _banned.TryRemove(ip, out _);
        }

        // Check circuit breaker
        if (_circuitOpen)
        {
            // Auto-reset after cooldown
            if (now - _circuitOpenedAt > options.CircuitCooldown)
            {
                _circuitOpen = false;
                Log.CircuitReset(logger);
            }
            else
            {
                Log.ConnectionBlocked(logger, ip.ToString(), "circuit-open", userAgent);
                return "circuit-open";
            }
        }

        var record = _ipRecords.GetOrAdd(ip, _ => new IpRecord());

        // Lock the per-IP record so concurrent handshakes from the same IP
        // cannot observe stale ConnectionCount / ActiveSessions values.
        lock (record.Gate)
        {
            record.PurgeOlderThan(now - options.RateWindow);

            // Check per-IP rate limit (connections per window)
            if (record.ConnectionCount >= options.MaxConnectionsPerIpPerWindow)
            {
                Ban(ip, now);
                Log.IpBanned(logger, ip.ToString(), GetBanDuration(ip).TotalMinutes,
                    "rate-limit", record.ConnectionCount, options.RateWindow.TotalSeconds);
                return "rate-limit";
            }

            // Check per-IP concurrent sessions
            if (record.ActiveSessions >= options.MaxConcurrentPerIp)
            {
                Log.ConnectionBlocked(logger, ip.ToString(), "max-concurrent", userAgent);
                return "max-concurrent";
            }

            // Record this connection and increment active sessions atomically
            record.RecordConnection(now);
            record.ActiveSessions++;
        }

        // Update global circuit breaker (lock-free, approximate count is fine)
        _globalConnections.Enqueue(now);
        PurgeGlobalOlderThan(now - options.CircuitWindow);

        if (_globalConnections.Count >= options.CircuitThreshold)
        {
            _circuitOpen = true;
            _circuitOpenedAt = now;
            Log.CircuitTripped(logger, _globalConnections.Count,
                options.CircuitWindow.TotalSeconds, options.CircuitCooldown.TotalSeconds);
        }

        return null; // allowed
    }

    /// <summary>
    /// Log that a session has started for the given IP. The active session
    /// count was already incremented atomically inside <see cref="TryAllow"/>.
    /// </summary>
    public void SessionStarted(IPAddress ip, string sessionId, string? userAgent)
    {
        Log.AuditConnect(logger, sessionId, ip, userAgent);
    }

    /// <summary>
    /// Release the per-IP session slot reserved by <see cref="TryAllow"/>.
    /// Must be called on every code path after a successful TryAllow,
    /// regardless of whether the session was actually established.
    /// </summary>
    public void ReleaseSlot(IPAddress ip)
    {
        if (_ipRecords.TryGetValue(ip, out var record))
        {
            lock (record.Gate)
            {
                record.ActiveSessions = Math.Max(0, record.ActiveSessions - 1);
            }
        }
    }

    /// <summary>
    /// Track a session ending for the given IP. Handles rapid disconnect
    /// detection and audit logging. Does not release the per-IP slot —
    /// call <see cref="ReleaseSlot"/> for that.
    /// </summary>
    public void SessionEnded(IPAddress ip, string sessionId, TimeSpan duration)
    {
        if (_ipRecords.TryGetValue(ip, out var record))
        {
            lock (record.Gate)
            {
                // If they had a very short session (< 2s) repeatedly, that's suspicious
                if (duration < options.SuspiciousSessionDuration)
                {
                    record.RapidDisconnects++;
                    if (record.RapidDisconnects >= options.MaxRapidDisconnects)
                    {
                        var now = timeProvider.GetUtcNow();
                        Ban(ip, now);
                        Log.IpBanned(logger, ip.ToString(), GetBanDuration(ip).TotalMinutes,
                            "rapid-disconnect", record.RapidDisconnects, 0);
                    }
                }
                else
                {
                    // Reset rapid disconnect counter on a normal session
                    record.RapidDisconnects = 0;
                }
            }
        }

        Log.AuditDisconnect(logger, sessionId, ip, duration.TotalSeconds);
    }

    /// <summary>
    /// Escalating ban: each offense doubles the duration, capped at 24 hours.
    /// </summary>
    private void Ban(IPAddress ip, DateTimeOffset now)
    {
        var strikes = _strikes.AddOrUpdate(ip, 1, (_, s) => s + 1);

        // Escalate: base * 2^(strikes-1), capped at MaxBanDuration
        var duration = TimeSpan.FromTicks(options.BanDuration.Ticks * (1L << Math.Min(strikes - 1, 10)));
        if (duration > options.MaxBanDuration)
            duration = options.MaxBanDuration;

        _banned[ip] = new BanRecord(now + duration);
    }

    private TimeSpan GetBanDuration(IPAddress ip) =>
        _banned.TryGetValue(ip, out var ban) ? ban.ExpiresAt - timeProvider.GetUtcNow() : TimeSpan.Zero;

    private void PurgeStaleBans(DateTimeOffset now)
    {
        foreach (var (ip, ban) in _banned)
        {
            if (now >= ban.ExpiresAt)
                _banned.TryRemove(ip, out _);
        }
    }

    private void PurgeGlobalOlderThan(DateTimeOffset cutoff)
    {
        while (_globalConnections.TryPeek(out var ts) && ts < cutoff)
            _globalConnections.TryDequeue(out _);
    }

    internal sealed record BanRecord(DateTimeOffset ExpiresAt);

    internal sealed class IpRecord
    {
        /// <summary>
        /// Serializes per-IP check-then-act in TryAllow and SessionEnded.
        /// </summary>
        public readonly object Gate = new();

        private readonly ConcurrentQueue<DateTimeOffset> _connections = new();

        public int ActiveSessions;
        public int RapidDisconnects;

        public int ConnectionCount => _connections.Count;

        public void RecordConnection(DateTimeOffset now) => _connections.Enqueue(now);

        public void PurgeOlderThan(DateTimeOffset cutoff)
        {
            while (_connections.TryPeek(out var ts) && ts < cutoff)
                _connections.TryDequeue(out _);
        }
    }
}
