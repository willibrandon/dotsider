using System.Text;
using Dotsider;
using Hex1b;

Console.OutputEncoding = Encoding.UTF8;

// Parse CLI arguments
string? filePath = null;
string? diffLeftPath = null;
string? diffRightPath = null;
int initialTab = 0;
int minStringLength = 4;
var isDiffMode = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "diff" when !isDiffMode && filePath is null:
            isDiffMode = true;
            break;
        case "--min-len" or "-n":
            if (i + 1 < args.Length && int.TryParse(args[++i], out var len) && len >= 1)
                minStringLength = len;
            else
            {
                Console.Error.WriteLine("Error: --min-len requires a positive integer");
                return 1;
            }
            break;
        case "--tab" or "-t":
            if (i + 1 < args.Length && int.TryParse(args[++i], out var tab) && tab >= 1 && tab <= 8)
                initialTab = tab - 1;
            else
            {
                Console.Error.WriteLine("Error: --tab requires a value from 1 to 8");
                return 1;
            }
            break;
        case "--help" or "-h":
            PrintUsage();
            return 0;
        default:
            if (args[i].StartsWith('-'))
            {
                Console.Error.WriteLine($"Error: Unknown option: {args[i]}");
                PrintUsage();
                return 1;
            }
            if (isDiffMode)
            {
                if (diffLeftPath is null) diffLeftPath = args[i];
                else if (diffRightPath is null) diffRightPath = args[i];
            }
            else
            {
                filePath = args[i];
            }
            break;
    }
}

// Determine mode
if (isDiffMode)
{
    if (diffLeftPath is null || diffRightPath is null)
    {
        Console.Error.WriteLine("Error: diff mode requires two assembly paths");
        Console.Error.WriteLine("Usage: dotsider diff <assembly1> <assembly2>");
        return 1;
    }
    if (!File.Exists(diffLeftPath))
    {
        Console.Error.WriteLine($"Error: File not found: {diffLeftPath}");
        return 1;
    }
    if (!File.Exists(diffRightPath))
    {
        Console.Error.WriteLine($"Error: File not found: {diffRightPath}");
        return 1;
    }

    diffLeftPath = Path.GetFullPath(diffLeftPath);
    diffRightPath = Path.GetFullPath(diffRightPath);

    await using var diffTerminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) =>
        {
            options.Theme = DotsiderTheme.Create();
            options.EnableMouse = true;

            var diffState = new DiffState(app, diffLeftPath, diffRightPath);
            var diffApp = new DiffApp(diffState);
            return ctx => diffApp.Build(ctx);
        })
        .WithMouse()
        .WithDiagnostics(appName: "dotsider-diff", forceEnable: true)
        .Build();

    await diffTerminal.RunAsync();
    return 0;
}

if (filePath is null)
{
    PrintUsage();
    return 1;
}

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"Error: File not found: {filePath}");
    return 1;
}

filePath = Path.GetFullPath(filePath);

// Check for NuGet package mode
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

// Standard single-assembly mode
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        options.Theme = DotsiderTheme.Create();
        options.EnableMouse = true;

        var state = new DotsiderState(app, filePath);
        state.CurrentTab = initialTab;
        state.StringsMinLength = minStringLength;
        var dotsiderApp = new DotsiderApp(state);
        return ctx => dotsiderApp.Build(ctx);
    })
    .WithMouse()
    .WithDiagnostics(appName: "dotsider", forceEnable: true)
    .Build();

await terminal.RunAsync();
return 0;

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: dotsider [options] <assembly.dll|exe|nupkg>");
    Console.Error.WriteLine("       dotsider diff <assembly1> <assembly2>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Analyze .NET assemblies like a boss.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    Console.Error.WriteLine("  -t, --tab <1-8>       Initial tab (1=General .. 7=SizeMap, 8=Dynamic)");
    Console.Error.WriteLine("  -n, --min-len <n>     Minimum raw string length (default: 4)");
    Console.Error.WriteLine("  -h, --help            Show this help");
}
