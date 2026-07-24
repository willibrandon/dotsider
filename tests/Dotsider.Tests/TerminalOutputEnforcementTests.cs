using System.Text.RegularExpressions;

namespace Dotsider.Tests;

/// <summary>
/// Enforces the source-level boundary around direct terminal output.
/// </summary>
[TestClass]
public sealed partial class TerminalOutputEnforcementTests
{
    /// <summary>
    /// Verifies human-readable output cannot bypass terminal escaping. The only direct write is
    /// the application's constant TUI restoration sequence.
    /// </summary>
    [TestMethod]
    public void ProductionSource_HasNoUnescapedConsoleOutput()
    {
        var sourceRoot = Path.Combine(TestHelpers.GetRepoRoot(), "src", "Dotsider");
        var matches = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(
                file => DirectConsoleWritePattern()
                    .Matches(File.ReadAllText(file))
                    .Select(match => (File: Path.GetFullPath(file), match.Value)))
            .ToArray();

        Assert.HasCount(1, matches);
        Assert.EndsWith(
            Path.Combine("Dotsider", "Program.cs"),
            matches[0].File,
            StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(
        @"Console\.(?:(?:Error|Out)\.)?Write(?:Line)?\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex DirectConsoleWritePattern();
}
