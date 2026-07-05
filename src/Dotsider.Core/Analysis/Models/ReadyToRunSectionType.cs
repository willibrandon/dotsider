namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The <c>ReadyToRunSectionType</c> ids from a crossgen2 image's <c>READYTORUN_SECTION</c> table
/// (<c>readytorun.h</c>). Distinct from the Native AOT module-section ids (200–399) that
/// <see cref="RtrSection"/> names; a classic R2R section table uses these 100-range ids with a
/// 12-byte <c>{Type, RVA, Size}</c> row layout.
/// </summary>
public enum ReadyToRunSectionType
{
    /// <summary>Compiler identifier string.</summary>
    CompilerIdentifier = 100,

    /// <summary>Import sections describing lazily-resolved fixup cells.</summary>
    ImportSections = 101,

    /// <summary>The runtime-function (pdata-style) table of precompiled code ranges.</summary>
    RuntimeFunctions = 102,

    /// <summary>A NativeArray mapping each MethodDef rid to its first runtime function.</summary>
    MethodDefEntryPoints = 103,

    /// <summary>Per-method exception-handling clause info.</summary>
    ExceptionInfo = 104,

    /// <summary>Per-method debug info (bounds and variable locations).</summary>
    DebugInfo = 105,

    /// <summary>Delay-load method call thunks.</summary>
    DelayLoadMethodCallThunks = 106,

    /// <summary>A NativeHashtable of the types available in this image.</summary>
    AvailableTypes = 108,

    /// <summary>A NativeHashtable mapping instantiated generic methods to runtime functions.</summary>
    InstanceMethodEntryPoints = 109,

    /// <summary>Inlining info (deprecated form).</summary>
    InliningInfo = 110,

    /// <summary>Profile (PGO) data info.</summary>
    ProfileDataInfo = 111,

    /// <summary>Manifest metadata blob listing the version-bubble assembly references.</summary>
    ManifestMetadata = 112,

    /// <summary>Custom-attribute presence bitmap.</summary>
    AttributePresence = 113,

    /// <summary>Inlining info (current form).</summary>
    InliningInfo2 = 114,

    /// <summary>The composite image's component-assembly table.</summary>
    ComponentAssemblies = 115,

    /// <summary>The filename of the owner composite executable that holds a component's code.</summary>
    OwnerCompositeExecutable = 116,

    /// <summary>PGO instrumentation data.</summary>
    PgoInstrumentationData = 117,

    /// <summary>MVIDs for the manifest assemblies, used to validate component identity.</summary>
    ManifestAssemblyMvids = 118,

    /// <summary>Cross-module inline info.</summary>
    CrossModuleInlineInfo = 119,

    /// <summary>Hot/cold runtime-function pairs for split method bodies.</summary>
    HotColdMap = 120,

    /// <summary>Map of which methods are generic.</summary>
    MethodIsGenericMap = 121,

    /// <summary>Map of enclosing types.</summary>
    EnclosingTypeMap = 122,

    /// <summary>Map of per-type generic info.</summary>
    TypeGenericInfoMap = 123,
}
