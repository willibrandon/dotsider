using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tool for ReadyToRun correlation: joins a crossgen2 image's managed methods to their
/// precompiled native bodies and reports a method's IL beside its native code.
/// </summary>
[McpServerToolType]
public sealed partial class ReadyToRunTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Correlates one method of a ReadyToRun image with its precompiled native code, returning the
    /// method's availability, code ranges and sizes, IL listing, and native disassembly. Identify
    /// the method by name (optionally <c>Type.Method</c>), by a <c>0x06…</c> MethodDef token, or by
    /// a <c>0x…</c> native address. An ambiguous query lists every candidate instead of guessing.
    /// </summary>
    /// <param name="methodOrAddress">A method name, a qualified <c>Type.Method</c>, a <c>0x06…</c> token, or a <c>0x…</c> native address.</param>
    /// <param name="assemblyPath">Path to the ReadyToRun image.</param>
    /// <param name="sessionId">PID of a running dotsider instance to correlate within instead.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with the correlation report, or an error for a miss or a non-ReadyToRun image.</returns>
    /// <exception cref="McpException">Thrown when the query is ambiguous, listing the candidates.</exception>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> CorrelateR2rMethod(
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
            if (analyzer.BinaryKind != BinaryKind.ReadyToRun)
                return "Error: correlate_r2r_method requires a ReadyToRun image.";

            var result = ReadyToRunCorrelationQuery.Resolve(analyzer, methodOrAddress, ct);
            return result.Outcome switch
            {
                ReadyToRunQueryOutcome.Resolved =>
                    JsonSerializer.Serialize(result.Report, DotsiderJsonOptions.Default),
                ReadyToRunQueryOutcome.Ambiguous =>
                    throw new McpException($"{result.Message}: " + string.Join("; ", result.Candidates.Select(c =>
                        $"{c.AssemblyName} {c.DeclaringType}::{c.Name} token 0x{c.Token:X8}"
                        + (c.VirtualAddress is { } va ? $" @ 0x{va:X}" : "")))),
                _ => $"Error: {result.Message}"
            };
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value).SendAndUnwrapAsync(
                new DotsiderRequest { Method = "r2r-correlate", MethodOrAddress = methodOrAddress }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
