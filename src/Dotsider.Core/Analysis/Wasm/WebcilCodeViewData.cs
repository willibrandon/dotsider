namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// Carries CodeView portable-PDB identity decoded from a Webcil debug entry.
/// The GUID and age match the sidecar PDB identity that System.Reflection.Metadata expects.
/// The path is the original build-time PDB path and is only used to derive local sidecar probes.
/// </summary>
/// <param name="Guid">The portable PDB signature GUID.</param>
/// <param name="Age">The CodeView age value.</param>
/// <param name="Path">The build-time PDB path recorded in the CodeView payload.</param>
internal readonly record struct WebcilCodeViewData(Guid Guid, int Age, string Path);
