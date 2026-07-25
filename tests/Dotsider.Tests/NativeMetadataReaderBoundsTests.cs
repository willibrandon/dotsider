using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Verifies NativeFormat traversal, text, graph, and malformed-input containment.
/// </summary>
[TestClass]
public sealed class NativeMetadataReaderBoundsTests
{
    private const int DocumentedMaxDepth = 256;
    private const int DocumentedMaxRecoveredTextCharacters = 1 << 24;
    private const int DocumentedMaxStringLength = 4096;
    private const int DocumentedMaxTraversalWork = 1 << 20;

    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Verifies repeated handles expand each record once without duplicate output.</summary>
    [TestMethod]
    public void ReadTypes_RepeatedRecordOffsets_ExpandOnce()
    {
        var builder = new SyntheticNativeMetadataBuilder(scopeCount: 2);
        var assemblyName = builder.AddString("Assembly");
        var namespaceName = builder.AddString("Namespace");
        var childNamespaceName = builder.AddString("ChildNamespace");
        var outerName = builder.AddString("Outer");
        var innerName = builder.AddString("Inner");
        var childName = builder.AddString("Child");
        var methodName = builder.AddString("Run");
        var method = builder.AddMethod(methodName);
        var inner = builder.AddType(innerName);
        var outer = builder.AddType(outerName, nestedTypeCount: 2, methodCount: 2);
        PatchAll(builder, outer.NestedTypeSlots, inner.Offset);
        PatchAll(builder, outer.MethodSlots, method);
        var child = builder.AddType(childName, methodCount: 1);
        builder.PatchHandle(child.MethodSlots[0], method);
        var childNamespace = builder.AddNamespace(
            childNamespaceName,
            typeCount: 2);
        PatchAll(builder, childNamespace.TypeSlots, child.Offset);
        var rootNamespace = builder.AddNamespace(
            namespaceName,
            typeCount: 2,
            childNamespaceCount: 2);
        PatchAll(builder, rootNamespace.TypeSlots, outer.Offset);
        PatchAll(builder, rootNamespace.ChildNamespaceSlots, childNamespace.Offset);
        var scope = builder.AddScope(assemblyName, rootNamespace.Offset);
        builder.SetScope(0, scope);
        builder.SetScope(1, scope);

        var types = ReadTypes(builder.Build());

        Assert.AreSequenceEqual(
            ["Namespace.Outer", "Namespace.Outer+Inner", "Namespace.ChildNamespace.Child"],
            types.Select(static type => type.FullName));
        TestAssert.All(
            types,
            static type => Assert.AreEqual("Assembly", type.AssemblyName));
        Assert.AreSequenceEqual(["Run"], types[0].MethodNames);
        Assert.IsEmpty(types[1].MethodNames);
        Assert.IsEmpty(types[2].MethodNames);
    }

