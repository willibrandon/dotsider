using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;

namespace Dotsider.Infrastructure;

/// <summary>
/// Assembly references and their resolved dependency graph.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliDependenciesPayload(
    IReadOnlyList<AssemblyRefInfo> AssemblyRefs,
    DependencyGraphPayload Graph);
