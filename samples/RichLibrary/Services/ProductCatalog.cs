using System.Runtime.CompilerServices;
using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// Product catalog with search and filtering capabilities.
/// </summary>
public sealed class ProductCatalog
{
    private readonly List<Product> _products = [];

    /// <summary>Adds a product to the catalog.</summary>
    public void AddProduct(Product product) => _products.Add(product);

    /// <summary>Searches products by name or category.</summary>
    public IEnumerable<Product> Search(string query)
    {
        return _products.Where(p =>
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Groups active products by category.</summary>
    public IEnumerable<IGrouping<string, Product>> GroupByCategory()
    {
        return _products
            .Where(p => p.IsActive)
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key);
    }

    /// <summary>Gets the total inventory value (price times stock count).</summary>
    public decimal GetTotalValue() =>
        _products.Sum(p => p.Price * p.StockCount);

    /// <summary>Returns the number of active products.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountActive() => _products.Count(p => p.IsActive);
}
