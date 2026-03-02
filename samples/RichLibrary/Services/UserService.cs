using System.Collections.Concurrent;
using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// In-memory user service implementing the repository pattern.
/// </summary>
public sealed class UserService : IRepository<User>
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _nextId;

    public User? GetById(int id) => _users.GetValueOrDefault(id);

    public IEnumerable<User> GetAll() => _users.Values;

    public void Add(User entity)
    {
        var id = Interlocked.Increment(ref _nextId);
        var user = entity with { Id = id };
        _users.TryAdd(id, user);
    }

    public void Update(User entity) => _users[entity.Id] = entity;

    public bool Delete(int id) => _users.TryRemove(id, out _);

    public IEnumerable<User> FindByRole(UserRole role) =>
        _users.Values.Where(u => u.Role == role);

    public User? FindByEmail(string email) =>
        _users.Values.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
}
