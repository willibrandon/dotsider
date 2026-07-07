namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Why a managed method does or does not have inspectable ReadyToRun native code. Distinguishing
/// these keeps a correlation report honest — a missing composite or unresolved component metadata
/// is not the same as a genuinely IL-only method.
/// </summary>
public enum ReadyToRunNativeAvailability
{
    /// <summary>The method has a precompiled native body that can be shown.</summary>
    Precompiled,

    /// <summary>The method is genuinely IL-only — not precompiled in this image.</summary>
    NotPrecompiled,

    /// <summary>The method belongs to a component whose owner composite executable is not on disk.</summary>
    OwnerCompositeMissing,

    /// <summary>The owning component's metadata could not be resolved by name and MVID.</summary>
    ComponentMetadataUnavailable,

    /// <summary>The method is precompiled, but the image architecture could not be identified.</summary>
    ArchUnsupported,
}
