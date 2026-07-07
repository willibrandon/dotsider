using RichLibrary.Models;
using System.Collections.Concurrent;

namespace RichLibrary.Services;

/// <summary>
/// In-memory user service implementing the repository pattern.
/// </summary>
public sealed class UserService : IRepository<User>
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _nextId;

    /// <inheritdoc />
    public User? GetById(int id) => _users.GetValueOrDefault(id);

    /// <inheritdoc />
    public IEnumerable<User> GetAll() => _users.Values;

    /// <inheritdoc />
    public void Add(User entity)
    {
        var id = Interlocked.Increment(ref _nextId);
        var user = entity with { Id = id };
        _users.TryAdd(id, user);
    }

    /// <inheritdoc />
    public void Update(User entity) => _users[entity.Id] = entity;

    /// <inheritdoc />
    public bool Delete(int id) => _users.TryRemove(id, out _);

    /// <summary>Finds all users with the specified role.</summary>
    public IEnumerable<User> FindByRole(UserRole role) =>
        _users.Values.Where(u => u.Role == role);

    /// <summary>Finds a user by email address (case-insensitive).</summary>
    public User? FindByEmail(string email) =>
        _users.Values.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>Tries to find a user by ID, returning null on error.</summary>
    public User? TryFindById(int id)
    {
        try { return GetById(id); }
        catch (Exception) { return null; }
    }

    /// <summary>Returns a summary of the user store.</summary>
    public string SummarizeUsers()
    {
        int count = _users.Count;
        return $"Total users: {count}";
    }
}
