using Dotsider.Infrastructure;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>Checks optional protocol property access for missing and non-object data.</summary>
[TestClass]
public sealed class JsonElementExtensionsTests
{
    /// <summary>Missing or non-object payloads have no optional property.</summary>
    /// <param name="json">The protocol data to inspect.</param>
    [TestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("42")]
    [DataRow("\"text\"")]
    [DataRow("{}")]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetPropertyOrNull_AbsentProperty_ReturnsNull(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.IsNull(document.RootElement.GetPropertyOrNull("typeCount"));
    }

    /// <summary>An absent response body has the same fallback as a missing property.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetPropertyOrNull_DefaultElement_ReturnsNull()
    {
        Assert.IsNull(default(JsonElement).GetPropertyOrNull("typeCount"));
    }

    /// <summary>A present property retains its JSON type and value.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetPropertyOrNull_PresentProperty_ReturnsValue()
    {
        using var document = JsonDocument.Parse("{\"typeCount\":42}");
        var value = TestAssert.HasValue(document.RootElement.GetPropertyOrNull("typeCount"));
        Assert.AreEqual(42, value.GetInt32());
    }
}
