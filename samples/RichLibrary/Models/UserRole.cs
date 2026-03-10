namespace RichLibrary.Models;

/// <summary>
/// Defines the roles a user can have.
/// </summary>
public enum UserRole
{
    /// <summary>Read-only access.</summary>
    Viewer = 0,

    /// <summary>Can edit content.</summary>
    Editor = 1,

    /// <summary>Full administrative access.</summary>
    Admin = 2,

    /// <summary>Elevated admin with system-level privileges.</summary>
    SuperAdmin = 3
}
