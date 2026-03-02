namespace RichLibrary.Models;

/// <summary>
/// New type in V2 — represents a customer order.
/// </summary>
public sealed record Order(
    int Id,
    int UserId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    DateTime OrderDate);

public sealed record OrderLine(
    int ProductId,
    int Quantity,
    decimal UnitPrice);
