namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Identifies which layer of .NET Framework binding policy rewrote a requested assembly
/// identity. The CLR walks app config first, then publisher policy (unless bypassed by
/// <c>&lt;publisherPolicy apply="no"/&gt;</c>), then machine.config; later layers override
/// earlier ones, so the effective winner is machine.config &gt; publisher &gt; app &gt;
/// framework unification.
/// </summary>
public enum PolicyLayer
{
    /// <summary>The CLR's built-in unification of well-known framework public key tokens.</summary>
    FrameworkUnification,

    /// <summary>
    /// A redirect declared in the architecture-correct
    /// <c>%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319\Config\machine.config</c>.
    /// </summary>
    MachineConfig,

    /// <summary>
    /// A redirect declared in a GAC-resident
    /// <c>policy.&lt;major&gt;.&lt;minor&gt;.&lt;simpleName&gt;</c> publisher-policy assembly.
    /// </summary>
    PublisherPolicy,

    /// <summary>A redirect declared in the application's <c>*.exe.config</c>/<c>*.dll.config</c>.</summary>
    AppConfig,

    /// <summary>
    /// The effective identity was anchored by a <c>&lt;codeBase&gt;</c> element rather than a
    /// version redirect — codeBase entries can come from any policy layer above.
    /// </summary>
    CodeBase,
}
