namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests that guard the advertised MCP tool surface and its metadata quality.
/// </summary>
public class ToolRegistrationTests : McpServerTestBase
{
    /// <summary>
    /// Every expected tool across the suite is present in ListTools output.
    /// </summary>
    [Fact]
    public async Task ListTools_ReturnsAllRegisteredTools()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);

        var names = tools.Select(t => t.Name).ToList();

        // Assembly tools
        Assert.Contains("get_assembly_info", names);
        Assert.Contains("list_types", names);
        Assert.Contains("list_methods", names);
        Assert.Contains("find_members", names);

        // IL tools
        Assert.Contains("disassemble_method", names);
        Assert.Contains("get_method_debug_info", names);
        Assert.Contains("get_source_link", names);
        Assert.Contains("search_il_opcodes", names);

        // Metadata tools
        Assert.Contains("get_pe_headers", names);
        Assert.Contains("get_clr_header", names);
        Assert.Contains("get_sections", names);
        Assert.Contains("get_custom_attributes", names);
        Assert.Contains("get_resources", names);
        Assert.Contains("resolve_token", names);

        // String tools
        Assert.Contains("extract_strings", names);

        // Dependency tools
        Assert.Contains("get_assembly_refs", names);
        Assert.Contains("get_dependency_graph", names);
        Assert.Contains("get_type_refs", names);

        // Size tools
        Assert.Contains("get_size_breakdown", names);
        Assert.Contains("get_largest_methods", names);

        // Symbol tools
        Assert.Contains("get_native_symbols", names);
        Assert.Contains("get_native_disassembly", names);

        // Native AOT tools
        Assert.Contains("get_native_aot_info", names);
        Assert.Contains("list_native_aot_sections", names);
        Assert.Contains("get_native_aot_size_contributors", names);
        Assert.Contains("explain_native_aot_size", names);

        // Correlation tools
        Assert.Contains("correlate_method", names);
        Assert.Contains("correlate_r2r_method", names);

        // Diff tools
        Assert.Contains("diff_assemblies", names);
        Assert.Contains("diff_size", names);
        Assert.Contains("check_size_budgets", names);

        // NuGet tools
        Assert.Contains("analyze_nupkg", names);

        // Session tools
        Assert.Contains("discover_dotsider_sessions", names);
        Assert.Contains("get_session_info", names);

        // Trace tools (session-only)
        Assert.Contains("get_trace_events", names);
        Assert.Contains("get_trace_counters", names);
        Assert.Contains("get_process_output", names);
        Assert.Contains("start_trace", names);
        Assert.Contains("stop_trace", names);

        // Navigation tools (session-only)
        Assert.Contains("get_current_view", names);
        Assert.Contains("navigate_to", names);
        Assert.Contains("capture_screen", names);
        Assert.Contains("navigate_to_il_definition", names);
        Assert.Contains("navigate_back", names);
        Assert.Contains("push_assembly", names);

        // Bundle tools
        Assert.Contains("get_bundle_info", names);
        Assert.Contains("list_bundle_entries", names);

        // Runtime tools
        Assert.Contains("find_framework_assembly", names);
        Assert.Contains("resolve_assembly", names);

        // Field tools
        Assert.Contains("list_fields", names);
    }

    /// <summary>
    /// No registered tool ships without a human-readable description, which MCP clients rely on.
    /// </summary>
    [Fact]
    public async Task AllTools_HaveDescriptions()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);

        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description),
                $"Tool '{tool.Name}' should have a description");
        }
    }
}
