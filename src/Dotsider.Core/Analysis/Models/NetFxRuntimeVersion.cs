namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// .NET Framework CLR version a <see cref="NetFxBindingContext"/> targets. The CLR version (not
/// the product TFM) drives the binding pipeline because the GAC layout, machine.config path,
/// framework runtime directory, reference-assemblies tree, and <c>appliesTo</c> filter all switch
/// on the CLR generation: <see cref="Clr2"/> covers .NET Framework 2.0 / 3.0 / 3.5 SP1 (process
/// runs on <c>v2.0.50727</c>); <see cref="Clr4"/> covers .NET Framework 4.0 through 4.8.x
/// (process runs on <c>v4.0.30319</c>).
/// </summary>
public enum NetFxRuntimeVersion
{
    /// <summary>
    /// CLR 2.0 generation — .NET Framework 2.0, 3.0, 3.5 SP1. Binds out of
    /// <c>%WINDIR%\assembly\GAC*</c> with token format <c>&lt;version&gt;__&lt;pkt&gt;</c> and
    /// reads <c>%WINDIR%\Microsoft.NET\Framework[64]\v2.0.50727</c>.
    /// </summary>
    Clr2,

    /// <summary>
    /// CLR 4 generation — .NET Framework 4.0 through 4.8.x. Binds out of
    /// <c>%WINDIR%\Microsoft.NET\assembly\GAC_*</c> with token format
    /// <c>v4.0_&lt;version&gt;__&lt;pkt&gt;</c> and reads
    /// <c>%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319</c>.
    /// </summary>
    Clr4,
}
