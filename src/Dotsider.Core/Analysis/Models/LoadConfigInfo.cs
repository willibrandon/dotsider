namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Parsed PE load configuration directory. Pointer-width fields are widened to
/// <see cref="ulong"/> so a single record covers PE32 and PE32+ images. Fields
/// beyond the directory's declared size are zero — real-world load configs are
/// truncated at many historical lengths.
/// </summary>
/// <param name="Size">The declared size of the load configuration directory.</param>
/// <param name="TimeDateStamp">The directory timestamp.</param>
/// <param name="MajorVersion">Major version number.</param>
/// <param name="MinorVersion">Minor version number.</param>
/// <param name="DependentLoadFlags">Default load flags applied when resolving DLL dependencies.</param>
/// <param name="SecurityCookie">The VA of the /GS security cookie, or 0 when absent.</param>
/// <param name="SehHandlerCount">Number of registered structured exception handlers (PE32 /SAFESEH).</param>
/// <param name="GuardCfCheckFunctionPointer">The VA of the Control Flow Guard check-function pointer, or 0.</param>
/// <param name="GuardCfFunctionCount">Number of entries in the Control Flow Guard function table.</param>
/// <param name="GuardFlags">Raw Control Flow Guard flags.</param>
/// <param name="GuardFlagsDescription">Decoded <paramref name="GuardFlags"/> summary, or "(none)".</param>
public sealed record LoadConfigInfo(
    uint Size,
    uint TimeDateStamp,
    ushort MajorVersion,
    ushort MinorVersion,
    ushort DependentLoadFlags,
    ulong SecurityCookie,
    ulong SehHandlerCount,
    ulong GuardCfCheckFunctionPointer,
    ulong GuardCfFunctionCount,
    uint GuardFlags,
    string GuardFlagsDescription);
