using System.Collections.Concurrent;
using System.CommandLine;
using System.Text;
using Dotsider;
using Dotsider.Commands;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;

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
            if (args[i] is "--tab" or "-t" or "--min-len" or "-n")
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

var diffCommand = new Command("diff", "Compare two assemblies side-by-side")
{
    diffLeftArg,
    diffRightArg
};

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
                Tab = s.CurrentTab,
                s.FilterMode,
            };
        });

    await using var diffTerminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) =>
        {
            options.Theme = DotsiderTheme.Create();
            options.EnableMouse = true;

            var diffState = new DiffState(app, left.FullName, right.FullName);
            capturedDiffState = diffState;
            var diffApp = new DiffApp(diffState);
            return ctx => diffApp.Build(ctx);
        })
        .WithMouse()
        .WithDiagnostics(appName: "dotsider-diff", forceEnable: true)
        .Build();

    diagnosticsListener.StartListening();
    await diffTerminal.RunAsync(ct);
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
                    Tab = s.SelectedDllState?.CurrentTab,
                    SelectedDll = s.SelectedDllEntry?.Name,
                };
            });

        await using var nugetTerminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) =>
            {
                options.Theme = DotsiderTheme.Create();
                options.EnableMouse = true;

                var nugetState = new NuGetState(app, filePath);
                capturedNugetState = nugetState;
                var nugetApp = new NuGetApp(nugetState);
                return ctx => nugetApp.Build(ctx);
            })
            .WithMouse()
            .WithDiagnostics(appName: "dotsider-nuget", forceEnable: true)
            .Build();

        nugetListener.StartListening();
        await nugetTerminal.RunAsync();
        return 0;
    }

    // Standard single-assembly TUI mode
    DotsiderState? capturedState = null;
    var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

    await using var diagnosticsListener = new DotsiderDiagnosticsListener(
        () => capturedState);

    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) =>
        {
            options.Theme = DotsiderTheme.Create();
            options.EnableMouse = true;

            var state = new DotsiderState(app, filePath, pendingMutations)
            {
                CurrentTab = initialTab,
                StringsMinLength = minStringLength
            };
            capturedState = state;

            var dotsiderApp = new DotsiderApp(state);
            return ctx => dotsiderApp.Build(ctx);
        })
        .WithMouse()
        .WithDiagnostics(appName: "dotsider", forceEnable: true)
        .Build();

    diagnosticsListener.StartListening();

    await terminal.RunAsync();
    return 0;
}
