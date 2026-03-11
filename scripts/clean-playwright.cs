#!/usr/bin/env dotnet run
#:sdk Microsoft.NET.Sdk

/// <summary>
/// Cleans up playwright-cli artifact files (.log, .yml, .yaml, .png) from
/// all auto-detected playwright directories in the repository.
///
/// Directories are preserved; only their contents are deleted.
/// Skips .claude/skills and node_modules directories.
///
/// Usage: dotnet run scripts/clean-playwright.cs [-- --dry-run] [-- --logs] [-- --older-than 1d]
/// </summary>

var root = FindRepoRoot(Directory.GetCurrentDirectory());
if (root is null)
{
    Console.Error.WriteLine("error: not inside a git repository");
    return 1;
}

// Parse arguments
var mode = "all";        // all, logs, snapshots
var dryRun = false;
var olderThan = (DateTime?)null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--dry-run" or "-n":
            dryRun = true;
            break;
        case "--logs":
            mode = "logs";
            break;
        case "--snapshots":
            mode = "snapshots";
            break;
        case "--older-than" when i + 1 < args.Length:
            if (!TryParseAge(args[++i], out var cutoff))
            {
                Console.Error.WriteLine($"error: invalid duration '{args[i]}'. Use format like 1d, 6h, 30m");
                return 1;
            }
            olderThan = cutoff;
            break;
        case "--help" or "-h":
            PrintHelp();
            return 0;
        default:
            Console.Error.WriteLine($"error: unknown option '{args[i]}'");
            PrintHelp();
            return 1;
    }
}

// Find all playwright artifact directories (skip skill definitions)
var dirs = Directory.GetDirectories(root, "*playwright*", SearchOption.AllDirectories)
    .Where(d => !d.Contains(".claude") && !d.Contains("node_modules"))
    .Where(d =>
    {
        var name = Path.GetFileName(d);
        return name is ".playwright" or ".playwright-cli"
            || name.StartsWith("playwright-cli");
    })
    .ToList();

if (dirs.Count == 0)
{
    Console.WriteLine("No playwright artifact directories found.");
    return 0;
}

var totalDeleted = 0;
var totalBytes = 0L;

foreach (var dir in dirs)
{
    var relative = Path.GetRelativePath(root, dir);
    Console.WriteLine($"\n  {relative}/");

    var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
    var targets = files.Where(f => MatchesFilter(f, mode, olderThan)).ToList();

    if (targets.Count == 0)
    {
        Console.WriteLine("    (no matching files)");
        continue;
    }

    foreach (var file in targets)
    {
        var info = new FileInfo(file);
        var age = DateTime.UtcNow - info.LastWriteTimeUtc;
        var ageStr = age.TotalHours < 1 ? $"{age.Minutes}m ago"
            : age.TotalDays < 1 ? $"{age.TotalHours:F0}h ago"
            : $"{age.TotalDays:F0}d ago";

        var sizeStr = info.Length < 1024 ? $"{info.Length}B"
            : info.Length < 1024 * 1024 ? $"{info.Length / 1024.0:F1}KB"
            : $"{info.Length / (1024.0 * 1024):F1}MB";

        var action = dryRun ? "would delete" : "deleted";
        Console.WriteLine($"    {action}: {info.Name}  ({sizeStr}, {ageStr})");

        if (!dryRun)
        {
            totalBytes += info.Length;
            File.Delete(file);
            totalDeleted++;
        }
        else
        {
            totalBytes += info.Length;
            totalDeleted++;
        }
    }

}

var verb = dryRun ? "Would delete" : "Deleted";
var sizeTotal = totalBytes < 1024 ? $"{totalBytes}B"
    : totalBytes < 1024 * 1024 ? $"{totalBytes / 1024.0:F1}KB"
    : $"{totalBytes / (1024.0 * 1024):F1}MB";
Console.WriteLine($"\n{verb} {totalDeleted} file(s), {sizeTotal}");
return 0;

// ---

/// <summary>
/// Returns true if the file matches the current filter mode and age constraint.
/// </summary>
static bool MatchesFilter(string path, string mode, DateTime? olderThan)
{
    var ext = Path.GetExtension(path).ToLowerInvariant();
    var isLog = ext is ".log";
    var isSnapshot = ext is ".yml" or ".yaml" or ".png";

    var typeMatch = mode switch
    {
        "logs" => isLog,
        "snapshots" => isSnapshot,
        _ => true
    };

    if (!typeMatch) return false;

    if (olderThan is not null)
    {
        var lastWrite = File.GetLastWriteTimeUtc(path);
        if (lastWrite > olderThan.Value) return false;
    }

    return true;
}

/// <summary>
/// Parses a human-readable duration like "1d", "6h", or "30m" into a UTC cutoff time.
/// </summary>
static bool TryParseAge(string input, out DateTime cutoff)
{
    cutoff = default;
    if (input.Length < 2) return false;

    var unit = input[^1];
    if (!int.TryParse(input[..^1], out var value)) return false;

    var span = unit switch
    {
        'm' => TimeSpan.FromMinutes(value),
        'h' => TimeSpan.FromHours(value),
        'd' => TimeSpan.FromDays(value),
        _ => TimeSpan.Zero
    };

    if (span == TimeSpan.Zero) return false;
    cutoff = DateTime.UtcNow - span;
    return true;
}

/// <summary>
/// Walks up from <paramref name="start"/> to find the nearest directory containing a .git folder.
/// </summary>
static string? FindRepoRoot(string start)
{
    var dir = start;
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

static void PrintHelp()
{
    Console.WriteLine("""
    Usage: dotnet run clean-playwright.cs [options]

    Options:
      --dry-run, -n       Show what would be deleted without deleting
      --logs              Only delete .log files
      --snapshots         Only delete .yml/.yaml/.png snapshots
      --older-than <age>  Only delete files older than <age> (e.g. 1d, 6h, 30m)
      --help, -h          Show this help

    Examples:
      dotnet run clean-playwright.cs                    # delete everything
      dotnet run clean-playwright.cs --dry-run          # preview what would go
      dotnet run clean-playwright.cs --logs --older-than 1d
      dotnet run clean-playwright.cs --snapshots -n
    """);
}
