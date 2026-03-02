using System.Runtime.CompilerServices;
using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// Product catalog with search and filtering capabilities.
/// </summary>
public sealed class ProductCatalog
{
    private readonly List<Product> _products = [];

    public void AddProduct(Product product) => _products.Add(product);

    public IEnumerable<Product> Search(string query)
    {
        return _products.Where(p =>
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<IGrouping<string, Product>> GroupByCategory()
    {
        return _products
            .Where(p => p.IsActive)
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key);
    }

    public decimal GetTotalValue() =>
        _products.Sum(p => p.Price * p.StockCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountActive() => _products.Count(p => p.IsActive);
}
