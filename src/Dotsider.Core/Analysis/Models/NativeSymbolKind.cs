namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// What a native symbol represents. Native AOT binaries carry compiler-generated code and data
/// symbols beyond ordinary functions; this classification drives the Size Map's category
/// grouping and the symbol view's presentation.
/// </summary>
public enum NativeSymbolKind
{
    /// <summary>A compiled method body.</summary>
    Function,

    /// <summary>A type's runtime MethodTable (vtable) — Windows <c>??_7…@@6B@</c> / Unix <c>_ZTV…</c>.</summary>
    MethodTable,

    /// <summary>A frozen (compile-time allocated) object, most often a string literal (<c>__Str_…</c>).</summary>
    FrozenObject,

    /// <summary>A generic dictionary or an unboxing/other compiler stub.</summary>
    Stub,

    /// <summary>A generic dictionary blob (<c>__GenericDict_…</c>).</summary>
    GenericDictionary,

    /// <summary>Static field storage (GC, non-GC, or thread statics).</summary>
    Statics,

    /// <summary>Other named data (readonly/writable data and the like).</summary>
    Data,

    /// <summary>A nameless function boundary recovered from unwind data when no symbols exist.</summary>
    Boundary
}
