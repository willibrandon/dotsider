using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Adds the runtime mstat writer's read-only <c>.names</c> section to a managed PE image.
/// </summary>
internal sealed class SyntheticMstat22PeBuilder : ManagedPEBuilder
{
    private readonly BlobBuilder _names;

    /// <summary>
    /// Initializes a managed PE builder with a custom serialized-names section.
    /// </summary>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="ilStream">The global-method body stream.</param>
    /// <param name="names">The serialized dependency-node names.</param>
    internal SyntheticMstat22PeBuilder(
        MetadataBuilder metadata,
        BlobBuilder ilStream,
        BlobBuilder names)
        : base(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream)
    {
        _names = names;
    }

    /// <inheritdoc/>
    protected override ImmutableArray<Section> CreateSections() =>
        base.CreateSections().Add(new Section(".names", SectionCharacteristics.MemRead));

    /// <inheritdoc/>
    protected override BlobBuilder SerializeSection(string name, SectionLocation location) =>
        name == ".names" ? _names : base.SerializeSection(name, location);
}
