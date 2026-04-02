using System.Collections.Concurrent;
using System.CommandLine;
using System.Text;
using Dotsider;
using Dotsider.Commands;
using Dotsider.Core.Analysis;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Diagnostics;

Console.OutputEncoding = Encoding.UTF8;

// --- Detect TUI mode (file arg anywhere, not a subcommand) ---

var subcommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "diff", "sessions", "analyze", "agent" };

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

// Diff subcommand
var diffLeftArg = new Argument<FileInfo>("left") { Description = "First assembly" };
var diffRightArg = new Argument<FileInfo>("right") { Description = "Second assembly" };

var escapeTimeoutOption = new Option<int>("--escape-timeout", "-e")
{
    Description = "Escape key timeout in milliseconds (default 100)",
    DefaultValueFactory = _ => 100
};

var diffCommand = new Command("diff", "Compare two assemblies side-by-side")
{
    diffLeftArg,
    diffRightArg
};
diffCommand.Options.Add(escapeTimeoutOption);

diffCommand.SetAction(async (parseResult, ct) =>
{
    var left = parseResult.GetValue(diffLeftArg)!;
    var right = parseResult.GetValue(diffRightArg)!;

    if (!left.Exists)
    {
        Console.Error.WriteLine($"Error: File not found: {left.FullName}");
        return 1;
    }

    if (!right.Exists)
    {
        Console.Error.WriteLine($"Error: File not found: {right.FullName}");
        return 1;
    }

    // Redirect apphost binaries to their companion managed .dll
    var leftPath = left.FullName;
    var rightPath = right.FullName;

    var leftCompanion = ApphostDetector.FindCompanionDll(leftPath);
    if (leftCompanion is not null)
    {
        Console.Error.WriteLine(
            $"Note: {left.Name} is a native apphost. "
            + $"Analyzing {Path.GetFileName(leftCompanion)} instead.");
        leftPath = leftCompanion;
    }

    var rightCompanion = ApphostDetector.FindCompanionDll(rightPath);
    if (rightCompanion is not null)
    {
        Console.Error.WriteLine(
            $"Note: {right.Name} is a native apphost. "
            + $"Analyzing {Path.GetFileName(rightCompanion)} instead.");
        rightPath = rightCompanion;
    }

    DiffState? capturedDiffState = null;

    await using var diagnosticsListener = new DotsiderDiagnosticsListener(
        () => null,
        assemblyInfoProvider: () =>
        {
            var s = capturedDiffState;
            if (s is null) return null;
            return new
            {
                Mode = "diff",
                FileName = $"{s.Left.FileName} \u2194 {s.Right.FileName}",
                Left = new
                {
                    s.Left.FilePath,
                    s.Left.FileName,
                    s.Left.FileSize,
                    s.Left.AssemblyName,
                    s.Left.AssemblyVersion,
                    s.Left.TargetFramework,
                },
                Right = new
                {
                    s.Right.FilePath,
                    s.Right.FileName,
                    s.Right.FileSize,
                    s.Right.AssemblyName,
                    s.Right.AssemblyVersion,
                    s.Right.TargetFramework,
                },
            };
        },
        currentViewProvider: () =>
        {
            var s = capturedDiffState;
            if (s is null) return null;
            return new
            {
                Mode = "diff",
                Tab = s.CurrentTab + 1,
                s.FilterMode,
            };
        });

    var escTimeoutMs = Math.Max(10, parseResult.GetValue(escapeTimeoutOption));
    var diffEscAdapter = new EscapeTimeoutPresentationAdapter(
        new ConsolePresentationAdapter(enableMouse: true),
        TimeSpan.FromMilliseconds(escTimeoutMs));

    var diffWorkload = new Hex1bAppWorkloadAdapter(diffEscAdapter.Capabilities);
    var diffTerminalOptions = new Hex1bTerminalOptions
    {
        PresentationAdapter = diffEscAdapter,
        WorkloadAdapter = diffWorkload
    };
    diffTerminalOptions.PresentationFilters.Add(new McpDiagnosticsPresentationFilter("dotsider-diff"));
    await using var diffTerminal = new Hex1bTerminal(diffTerminalOptions);
    diffEscAdapter.Terminal = diffTerminal;

    var diffAppOptions = new Hex1bAppOptions
    {
        WorkloadAdapter = diffWorkload,
        Theme = DotsiderTheme.Create(),
        EnableMouse = true
    };

    DiffApp? diffApp = null;
    Hex1bApp? diffHex1bApp = null;

    diffHex1bApp = new Hex1bApp(ctx =>
    {
        if (capturedDiffState is null)
        {
            var diffState = new DiffState(diffHex1bApp!, leftPath, rightPath);
            capturedDiffState = diffState;
            diffApp = new DiffApp(diffState);
        }
        return diffApp!.Build(ctx);
    }, diffAppOptions);

    diagnosticsListener.StartListening();

    CursorColorHelper.SetThemeCursorColor();

    try
    {
        await diffHex1bApp.RunAsync(ct);
    }
    finally
    {
        CursorColorHelper.ResetCursorColor();
        diffHex1bApp.Dispose();
    }

    return 0;
});

rootCommand.Subcommands.Add(diffCommand);
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

    try
    {
        await hex1bApp.RunAsync();
    }
    finally
    {
        CursorColorHelper.ResetCursorColor();
        hex1bApp.Dispose();
    }

    return 0;
}
