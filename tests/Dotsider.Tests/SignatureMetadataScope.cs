using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Owns a minimal in-memory metadata image containing arbitrary signature blobs and TypeSpec rows.
/// </summary>
internal sealed class SignatureMetadataScope : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly PEReader _peReader;

    private SignatureMetadataScope(
        MemoryStream stream,
        PEReader peReader,
        MetadataReader reader,
        IReadOnlyList<BlobHandle> blobs,
        IReadOnlyList<TypeSpecificationHandle> typeSpecifications)
    {
        _stream = stream;
        _peReader = peReader;
        Reader = reader;
        Blobs = blobs;
        TypeSpecifications = typeSpecifications;
    }

    /// <summary>Gets the metadata reader for the synthetic image.</summary>
    public MetadataReader Reader { get; }

    /// <summary>Gets the arbitrary blob handles in the order supplied to <see cref="Create"/>.</summary>
    public IReadOnlyList<BlobHandle> Blobs { get; }

    /// <summary>Gets the TypeSpec handles in the order supplied to <see cref="Create"/>.</summary>
    public IReadOnlyList<TypeSpecificationHandle> TypeSpecifications { get; }

    /// <summary>
    /// Creates a minimal metadata image containing the requested blobs and TypeSpec signatures.
    /// </summary>
    /// <param name="blobs">Arbitrary blobs exposed through <see cref="Blobs"/>.</param>
    /// <param name="typeSpecifications">Signature blobs used to create TypeSpec rows.</param>
    /// <returns>A disposable scope over the serialized metadata image.</returns>
    public static SignatureMetadataScope Create(
        IReadOnlyList<byte[]>? blobs = null,
        IReadOnlyList<byte[]>? typeSpecifications = null)
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString("SignatureTests"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        var module = metadata.AddModule(
            0,
            metadata.GetOrAddString("SignatureTests.dll"),
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
        metadata.AddTypeReference(
            module,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("ReferencedType"));

        var blobHandles = new List<BlobHandle>(blobs?.Count ?? 0);
        if (blobs is not null)
        {
            foreach (var blob in blobs)
            {
                blobHandles.Add(metadata.GetOrAddBlob(blob));
            }
        }

        var typeSpecificationHandles = new List<TypeSpecificationHandle>(typeSpecifications?.Count ?? 0);
        if (typeSpecifications is not null)
        {
            foreach (var signature in typeSpecifications)
            {
                typeSpecificationHandles.Add(metadata.AddTypeSpecification(metadata.GetOrAddBlob(signature)));
            }
        }

        var peBuilder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream: new BlobBuilder());
        var image = new BlobBuilder();
        peBuilder.Serialize(image);

        var stream = new MemoryStream(image.ToArray(), writable: false);
        var peReader = new PEReader(stream);
        return new SignatureMetadataScope(
            stream,
            peReader,
            peReader.GetMetadataReader(),
            blobHandles,
            typeSpecificationHandles);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _peReader.Dispose();
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
