namespace RichLibrary.Models;

/// <summary>
/// Defines the roles a user can have (V2 — added Moderator).
/// </summary>
public enum UserRole
{
    /// <summary>Read-only access.</summary>
    Viewer = 0,

    /// <summary>Can edit content.</summary>
    Editor = 1,

    /// <summary>Can moderate user-generated content.</summary>
    Moderator = 2,

    /// <summary>Full administrative access.</summary>
    Admin = 3,

    /// <summary>Elevated admin with system-level privileges.</summary>
    SuperAdmin = 4
}
