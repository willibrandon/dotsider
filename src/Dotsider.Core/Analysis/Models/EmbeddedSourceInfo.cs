namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Embedded source decoded from a portable PDB document.
/// </summary>
/// <param name="Document">The PDB document path.</param>
/// <param name="Text">The decoded source text.</param>
/// <param name="Bytes">The decoded source bytes.</param>
public sealed record EmbeddedSourceInfo(string Document, string Text, byte[] Bytes);