    /// <summary>
    /// Verifies repeated type handles cannot multiply traversal of one large method list.
    /// </summary>
    [TestMethod]
    public void ReadTypes_RepeatedTypeWithLargeMethodList_ExpandsLinearly()
    {
        const int MethodCount = 4_096;
        const int TypeReferenceCount = 1_024;

        var builder = new SyntheticNativeMetadataBuilder();
        var typeName = builder.AddString("Type");
        var methodName = builder.AddString("Run");
        var method = builder.AddMethod(methodName);
        var type = builder.AddType(typeName, methodCount: MethodCount);
        PatchAll(builder, type.MethodSlots, method);
        var rootNamespace = builder.AddNamespace(0, typeCount: TypeReferenceCount);
        PatchAll(builder, rootNamespace.TypeSlots, type.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        var recovered = Assert.ContainsSingle(ReadTypes(builder.Build()));

        Assert.AreEqual("Type", recovered.FullName);
        Assert.AreSequenceEqual(["Run"], recovered.MethodNames);
    }

    /// <summary>
    /// Verifies distinct method records sharing one name remain distinct and in metadata order.
    /// </summary>
    [TestMethod]
    public void ReadTypes_DistinctMethodsSharingName_PreserveBothInOrder()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var typeName = builder.AddString("Type");
        var methodName = builder.AddString("Shared");
        var firstMethod = builder.AddMethod(methodName);
        var secondMethod = builder.AddMethod(methodName);
        var type = builder.AddType(typeName, methodCount: 2);
        builder.PatchHandle(type.MethodSlots[0], firstMethod);
        builder.PatchHandle(type.MethodSlots[1], secondMethod);
        var rootNamespace = builder.AddNamespace(0, typeCount: 1);
        builder.PatchHandle(rootNamespace.TypeSlots[0], type.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        var recovered = Assert.ContainsSingle(ReadTypes(builder.Build()));

        Assert.AreSequenceEqual(["Shared", "Shared"], recovered.MethodNames);
    }

    /// <summary>Verifies self and mutual cycles terminate for both namespaces and types.</summary>
    [TestMethod]
    public void ReadTypes_SelfAndMutualCycles_TerminateWithoutDuplicates()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var aName = builder.AddString("A");
        var bName = builder.AddString("B");
        var cName = builder.AddString("C");
        var otherNamespaceName = builder.AddString("Other");
        var a = builder.AddType(aName, nestedTypeCount: 2);
        var b = builder.AddType(bName, nestedTypeCount: 1);
        builder.PatchHandle(a.NestedTypeSlots[0], a.Offset);
        builder.PatchHandle(a.NestedTypeSlots[1], b.Offset);
        builder.PatchHandle(b.NestedTypeSlots[0], a.Offset);
        var c = builder.AddType(cName);
        var otherNamespace = builder.AddNamespace(
            otherNamespaceName,
            typeCount: 1,
            childNamespaceCount: 1);
        builder.PatchHandle(otherNamespace.TypeSlots[0], c.Offset);
        var rootNamespace = builder.AddNamespace(
            0,
            typeCount: 1,
            childNamespaceCount: 2);
        builder.PatchHandle(rootNamespace.TypeSlots[0], a.Offset);
        builder.PatchHandle(rootNamespace.ChildNamespaceSlots[0], rootNamespace.Offset);
        builder.PatchHandle(rootNamespace.ChildNamespaceSlots[1], otherNamespace.Offset);
        builder.PatchHandle(otherNamespace.ChildNamespaceSlots[0], rootNamespace.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        var types = ReadTypes(builder.Build());

        Assert.AreSequenceEqual(
            ["A", "A+B", "Other.C"],
            types.Select(static type => type.FullName));
    }

    /// <summary>
    /// Verifies a namespace cycle is expanded once and leaves the text budget available for
    /// later siblings.
    /// </summary>
    [TestMethod]
    public void ReadTypes_NamespaceCycle_DoesNotConsumeTextBudget()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var loopName = new string('x', DocumentedMaxStringLength);
        var loopNameOffset = builder.AddString(loopName);
        var safeNamespaceName = builder.AddString("Safe");
        var typeName = builder.AddString("Good");
        var type = builder.AddType(typeName);
        var safeNamespace = builder.AddNamespace(safeNamespaceName, typeCount: 1);
        builder.PatchHandle(safeNamespace.TypeSlots[0], type.Offset);
        var rootNamespace = builder.AddNamespace(
            loopNameOffset,
            childNamespaceCount: 2);
        builder.PatchHandle(rootNamespace.ChildNamespaceSlots[0], rootNamespace.Offset);
        builder.PatchHandle(rootNamespace.ChildNamespaceSlots[1], safeNamespace.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        var recovered = Assert.ContainsSingle(ReadTypes(builder.Build()));

        Assert.AreEqual($"{loopName}.Safe.Good", recovered.FullName);
    }

    /// <summary>
    /// Verifies an over-depth first encounter does not suppress a later shallow reference.
    /// </summary>
    [TestMethod]
    public void ReadTypes_DeepThenShallowReference_ExpandsAtShallowDepth()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var linkName = builder.AddString("Link");
        var targetName = builder.AddString("Target");
        var target = builder.AddType(targetName);
        var deepRoot = target.Offset;
        for (var i = 0; i < DocumentedMaxDepth; i++)
        {
            var link = builder.AddType(linkName, nestedTypeCount: 1);
            builder.PatchHandle(link.NestedTypeSlots[0], deepRoot);
            deepRoot = link.Offset;
        }

        var rootNamespace = builder.AddNamespace(0, typeCount: 2);
        builder.PatchHandle(rootNamespace.TypeSlots[0], deepRoot);
        builder.PatchHandle(rootNamespace.TypeSlots[1], target.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        var types = ReadTypes(builder.Build());

        Assert.HasCount(DocumentedMaxDepth + 1, types);
        Assert.ContainsSingle(
            static type => type.FullName == "Target",
            types);
    }

    /// <summary>
    /// Verifies exactly the maximum traversal work, including skipped forwarders, succeeds.
    /// </summary>
    [TestMethod]
    public void ReadTypes_TraversalWorkAtLimit_RecoversType()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var typeName = builder.AddString("Type");
        var type = builder.AddType(typeName);
        var rootNamespace = builder.AddNamespaceWithRepeatedForwarders(
            0,
            type.Offset,
            DocumentedMaxTraversalWork - 3);
        var scope = builder.AddScope(0, rootNamespace);
        builder.SetScope(0, scope);

        var recovered = Assert.ContainsSingle(ReadTypes(builder.Build()));

        Assert.AreEqual("Type", recovered.FullName);
    }

