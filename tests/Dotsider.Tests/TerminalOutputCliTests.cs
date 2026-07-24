using System.Globalization;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Exercises terminal escaping through the real dotsider command surface.
/// </summary>
[TestClass]
public sealed class TerminalOutputCliTests(TestContext testContext)
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies a real compiler-emitted user string is visible but inert in text output.
    /// </summary>
    [TestMethod]
    public async Task AnalyzeStrings_CompilerControlPayload_WritesTerminalSafeText()
    {
        var (exitCode, stdout, stderr) = await TestHelpers.RunDotsiderAsync(
            "analyze",
            Samples.TerminalControlLibDll,
            "--strings");

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(stderr);
        Assert.Contains(TerminalControlTestData.VisibleCompilerPayload, stdout);
        AssertTerminalSafe(stdout);
    }

    /// <summary>
    /// Verifies JSON output retains the exact compiler-emitted user string.
    /// </summary>
    [TestMethod]
    public async Task AnalyzeStringsJson_CompilerControlPayload_RoundTripsExactValue()
    {
        var (exitCode, stdout, stderr) = await TestHelpers.RunDotsiderAsync(
            "analyze",
            Samples.TerminalControlLibDll,
            "--strings",
            "--json");

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(stderr);
        using var document = JsonDocument.Parse(stdout);
        var values = document.RootElement
            .GetProperty("userStrings")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("value").GetString())
            .ToArray();
        Assert.Contains(TerminalControlTestData.CompilerPayload, values);
        AssertTerminalSafe(stdout);
    }

    /// <summary>
    /// Verifies hostile metadata names are escaped by every relevant text formatter,
    /// including file output and stderr.
    /// </summary>
    [TestMethod]
    public async Task AnalyzeText_HostileMetadataNames_AllPresentationSinksAreSafe()
    {
        var assemblyPath = CreateSyntheticAssembly();
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"dotsider-terminal-output-{Guid.NewGuid():N}.txt");
        try
        {
            var cases = new (string Option, string Expected)[]
            {
                ("--types", TerminalControlTestData.VisibleTypeName),
                ("--methods", TerminalControlTestData.VisibleMethodName),
                ("--fields", TerminalControlTestData.VisibleFieldName),
                ("--strings", TerminalControlTestData.VisibleUserString)
            };

            foreach (var (option, expected) in cases)
            {
                var (exitCode, stdout, stderr) = await TestHelpers.RunDotsiderAsync(
                    "analyze",
                    assemblyPath,
                    option);

                Assert.AreEqual(0, exitCode);
                Assert.IsEmpty(stderr);
                Assert.Contains(expected, stdout);
                AssertTerminalSafe(stdout);
            }

            var (fileExitCode, _, fileStderr) = await TestHelpers.RunDotsiderAsync(
                "analyze",
                assemblyPath,
                "--types",
                "-o",
                outputPath);
            Assert.AreEqual(0, fileExitCode);
            Assert.IsEmpty(fileStderr);
            var fileText = await File.ReadAllTextAsync(
                outputPath,
                _testContext.CancellationToken);
            Assert.Contains(TerminalControlTestData.VisibleTypeName, fileText);
            AssertTerminalSafe(fileText);

            var (errorExitCode, _, errorText) = await TestHelpers.RunDotsiderAsync(
                "analyze",
                assemblyPath,
                "--il",
                "Missing\u001B]0;owned\u0007.Method");
            Assert.AreEqual(1, errorExitCode);
            Assert.Contains("Missing␛]0;owned␇", errorText);
            AssertTerminalSafe(errorText);
        }
        finally
        {
            File.Delete(assemblyPath);
            File.Delete(outputPath);
        }
    }

    /// <summary>
    /// Verifies JSON projections of hostile metadata names remain lossless.
    /// </summary>
    [TestMethod]
    public async Task AnalyzeJson_HostileMetadataNames_RoundTripExactValues()
    {
        var assemblyPath = CreateSyntheticAssembly();
        try
        {
            await AssertJsonContainsAsync("--types", "name", TerminalControlTestData.TypeName);
            await AssertJsonContainsAsync("--methods", "name", TerminalControlTestData.MethodName);
            await AssertJsonContainsAsync("--fields", "name", TerminalControlTestData.FieldName);

            var (exitCode, stdout, stderr) = await TestHelpers.RunDotsiderAsync(
                "analyze",
                assemblyPath,
                "--strings",
                "--json");
            Assert.AreEqual(0, exitCode);
            Assert.IsEmpty(stderr);
            using var document = JsonDocument.Parse(stdout);
            var values = document.RootElement
                .GetProperty("userStrings")
                .EnumerateArray()
                .Select(entry => entry.GetProperty("value").GetString())
                .ToArray();
            Assert.Contains(TerminalControlTestData.UserString, values);

            async Task AssertJsonContainsAsync(string option, string property, string expected)
            {
                var (caseExitCode, caseStdout, caseStderr) = await TestHelpers.RunDotsiderAsync(
                    "analyze",
                    assemblyPath,
                    option,
                    "--json");

                Assert.AreEqual(0, caseExitCode);
                Assert.IsEmpty(caseStderr);
                using var caseDocument = JsonDocument.Parse(caseStdout);
                var caseValues = caseDocument.RootElement
                    .EnumerateArray()
                    .Select(entry => entry.GetProperty(property).GetString())
                    .ToArray();
                Assert.Contains(expected, caseValues);
            }
        }
        finally
        {
            File.Delete(assemblyPath);
        }
    }

    private static string CreateSyntheticAssembly()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"dotsider-terminal-metadata-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(
            path,
            SyntheticMetadataBuilder.BuildTerminalControlAssembly(
                TerminalControlTestData.TypeName,
                TerminalControlTestData.MethodName,
                TerminalControlTestData.FieldName,
                TerminalControlTestData.UserString));
        return path;
    }

    private static void AssertTerminalSafe(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '\r' or '\n')
            {
                continue;
            }

            if (char.IsSurrogate(character))
            {
                if (char.IsHighSurrogate(character) &&
                    index + 1 < value.Length &&
                    char.IsLowSurrogate(value[index + 1]))
                {
                    index++;
                    continue;
                }

                Assert.Fail(
                    $"Output contains invalid UTF-16 at index {index.ToString(CultureInfo.InvariantCulture)}.");
            }

            var category = char.GetUnicodeCategory(character);
            if (category is UnicodeCategory.Control or
                UnicodeCategory.Format or
                UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator)
            {
                Assert.Fail(
                    $"Output contains unsafe U+{((int)character).ToString("X4", CultureInfo.InvariantCulture)} "
                    + $"at index {index.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
    }
}
