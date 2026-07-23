using Dotsider.Tests.Shared;

namespace Dotsider.Tests;

/// <summary>
/// CLI integration tests that invoke the dotsider process to verify
/// argument parsing, output formatting, and error handling.
/// </summary>
[TestClass]
public class CliTests
{
    private static SampleAssemblyFixture Fixture => SampleAssemblyHost.Instance;

    // --- Default analyze output ---

    /// <summary>
    /// Verifies analyze default lists types methods and references.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Default_ListsTypesMethodsAndReferences()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.HelloWorldDll);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Types (", stdout);
        Assert.Contains("Methods (", stdout);
        Assert.Contains("References (", stdout);
        // Should list actual items, not just counts
        Assert.Contains("Program", stdout);
        Assert.Contains("System.Runtime", stdout);
    }

    /// <summary>
    /// Verifies analyze default output includes portable PDB summary lines.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Default_ShowsPortablePdbSummary()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("PDB:", stdout);
        Assert.Contains("Sidecar(", stdout);
        Assert.Contains("SourceLink: present", stdout);
    }

    /// <summary>
    /// Verifies analyze default JSON includes portable PDB metadata.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Default_Json_IncludesPortablePdbMetadata()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--json");

        Assert.AreEqual(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.AreEqual("sidecar", json.GetProperty("pdbProvenance").GetProperty("kind").GetString());
        Assert.IsTrue(json.GetProperty("sourceLink").GetProperty("isPresent").GetBoolean());
        Assert.IsGreaterThan(0, json.GetProperty("debugDirectory").GetArrayLength());
    }

    /// <summary>
    /// Verifies analyze IL output includes portable PDB annotations.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Il_ShowsPortablePdbAnnotations()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--il", "RichLibrary.Services.UserService.Add");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("// PDB: Sidecar", stdout);
        Assert.Contains("// Source Link: present", stdout);
        Assert.Contains("UserService.cs", stdout);
        Assert.Contains("[source link]", stdout);
        Assert.Contains("// id", stdout);
        Assert.DoesNotContain("raw.githubusercontent.com", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --il</c> renders a generic method instantiation instead of its raw
    /// MethodSpec metadata token.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Il_MethodSpec_PrintsConstructedGenericMethod()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze",
            typeof(MethodSpecReproFixture).Assembly.Location,
            "--il",
            $"{MethodSpecReproFixture.TypeName}.{MethodSpecReproFixture.MethodName}");

        Assert.AreEqual(0, exitCode);
        foreach (var expectedDisplay in MethodSpecReproFixture.ExpectedDisplays)
            Assert.Contains($"call {expectedDisplay}", stdout);
        Assert.DoesNotContain("call 0x2B", stdout);
    }

    /// <summary>
    /// Verifies analyze embedded source prints source text from an embedded portable PDB.
    /// </summary>
    [TestMethod]
    public async Task Analyze_EmbeddedSource_PrintsEmbeddedSource()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.EmbeddedSourceLibDll, "--embedded-source",
            "EmbeddedSourceLib.EmbeddedSourceFixture.Compute");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("internal static class EmbeddedSourceFixture", stdout);
        Assert.Contains("return doubled + 1;", stdout);
    }

    // --- P1: --output safety ---

    /// <summary>
    /// Verifies analyze missing input does not truncate output file.
    /// </summary>
    [TestMethod]
    public async Task Analyze_MissingInput_DoesNotTruncateOutputFile()
    {
        var outputFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(outputFile, "original content");

            var (exitCode, _, stderr) = await RunDotsiderAsync(
                "analyze", "nonexistent-assembly.dll", "-o", outputFile);

            Assert.AreNotEqual(0, exitCode);
            Assert.Contains("File not found", stderr);
            Assert.AreEqual("original content", File.ReadAllText(outputFile));
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    /// <summary>
    /// Verifies analyze same input and output rejects with error.
    /// </summary>
    [TestMethod]
    public async Task Analyze_SameInputAndOutput_RejectsWithError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.HelloWorldDll, "-o", Fixture.HelloWorldDll);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("Output path cannot be the same as the input file", stderr);
    }

    /// <summary>
    /// Verifies analyze invalid output path produces controlled error.
    /// </summary>
    [TestMethod]
    public async Task Analyze_InvalidOutputPath_ProducesControlledError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.HelloWorldDll, "-o", "/nonexistent/dir/report.txt");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("Error:", stderr);
    }

    /// <summary>
    /// Verifies analyze valid output writes to file.
    /// </summary>
    [TestMethod]
    public async Task Analyze_ValidOutput_WritesToFile()
    {
        var outputFile = Path.GetTempFileName();
        try
        {
            var (exitCode, stdout, _) = await RunDotsiderAsync(
                "analyze", Fixture.HelloWorldDll, "--types", "-o", outputFile);

            Assert.AreEqual(0, exitCode);
            // stdout should be empty when writing to file
            Assert.IsEmpty(stdout.Trim());
            // File should have the table
            var content = File.ReadAllText(outputFile);
            Assert.Contains("Namespace", content);
            Assert.Contains("Program", content);
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    // --- P2: TUI option ordering ---

    /// <summary>
    /// Verifies tui mode options before file routes to tui mode.
    /// </summary>
    [TestMethod]
    public async Task TuiMode_OptionsBeforeFile_RoutesToTuiMode()
    {
        // "--tab 2 <file>" should enter TUI mode, not fall through to subcommand parser.
        // We use a nonexistent file to avoid actually launching the TUI — the key assertion
        // is that we get "File not found" from RunTui, not a System.CommandLine parse error.
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "--tab", "2", "nonexistent-assembly.dll");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        // Should NOT contain System.CommandLine error text
        Assert.DoesNotContain("Required command was not provided", stderr);
    }

    /// <summary>
    /// Verifies tui mode options after file still work.
    /// </summary>
    [TestMethod]
    public async Task TuiMode_OptionsAfterFile_StillWork()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "nonexistent-assembly.dll", "--tab", "2");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Required command was not provided", stderr);
    }

    // --- P2: --escape-timeout option routing ---

    /// <summary>
    /// Verifies tui mode escape timeout option routes to tui mode.
    /// </summary>
    [TestMethod]
    public async Task TuiMode_EscapeTimeoutOption_RoutesToTuiMode()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "--escape-timeout", "200", "nonexistent-assembly.dll");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Required command was not provided", stderr);
    }

    /// <summary>
    /// Verifies tui mode short escape timeout alias routes to tui mode.
    /// </summary>
    [TestMethod]
    public async Task TuiMode_ShortEscapeTimeoutAlias_RoutesToTuiMode()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "-e", "200", "nonexistent-assembly.dll");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Required command was not provided", stderr);
    }

    /// <summary>
    /// Verifies diff mode escape timeout option accepted.
    /// </summary>
    [TestMethod]
    public async Task DiffMode_EscapeTimeoutOption_Accepted()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "diff", "--escape-timeout", "200", "nonexistent-left.dll", "nonexistent-right.dll");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Unrecognized", stderr);
    }

    /// <summary>
    /// Verifies diff mode short escape timeout alias accepted.
    /// </summary>
    [TestMethod]
    public async Task DiffMode_ShortEscapeTimeoutAlias_Accepted()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "diff", "-e", "200", "nonexistent-left.dll", "nonexistent-right.dll");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Unrecognized", stderr);
    }

    // --- No-args exit code (WinGet validation) ---

    /// <summary>
    /// Verifies no args shows help and returns zero.
    /// </summary>
    [TestMethod]
    public async Task NoArgs_ShowsHelpAndReturnsZero()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync();

        Assert.AreEqual(0, exitCode);
        Assert.Contains("dotsider", stdout);
        Assert.Contains("Commands:", stdout);
    }

    /// <summary>
    /// Verifies json flag alone returns non zero.
    /// </summary>
    [TestMethod]
    public async Task JsonFlagAlone_ReturnsNonZero()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync("--json");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("Required command was not provided", stderr);
    }

    // --- Apphost Detection ---

    /// <summary>
    /// Verifies analyze apphost auto redirects to managed dll.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Apphost_AutoRedirectsToManagedDll()
    {
        var (exitCode, stdout, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.HelloWorldExe);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("apphost", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HelloWorld", stdout);
    }

    // --- Fields ---

    /// <summary>Verifies that --fields lists field definitions in text mode.</summary>
    [TestMethod]
    public async Task Analyze_Fields_ListsFieldDefinitions()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--fields");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Namespace", stdout);
        Assert.Contains("Name", stdout);
        Assert.Contains("Signature", stdout);
    }

    /// <summary>Verifies that --fields with --json outputs a JSON array.</summary>
    [TestMethod]
    public async Task Analyze_Fields_Json_OutputsJson()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--fields", "--json");

        Assert.AreEqual(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.AreEqual(System.Text.Json.JsonValueKind.Array, json.ValueKind);
        Assert.IsGreaterThan(0, json.GetArrayLength());
    }

    // --- Bundle ---

    /// <summary>Verifies that --bundle shows the manifest for a single-file bundle.</summary>
    [TestMethod]
    public async Task Analyze_Bundle_ShowsManifest()
    {
        Assert.IsNotNull(Fixture.SelfContainedConsoleExe);
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.SelfContainedConsoleExe!, "--bundle");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Bundle version:", stdout);
        Assert.Contains("Entries:", stdout);
    }

    /// <summary>Verifies that --bundle with --json outputs structured manifest data.</summary>
    [TestMethod]
    public async Task Analyze_Bundle_Json_OutputsJson()
    {
        Assert.IsNotNull(Fixture.SelfContainedConsoleExe);
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.SelfContainedConsoleExe!, "--bundle", "--json");

        Assert.AreEqual(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.IsGreaterThan(0, json.GetProperty("fileCount").GetInt32());
    }

    /// <summary>Verifies that --bundle on a non-bundle file returns an error.</summary>
    [TestMethod]
    public async Task Analyze_Bundle_NonBundle_ReturnsError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--bundle");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("not a single-file bundle", stderr);
    }

    /// <summary>
    /// Verifies that the real CLI reports a malformed recognized bundle without exposing parser details.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Bundle_MalformedManifest_ReturnsStableError()
    {
        var path = SyntheticSingleFileBundle.Create(fileCount: 0);
        try
        {
            var (exitCode, _, stderr) = await RunDotsiderAsync("analyze", path, "--bundle");

            Assert.AreEqual(1, exitCode);
            Assert.Contains("Error: Invalid single-file bundle manifest", stderr);
            Assert.DoesNotContain("file count", stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies that --bundle -o rejects writing to the same input file.</summary>
    [TestMethod]
    public async Task Analyze_Bundle_SameInputAndOutput_RejectsWithError()
    {
        Assert.IsNotNull(Fixture.SelfContainedConsoleExe);
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.SelfContainedConsoleExe!, "--bundle", "-o", Fixture.SelfContainedConsoleExe!);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("Output path cannot be the same as the input file", stderr);
    }

    /// <summary>Verifies that default output for a bundle-backed assembly shows DisplayName.</summary>
    [TestMethod]
    public async Task Analyze_DefaultOutput_BundleBacked_ShowsDisplayName()
    {
        Assert.IsNotNull(Fixture.SelfContainedConsoleExe);
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.SelfContainedConsoleExe!);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("from bundle", stdout);
    }

    /// <summary>Verifies that default JSON output for a bundle-backed assembly includes bundle properties.</summary>
    [TestMethod]
    public async Task Analyze_DefaultOutput_BundleBacked_Json_IncludesProperties()
    {
        Assert.IsNotNull(Fixture.SelfContainedConsoleExe);
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.SelfContainedConsoleExe!, "--json");

        Assert.AreEqual(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.IsTrue(json.GetProperty("isBundleBacked").GetBoolean());
        Assert.AreEqual("SelfContainedConsole.dll", json.GetProperty("displayName").GetString());
        Assert.IsNotNull(json.GetProperty("preferredRuntimePack").GetString());
    }

    /// <summary>
    /// Verifies <c>analyze --deps --json</c> emits a transitive graph with nodes at depth
    /// greater than zero and edges whose source is not always the root, and that no internal
    /// navigation fields leak into the payload.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Deps_Json_EmitsTransitiveGraphWithoutNavigationLeak()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--deps", "--json");

        Assert.AreEqual(0, exitCode);
        var root = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.IsTrue(root.TryGetProperty("graph", out var graph));
        Assert.IsTrue(graph.TryGetProperty("nodes", out var nodes));
        Assert.IsTrue(graph.TryGetProperty("edges", out var edges));

        string? rootId = null;
        var anyDepthOverZero = false;
        foreach (var n in nodes.EnumerateArray())
        {
            Assert.IsTrue(n.TryGetProperty("id", out var id));
            foreach (var leak in new[]
            {
                "resolvedPath", "referencingFilePath", "referencingBundlePath",
                "referencingTargetFramework", "referencingPreferredRuntimePack",
                "candidateProbePath", "isFrameworkAssembly", "resolved",
            })
            {
                Assert.IsFalse(n.TryGetProperty(leak, out _), $"node must not expose {leak}");
            }

            if (n.TryGetProperty("isRoot", out var isRoot) && isRoot.GetBoolean())
                rootId = id.GetString();
            if (n.TryGetProperty("depth", out var depth) && depth.GetInt32() > 0)
                anyDepthOverZero = true;
        }

        Assert.IsTrue(anyDepthOverZero);
        Assert.IsNotNull(rootId);

        var anyNonRootSource = false;
        foreach (var e in edges.EnumerateArray())
        {
            Assert.IsTrue(e.TryGetProperty("sourceId", out var src));
            if (src.GetString() != rootId) anyNonRootSource = true;
        }
        Assert.IsTrue(anyNonRootSource);
    }

    /// <summary>
    /// Verifies <c>analyze --deps --json</c> on a Native AOT binary emits the compiled-in
    /// assemblies and the native import modules, with only non-default node kinds serialized.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Deps_NativeAot_Json_EmitsAssembliesAndImports()
    {
        TestSkip.When(Fixture.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--deps", "--json");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("System.Private.CoreLib", stdout);
        Assert.Contains("\"nativeImport\"", stdout);
    }

    /// <summary>
    /// Verifies managed <c>analyze --deps --json</c> output is byte-compatible with the
    /// pre-AOT shape: the default assembly kind is never serialized.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Deps_Managed_Json_OmitsDefaultKind()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--deps", "--json");

        Assert.AreEqual(0, exitCode);
        Assert.DoesNotContain("\"kind\"", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --size</c> on a Native AOT binary with an mstat sidecar prints the
    /// per-assembly breakdown and the data categories instead of an empty tree.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Size_NativeAot_PrintsAssemblyBreakdown()
    {
        TestSkip.When(Fixture.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--size");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("System.Private.CoreLib", stdout);
        Assert.Contains("Blobs", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --size --json</c> on a Native AOT binary carries the new node
    /// kinds and the dependency-graph node names that make the tree joinable.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Size_NativeAot_Json_HasAotKindsAndNodeNames()
    {
        TestSkip.When(Fixture.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--size", "--json");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("\"category\"", stdout);
        Assert.Contains("aotNodeName", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --size --json</c> on a managed assembly is unchanged by the AOT
    /// additions: no aotNodeName property appears.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Size_Managed_Json_HasNoAotProperties()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--size", "--json");

        Assert.AreEqual(0, exitCode);
        Assert.DoesNotContain("aotNodeName", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --symbols</c> on a Native AOT binary prints the provenance header
    /// and the symbol table.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Symbols_NativeAot_PrintsTable()
    {
        TestSkip.When(Fixture.NativeAotConsoleSymbols is null, "native symbols were not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--symbols");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Source:", stdout);
        Assert.Contains("Symbols (", stdout);
        Assert.Contains("0x", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --symbols --json</c> carries the provenance and the symbol list.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Symbols_NativeAot_Json_CarriesProvenance()
    {
        TestSkip.When(Fixture.NativeAotConsoleSymbols is null, "native symbols were not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--symbols", "--json");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("\"source\"", stdout);
        Assert.Contains("\"status\"", stdout);
        Assert.Contains("\"symbols\"", stdout);
        Assert.Contains("virtualAddress", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --symbols</c> on a managed assembly exits 1 — there are no native
    /// symbols to read.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Symbols_Managed_ExitsOne()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--symbols");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("managed", stderr);
    }

    /// <summary>
    /// Verifies default and JSON analyze output report a raw SDK WebAssembly module as Wasm.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Wasm_PrintsModuleSummary()
    {
        var wasmPath = GetWasmNativePath();

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", wasmPath);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Kind:       WebAssembly (.NET)", stdout);
        Assert.Contains("Functions:", stdout);
        Assert.Contains("Symbols:", stdout);

        var (jsonExitCode, jsonStdout, _) = await RunDotsiderAsync(
            "analyze", wasmPath, "--json");

        Assert.AreEqual(0, jsonExitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonStdout);
        Assert.AreEqual("wasm", json.GetProperty("binaryKind").GetString());
        Assert.AreEqual("Wasm32", json.GetProperty("architecture").GetString());
        Assert.IsGreaterThan(0, json.GetProperty("wasm").GetProperty("definedFunctionCount").GetInt32());
    }

    /// <summary>
    /// Verifies <c>analyze --symbols</c> on a raw Wasm module prints WebAssembly provenance.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Symbols_Wasm_PrintsTable()
    {
        var wasmPath = GetWasmNativePath();

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", wasmPath, "--symbols");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("WebAssembly", stdout);
        Assert.Contains("Symbols (", stdout);
        Assert.Contains("0x", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --size</c> on a raw Wasm module reports the Wasm function tree.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Size_Wasm_PrintsFunctionBreakdown()
    {
        var wasmPath = GetWasmNativePath();

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", wasmPath, "--size");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("(Wasm)", stdout);
        Assert.Contains("Functions", stdout);
    }

    /// <summary>Verifies <c>analyze --disasm 0xVA</c> on a Native AOT binary prints a named listing.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Disasm_NativeAot_ByAddress_PrintsListing()
    {
        TestSkip.When(Fixture.NativeAotConsoleExe is null || !File.Exists(Fixture.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        ulong va;
        using (var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(Fixture.NativeAotConsoleExe!))
        {
            var fn = analyzer.NativeSymbols?.Symbols.FirstOrDefault(s =>
                s.Kind == Dotsider.Core.Analysis.Models.NativeSymbolKind.Function
                && s.ManagedName is not null && s.FileOffset is not null && s.Size > 0);
            Assert.IsNotNull(fn);
            va = fn!.VirtualAddress;
        }

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--disasm", $"0x{va:x}");

        Assert.AreEqual(0, exitCode);
        Assert.Contains($"0x{va:x}:", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --disasm</c> on a raw Wasm module decodes a real function body.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Disasm_Wasm_ByAddress_PrintsListing()
    {
        var wasmPath = GetWasmNativePath();

        ulong va;
        using (var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(wasmPath))
        {
            var symbol = FindWasmFunctionWithNamedCall(analyzer);
            va = symbol.VirtualAddress;
        }

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", wasmPath, "--disasm", $"0x{va:x}");

        Assert.AreEqual(0, exitCode);
        Assert.Contains($"0x{va:x}:", stdout);
        Assert.Contains("call", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --disasm</c> accepts WebAssembly function identifiers in the forms users
    /// see in wasm tooling: <c>func:N</c> and a bare decimal function index.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Disasm_Wasm_ByFunctionIndex_PrintsListing()
    {
        var wasmPath = GetWasmNativePath();

        string funcAlias;
        string funcIndex;
        using (var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(wasmPath))
        {
            var symbol = analyzer.NativeSymbols!.Symbols.First(s =>
                s.Aliases.Any(static alias => alias.StartsWith("func:", StringComparison.Ordinal)));
            funcAlias = symbol.Aliases.First(static alias => alias.StartsWith("func:", StringComparison.Ordinal));
            funcIndex = funcAlias["func:".Length..];
        }

        var (aliasExitCode, aliasStdout, _) = await RunDotsiderAsync(
            "analyze", wasmPath, "--disasm", funcAlias);
        var (indexExitCode, indexStdout, _) = await RunDotsiderAsync(
            "analyze", wasmPath, "--disasm", funcIndex);

        Assert.AreEqual(0, aliasExitCode);
        Assert.AreEqual(0, indexExitCode);
        Assert.Contains("func[", aliasStdout);
        Assert.Contains("func[", indexStdout);
    }

    /// <summary>
    /// Verifies Webcil-wrapped <c>.wasm</c> app assemblies use managed metadata CLI behavior.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_WebcilWasm_PrintsManagedMetadata()
    {
        TestSkip.When(Fixture.WasmConsoleWebcilWasm is null,
            "browser-wasm publish did not produce the Webcil app assembly on this leg.");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.WasmConsoleWebcilWasm!);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Kind:       Managed", stdout);
        Assert.Contains("Webcil:", stdout);
        Assert.DoesNotContain("Kind:       WebAssembly (.NET)", stdout);
    }

    /// <summary>Verifies <c>analyze --disasm</c> on a managed assembly exits 1 with a native-symbols error.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Disasm_Managed_ExitsOne()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--disasm", "Foo");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("native symbols", stderr);
    }

    /// <summary>
    /// Verifies the default <c>analyze --json</c> info carries the native symbol provenance
    /// fields for a Native AOT binary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Info_NativeAot_Json_CarriesSymbolProvenance()
    {
        TestSkip.When(Fixture.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--json");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("nativeSymbolCount", stdout);
        Assert.Contains("nativeSymbolSource", stdout);
        Assert.Contains("nativeSymbolStatus", stdout);
        Assert.Contains("nativeSymbolsPath", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --why</c> prints the root-first dependency chain for a compiled
    /// method when both sidecars are present.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Why_KnownType_PrintsChain()
    {
        TestSkip.When(Fixture.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        TestSkip.When(Fixture.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--why", "Program");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("in the binary? (root first)", stdout);
        Assert.Contains("1.", stdout);
    }

    /// <summary>
    /// Verifies <c>analyze --why --json</c> emits the chain as structured steps.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Why_Json_EmitsChainSteps()
    {
        TestSkip.When(Fixture.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        TestSkip.When(Fixture.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--why", "Program", "--json");

        Assert.AreEqual(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.IsGreaterThan(0, json.GetProperty("chain").GetArrayLength());
        Assert.IsFalse(string.IsNullOrEmpty(json.GetProperty("target").GetString()));
    }

    /// <summary>
    /// Verifies <c>analyze --why</c> with an unknown name errors with a clear message.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Why_UnknownName_Errors()
    {
        TestSkip.When(Fixture.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        TestSkip.When(Fixture.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--why", "NoSuchThingAnywhere12345");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("no compiled type or method matches", stderr);
    }

    /// <summary>
    /// Verifies <c>analyze --why</c> on a managed assembly explains the sidecar requirement.
    /// </summary>
    [TestMethod]
    public async Task Analyze_Why_ManagedAssembly_Errors()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--why", "Program");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("requires a Native AOT binary", stderr);
    }

    // --- Pre-ILC correlation ---

    /// <summary>
    /// Verifies plain <c>analyze</c> prints the pre-ILC probe summary without attaching.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Default_PreIlc_PrintsProbeSummaryWithoutAttaching()
    {
        TestSkip.When(Fixture.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Pre-ILC:", stdout);
        Assert.Contains("Origin:", stdout);
        // The counts summary attaches; a plain info dump must not print it.
        Assert.DoesNotContain("Correlation:", stdout);
    }

    /// <summary>
    /// Verifies the default <c>analyze --json</c> carries the <c>preIlc</c> probe object.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Default_PreIlc_Json_CarriesProbeObject()
    {
        TestSkip.When(Fixture.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--json");

        Assert.AreEqual(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        var preIlc = json.GetProperty("preIlc");
        Assert.IsTrue(preIlc.GetProperty("hasAttachableCompanion").GetBoolean());
        Assert.IsFalse(string.IsNullOrEmpty(preIlc.GetProperty("managedAssemblyPath").GetString()));
    }

    /// <summary>
    /// Verifies bare <c>--correlate</c> attaches and prints the correlation counts.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Correlate_Bare_PrintsCounts()
    {
        TestSkip.When(Fixture.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--correlate");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Correlation:", stdout);
        Assert.Contains("methods", stdout);
    }

    /// <summary>
    /// Verifies <c>--correlate Type.Method</c> resolves a unique method and prints its IL.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Correlate_ByName_PrintsIl()
    {
        TestSkip.When(Fixture.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--correlate", "Greeter.Describe");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Greeter::Describe", stdout);
        Assert.Contains("--- IL (pre-ILC) ---", stdout);
    }

    /// <summary>
    /// Verifies <c>--correlate 0xVA</c> resolves by address and prints the native listing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Correlate_ByAddress_PrintsNative()
    {
        TestSkip.When(Fixture.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");
        TestSkip.When(Fixture.NativeAotConsoleSymbols is null, "native symbols were not produced");

        ulong va;
        using (var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(Fixture.NativeAotConsoleExe!))
        {
            analyzer.AttachPreIlcCompanions();
            var correlation = analyzer.ManagedNativeIndex?.Methods.FirstOrDefault(m =>
                m.Status == Dotsider.Core.Analysis.Models.MethodCorrelationStatus.CorrelatedExact
                && m.NativeSymbols.Count > 0
                && m.NativeSymbols[0].FileOffset is not null);
            Assert.IsNotNull(correlation);
            va = correlation!.NativeSymbols[0].VirtualAddress;
        }

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--correlate", $"0x{va:x}");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("--- Native ---", stdout);
    }

    /// <summary>
    /// Verifies an ambiguous name (overloads) lists every candidate and exits non-zero.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Correlate_AmbiguousName_ListsCandidatesAndExitsNonZero()
    {
        TestSkip.When(Fixture.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.NativeAotConsoleExe!, "--correlate", "Greeter.Greet");

        Assert.AreEqual(2, exitCode);
        Assert.Contains("ambiguous", stderr);
        Assert.Contains("Greeter::Greet", stderr);
    }

    /// <summary>
    /// Verifies <c>--correlate</c> on a managed assembly explains the Native AOT requirement.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Analyze_Correlate_ManagedAssembly_ExitsOne()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", Fixture.RichLibraryDll, "--correlate", "Foo");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("requires a Native AOT binary", stderr);
    }

    // --- Helpers ---

    private static Task<(int ExitCode, string Stdout, string Stderr)> RunDotsiderAsync(
        params string[] arguments) => TestHelpers.RunDotsiderAsync(arguments);

    private static Dotsider.Core.Analysis.Models.NativeSymbol FindWasmFunctionWithNamedCall(
        Dotsider.Core.Analysis.AssemblyAnalyzer analyzer)
    {
        var info = analyzer.NativeSymbols;
        Assert.IsNotNull(info);
        foreach (var symbol in info.Symbols.Take(512))
        {
            var result = Dotsider.Core.Analysis.Disasm.NativeDisassembler.DisassembleSymbol(analyzer, symbol);
            if (result is null)
                continue;

            if (result.Value.Instructions.Any(static instruction => instruction.TargetName is not null))
                return symbol;
        }

        throw new InvalidOperationException("No Wasm function with a named direct call was found.");
    }

    private static string GetWasmNativePath()
    {
        TestSkip.When(Fixture.WasmConsoleNativeWasm is null && Fixture.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return Fixture.WasmConsoleNativeWasm ?? Fixture.ReadyToRunConsoleWasmNativeWasm!;
    }
}
