using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for IL disassembly and opcode searching.
/// </summary>
[McpServerToolType]
public sealed partial class IlTools(DotsiderSessionManager sessionManager, ILogger<IlTools> logger)
{
    /// <summary>
    /// Disassembles a method's IL bytecode into human-readable instructions.
    /// </summary>
    /// <param name="typeName">Full or partial declaring type name (matched with EndsWith).</param>
    /// <param name="methodName">Exact method name (case-insensitive).</param>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with method metadata and IL instruction listing.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> DisassembleMethod(
        string typeName,
        string methodName,
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var disassembler = new IlDisassembler(analyzer);

            var method = analyzer.MethodDefs.FirstOrDefault(m =>
                m.DeclaringType.EndsWith(typeName, StringComparison.OrdinalIgnoreCase)
                && m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

            if (method is null)
                return $"Error: Method not found: {typeName}.{methodName}";

            var instructions = disassembler.Disassemble(method);
            return JsonSerializer.Serialize(new { Method = method, Instructions = instructions },
                DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "disassemble", TypeName = typeName, MethodName = methodName }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Searches for methods containing specific IL opcodes.
    /// </summary>
    /// <param name="query">Opcode to search for (e.g., 'call', 'newobj', 'throw').</param>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of methods with matching IL instructions.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> SearchIlOpcodes(
        string query,
        string? assemblyPath = null,
        int? sessionId = null,
        int? maxResults = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var disassembler = new IlDisassembler(analyzer);
            var max = maxResults ?? 50;
            var results = new List<object>();

            foreach (var method in analyzer.MethodDefs)
            {
                if (results.Count >= max) break;
                try
                {
                    var instructions = disassembler.Disassemble(method);
                    var matches = instructions.Where(i =>
                        i.OpCode.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count > 0)
                        results.Add(new { Method = $"{method.DeclaringType}.{method.Name}", Matches = matches });
                }
                catch (Exception ex)
                {
                    LogSkipMethod(logger, ex, method.DeclaringType, method.Name);
                }
            }

            return JsonSerializer.Serialize(results, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "search-il-opcodes", Query = query, MaxResults = maxResults }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping method {Type}.{Method} — cannot disassemble")]
    private static partial void LogSkipMethod(ILogger logger, Exception exception, string type, string method);
}
