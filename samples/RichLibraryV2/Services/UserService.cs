using System.Collections.Concurrent;
using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// In-memory user service (V2 — no longer implements IRepository, changed signatures).
/// </summary>
public sealed class UserService
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _nextId;

    public User? GetById(int id) => _users.GetValueOrDefault(id);

    public IReadOnlyList<User> GetAll() => _users.Values.ToList();

    public int Add(User entity)
    {
        var id = Interlocked.Increment(ref _nextId);
        var user = entity with { Id = id };
        _users.TryAdd(id, user);
        return id;
    }

    public void Update(User entity) => _users[entity.Id] = entity;

    public bool Delete(int id) => _users.TryRemove(id, out _);

    public IEnumerable<User> FindByRole(UserRole role) =>
        _users.Values.Where(u => u.Role == role);

    public User? FindByEmail(string email) =>
        _users.Values.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<User> FindByTag(string tag) =>
        _users.Values.Where(u => u.Tags.Contains(tag));
}
