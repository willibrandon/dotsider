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

/// <summary>
/// Represents a single line item in an order.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Quantity">The quantity ordered.</param>
/// <param name="UnitPrice">The price per unit.</param>
public sealed record OrderLine(
    int ProductId,
    int Quantity,
    decimal UnitPrice);