    /// <summary>
    /// Verifies one handle beyond the traversal budget rejects the active namespace atomically.
    /// </summary>
    [TestMethod]
    public void ReadTypes_TraversalWorkOverLimit_OmitsActiveNamespace()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var typeName = builder.AddString("Type");
        var type = builder.AddType(typeName);
        var rootNamespace = builder.AddNamespaceWithRepeatedForwarders(
            0,
            type.Offset,
            DocumentedMaxTraversalWork - 2);
        var scope = builder.AddScope(0, rootNamespace);
        builder.SetScope(0, scope);

        Assert.IsEmpty(ReadTypes(builder.Build()));
    }

    /// <summary>
    /// Verifies repeated references to one maximum-length string reuse one decoded value.
    /// </summary>
    [TestMethod]
    public void ReadTypes_RepeatedLongStringOffset_DecodesOnce()
    {
        const int methodCount = 2_048;
        var builder = new SyntheticNativeMetadataBuilder();
        var value = new string('x', DocumentedMaxStringLength);
        var sharedName = builder.AddString(value);
        var methods = new int[methodCount];
        for (var i = 0; i < methods.Length; i++)
        {
            methods[i] = builder.AddMethod(sharedName);
        }

        var type = builder.AddType(sharedName, methodCount: methods.Length);
        for (var i = 0; i < methods.Length; i++)
        {
            builder.PatchHandle(type.MethodSlots[i], methods[i]);
        }

        var rootNamespace = builder.AddNamespace(sharedName, typeCount: 1);
        builder.PatchHandle(rootNamespace.TypeSlots[0], type.Offset);
        var scope = builder.AddScope(sharedName, rootNamespace.Offset);
        builder.SetScope(0, scope);

        var recovered = Assert.ContainsSingle(ReadTypes(builder.Build()));

        Assert.AreEqual(value, recovered.AssemblyName);
        Assert.AreEqual($"{value}.{value}", recovered.FullName);
        Assert.HasCount(methodCount, recovered.MethodNames);
        TestAssert.All(
            recovered.MethodNames,
            method => Assert.AreSame(recovered.MethodNames[0], method));
    }

    /// <summary>
    /// Verifies unique names consume the complete text budget and omit the one-over active type.
    /// </summary>
    [TestMethod]
    public void ReadTypes_UniqueNamesOverTextBudget_PreserveCommittedPrefix()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var maximumName = new string('n', DocumentedMaxStringLength);
        var exactTypeCount = DocumentedMaxRecoveredTextCharacters
            / DocumentedMaxStringLength;
        var typeOffsets = new int[exactTypeCount + 1];
        for (var i = 0; i < exactTypeCount; i++)
        {
            var name = builder.AddString(maximumName);
            typeOffsets[i] = builder.AddType(name).Offset;
        }

        var overName = builder.AddString("x");
        typeOffsets[^1] = builder.AddType(overName).Offset;
        var rootNamespace = builder.AddNamespace(0, typeCount: typeOffsets.Length);
        for (var i = 0; i < typeOffsets.Length; i++)
        {
            builder.PatchHandle(rootNamespace.TypeSlots[i], typeOffsets[i]);
        }

        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        var types = ReadTypes(builder.Build());

        Assert.HasCount(exactTypeCount, types);
        TestAssert.All(
            types,
            type => Assert.AreEqual(maximumName, type.FullName));
        Assert.DoesNotContain(
            static type => type.FullName == "x",
            types);
    }

    /// <summary>Verifies a name beyond the byte-length limit omits the active type.</summary>
    [TestMethod]
    public void ReadTypes_NameLongerThanLimit_OmitsActiveType()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var oversizedName = builder.AddString(
            (uint)DocumentedMaxStringLength + 1,
            new byte[DocumentedMaxStringLength + 1]);
        var type = builder.AddType(oversizedName);
        var rootNamespace = builder.AddNamespace(0, typeCount: 1);
        builder.PatchHandle(rootNamespace.TypeSlots[0], type.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        Assert.IsEmpty(ReadTypes(builder.Build()));
    }

    /// <summary>Verifies invalid UTF-8 in a name omits the active type.</summary>
    [TestMethod]
    public void ReadTypes_InvalidUtf8Name_OmitsActiveType()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var invalidName = builder.AddString(2, [0xC3, 0x28]);
        var type = builder.AddType(invalidName);
        var rootNamespace = builder.AddNamespace(0, typeCount: 1);
        builder.PatchHandle(rootNamespace.TypeSlots[0], type.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        Assert.IsEmpty(ReadTypes(builder.Build()));
    }

    /// <summary>
    /// Verifies missing and truncated encodings at every width, plus the reserved prefix, fail
    /// closed.
    /// </summary>
    /// <param name="prefix">The first unsigned byte, or -1 when even that byte is absent.</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(0x01)]
    [DataRow(0x03)]
    [DataRow(0x07)]
    [DataRow(0x0F)]
    [DataRow(0x1F)]
    public void ReadTypes_TruncatedOrInvalidUnsigned_ReturnsEmpty(int prefix)
    {
        var suffix = prefix < 0 ? Array.Empty<byte>() : [(byte)prefix];

        Assert.IsEmpty(ReadTypes(BuildRawBlob(suffix)));
    }

    /// <summary>Verifies invalid section coordinates do not escape the supplied image.</summary>
    /// <param name="malformation">The section range rule to violate.</param>
    [TestMethod]
    [DataRow("NegativeOffset")]
    [DataRow("OffsetOutsideImage")]
    [DataRow("SizeOverflow")]
    [DataRow("TruncatedSection")]
    public void ReadTypes_InvalidSectionRange_ReturnsEmpty(string malformation)
    {
        var blob = BuildSingleTypeBlob("Type");
        var section = malformation switch
        {
            "NegativeOffset" => MetadataSection(-1, blob.Length),
            "OffsetOutsideImage" => MetadataSection(int.MaxValue, blob.Length),
            "SizeOverflow" => MetadataSection(1, long.MaxValue),
            "TruncatedSection" => MetadataSection(0, (long)blob.Length + 1),
            _ => throw new ArgumentOutOfRangeException(nameof(malformation)),
        };

        Assert.IsEmpty(NativeMetadataReader.ReadTypes(blob, [section]));
    }

    /// <summary>Verifies impossible counts, offsets, and collection extents fail closed.</summary>
    /// <param name="malformation">The NativeFormat structure to corrupt.</param>
    [TestMethod]
    [DataRow("ByteCollectionCount")]
    [DataRow("ByteCollectionExtent")]
    [DataRow("NamespaceHandleCount")]
    [DataRow("NamespaceHandleList")]
    [DataRow("ScopeCount")]
    [DataRow("ScopeList")]
    [DataRow("ScopeOffset")]
    [DataRow("StringCount")]
    [DataRow("TypeMethodList")]
    public void ReadTypes_ImpossibleCountOrCollection_ReturnsEmpty(string malformation)
    {
        var blob = malformation switch
        {
            "ByteCollectionCount" => BuildInvalidByteCollectionBlob(),
            "ByteCollectionExtent" => BuildTruncatedByteCollectionBlob(),
            "NamespaceHandleCount" => BuildInvalidNamespaceCountBlob(),
            "NamespaceHandleList" => BuildTruncatedNamespaceHandleListBlob(),
            "ScopeCount" => BuildRawBlob(EncodeUnsigned(uint.MaxValue)),
            "ScopeList" => BuildRawBlob(EncodeUnsigned(1)),
            "ScopeOffset" => BuildRawBlob(
                Concat(EncodeUnsigned(1), EncodeUnsigned(uint.MaxValue))),
            "StringCount" => BuildInvalidStringCountBlob(),
            "TypeMethodList" => BuildTruncatedTypeMethodListBlob(),
            _ => throw new ArgumentOutOfRangeException(nameof(malformation)),
        };

        Assert.IsEmpty(ReadTypes(blob));
    }

    /// <summary>
    /// Verifies a malformed active type is omitted while a fully committed prefix survives.
    /// </summary>
    [TestMethod]
    public void ReadTypes_MalformedTypeAfterValidType_PreservesOnlyCommittedPrefix()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var goodName = builder.AddString("Good");
        var goodMethodName = builder.AddString("Run");
        var goodMethod = builder.AddMethod(goodMethodName);
        var goodType = builder.AddType(goodName, methodCount: 1);
        builder.PatchHandle(goodType.MethodSlots[0], goodMethod);
        var badName = builder.AddString("Bad");
        var badType = builder.AddType(badName, methodCount: 1);
        builder.PatchHandle(badType.MethodSlots[0], int.MaxValue);
        var rootNamespace = builder.AddNamespace(0, typeCount: 2);
        builder.PatchHandle(rootNamespace.TypeSlots[0], goodType.Offset);
        builder.PatchHandle(rootNamespace.TypeSlots[1], badType.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);

        var recovered = Assert.ContainsSingle(ReadTypes(builder.Build()));

        Assert.AreEqual("Good", recovered.FullName);
        Assert.AreSequenceEqual(["Run"], recovered.MethodNames);
    }

    /// <summary>
    /// Verifies a compiler-produced NativeAOT image routes patched cyclic metadata through the
    /// public analyzer facade.
    /// </summary>
    [TestMethod]
    public void RecoveredTypes_PatchedNativeAotMetadata_UsesBoundedReader()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var path = Samples.NativeAotConsoleExe!;
        var image = File.ReadAllBytes(path);
        using var original = new AssemblyAnalyzer(image, path);
        var metadata = Assert.ContainsSingle(
            static section => section.SectionId == ReadyToRunReader.EmbeddedMetadata,
            original.ReadyToRunSections);
        Assert.IsNotNull(metadata.FileOffset);

        var builder = new SyntheticNativeMetadataBuilder(scopeCount: 2);
        var assemblyName = builder.AddString("PatchedAssembly");
        var typeName = builder.AddString("PatchedType");
        var methodName = builder.AddString("Run");
        var method = builder.AddMethod(methodName);
        var type = builder.AddType(typeName, nestedTypeCount: 1, methodCount: 2);
        builder.PatchHandle(type.NestedTypeSlots[0], type.Offset);
        PatchAll(builder, type.MethodSlots, method);
        var rootNamespace = builder.AddNamespace(0, typeCount: 2);
        PatchAll(builder, rootNamespace.TypeSlots, type.Offset);
        var scope = builder.AddScope(assemblyName, rootNamespace.Offset);
        builder.SetScope(0, scope);
        builder.SetScope(1, scope);
        var replacement = builder.Build();
        Assert.IsGreaterThanOrEqualTo((long)replacement.Length, metadata.Size);
        replacement.CopyTo(image.AsSpan(metadata.FileOffset.Value));

        using var patched = new AssemblyAnalyzer(image, path);
        var recovered = Assert.ContainsSingle(patched.RecoveredTypes);

        Assert.AreEqual("PatchedType", recovered.FullName);
        Assert.AreEqual("PatchedAssembly", recovered.AssemblyName);
        Assert.AreSequenceEqual(["Run"], recovered.MethodNames);
    }

    private static byte[] BuildInvalidByteCollectionBlob()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var scopeOffset = builder.Length;
        for (var i = 0; i < 7; i++)
        {
            builder.AppendUnsigned(0);
        }

        builder.AppendUnsigned(uint.MaxValue);
        builder.SetScope(0, scopeOffset);
        return builder.Build();
    }

    private static byte[] BuildInvalidNamespaceCountBlob()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var namespaceOffset = builder.Length;
        builder.AppendUnsigned(0);
        builder.AppendUnsigned(0);
        builder.AppendUnsigned((uint)DocumentedMaxTraversalWork + 1);
        var scope = builder.AddScope(0, namespaceOffset);
        builder.SetScope(0, scope);
        return builder.Build();
    }

    private static byte[] BuildTruncatedByteCollectionBlob()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var scopeOffset = builder.Length;
        for (var i = 0; i < 7; i++)
        {
            builder.AppendUnsigned(0);
        }

        builder.AppendUnsigned(100);
        builder.SetScope(0, scopeOffset);
        return builder.Build();
    }

    private static byte[] BuildTruncatedNamespaceHandleListBlob()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var namespaceOffset = builder.Length;
        builder.AppendUnsigned(0);
        builder.AppendUnsigned(0);
        builder.AppendUnsigned(100);
        var scope = builder.AddScope(0, namespaceOffset);
        builder.SetScope(0, scope);
        return builder.Build();
    }

    private static byte[] BuildTruncatedTypeMethodListBlob()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var typeOffset = builder.Length;
        for (var i = 0; i < 7; i++)
        {
            builder.AppendUnsigned(0);
        }

        builder.AppendUnsigned(0);
        builder.AppendUnsigned(100);
        var rootNamespace = builder.AddNamespace(0, typeCount: 1);
        builder.PatchHandle(rootNamespace.TypeSlots[0], typeOffset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);
        return builder.Build();
    }

    private static byte[] BuildInvalidStringCountBlob()
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var nameOffset = builder.AddString(uint.MaxValue, []);
        var type = builder.AddType(nameOffset);
        var rootNamespace = builder.AddNamespace(0, typeCount: 1);
        builder.PatchHandle(rootNamespace.TypeSlots[0], type.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);
        return builder.Build();
    }

    private static byte[] BuildRawBlob(ReadOnlySpan<byte> suffix)
    {
        var result = new byte[sizeof(uint) + suffix.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, 0xDEAD_DFFD);
        suffix.CopyTo(result.AsSpan(sizeof(uint)));
        return result;
    }

    private static byte[] BuildSingleTypeBlob(string name)
    {
        var builder = new SyntheticNativeMetadataBuilder();
        var nameOffset = builder.AddString(name);
        var type = builder.AddType(nameOffset);
        var rootNamespace = builder.AddNamespace(0, typeCount: 1);
        builder.PatchHandle(rootNamespace.TypeSlots[0], type.Offset);
        var scope = builder.AddScope(0, rootNamespace.Offset);
        builder.SetScope(0, scope);
        return builder.Build();
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(static part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }

    private static byte[] EncodeUnsigned(uint value)
    {
        var result = new byte[1 + sizeof(uint)];
        result[0] = 0x0F;
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(1), value);
        return result;
    }

    private static RtrSection MetadataSection(int fileOffset, long size) =>
        new(
            ReadyToRunReader.EmbeddedMetadata,
            "ReadonlyBlob (EmbeddedMetadata)",
            0,
            size,
            fileOffset);

    private static void PatchAll(
        SyntheticNativeMetadataBuilder builder,
        IEnumerable<int> slots,
        int offset)
    {
        foreach (var slot in slots)
        {
            builder.PatchHandle(slot, offset);
        }
    }

    private static IReadOnlyList<RecoveredType> ReadTypes(byte[] blob) =>
        NativeMetadataReader.ReadTypes(
            blob,
            [MetadataSection(0, blob.Length)]);
}
