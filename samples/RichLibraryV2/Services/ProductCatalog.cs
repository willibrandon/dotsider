using System.Runtime.CompilerServices;
using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// Product catalog (V2 — StockCount→Quantity change, added pagination).
/// </summary>
public sealed class ProductCatalog
{
    private readonly List<Product> _products = [];

    public void AddProduct(Product product) => _products.Add(product);

    public IEnumerable<Product> Search(string query, int maxResults = 100)
    {
        return _products.Where(p =>
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults);
    }

    public IEnumerable<IGrouping<string, Product>> GroupByCategory()
    {
        return _products
            .Where(p => p.IsActive)
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key);
    }

    public decimal GetTotalValue() =>
        _products.Sum(p => p.Price * p.Quantity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountActive() => _products.Count(p => p.IsActive);

    public IEnumerable<Product> GetPage(int page, int pageSize = 20) =>
        _products.Skip(page * pageSize).Take(pageSize);
}
