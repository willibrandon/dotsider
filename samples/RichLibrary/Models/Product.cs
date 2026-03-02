namespace RichLibrary.Models;

/// <summary>
/// Represents a product in the catalog.
/// </summary>
public sealed record Product(
    int Id,
    string Name,
    decimal Price,
    string Category,
    int StockCount,
    bool IsActive = true);
