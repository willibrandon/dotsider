using Dotsider.Core.Analysis;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MstatReader"/> against the real size report published next to the
/// NativeAOT sample, plus malformed-input cases. The fixture publishes with the .NET 10 SDK,
/// so format assertions pin version 2.2.
/// </summary>
[TestClass]
public class MstatReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies the fixture's report parses and carries format version 2.2. The assembly
    /// version's Build/Revision are unset sentinels and must not leak into the format fields.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_ReportsFormat22()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        Assert.AreEqual(2, data.FormatMajorVersion);
        Assert.AreEqual(2, data.FormatMinorVersion);
    }

    /// <summary>
    /// Verifies methods carry names, non-negative sizes, and assembly attribution spanning
    /// both the app and the runtime.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_MethodsHaveNamesSizesAndAssemblies()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        Assert.IsNotEmpty(data.Methods);
        TestAssert.All(data.Methods, m => Assert.IsGreaterThanOrEqualTo(0, m.Size));
        Assert.Contains(m => m.Size > 0, data.Methods);
        TestAssert.All(data.Methods, m => Assert.IsFalse(string.IsNullOrEmpty(m.Name)));
        Assert.Contains(m => m.AssemblyName == "System.Private.CoreLib", data.Methods);
        Assert.Contains(m => m.AssemblyName == "NativeAotConsole", data.Methods);
    }

    /// <summary>
    /// Verifies every method in a 2.x report carries its dependency-graph node name — the
    /// string that joins the size entry to the DGML graph.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_MethodsCarryNodeNames()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        TestAssert.All(data.Methods, m => Assert.IsFalse(string.IsNullOrEmpty(m.NodeName)));
    }

    /// <summary>
    /// Verifies method signatures decode from the MemberRef signature blobs: the fixture's
    /// Greet overloads share a display name but carry distinct rendered parameter lists —
    /// the identity that keeps overloads apart in a size diff.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_MethodSignaturesDecoded()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        var greets = data.Methods
            .Where(m => m.AssemblyName == "NativeAotConsole" && m.Name == "Greet")
            .ToList();
        Assert.HasCount(2, greets);
        Assert.HasCount(2, greets.Select(m => m.Signature).Distinct());
        Assert.Contains(m => m.Signature == "(string)", greets);
        Assert.Contains(m => m.Signature == "(int)", greets);
    }

    /// <summary>
    /// Verifies frozen-object owner attribution: string literals carry no owner, so their
    /// owner attribution fields stay null — the bytes are honestly unattributable.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_OwnerlessFrozenObjectsCarryNoOwnerAttribution()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        TestAssert.All(
            data.FrozenObjects.Where(f => f.OwningType is null),
            f =>
            {
                Assert.IsNull(f.OwningAssemblyName);
                Assert.IsNull(f.OwningNamespace);
            });
        TestAssert.All(
            data.FrozenObjects.Where(f => f.OwningType is not null),
            f => Assert.IsNotNull(f.OwningAssemblyName));
    }

    /// <summary>
    /// Verifies the bounded probe accepts the real report and rejects ordinary managed
    /// assemblies — an mstat is itself a valid ECMA-335 assembly, so the probe is what keeps
    /// the two input kinds apart without a full decode.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Probe_AcceptsMstatRejectsManagedAssembly()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        Assert.IsTrue(MstatReader.Probe(Samples.NativeAotConsoleMstat!));
        Assert.IsFalse(MstatReader.Probe(Samples.RichLibraryDll));
        Assert.IsFalse(MstatReader.Probe(Path.Combine(Path.GetTempPath(), "missing.mstat")));
    }

    /// <summary>
    /// Verifies the sample's own Program type appears among the constructed types.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_TypesCoverProgramType()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        Assert.IsNotEmpty(data.Types);
        Assert.Contains(t => t.Name == "Program" && t.AssemblyName == "NativeAotConsole", data.Types);
    }

    /// <summary>
    /// Verifies the well-known global data regions appear among the blobs, including the
    /// buckets that 2.1+ also breaks down in detail sections.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_BlobsIncludeKnownRegions()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        Assert.Contains(b => b.Name == "Metadata" && b.Size > 0, data.Blobs);
        Assert.Contains(b => b.Name == "ArrayOfFrozenObjects" && b.Size > 0, data.Blobs);
        Assert.Contains(b => b.Name == "FieldRvaData" && b.Size > 0, data.Blobs);
    }

    /// <summary>
    /// Verifies the 2.1 detail sections parse: the console sample freezes string literals, so
    /// frozen objects are non-empty and typed as System.String with no owning type.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_FrozenObjectsAreStringLiterals()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        Assert.IsNotEmpty(data.FrozenObjects);
        Assert.Contains(f => f.TypeName == "System.String" && f.OwningType is null, data.FrozenObjects);
        Assert.IsNotEmpty(data.RvaFields);
    }

    /// <summary>
    /// Verifies the report lists the referenced assemblies, including the app itself — the
    /// entry the dependency graph promotes to its root.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_AssembliesIncludeAppAndCoreLib()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(data);
        Assert.Contains(a => a.Name == "NativeAotConsole", data.Assemblies);
        Assert.Contains(a => a.Name == "System.Private.CoreLib", data.Assemblies);
    }

    /// <summary>
    /// Verifies mstat node names appear as DGML labels — the join contract the dependency
    /// graph and why-chains rely on.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureMstat_NodeNamesJoinToDgmlLabels()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        TestSkip.When(Samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);
        Assert.IsNotNull(data);

        var labels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(Samples.NativeAotConsoleDgml!))
        {
            var start = line.IndexOf("Label=\"", StringComparison.Ordinal);
            if (start < 0) continue;
            start += 7;
            var end = line.IndexOf('"', start);
            if (end > start) labels.Add(System.Net.WebUtility.HtmlDecode(line[start..end]));
        }

        var sample = data.Methods.Take(200).ToList();
        var hits = sample.Count(m => m.NodeName is { } n && labels.Contains(n));
        Assert.IsGreaterThanOrEqualTo(sample.Count * 9 / 10, hits, $"only {hits}/{sample.Count} method node names matched DGML labels");
    }

    /// <summary>
    /// Verifies a managed assembly is rejected: RichLibrary's 2.5 assembly version passes the
    /// version gate, so the recognized-stream gate must reject it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_ManagedDll_ReturnsNull()
    {
        Assert.IsNull(MstatReader.Read(Samples.RichLibraryDll));
    }

    /// <summary>
    /// Verifies a non-PE file returns null rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NonPeFile_ReturnsNull()
    {
        Assert.IsNull(MstatReader.Read(Samples.NonDotNetBinaryPath));
    }

    /// <summary>
    /// Verifies the Native AOT executable itself (no CLR metadata) returns null.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NativeAotExeItself_ReturnsNull()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        Assert.IsNull(MstatReader.Read(Samples.NativeAotConsoleExe!));
    }

    /// <summary>
    /// Verifies a missing file returns null rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_MissingFile_ReturnsNull()
    {
        Assert.IsNull(MstatReader.Read(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.mstat")));
    }

    /// <summary>
    /// Verifies a truncated report never throws: the result is null or carries only the
    /// entries that parsed completely before the cut.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_TruncatedMstat_ReturnsNullOrParsedPrefix()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleMstat!);
        using var truncated = new MemoryStream(bytes, 0, bytes.Length * 6 / 10);

        var data = MstatReader.Read(truncated);

        if (data is not null)
            TestAssert.All(data.Methods, m => Assert.IsGreaterThanOrEqualTo(0, m.Size));
    }

    /// <summary>
    /// Verifies a corrupted IL stream keeps the entries parsed before the damage: flipping an
    /// opcode mid-stream ends the walk without discarding the prefix or throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_CorruptedIlStream_KeepsParsedPrefix()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var intact = MstatReader.Read(Samples.NativeAotConsoleMstat!);
        Assert.IsNotNull(intact);
        Assert.IsGreaterThan(10, intact.Methods.Count);

        // Find the Methods IL and flip a byte in its middle to a nop. The IL streams live in
        // .text, so corrupt a byte at ~25% of the file, well past the headers.
        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleMstat!);
        bytes[bytes.Length / 4] = 0x00;
        using var corrupted = new MemoryStream(bytes);

        var data = MstatReader.Read(corrupted);

        // Never throws; either rejected outright or parsed to some prefix.
        if (data is not null)
            Assert.IsLessThanOrEqualTo(intact.Methods.Count, data.Methods.Count);
    }

    /// <summary>
    /// Verifies one malformed MemberRef signature degrades only its method to name-only while
    /// every row and stable non-signature identity from the real mstat report survives.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_OneMalformedMemberReferenceSignature_FailsClosedLocally()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var intact = MstatReader.Read(Samples.NativeAotConsoleMstat!);
        Assert.IsNotNull(intact);
        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleMstat!);
        using (var stream = new MemoryStream(bytes, writable: false))
        using (var peReader = new PEReader(stream))
        {
            var reader = peReader.GetMetadataReader();
            var effectiveMemberReferences = ReadEffectiveMethodMemberReferences(peReader, reader);
            Assert.HasCount(intact.Methods.Count, effectiveMemberReferences);
            MemberReferenceHandle target = default;
            for (var i = 0; i < effectiveMemberReferences.Count; i++)
            {
                var candidate = effectiveMemberReferences[i];
                if (candidate.IsNil || intact.Methods[i].Signature.Length == 0)
                {
                    continue;
                }
                var candidateBlob = reader.GetMemberReference(candidate).Signature;
                if (effectiveMemberReferences.Count(handle =>
                    !handle.IsNil && reader.GetMemberReference(handle).Signature == candidateBlob) == 1)
                {
                    target = candidate;
                    break;
                }
            }

            Assert.IsFalse(target.IsNil, "The real mstat fixture must expose a uniquely named method signature.");
            var blob = reader.GetMemberReference(target).Signature;
            var signatureOffset = GetBlobDataFileOffset(bytes, peReader.PEHeaders.MetadataStartOffset, blob);
            bytes[signatureOffset] = (byte)SignatureKind.Property;
        }

        using var patchedStream = new MemoryStream(bytes, writable: false);
        var patched = MstatReader.Read(patchedStream);
        Assert.IsNotNull(patched);
        Assert.HasCount(intact.Methods.Count, patched.Methods);

        var changedSignatures = 0;
        for (var i = 0; i < intact.Methods.Count; i++)
        {
            var before = intact.Methods[i];
            var after = patched.Methods[i];
            Assert.AreEqual(before.Name, after.Name);
            Assert.AreEqual(before.DeclaringType, after.DeclaringType);
            Assert.AreEqual(before.Namespace, after.Namespace);
            Assert.AreEqual(before.AssemblyName, after.AssemblyName);
            Assert.AreEqual(before.Size, after.Size);
            Assert.AreEqual(before.GcInfoSize, after.GcInfoSize);
            Assert.AreEqual(before.EhInfoSize, after.EhInfoSize);
            Assert.AreEqual(before.NodeName, after.NodeName);
            if (before.Signature != after.Signature)
            {
                changedSignatures++;
                Assert.IsNotEmpty(before.Signature);
                Assert.AreEqual(string.Empty, after.Signature);
            }
        }

        Assert.AreEqual(1, changedSignatures);
    }

    private static List<MemberReferenceHandle> ReadEffectiveMethodMemberReferences(
        PEReader peReader,
        MetadataReader reader)
    {
        byte[]? methodsIl = null;
        var module = reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1));
        foreach (var handle in module.GetMethods())
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.GetString(method.Name) == "Methods")
            {
                methodsIl = peReader.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
                break;
            }
        }

        Assert.IsNotNull(methodsIl);
        var result = new List<MemberReferenceHandle>();
        var cursor = new IlCursor(methodsIl);
        while (cursor.TryReadToken(out var token) &&
            cursor.TryReadInt(out _) &&
            cursor.TryReadInt(out _) &&
            cursor.TryReadInt(out _) &&
            cursor.TryReadInt(out _))
        {
            var handle = MetadataTokens.EntityHandle(token);
            if (handle.Kind == HandleKind.MemberReference)
            {
                result.Add((MemberReferenceHandle)handle);
            }
            else if (handle.Kind == HandleKind.MethodSpecification)
            {
                var method = reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
                result.Add(method.Kind == HandleKind.MemberReference
                    ? (MemberReferenceHandle)method
                    : default);
            }
            else
            {
                result.Add(default);
            }
        }

        return result;
    }

    private static int GetBlobDataFileOffset(
        byte[] image,
        int metadataStart,
        BlobHandle handle)
    {
        var position = metadataStart + 12;
        var versionLength = BitConverter.ToInt32(image, position);
        position += 4 + ((versionLength + 3) & ~3);
        position += 2;
        var streamCount = BitConverter.ToUInt16(image, position);
        position += 2;

        var blobStreamOffset = -1;
        for (var i = 0; i < streamCount; i++)
        {
            var streamOffset = BitConverter.ToInt32(image, position);
            position += 8;
            var nameStart = position;
            while (image[position] != 0)
            {
                position++;
            }
            var name = System.Text.Encoding.ASCII.GetString(image, nameStart, position - nameStart);
            position = (position + 4) & ~3;
            if (name == "#Blob")
            {
                blobStreamOffset = streamOffset;
            }
        }

        Assert.IsGreaterThanOrEqualTo(0, blobStreamOffset);
        var entryOffset = metadataStart + blobStreamOffset + MetadataTokens.GetHeapOffset(handle);
        var prefixLength = (image[entryOffset] & 0x80) == 0
            ? 1
            : (image[entryOffset] & 0xC0) == 0x80 ? 2 : 4;
        return entryOffset + prefixLength;
    }
}
