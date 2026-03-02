using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// New service in V2 — manages orders.
/// </summary>
public sealed class OrderService
{
    private readonly List<Order> _orders = [];
    private int _nextId;

    public Order CreateOrder(int userId, IEnumerable<OrderLine> lines)
    {
        var lineList = lines.ToList();
        var total = lineList.Sum(l => l.Quantity * l.UnitPrice);
        var order = new Order(++_nextId, userId, lineList, total, DateTime.UtcNow);
        _orders.Add(order);
        return order;
    }

    public IEnumerable<Order> GetOrdersByUser(int userId) =>
        _orders.Where(o => o.UserId == userId);

    public decimal GetTotalRevenue() => _orders.Sum(o => o.Total);
}
