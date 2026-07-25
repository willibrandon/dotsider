using Dotsider.Infrastructure;
using System.CommandLine;
using System.Diagnostics;

namespace Dotsider.Commands;

/// <summary>
/// Agent command group: MCP server launch and AI skill file initialization.
/// </summary>
internal static class AgentCommand
{
    private static readonly Dictionary<string, string> s_providerPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = Path.Combine(".claude", "skills", "dotsider", "SKILL.md"),
        ["gemini"] = Path.Combine(".gemini", "skills", "dotsider", "SKILL.md"),
        ["copilot"] = Path.Combine(".github", "skills", "dotsider", "SKILL.md"),
        ["cursor-agent"] = Path.Combine(".cursor", "skills", "dotsider", "SKILL.md"),
        ["opencode"] = Path.Combine(".opencode", "skill", "dotsider", "SKILL.md"),
        ["codex"] = Path.Combine(".agents", "skills", "dotsider", "SKILL.md"),
        ["windsurf"] = Path.Combine(".windsurf", "skills", "dotsider", "SKILL.md"),
        ["kilocode"] = Path.Combine(".kilocode", "skills", "dotsider", "SKILL.md"),
        ["amp"] = Path.Combine(".agents", "skills", "dotsider", "SKILL.md"),
        ["qwen"] = Path.Combine(".qwen", "skills", "dotsider", "SKILL.md"),
    };

    /// <summary>
    /// Creates the "agent" command with "mcp" and "init" subcommands.
    /// </summary>
    public static Command Create(Option<bool> jsonOption)
    {
        var command = new Command("agent", "MCP server and AI skill file management");

        command.Subcommands.Add(CreateMcpCommand());
        command.Subcommands.Add(CreateInitCommand(jsonOption));

        return command;
    }

    private static Command CreateMcpCommand()
    {
        var command = new Command("mcp", "Launch the dotsider MCP server");

        command.SetAction(async (_, ct) =>
        {
            // Look for dotsider-mcp in PATH or alongside this executable
            var candidates = new List<string> { "dotsider-mcp" };

            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (exeDir is not null)
            {
                candidates.Add(Path.Combine(exeDir, "dotsider-mcp"));
                if (OperatingSystem.IsWindows())
                    candidates.Add(Path.Combine(exeDir, "dotsider-mcp.exe"));
            }

            foreach (var candidate in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo(candidate) { UseShellExecute = false };
                    using var process = Process.Start(psi);
                    if (process is not null)
                    {
                        try
                        {
                            await process.WaitForExitAsync(ct);
                        }
                        catch (OperationCanceledException)
                        {
                            // Ctrl+C: stay alive so the child isn't orphaned.
                            // On macOS/Linux, an orphaned child's terminal read
                            // returns EIO, causing a noisy IOException stack trace.
                            if (!process.HasExited)
                            {
                                using var gracefulCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                try
                                {
                                    await process.WaitForExitAsync(gracefulCts.Token);
                                }
                                catch (OperationCanceledException)
                                {
                                    process.Kill();
                                    await process.WaitForExitAsync(CancellationToken.None);
                                }
                            }
                        }

                        return process.ExitCode;
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Not found at this path, try next
                }
            }

            OutputFormatter.WriteError("Error: dotsider-mcp not found.");
            OutputFormatter.WriteError("Install it with: dotnet tool install -g dotsider-mcp");
            return 1;
        });

        return command;
    }

    private static Command CreateInitCommand(Option<bool> jsonOption)
    {
        var aiOption = new Option<string?>("--ai")
        {
            Description = "AI provider — writes to the provider's skill path relative to the current directory (claude, gemini, copilot, cursor-agent, opencode, codex, windsurf, kilocode, amp, qwen)"
        };

        var pathOption = new Option<string?>("--path")
        {
            Description = "Explicit output file path"
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing file"
        };

        var stdoutOption = new Option<bool>("--stdout")
        {
            Description = "Write skill content to stdout instead of a file"
        };

        var command = new Command("init", "Initialize an AI skill file for dotsider")
        {
            aiOption,
            pathOption,
            forceOption,
            stdoutOption
        };

        command.SetAction((parseResult, _) =>
        {
            var ai = parseResult.GetValue(aiOption);
            var path = parseResult.GetValue(pathOption);
            var force = parseResult.GetValue(forceOption);
            var stdout = parseResult.GetValue(stdoutOption);
            var json = parseResult.GetValue(jsonOption);

            var content = GetSkillContent();

            // --stdout: just print and exit
            if (stdout)
            {
                using var formatter = new OutputFormatter();
                formatter.WriteBlock(content);
                return Task.FromResult(0);
            }

            // Resolve output path
            string outputPath;
            if (path is not null)
            {
                outputPath = Path.GetFullPath(path);
            }
            else if (ai is not null)
            {
                if (!s_providerPaths.TryGetValue(ai, out var relativePath))
                {
                    OutputFormatter.WriteError($"Error: Unknown provider '{ai}'.");
                    OutputFormatter.WriteError($"Valid providers: {string.Join(", ", s_providerPaths.Keys.Order())}");
                    return Task.FromResult(1);
                }

                outputPath = Path.GetFullPath(relativePath);
            }
            else
            {
                OutputFormatter.WriteError("Error: Specify --ai <provider>, --path <file>, or --stdout.");
                return Task.FromResult(1);
            }

            // Check for existing file
            if (File.Exists(outputPath) && !force)
            {
                OutputFormatter.WriteError($"Error: File already exists: {outputPath}");
                OutputFormatter.WriteError("Use --force to overwrite.");
                return Task.FromResult(1);
            }

            // Write the file
            var dir = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, content);

            if (json)
            {
                using var formatter = new OutputFormatter { JsonMode = true };
                formatter.WriteJson(new { Path = outputPath, Provider = ai ?? "custom" });
            }
            else
            {
                using var formatter = new OutputFormatter();
                formatter.WriteLine($"Created: {outputPath}");
            }

            return Task.FromResult(0);
        });

        return command;
    }

    /// <summary>
    /// Returns the SKILL.md content for AI provider integration.
    /// </summary>
    internal static string GetSkillContent() => """
        ---
        name: dotsider
        description: .NET assembly analysis with dotsider CLI. Use when analyzing .NET assemblies, inspecting IL, comparing assemblies, or working with running dotsider sessions.
        ---

        # Dotsider — .NET Assembly Analysis

        Dotsider is a CLI and TUI tool for .NET assembly analysis. Use the `dotsider` command to inspect assemblies, interact with running TUI instances, and manage MCP integration.

        ## Quick Reference

        ### Analyze an assembly

        ```bash
        dotsider analyze MyApp.dll                    # assembly info (name, version, framework)
        dotsider analyze MyApp.dll --types             # list type definitions
        dotsider analyze MyApp.dll --methods            # list method definitions
        dotsider analyze MyApp.dll --il Type.Method     # disassemble a method to IL
        dotsider analyze MyApp.dll --deps               # assembly references and dependency graph
        dotsider analyze MyApp.dll --strings            # extract user, metadata, and binary strings
        dotsider analyze MyApp.dll --size               # size breakdown (namespace > type > method)
        dotsider analyze MyApp.dll --json               # output as JSON
        dotsider analyze MyApp.dll -o report.txt        # write output to a file
        ```

        ### Interact with running sessions

        ```bash
        dotsider sessions list                          # discover running dotsider TUI instances
        dotsider sessions info <pid>                    # assembly info + current view
        dotsider sessions view <pid>                    # current tab and view state
        dotsider sessions navigate <pid> <tab>          # switch to tab (1-8, see tab list below)
        dotsider sessions capture <pid>                    # capture screen as plain text
        ```

        ### Tracing

        ```bash
        dotsider sessions trace start <pid>             # start tracing the loaded assembly
        dotsider sessions trace start <pid> -- -v       # pass arguments to the traced process
        dotsider sessions trace events <pid>            # get JIT, GC, exception events
        dotsider sessions trace events <pid> --category jit --max 50
        dotsider sessions trace counters <pid>          # get performance counters
        dotsider sessions trace output <pid>            # get stdout/stderr from traced process
        dotsider sessions trace stop <pid>              # stop the active trace
        ```

        ### Compare assemblies

        ```bash
        dotsider diff Left.dll Right.dll                # interactive diff TUI
        dotsider diff old/App.mstat new/App.mstat       # AOT size diff (delta treemap)
        dotsider diff old/App.mstat new/App.mstat --json # headless size-diff document
        ```

        ### Check AOT size regressions (CI gate)

        ```bash
        dotsider size-check new/App --baseline old/App.mstat          # delta report, exit 0
        dotsider size-check new/App --baseline old/App.mstat \
          --budget total:growth=1% --budget ns=MyApp.Generated:growth=0  # exit 2 on breach
        dotsider size-check new/App --budget max=25mb                 # absolute cap, no baseline
        ```

        ## Workflows

        ### Security audit
        1. `dotsider analyze Target.dll --types --json` to get all types
        2. Look for types handling crypto, network, file I/O
        3. `dotsider analyze Target.dll --il SuspiciousType.Method` to inspect IL
        4. `dotsider analyze Target.dll --strings` to find hardcoded secrets

        ### Performance analysis
        1. Start a dotsider TUI: `dotsider MyApp.dll`
        2. `dotsider sessions trace start <pid>` to begin tracing
        3. `dotsider sessions trace events <pid> --category jit` for JIT events
        4. `dotsider sessions trace counters <pid>` for runtime counters
        5. `dotsider analyze MyApp.dll --size` for method size hotspots

        ### Dependency review
        1. `dotsider analyze MyApp.dll --deps --json` for assembly references
        2. Check versions, public key tokens, culture settings
        3. Compare against known-good baselines with `dotsider diff`

        ### Size regression triage (Native AOT)
        1. Publish both builds with `IlcGenerateMstatFile` (and `IlcGenerateDgmlFile` for why-chains)
        2. `dotsider size-check new/App --baseline old/App.mstat --json` for the delta document
        3. `dotsider size-check ... --budget ns=<Namespace>:growth=0 --why` to gate and explain growth
        4. `dotsider diff old/App.mstat new/App.mstat` for the interactive delta treemap

        ## Tips

        - All commands support `--json` for machine-readable output
        - Session commands require a running dotsider TUI instance
        - Tab numbers: 1=General, 2=PE/Metadata, 3=IL / Native, 4=Strings, 5=Hex Dump, 6=Dep Graph, 7=Size Map, 8=Dynamic
        - Capture outputs plain text of the current TUI screen
        """;
}
