using RichLibrary.Models;
using System.Collections.Concurrent;

namespace RichLibrary.Services;

/// <summary>
/// In-memory user service (V2 — no longer implements IRepository, changed signatures).
/// </summary>
public sealed class UserService
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _nextId;

    /// <summary>Gets a user by identifier, or <c>null</c> if not found.</summary>
    public User? GetById(int id) => _users.GetValueOrDefault(id);

    /// <summary>Gets all users as a read-only list.</summary>
    public IReadOnlyList<User> GetAll() => [.. _users.Values];

    /// <summary>Adds a user and returns the assigned identifier.</summary>
    public int Add(User entity)
    {
        var id = Interlocked.Increment(ref _nextId);
        var user = entity with { Id = id };
        _users.TryAdd(id, user);
        return id;
    }

    /// <summary>Updates an existing user.</summary>
    public void Update(User entity) => _users[entity.Id] = entity;

    /// <summary>Deletes the user with the specified identifier.</summary>
    public bool Delete(int id) => _users.TryRemove(id, out _);

    /// <summary>Finds all users with the specified role.</summary>
    public IEnumerable<User> FindByRole(UserRole role) =>
        _users.Values.Where(u => u.Role == role);

    /// <summary>Finds a user by email address (case-insensitive).</summary>
    public User? FindByEmail(string email) =>
        _users.Values.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds all users that have the specified tag.</summary>
    public IEnumerable<User> FindByTag(string tag) =>
        _users.Values.Where(u => u.Tags.Contains(tag));

    /// <summary>Tries to find a user by ID, returning null on error.</summary>
    public User? TryFindById(int id)
    {
        try { return GetById(id); }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>Returns a summary of the user store.</summary>
    public string SummarizeUsers()
    {
        int count = _users.Count;
        int active = _users.Values.Count(u => u.Role != UserRole.Viewer);
        return $"Total users: {count}, Active: {active}";
    }
}
