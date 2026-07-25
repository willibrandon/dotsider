namespace Dotsider.Core.Analysis;

/// <summary>
/// Identifies a native PDB through a PE CodeView record.
/// </summary>
/// <param name="Guid">The PDB GUID.</param>
/// <param name="Age">The PDB age.</param>
/// <param name="PdbPath">The path recorded in the PE image.</param>
internal readonly record struct CodeViewId(Guid Guid, int Age, string PdbPath);
