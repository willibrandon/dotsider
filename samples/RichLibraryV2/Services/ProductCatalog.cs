using System.Runtime.CompilerServices;
using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// Product catalog (V2 — StockCount to Quantity change, added pagination).
/// </summary>
public sealed class ProductCatalog
{
    private readonly List<Product> _products = [];

    /// <summary>Adds a product to the catalog.</summary>
    public void AddProduct(Product product) => _products.Add(product);

    /// <summary>Searches products by name or category, limited to the specified max results.</summary>
    public IEnumerable<Product> Search(string query, int maxResults = 100)
    {
        return _products.Where(p =>
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults);
    }

    /// <summary>Groups active products by category.</summary>
    public IEnumerable<IGrouping<string, Product>> GroupByCategory()
    {
        return _products
            .Where(p => p.IsActive)
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key);
    }

    /// <summary>Gets the total inventory value (price times quantity).</summary>
    public decimal GetTotalValue() =>
        _products.Sum(p => p.Price * p.Quantity);

    /// <summary>Returns the number of active products.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountActive() => _products.Count(p => p.IsActive);

    /// <summary>Returns a page of products.</summary>
    public IEnumerable<Product> GetPage(int page, int pageSize = 20) =>
        _products.Skip(page * pageSize).Take(pageSize);
}
