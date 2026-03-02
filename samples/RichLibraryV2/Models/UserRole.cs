namespace RichLibrary.Models;

/// <summary>
/// Defines the roles a user can have (V2 — added Moderator).
/// </summary>
public enum UserRole
{
    Viewer = 0,
    Editor = 1,
    Moderator = 2,
    Admin = 3,
    SuperAdmin = 4
}
