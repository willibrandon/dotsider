using System.ComponentModel.DataAnnotations;

namespace RichLibrary.Models;

/// <summary>
/// Represents an application user (V2 — added PhoneNumber, LastLoginAt).
/// </summary>
public sealed record User
{
    [Required]
    public int Id { get; init; }

    [Required]
    [StringLength(100)]
    public required string Name { get; init; }

    [EmailAddress]
    public string? Email { get; init; }

    [Phone]
    public string? PhoneNumber { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; init; }

    public UserRole Role { get; init; } = UserRole.Viewer;

    public IReadOnlyList<string> Tags { get; init; } = [];
}
