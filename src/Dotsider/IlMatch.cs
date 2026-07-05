using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider;

/// <summary>
/// Represents a single text match occurrence within a method's IL disassembly.
/// </summary>
/// <param name="Method">The method containing this match.</param>
/// <param name="Line">The 1-based line number within the disassembly text.</param>
/// <param name="Column">The 1-based column number within the line.</param>
/// <param name="Length">The length of the matched text.</param>
/// <param name="Owner">The pre-ILC local-reference assembly that owns the method, or null for the routed root.</param>
public sealed record IlMatch(MethodDefInfo Method, int Line, int Column, int Length, AssemblyAnalyzer? Owner = null);
