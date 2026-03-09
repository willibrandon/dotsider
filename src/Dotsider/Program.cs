using System.Collections.Concurrent;
using System.CommandLine;
using System.Text;
using Dotsider;
using Dotsider.Commands;
using Dotsider.Diagnostics;
using Hex1b;

Console.OutputEncoding = Encoding.UTF8;

// --- Global options ---

var jsonOption = new Option<bool>("--json")
{
    Description = "Output results as JSON",
    Recursive = true
};

// --- Root command: TUI mode ---

var fileArg = new Argument<FileInfo?>("file")
{
    Description = "Assembly file (.dll, .exe, or .nupkg)",
    Arity = ArgumentArity.ZeroOrOne
};

var tabOption = new Option<int?>("--tab", "-t")
{
    Description = "Initial tab (1=General .. 7=SizeMap, 8=Dynamic)"
};

var minLenOption = new Option<int?>("--min-len", "-n")
{
    Description = "Minimum raw string length (default: 4)"
};

var rootCommand = new RootCommand("Analyze .NET assemblies like a boss.")
{
    fileArg,
    tabOption,
    minLenOption
};
rootCommand.Options.Add(jsonOption);

rootCommand.SetAction(async (parseResult, ct) =>
{
    var file = parseResult.GetValue(fileArg);
    if (file is null)
    {
        // No file provided — show help
        return 1;
    }

    if (!file.Exists)
    {
        Console.Error.WriteLine($"Error: File not found: {file.FullName}");
        return 1;
    }

    var filePath = file.FullName;
    var initialTab = (parseResult.GetValue(tabOption) ?? 1) - 1;
    if (initialTab < 0) initialTab = 0;
    if (initialTab > 7) initialTab = 7;
    var minStringLength = parseResult.GetValue(minLenOption) ?? 4;

    // NuGet package mode
    if (filePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
    {
        await using var nugetTerminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) =>
            {
                options.Theme = DotsiderTheme.Create();
                options.EnableMouse = true;

                var nugetState = new NuGetState(app, filePath);
                var nugetApp = new NuGetApp(nugetState);
                return ctx => nugetApp.Build(ctx);
            })
            .WithMouse()
            .WithDiagnostics(appName: "dotsider-nuget", forceEnable: true)
            .Build();

        await nugetTerminal.RunAsync();
        return 0;
    }

    // Standard single-assembly TUI mode
    DotsiderState? capturedState = null;
    var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

    await using var diagnosticsListener = new DotsiderDiagnosticsListener(
        () => capturedState, pendingMutations);

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
});

// --- Diff subcommand ---

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

    await using var diffTerminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) =>
        {
            options.Theme = DotsiderTheme.Create();
            options.EnableMouse = true;

            var diffState = new DiffState(app, left.FullName, right.FullName);
            var diffApp = new DiffApp(diffState);
            return ctx => diffApp.Build(ctx);
        })
        .WithMouse()
        .WithDiagnostics(appName: "dotsider-diff", forceEnable: true)
        .Build();

    await diffTerminal.RunAsync();
    return 0;
});

rootCommand.Subcommands.Add(diffCommand);

// --- Sessions subcommand ---

rootCommand.Subcommands.Add(SessionsCommand.Create(jsonOption));

// --- Analyze subcommand ---

rootCommand.Subcommands.Add(AnalyzeCommand.Create(jsonOption));

// --- Parse and invoke ---

return await rootCommand.Parse(args).InvokeAsync();
