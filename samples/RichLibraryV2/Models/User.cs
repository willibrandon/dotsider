using System.ComponentModel.DataAnnotations;

namespace RichLibrary.Models;

/// <summary>
/// Represents an application user (V2 — added PhoneNumber, LastLoginAt).
/// </summary>
public sealed record User
{
    /// <summary>Gets the unique identifier.</summary>
    [Required]
    public int Id { get; init; }

    /// <summary>Gets the display name.</summary>
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }

    /// <summary>Gets the email address.</summary>
    [EmailAddress]
    public string? Email { get; init; }

    /// <summary>Gets the phone number.</summary>
    [Phone]
    public string? PhoneNumber { get; init; }

    /// <summary>Gets the UTC timestamp when the user was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Gets the UTC timestamp of the user's last login.</summary>
    public DateTime? LastLoginAt { get; init; }

    /// <summary>Gets the user's assigned role.</summary>
    public UserRole Role { get; init; } = UserRole.Viewer;

    /// <summary>Gets the collection of tags associated with the user.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
