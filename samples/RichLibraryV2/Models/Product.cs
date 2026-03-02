namespace RichLibrary.Models;

/// <summary>
/// Represents a product in the catalog (V2 — renamed StockCount→Quantity, added Sku).
/// </summary>
public sealed record Product(
    int Id,
    string Name,
    decimal Price,
    string Category,
    int Quantity,
    string? Sku = null,
    bool IsActive = true);
