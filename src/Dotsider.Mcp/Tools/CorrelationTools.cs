using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tool for pre-ILC correlation: joins a Native AOT binary to the managed assembly it was
/// compiled from and reports a method's pre-ILC IL beside its native code.
/// </summary>
[McpServerToolType]
public sealed partial class CorrelationTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Correlates one method of a Native AOT binary with its pre-ILC managed source, returning
    /// the method's status, native symbols and sizes, IL listing, and correlation-aware native
    /// disassembly. Identify the method by name (optionally <c>Type.Method</c>) or by a <c>0x…</c>
    /// native address. An ambiguous name (overloads) lists every candidate instead of guessing.
    /// </summary>
    /// <param name="methodOrAddress">A method name, a qualified <c>Type.Method</c>, or a <c>0x…</c> native address.</param>
    /// <param name="assemblyPath">Path to the Native AOT binary. Its pre-ILC assembly is opened from the build tree.</param>
    /// <param name="sessionId">PID of a running dotsider instance to correlate within instead.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with the correlation report, or an error for a miss or unavailable index.</returns>
    /// <exception cref="McpException">Thrown when the name is ambiguous, listing the candidates.</exception>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> CorrelateMethod(
        string methodOrAddress,
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            if (string.IsNullOrWhiteSpace(methodOrAddress))
                throw new McpException("methodOrAddress is required.");

            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            if (analyzer.BinaryKind != BinaryKind.NativeAot)
                return "Error: correlate_method requires a Native AOT binary.";

            var result = CorrelationQuery.Resolve(analyzer, methodOrAddress, ct);
            return result.Outcome switch
            {
                CorrelationQueryOutcome.Resolved =>
                    McpJson.Serialize(result.Report),
                CorrelationQueryOutcome.Ambiguous =>
                    throw new McpException($"{result.Message}: " + string.Join("; ", result.Candidates.Select(c =>
                        $"{c.AssemblyName} {c.DeclaringType}::{c.Name} token 0x{c.Token:X8}"
                        + (c.VirtualAddress is { } va ? $" @ 0x{va:X}" : "")))),
                _ => $"Error: {result.Message}"
            };
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value).SendAndUnwrapAsync(
                new DotsiderRequest { Method = "correlate-method", MethodOrAddress = methodOrAddress }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
