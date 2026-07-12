using System.Reflection.Metadata;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Walks an <c>InstanceMethodEntryPoints</c> method signature (the crossgen2 encoding) to recover
/// the owning MethodDef token and a rendered instantiation, and — critically — to advance to the
/// runtime-function index that follows it so the entry point can be marked. Signatures use the
/// ECMA compressed-integer codec and a recursive type grammar; every form is walked so the offset
/// lands correctly even when a shape is only summarized for display.
/// </summary>
internal static class ReadyToRunSignatureWalker
{
    /// <summary>Walks the method signature at <paramref name="offset"/>.</summary>
    /// <param name="reader">The image reader.</param>
    /// <param name="offset">The file offset of the signature.</param>
    /// <param name="metadata">The metadata reader for resolving token names, or null.</param>
    /// <param name="moduleMetadata">Resolves a ReadyToRun module override index to metadata, or null when unavailable.</param>
    /// <param name="systemMetadata">
    /// The system module metadata used by composite primitive-owner signatures without a module override.
    /// </param>
    /// <returns>The recovered method signature information.</returns>
    public static ReadyToRunMethodSignature ParseMethod(
        R2RNativeReader reader,
        int offset,
        MetadataReader? metadata,
        Func<int, MetadataReader?>? moduleMetadata = null,
        MetadataReader? systemMetadata = null)
    {
        var walker = new ReadyToRunSignatureWalkerState(
            reader, offset, metadata, moduleMetadata, systemMetadata);
        walker.ParseMethod();
        return new ReadyToRunMethodSignature(
            walker.Offset, walker.MethodToken, walker.RenderInstantiation(), walker.CrossModule, walker.ModuleIndex);
    }
}
