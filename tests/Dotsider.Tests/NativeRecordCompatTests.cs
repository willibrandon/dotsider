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
public class NativeRecordCompatTests
{
    /// <summary>Verifies the pre-ManagedNativeHeader ten-argument ClrHeader constructor still works.</summary>
    [Fact(Timeout = 30_000)]
    public void ClrHeader_OldConstructor_DefaultsManagedNativeHeader()
    {
        var header = new ClrHeader(2, 5, 0x100, 0x200, CorFlags.ILOnly, 0x06000001, 0, 0, 0, 0);
        Assert.Equal(default, header.ManagedNativeHeader);
        Assert.Equal(0, header.ManagedNativeHeader.Size);
    }

    /// <summary>Verifies the old ten-output ClrHeader deconstruction still works.</summary>
    [Fact(Timeout = 30_000)]
    public void ClrHeader_OldDeconstruct_YieldsTen()
    {
        var header = new ClrHeader(2, 5, 0x100, 0x200, CorFlags.ILOnly, 0x06000001, 0x300, 0x40, 0, 0,
            new DirectoryEntry(0x500, 0x60));
        var (major, minor, metadataRva, metadataSize, flags, entryPoint, resRva, resSize, snRva, snSize) = header;
        Assert.Equal(2, major);
        Assert.Equal(5, minor);
        Assert.Equal(0x100, metadataRva);
        Assert.Equal(0x200, metadataSize);
        Assert.Equal(CorFlags.ILOnly, flags);
        Assert.Equal(0x06000001, entryPoint);
        Assert.Equal(0x300, resRva);
        Assert.Equal(0x40, resSize);
        Assert.Equal(0, snRva);
        Assert.Equal(0, snSize);
    }

    /// <summary>Verifies the old five-argument NativeSymbolInfo constructor still works.</summary>
    [Fact(Timeout = 30_000)]
    public void NativeSymbolInfo_OldConstructor_DefaultsNewFields()
    {
        var info = new NativeSymbolInfo([], NativeSymbolSource.NativePdb, NativeSymbolStatus.Loaded, "p", "d");
        Assert.Equal(NativeArchitecture.Unknown, info.Architecture);
        Assert.Null(info.SourceMap);
    }

    /// <summary>Verifies the old five-output NativeSymbolInfo deconstruction still works.</summary>
    [Fact(Timeout = 30_000)]
    public void NativeSymbolInfo_OldDeconstruct_YieldsFive()
    {
        var info = new NativeSymbolInfo([], NativeSymbolSource.NativePdb, NativeSymbolStatus.Loaded, "p", "d",
            NativeArchitecture.X64, null);
        var (symbols, source, status, path, diagnostic) = info;
        Assert.Empty(symbols);
        Assert.Equal(NativeSymbolSource.NativePdb, source);
        Assert.Equal(NativeSymbolStatus.Loaded, status);
        Assert.Equal("p", path);
        Assert.Equal("d", diagnostic);
    }

    /// <summary>Verifies the old five- and six-argument SizeNode constructors still work.</summary>
    [Fact(Timeout = 30_000)]
    public void SizeNode_OldConstructors_DefaultNativeAddress()
    {
        var five = new SizeNode("n", "p", 10, SizeNodeKind.Method, []);
        var six = new SizeNode("n", "p", 10, SizeNodeKind.Method, [], "aot");
        Assert.Null(five.NativeAddress);
        Assert.Null(six.NativeAddress);
        Assert.Equal("aot", six.AotNodeName);
    }

    /// <summary>Verifies the old six-output SizeNode deconstruction still works.</summary>
    [Fact(Timeout = 30_000)]
    public void SizeNode_OldDeconstruct_YieldsSix()
    {
        var node = new SizeNode("n", "p", 10, SizeNodeKind.Type, [], "aot", 0x1000);
        var (name, fullPath, size, kind, children, aotNodeName) = node;
        Assert.Equal("n", name);
        Assert.Equal("p", fullPath);
        Assert.Equal(10, size);
        Assert.Equal(SizeNodeKind.Type, kind);
        Assert.Empty(children);
        Assert.Equal("aot", aotNodeName);
    }
}
