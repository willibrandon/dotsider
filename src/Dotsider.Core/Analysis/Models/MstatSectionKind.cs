namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The report section a normalized <see cref="MstatSizeEntry"/> came from. Each section has its
/// own identity key shape and attribution rules; see <see cref="Dotsider.Core.Analysis.MstatSizeIndex"/>.
/// </summary>
public enum MstatSectionKind
{
    /// <summary>A compiled method body (code + GC info + EH info bytes).</summary>
    Method,

    /// <summary>A constructed type's MethodTable data.</summary>
    MethodTable,

    /// <summary>A named global data region.</summary>
    Blob,

    /// <summary>An object frozen into the image at compile time.</summary>
    FrozenObject,

    /// <summary>A field's RVA data mapped into the image.</summary>
    RvaField,

    /// <summary>An embedded manifest resource.</summary>
    Resource
}
