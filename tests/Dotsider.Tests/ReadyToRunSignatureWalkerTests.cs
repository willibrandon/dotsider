using Dotsider.Core.Analysis.ReadyToRun;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Synthetic ReadyToRun signature walker regressions for metadata-scope transitions.
/// </summary>
[TestClass]
public class ReadyToRunSignatureWalkerTests
{
    /// <summary>
    /// Verifies a module-wrapped generic-instantiation type decodes its generic type in the referenced
    /// module while decoding type arguments in the outer signature scope, matching the runtime reader.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ModuleZapsig_GenericInstantiation_ParsesArgumentsInOuterMetadataScope()
    {
        using var outer = new MetadataScope(BuildAssembly("Outer", "CurrentGeneric", "OuterArg"));
        using var module = new MetadataScope(BuildAssembly("Module", "ExternalGeneric", "WrongArg"));

        byte[] signature =
        [
            0x04,       // READYTORUN_METHOD_SIG_MethodInstantiation
            0x01,       // MethodDef rid 1
            0x01,       // one method-instantiation argument
            0x3f, 0x02, // MODULE_ZAPSIG module 2
            0x15,       // GENERICINST
            0x12, 0x08, // CLASS TypeDef rid 2 => ExternalGeneric in module scope
            0x01,       // one generic type argument
            0x12, 0x0c  // CLASS TypeDef rid 3 => OuterArg in outer scope
        ];

        var sig = ReadyToRunSignatureWalker.ParseMethod(
            new R2RNativeReader(signature), 0, outer.Reader, i => i == 2 ? module.Reader : null);

        Assert.AreEqual(signature.Length, sig.Offset);
        Assert.AreEqual(0x0600_0001, sig.MethodToken);
        Assert.IsTrue(sig.CrossModule);
        Assert.AreEqual(2, sig.ModuleIndex);
        Assert.AreEqual("<ExternalGeneric<OuterArg>>", sig.InstantiationDisplay);
    }

    /// <summary>
    /// Verifies unresolved module-zapsig types are not resolved against the outer metadata by accident.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ModuleZapsig_UnresolvedModule_DoesNotResolveAgainstOuterMetadata()
    {
        using var outer = new MetadataScope(BuildAssembly("Outer", "WrongType"));

        byte[] signature =
        [
            0x04,       // READYTORUN_METHOD_SIG_MethodInstantiation
            0x01,       // MethodDef rid 1
            0x01,       // one method-instantiation argument
            0x3f, 0x07, // MODULE_ZAPSIG module 7, intentionally unresolved
            0x12, 0x08  // CLASS TypeDef rid 2; this is WrongType in the outer metadata
        ];

        var sig = ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, outer.Reader);

        Assert.AreEqual("<Type>", sig.InstantiationDisplay);
        Assert.AreEqual(7, sig.ModuleIndex);
        Assert.IsTrue(sig.CrossModule);
    }

    private static byte[] BuildAssembly(string assemblyName, params string[] typeNames)
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString(assemblyName),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddModule(
            0,
            metadata.GetOrAddString(assemblyName + ".dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddTypeDefinition(
            default,
            default,
            metadata.GetOrAddString("<Module>"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));

        foreach (var typeName in typeNames)
        {
            metadata.AddTypeDefinition(
                TypeAttributes.Public,
                metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString(typeName),
                baseType: default,
                fieldList: MetadataTokens.FieldDefinitionHandle(1),
                methodList: MetadataTokens.MethodDefinitionHandle(1));
        }

        var pe = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream: new BlobBuilder());
        var blob = new BlobBuilder();
        pe.Serialize(blob);
        return blob.ToArray();
    }

    private sealed class MetadataScope : IDisposable
    {
        private readonly MemoryStream _stream;
        private readonly PEReader _reader;

        public MetadataScope(byte[] assemblyBytes)
        {
            _stream = new MemoryStream(assemblyBytes);
            _reader = new PEReader(_stream);
            Reader = _reader.GetMetadataReader();
        }

        public MetadataReader Reader { get; }

        public void Dispose()
        {
            _reader.Dispose();
            _stream.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
