using Dotsider.Core.Analysis.Models;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Back-compatibility tests for the #178 shipped-record extensions: the pre-#178 construction and
/// deconstruction shapes of <see cref="NativeSymbolInfo"/> (five-arg) and <see cref="SizeNode"/>
/// (five/six-arg) still compile and behave, with the new fields defaulting. The #180 ReadyToRun work
/// adds <see cref="ClrHeader.ManagedNativeHeader"/>; its ten-arg constructor and deconstruction shape
/// must remain source-compatible.
/// </summary>
[TestClass]
public class NativeRecordCompatTests
{
    /// <summary>Verifies the pre-ManagedNativeHeader ten-argument ClrHeader constructor still works.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ClrHeader_OldConstructor_DefaultsManagedNativeHeader()
    {
        var header = new ClrHeader(2, 5, 0x100, 0x200, CorFlags.ILOnly, 0x06000001, 0, 0, 0, 0);
        Assert.AreEqual(default, header.ManagedNativeHeader);
        Assert.AreEqual(0, header.ManagedNativeHeader.Size);
    }

    /// <summary>Verifies the old ten-output ClrHeader deconstruction still works.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ClrHeader_OldDeconstruct_YieldsTen()
    {
        var header = new ClrHeader(2, 5, 0x100, 0x200, CorFlags.ILOnly, 0x06000001, 0x300, 0x40, 0, 0,
            new DirectoryEntry(0x500, 0x60));
        var (major, minor, metadataRva, metadataSize, flags, entryPoint, resRva, resSize, snRva, snSize) = header;
        Assert.AreEqual(2, major);
        Assert.AreEqual(5, minor);
        Assert.AreEqual(0x100, metadataRva);
        Assert.AreEqual(0x200, metadataSize);
        Assert.AreEqual(CorFlags.ILOnly, flags);
        Assert.AreEqual(0x06000001, entryPoint);
        Assert.AreEqual(0x300, resRva);
        Assert.AreEqual(0x40, resSize);
        Assert.AreEqual(0, snRva);
        Assert.AreEqual(0, snSize);
    }

    /// <summary>Verifies the old five-argument NativeSymbolInfo constructor still works.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeSymbolInfo_OldConstructor_DefaultsNewFields()
    {
        var info = new NativeSymbolInfo([], NativeSymbolSource.NativePdb, NativeSymbolStatus.Loaded, "p", "d");
        Assert.AreEqual(NativeArchitecture.Unknown, info.Architecture);
        Assert.IsNull(info.SourceMap);
    }

    /// <summary>Verifies the old five-output NativeSymbolInfo deconstruction still works.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeSymbolInfo_OldDeconstruct_YieldsFive()
    {
        var info = new NativeSymbolInfo([], NativeSymbolSource.NativePdb, NativeSymbolStatus.Loaded, "p", "d",
            NativeArchitecture.X64, null);
        var (symbols, source, status, path, diagnostic) = info;
        Assert.IsEmpty(symbols);
        Assert.AreEqual(NativeSymbolSource.NativePdb, source);
        Assert.AreEqual(NativeSymbolStatus.Loaded, status);
        Assert.AreEqual("p", path);
        Assert.AreEqual("d", diagnostic);
    }

    /// <summary>Verifies the old five- and six-argument SizeNode constructors still work.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SizeNode_OldConstructors_DefaultNativeAddress()
    {
        var five = new SizeNode("n", "p", 10, SizeNodeKind.Method, []);
        var six = new SizeNode("n", "p", 10, SizeNodeKind.Method, [], "aot");
        Assert.IsNull(five.NativeAddress);
        Assert.IsNull(six.NativeAddress);
        Assert.AreEqual("aot", six.AotNodeName);
    }

    /// <summary>Verifies the old six-output SizeNode deconstruction still works.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SizeNode_OldDeconstruct_YieldsSix()
    {
        var node = new SizeNode("n", "p", 10, SizeNodeKind.Type, [], "aot", 0x1000);
        var (name, fullPath, size, kind, children, aotNodeName) = node;
        Assert.AreEqual("n", name);
        Assert.AreEqual("p", fullPath);
        Assert.AreEqual(10, size);
        Assert.AreEqual(SizeNodeKind.Type, kind);
        Assert.IsEmpty(children);
        Assert.AreEqual("aot", aotNodeName);
    }
}
