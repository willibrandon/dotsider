namespace RichLibrary.Models;

/// <summary>
/// New type in V2 — audit log entry.
/// </summary>
public sealed record AuditLog(
    int Id,
    string Action,
    int UserId,
    DateTime Timestamp,
    string? Details = null);
