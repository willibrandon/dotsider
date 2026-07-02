namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single entry in the PE export table.
/// </summary>
/// <param name="Ordinal">The biased export ordinal (ordinal base applied).</param>
/// <param name="Name">The exported name, or null for ordinal-only exports.</param>
/// <param name="Rva">The RVA of the exported symbol, or of the forwarder string.</param>
/// <param name="ForwardedTo">
/// The forwarder target (e.g. "NTDLL.RtlAllocateHeap") when the export forwards to
/// another module, or null for regular exports.
/// </param>
public sealed record ExportedFunctionInfo(
    int Ordinal, string? Name, int Rva, string? ForwardedTo);
