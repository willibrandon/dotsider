using System.Collections.Concurrent;
using System.CommandLine;
using System.Text;
using Dotsider;
using Dotsider.Commands;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Diagnostics;

Console.OutputEncoding = Encoding.UTF8;

// --- Detect TUI mode (file arg anywhere, not a subcommand) ---

var subcommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "diff", "sessions", "analyze", "agent", "size-check" };

// Find the first positional argument (not an option and not a known subcommand).
// This handles both "dotsider file.dll --tab 2" and "dotsider --tab 2 file.dll".
static string? FindFileArg(string[] args, HashSet<string> subcommands)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith('-'))
        {
            // Skip options that take a value (--tab N, -t N, --min-len N, -n N)
            if (args[i] is "--tab" or "-t" or "--min-len" or "-n" or "--escape-timeout" or "-e")
                i++;
            continue;
        }

        // First non-option arg: if it's a subcommand, this isn't TUI mode
        if (subcommands.Contains(args[i]))
            return null;

        return args[i];
    }

    return null;
}

// No args = show help and exit 0 (not 1). Package manager validation (e.g. WinGet)
// runs the exe bare and treats non-zero exit codes as installation failures.
if (args.Length == 0)
    args = ["--help"];

var fileArg = FindFileArg(args, subcommands);
if (fileArg is not null)
{
    return await RunTui(args, fileArg);
}

// --- System.CommandLine: subcommands only (no file arg on root) ---

var jsonOption = new Option<bool>("--json")
{
    Description = "Output results as JSON",
    Recursive = true
};

var rootCommand = new RootCommand("dotsider — .NET assembly analysis TUI and CLI");
rootCommand.Options.Add(jsonOption);

rootCommand.Subcommands.Add(DiffCommand.Create(jsonOption));
rootCommand.Subcommands.Add(SizeCheckCommand.Create(jsonOption));
rootCommand.Subcommands.Add(SessionsCommand.Create(jsonOption));
rootCommand.Subcommands.Add(AnalyzeCommand.Create(jsonOption));
rootCommand.Subcommands.Add(AgentCommand.Create(jsonOption));

return await rootCommand.Parse(args).InvokeAsync();

// --- TUI mode: dotsider <file> [--tab N] [--min-len N] ---

