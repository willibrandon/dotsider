namespace RichLibrary;

/// <summary>
/// Generic repository interface for CRUD operations.
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>Gets an entity by its identifier, or <c>null</c> if not found.</summary>
    T? GetById(int id);

    /// <summary>Gets all entities in the repository.</summary>
    IEnumerable<T> GetAll();

    /// <summary>Adds a new entity to the repository.</summary>
    void Add(T entity);

    /// <summary>Updates an existing entity in the repository.</summary>
    void Update(T entity);

    /// <summary>Deletes the entity with the specified identifier.</summary>
    bool Delete(int id);
}
