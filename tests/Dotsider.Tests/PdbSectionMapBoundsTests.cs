using Dotsider.Core.Analysis.NativePdb;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>Verifies section-map RVA arithmetic at the unsigned address boundary.</summary>
[TestClass]
public sealed class PdbSectionMapBoundsTests
{
    /// <summary>Verifies the largest representable RVA remains a valid mapping.</summary>
    [TestMethod]
    public void ToRva_MaximumRepresentableAddress_ReturnsValue()
    {
        var map = BuildMap(0xFFFF_FFF0, 0x0F);

        Assert.AreEqual(uint.MaxValue, map.ToRva(1, 0x0F));
        Assert.AreEqual(uint.MaxValue, map.SectionEndRva(1));
    }

    /// <summary>Verifies an RVA addition beyond the unsigned address space is rejected.</summary>
    [TestMethod]
    public void ToRva_AddressOverflow_ReturnsNull()
    {
        var map = BuildMap(0xFFFF_FFF0, 0x10);

        Assert.IsNull(map.ToRva(1, 0x10));
    }

    /// <summary>Verifies an overflowing section end cannot become a wrapped containment bound.</summary>
    [TestMethod]
    public void SectionEndRva_AddressOverflow_ReturnsZero()
    {
        var map = BuildMap(0xFFFF_FFF0, 0x10);

        Assert.AreEqual(0U, map.SectionEndRva(1));
    }

    private static PdbSectionMap BuildMap(uint virtualAddress, uint virtualSize)
    {
        var header = new byte[40];
        ".text"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), virtualSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), virtualAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(36), 0x6000_0020);
        return PdbSectionMap.FromSectionHeaders(header);
    }
}