static async Task<int> RunTui(string[] args, string filePath)
{
    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"Error: File not found: {filePath}");
        return 1;
    }

    var parsed = TuiArgParser.Parse(args, filePath);
    var initialTab = parsed.InitialTab;
    var minStringLength = parsed.MinStringLength;

    // NuGet package mode
    if (filePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
    {
        NuGetState? capturedNugetState = null;

        await using var nugetListener = new DotsiderDiagnosticsListener(
            () => capturedNugetState?.SelectedDllState,
            assemblyInfoProvider: () =>
            {
                var s = capturedNugetState;
                if (s is null) return null;
                return new
                {
                    Mode = "nuget",
                    s.Package.FilePath,
                    s.Package.FileName,
                    s.Package.PackageId,
                    s.Package.PackageVersion,
                    s.Package.Authors,
                    s.Package.Description,
                    DllCount = s.Package.DllFiles.Count,
                    SelectedDll = s.SelectedDllState?.Analyzer.FileName,
                };
            },
            currentViewProvider: () =>
            {
                var s = capturedNugetState;
                if (s is null) return null;
                return new
                {
                    Mode = "nuget",
                    s.IsBrowsingPackage,
                    Tab = s.SelectedDllState is { } dll ? dll.CurrentTab + 1 : (int?)null,
                    SelectedDll = s.SelectedDllEntry?.Name,
                };
            });

        var nugetEscAdapter = new EscapeTimeoutPresentationAdapter(
            new ConsolePresentationAdapter(enableMouse: true),
            TimeSpan.FromMilliseconds(parsed.EscapeTimeoutMs));

        var nugetWorkload = new Hex1bAppWorkloadAdapter(nugetEscAdapter.Capabilities);
        var nugetTerminalOptions = new Hex1bTerminalOptions
        {
            PresentationAdapter = nugetEscAdapter,
            WorkloadAdapter = nugetWorkload
        };
        nugetTerminalOptions.PresentationFilters.Add(new McpDiagnosticsPresentationFilter("dotsider-nuget"));
        await using var nugetTerminal = new Hex1bTerminal(nugetTerminalOptions);
        nugetEscAdapter.Terminal = nugetTerminal;

        var nugetAppOptions = new Hex1bAppOptions
        {
            WorkloadAdapter = nugetWorkload,
            Theme = DotsiderTheme.Create(),
            EnableMouse = true
        };

        NuGetApp? nugetApp = null;
        Hex1bApp? nugetHex1bApp = null;

        nugetHex1bApp = new Hex1bApp(ctx =>
        {
            capturedNugetState ??= new NuGetState(nugetHex1bApp!, filePath);
            nugetApp ??= new NuGetApp(capturedNugetState);
            return nugetApp.Build(ctx);
        }, nugetAppOptions);

        nugetListener.StartListening();

        CursorColorHelper.SetThemeCursorColor();

        try
        {
            await nugetHex1bApp.RunAsync();
        }
        finally
        {
            CursorColorHelper.ResetCursorColor();
            nugetHex1bApp.Dispose();
        }

        return 0;
    }

    // Standard single-assembly TUI mode
    DotsiderState? capturedState = null;
    var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

    await using var diagnosticsListener = new DotsiderDiagnosticsListener(
        () => capturedState);

    var escAdapter = new EscapeTimeoutPresentationAdapter(
        new ConsolePresentationAdapter(enableMouse: true),
        TimeSpan.FromMilliseconds(parsed.EscapeTimeoutMs));

    var workload = new Hex1bAppWorkloadAdapter(escAdapter.Capabilities);


    var terminalOptions = new Hex1bTerminalOptions
    {
        PresentationAdapter = escAdapter,
        WorkloadAdapter = workload
    };
    terminalOptions.PresentationFilters.Add(new McpDiagnosticsPresentationFilter("dotsider"));
    await using var terminal = new Hex1bTerminal(terminalOptions);
    escAdapter.Terminal = terminal;

    var appOptions = new Hex1bAppOptions
    {
        WorkloadAdapter = workload,
        Theme = DotsiderTheme.Create(),
        EnableMouse = true
    };

    DotsiderApp? dotsiderApp = null;
    Hex1bApp? hex1bApp = null;

    hex1bApp = new Hex1bApp(ctx =>
    {
        capturedState ??= new DotsiderState(hex1bApp!, filePath, pendingMutations)
        {
            CurrentTab = initialTab,
            StringsMinLength = minStringLength
        };
        dotsiderApp ??= new DotsiderApp(capturedState);
        return dotsiderApp.Build(ctx);
    }, appOptions);

    diagnosticsListener.StartListening();

    CursorColorHelper.SetThemeCursorColor();

    // Safety net: some exit paths bypass the finally below — a crash on a background render/input
    // thread, or Ctrl+C — which would leave the terminal in mouse-reporting mode (mouse movement
    // then echoes as escape sequences at the shell). Disable mouse reporting, show the cursor, and
    // leave the alternate screen on every exit path, and log an unhandled exception so its root
    // cause is captured even when normal cleanup is skipped.
    static void RestoreTerminal()
    {
        try
        {
            Console.Out.Write("\x1b[?1003l\x1b[?1002l\x1b[?1000l\x1b[?1006l\x1b[?25h\x1b[?1049l");
            Console.Out.Flush();
        }
        catch
        {
            // Nothing more we can do while tearing down.
        }
    }

    void OnUnhandled(object? _, UnhandledExceptionEventArgs e)
    {
        RestoreTerminal();
        try
        {
            var log = Path.Combine(Path.GetTempPath(), "dotsider-crash.log");
            File.AppendAllText(log, $"{DateTime.Now:O}\n{(e.ExceptionObject as Exception)?.ToString() ?? e.ExceptionObject}\n\n");
            Console.Error.WriteLine($"dotsider crashed; details written to {log}");
        }
        catch
        {
            // Best-effort.
        }
    }

    void OnProcessExit(object? _, EventArgs __) => RestoreTerminal();

    AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

    try
    {
        await hex1bApp.RunAsync();
    }
    finally
    {
        // RestoreTerminal must run even if Dispose throws — otherwise a teardown failure leaves the
        // terminal in the alternate screen with mouse reporting on (mouse motion then echoes as escape
        // sequences at the shell). Catch and log the teardown so the terminal is always restored and
        // the failure leaves a trace instead of vanishing into a bare process exit.
        try
        {
            CursorColorHelper.ResetCursorColor();
            hex1bApp.Dispose();
        }
        catch (Exception ex)
        {
            try
            {
                var log = Path.Combine(Path.GetTempPath(), "dotsider-crash.log");
                File.AppendAllText(log, $"{DateTime.Now:O}\nTeardown: {ex}\n\n");
            }
            catch
            {
                // Best-effort.
            }
        }
        finally
        {
            RestoreTerminal();
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
    }

    return 0;
}
