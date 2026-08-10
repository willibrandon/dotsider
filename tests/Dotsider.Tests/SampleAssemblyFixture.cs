using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// MSTest fixture that builds and exposes paths to all sample assemblies shared across tests.
/// </summary>
internal class SampleAssemblyFixture
{
    private string _repoRoot = null!;

    // Exe samples (have both .dll and apphost binary)
    /// <summary>
    /// Path to the built HelloWorld.dll managed assembly.
    /// </summary>
    public string HelloWorldDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built HelloWorld apphost executable.
    /// </summary>
    public string HelloWorldExe { get; private set; } = null!;
    /// <summary>
    /// Path to the built ComplexApp.dll sample assembly.
    /// </summary>
    public string ComplexAppDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built ComplexApp apphost executable.
    /// </summary>
    public string ComplexAppExe { get; private set; } = null!;
    /// <summary>
    /// Path to the built MinimalApi.dll sample assembly.
    /// </summary>
    public string MinimalApiDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built MinimalApi apphost executable.
    /// </summary>
    public string MinimalApiExe { get; private set; } = null!;

    // Library samples
    /// <summary>
    /// Path to the built RichLibrary.dll sample assembly.
    /// </summary>
    public string RichLibraryDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built RichLibrary v2 sample assembly.
    /// </summary>
    public string RichLibraryV2Dll { get; private set; } = null!;
    /// <summary>
    /// Path to the built NativeLib.dll sample assembly.
    /// </summary>
    public string NativeLibDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built EmptyLib.dll sample assembly.
    /// </summary>
    public string EmptyLibDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built EmbeddedSourceLib.dll sample assembly.
    /// </summary>
    public string EmbeddedSourceLibDll { get; private set; } = null!;
    /// <summary>
    /// Path to the compiler-built terminal-control sample assembly.
    /// </summary>
    public string TerminalControlLibDll { get; private set; } = null!;

    /// <summary>
    /// Path to the built AppLocalRollForward.dll sample assembly. Reproduces the AppLocal
    /// framework-PKT roll-forward scenario: Microsoft.Diagnostics.Tracing.TraceEvent's
    /// compiled AssemblyRef targets <c>Microsoft.Diagnostics.NETCore.Client v0.2.10.10501</c>,
    /// while the package-restored runtime asset on disk is a strictly higher build of the
    /// same simple name and PKT.
    /// </summary>
    public string AppLocalRollForwardDll { get; private set; } = null!;

    // NuGet package
    /// <summary>
    /// Path to the built RichLibrary NuGet package.
    /// </summary>
    public string RichLibraryNupkg { get; private set; } = null!;

    // .NET Framework sample (Windows only — net48 requires Windows)
    /// <summary>
    /// Path to the built .NET Framework sample executable (Windows only).
    /// </summary>
    public string? NetFxConsoleExe { get; private set; }

    /// <summary>
    /// Path to the built NetFxBindingRedirects sample executable (Windows only).
    /// </summary>
    public string? NetFxBindingRedirectsExe { get; private set; }

    /// <summary>
    /// Map of interesting assembly references in the NetFxBindingRedirects sample to the
    /// runtime oracle entry recording what the actual .NET Framework CLR loaded for them.
    /// Tests use this dictionary to enforce literal CLR accuracy on dotsider's NetFxBinder.
    /// <see langword="null"/> on non-Windows.
    /// </summary>
    public IReadOnlyDictionary<string, NetFxOracleEntry>? NetFxBindingRedirectsOracle { get; private set; }

    /// <summary>
    /// Path to the built NetFxBindingRedirects.Clr2 sample executable (Windows only). The CLR 2 /
    /// .NET Framework 3.5 sibling of <see cref="NetFxBindingRedirectsExe"/>; exercises the GAC
    /// at <c>%WINDIR%\assembly</c>, framework runtime <c>v2.0.50727</c>, no-prefix GAC tokens,
    /// and the v3.0 reference-assemblies allowlist via WindowsBase.
    /// </summary>
    public string? NetFxBindingRedirectsClr2Exe { get; private set; }

    /// <summary>
    /// Map of interesting assembly references in the NetFxBindingRedirects.Clr2 sample to the
    /// runtime oracle entry recording what the actual CLR 2.0 runtime loaded for them.
    /// <see langword="null"/> when CLR 2 isn't present on the host (see <see cref="Clr2RuntimePresent"/>)
    /// or on non-Windows.
    /// </summary>
    public IReadOnlyDictionary<string, NetFxOracleEntry>? NetFxBindingRedirectsClr2Oracle { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>mscorlib.dll</c> exists under the architecture-correct
    /// <c>%WINDIR%\Microsoft.NET\Framework[64]\v2.0.50727</c> directory, i.e. the .NET Framework
    /// 3.5 runtime is installed. Tests that probe live CLR 2 paths gate on this; synthetic
    /// temp-tree tests do not.
    /// </summary>
    public bool Clr2RuntimePresent { get; private set; }

    // Dotted assembly name sample (e.g., Company.Product.Tool)
    /// <summary>
    /// Path to the dotted-name sample assembly (Dotted.Name.App.dll).
    /// </summary>
    public string DottedNameAppDll { get; private set; } = null!;
    /// <summary>
    /// Path to the dotted-name sample apphost executable.
    /// </summary>
    public string DottedNameAppExe { get; private set; } = null!;

    // NativeAOT sample
    /// <summary>
    /// Path to the published NativeAOT sample executable.
    /// </summary>
    public string? NativeAotConsoleExe { get; private set; }

    /// <summary>
    /// Path to the ILC size report published next to the NativeAOT sample, or null when the
    /// publish did not produce one. Tests gate on this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotConsoleMstat { get; private set; }

    /// <summary>
    /// Path to the ILC dependency-graph DGML published next to the NativeAOT sample (the
    /// codegen graph, falling back to the scan graph), or null when the publish did not
    /// produce one. Tests gate on this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotConsoleDgml { get; private set; }

    /// <summary>
    /// Path to the native symbol file beside the NativeAOT sample — the Windows PDB, the Linux
    /// <c>.dbg</c>, or the macOS dSYM inner DWARF file — for the current platform, or null when
    /// the publish did not produce one. Tests gate on this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotConsoleSymbols { get; private set; }

    /// <summary>
    /// Path to the NativeAOT sample's <c>.dSYM</c> bundle directory (macOS), or null when the
    /// publish did not produce one. Tests gate on this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotConsoleDsym { get; private set; }

    /// <summary>
    /// Path to the NativeAOT sample's pre-ILC managed input in its intermediate tree
    /// (<c>obj\Release\&lt;tfm&gt;\&lt;rid&gt;\NativeAotConsole.dll</c>), or null. Tests gate on
    /// this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotConsoleManagedDll { get; private set; }

    /// <summary>
    /// Path to the portable PDB beside the pre-ILC managed input, or null.
    /// </summary>
    public string? NativeAotConsoleManagedPdb { get; private set; }

    /// <summary>
    /// Path to the NativeAOT sample's ILC response file
    /// (<c>obj\...\native\NativeAotConsole.ilc.rsp</c>), or null.
    /// </summary>
    public string? NativeAotConsoleIlcRsp { get; private set; }

    /// <summary>
    /// Path to the published V2 NativeAOT sample executable. The project folder is
    /// <c>NativeAotConsoleV2</c> but its AssemblyName is pinned to <c>NativeAotConsole</c> so
    /// the mstat pair diffs as two builds of the same application — the output file is named
    /// <c>NativeAotConsole</c>, disambiguated by folder.
    /// </summary>
    public string? NativeAotConsoleV2Exe { get; private set; }

    /// <summary>
    /// Path to the ILC size report published next to the V2 NativeAOT sample, or null when the
    /// publish did not produce one. Tests gate on this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotConsoleV2Mstat { get; private set; }

    /// <summary>
    /// Path to the ILC dependency-graph DGML published next to the V2 NativeAOT sample (the
    /// codegen graph, falling back to the scan graph), or null when the publish did not
    /// produce one. Tests gate on this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotConsoleV2Dgml { get; private set; }

    /// <summary>
    /// Path to the NativeAOT sample published with <c>UseArtifactsOutput</c> — the exe under
    /// <c>artifacts\publish\&lt;proj&gt;\&lt;pivot&gt;</c> — or null when the publish did not run.
    /// Tests gate on this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotArtifactsExe { get; private set; }

    /// <summary>
    /// Path to the published Native AOT shared-library binary (<c>.dll</c>/<c>.so</c>/<c>.dylib</c>
    /// per platform), or null when the publish did not run. Tests gate on this with
    /// <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? NativeAotLibraryBinary { get; private set; }

    /// <summary>
    /// Path to the published HardwareIntrinsics NativeAOT sample — one method per intrinsic family —
    /// or null when the publish did not run. Tests gate on this with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? HardwareIntrinsicsExe { get; private set; }

    // Self-contained single-file sample
    /// <summary>
    /// Path to the published self-contained single-file sample executable.
    /// </summary>
    public string? SelfContainedConsoleExe { get; private set; }

    // ReadyToRun (crossgen2) samples
    /// <summary>
    /// Path to the published non-composite ReadyToRun assembly (crossgen'd in place), or null when
    /// the crossgen2 publish did not run for this RID. Tests gate with <c>Assert.SkipWhen</c>.
    /// </summary>
    public string? ReadyToRunConsoleDll { get; private set; }

    /// <summary>
    /// Path to the ReadyToRun sample's apphost executable (the apphost → companion redirect case),
    /// or null when the publish did not run.
    /// </summary>
    public string? ReadyToRunConsoleExe { get; private set; }

    /// <summary>
    /// Path to the real <c>win-x86</c> ReadyToRun console DLL when the SDK can publish it.
    /// Decoder tests use this crossgen2 output to exercise the x86 path against a real image.
    /// <see langword="null"/> means the current SDK feed lacks that RID's packs.
    /// </summary>
    public string? ReadyToRunConsoleX86Dll { get; private set; }

    /// <summary>
    /// Path to the real <c>linux-arm</c> ReadyToRun console DLL when the SDK can publish it.
    /// Decoder tests use this crossgen2 output to exercise the ARM32 Thumb path against a real image.
    /// <see langword="null"/> means the current SDK feed lacks that RID's packs.
    /// </summary>
    public string? ReadyToRunConsoleArm32Dll { get; private set; }

    /// <summary>
    /// Path to the real <c>linux-riscv64</c> ReadyToRun console DLL when the SDK can publish it.
    /// Decoder tests use this crossgen2 output to exercise the RISC-V64 path against a real image.
    /// <see langword="null"/> means the current SDK feed lacks that RID's packs.
    /// </summary>
    public string? ReadyToRunConsoleRiscV64Dll { get; private set; }

    /// <summary>
    /// Path to the real <c>linux-loongarch64</c> ReadyToRun console DLL when the SDK can publish it.
    /// Decoder tests use this crossgen2 output to exercise the LoongArch64 path against a real image.
    /// <see langword="null"/> means the current SDK feed lacks that RID's packs.
    /// </summary>
    public string? ReadyToRunConsoleLoongArch64Dll { get; private set; }

    /// <summary>
    /// Path to the SDK-produced browser Wasm runtime module when the wasm-tools workload is present.
    /// Decoder tests parse the module's code section and disassemble real function bodies.
    /// <see langword="null"/> means the current SDK/workload set cannot publish browser Wasm.
    /// </summary>
    public string? ReadyToRunConsoleWasmNativeWasm { get; private set; }

    /// <summary>
    /// Path to the dedicated SDK-produced browser Wasm runtime module from <c>samples/WasmConsole</c>.
    /// Raw Wasm open, symbols, disassembly, and size tests prefer this focused fixture.
    /// <see langword="null"/> means the current SDK/workload set cannot publish browser Wasm.
    /// </summary>
    public string? WasmConsoleNativeWasm { get; private set; }

    /// <summary>
    /// Path to the AOT-compiled browser Wasm runtime module from <c>samples/WasmConsole</c>.
    /// This fixture is produced with <c>RunAOTCompilation=true</c> when the SDK supports it.
    /// <see langword="null"/> means the current SDK/workload set cannot publish browser Wasm AOT.
    /// </summary>
    public string? WasmConsoleAotNativeWasm { get; private set; }

    /// <summary>
    /// Path to the Webcil-wrapped managed app assembly from <c>samples/WasmConsole</c>.
    /// Managed Webcil tests use this to verify .wasm opens as metadata/IL, not native Wasm.
    /// <see langword="null"/> means the current SDK/workload set cannot publish browser Wasm.
    /// </summary>
    public string? WasmConsoleWebcilWasm { get; private set; }

    /// <summary>
    /// Path to the composite global image (<c>ReadyToRunComposite.r2r.dll</c>) — metadata-less native
    /// PE whose components resolve from siblings — or null when the composite publish did not run.
    /// </summary>
    public string? ReadyToRunCompositeImage { get; private set; }

    /// <summary>
    /// Path to a composite component DLL (<c>ReadyToRunComposite.dll</c>) that carries an
    /// <c>OwnerCompositeExecutable</c> pointing at <see cref="ReadyToRunCompositeImage"/>, or null.
    /// </summary>
    public string? ReadyToRunCompositeComponent { get; private set; }

    /// <summary>
    /// Path to the second composite component (<c>ReadyToRunComponentLib.dll</c>), beside the
    /// composite for MVID resolution, or null when the composite publish did not run.
    /// </summary>
    public string? ReadyToRunComponentLibDll { get; private set; }

    /// <summary>The <c>ReadyToRunComposite.dll</c> component's module version id, for identity assertions.</summary>
    public Guid ReadyToRunCompositeComponentMvid { get; private set; }

    /// <summary>The <c>ReadyToRunComponentLib.dll</c> component's module version id, for identity assertions.</summary>
    public Guid ReadyToRunComponentLibMvid { get; private set; }

    // Non-.NET binary for error case testing
    /// <summary>
    /// Path to a small non-.NET binary used for BadImageFormatException scenarios.
    /// </summary>
    public string NonDotNetBinaryPath { get; private set; } = null!;

    /// <summary>
    /// Builds all sample projects and materializes the fixture paths.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _repoRoot = TestHelpers.GetRepoRoot();

        // Build core samples in parallel
        var builds = new List<Task>
        {
            BuildProject("samples/HelloWorld"),
            BuildProject("samples/RichLibrary"),
            BuildProject("samples/RichLibraryV2"),
            BuildProject("samples/ComplexApp"),
            BuildProject("samples/MinimalApi"),
            BuildProject("samples/NativeLib"),
            BuildProject("samples/EmptyLib"),
            BuildProject("samples/EmbeddedSourceLib"),
            BuildProject("samples/TerminalControlLib"),
            BuildProject("samples/Dotted.Name.App"),
            BuildProject("samples/AppLocalRollForward"),
        };

        // net48 needs Windows; NativeAOT builds on all platforms.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builds.Add(BuildProject("samples/NetFxConsole"));
            // Build only the EXE — its csproj's AfterTargets runs MSBuild on the five helper
            // projects and copies their outputs into lib/, external/, fr/. Building the helpers
            // separately would race with the EXE's post-build copy.
            builds.Add(BuildProject("samples/NetFxBindingRedirects"));
            // CLR 2 sibling — same shape, AfterTargets stages its own helpers.
            builds.Add(BuildProject("samples/NetFxBindingRedirects.Clr2"));
        }

        builds.Add(PublishNativeAotProject("samples/NativeAotConsole"));
        builds.Add(PublishNativeAotProject("samples/NativeAotConsoleV2"));
        builds.Add(PublishNativeAotProject("samples/NativeAotArtifactsConsole"));
        builds.Add(PublishNativeAotProject("samples/NativeAotLibrary"));
        builds.Add(PublishNativeAotProject("samples/HardwareIntrinsics"));
        builds.Add(PublishSelfContainedProject("samples/SelfContainedConsole"));

        // ReadyToRun crossgen2 publishes: the console framework-dependent (small apphost + R2R dll),
        // the composite self-contained (crossgen bundles the app assemblies into a *.r2r.dll with the
        // components beside it). Tolerant of a RID without crossgen2 — R2R tests then skip.
        builds.Add(PublishReadyToRunProject("samples/ReadyToRunConsole", selfContained: false));
        builds.Add(PublishReadyToRunProject("samples/ReadyToRunConsole", "win-x86", selfContained: false));
        builds.Add(PublishReadyToRunProject("samples/ReadyToRunConsole", "linux-arm", selfContained: true));
        builds.Add(PublishReadyToRunProject("samples/ReadyToRunConsole", "linux-riscv64", selfContained: true));
        builds.Add(PublishReadyToRunProject("samples/ReadyToRunConsole", "linux-loongarch64", selfContained: true));
        builds.Add(PublishWasmProject("samples/ReadyToRunConsole"));
        builds.Add(PublishWasmProject("samples/WasmConsole"));
        builds.Add(PublishReadyToRunProject("samples/ReadyToRunComposite", selfContained: true));

        await Task.WhenAll(builds);
        await PublishWasmProject("samples/WasmConsole", runAotCompilation: true);

        var config = TestProcessEnvironment.DebugBuildConfiguration;
        var releaseConfig = TestProcessEnvironment.ReleaseBuildConfiguration;
        var tfm = "net10.0";
        var apphostExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        HelloWorldDll = SamplePath("HelloWorld", config, tfm, "HelloWorld.dll");
        HelloWorldExe = SamplePath("HelloWorld", config, tfm, $"HelloWorld{apphostExt}");
        ComplexAppDll = SamplePath("ComplexApp", config, tfm, "ComplexApp.dll");
        ComplexAppExe = SamplePath("ComplexApp", config, tfm, $"ComplexApp{apphostExt}");
        MinimalApiDll = SamplePath("MinimalApi", config, tfm, "MinimalApi.dll");
        MinimalApiExe = SamplePath("MinimalApi", config, tfm, $"MinimalApi{apphostExt}");
        RichLibraryDll = SamplePath("RichLibrary", config, tfm, "RichLibrary.dll");
        RichLibraryV2Dll = SamplePath("RichLibraryV2", config, tfm, "RichLibrary.dll");
        NativeLibDll = SamplePath("NativeLib", config, tfm, "NativeLib.dll");
        EmptyLibDll = SamplePath("EmptyLib", config, tfm, "EmptyLib.dll");
        EmbeddedSourceLibDll = SamplePath("EmbeddedSourceLib", config, tfm, "EmbeddedSourceLib.dll");
        TerminalControlLibDll = SamplePath("TerminalControlLib", config, tfm, "TerminalControlLib.dll");
        AppLocalRollForwardDll = SamplePath(
            "AppLocalRollForward", config, tfm, "AppLocalRollForward.dll");
        DottedNameAppDll = SamplePath("Dotted.Name.App", config, tfm, "Dotted.Name.App.dll");
        DottedNameAppExe = SamplePath("Dotted.Name.App", config, tfm, $"Dotted.Name.App{apphostExt}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            NetFxConsoleExe = Path.Combine(_repoRoot, "samples", "NetFxConsole",
                "bin", config, "net48", "NetFxConsole.exe");

            NetFxBindingRedirectsExe = Path.Combine(_repoRoot, "samples", "NetFxBindingRedirects",
                "bin", config, "net48", "NetFxBindingRedirects.exe");

            NetFxBindingRedirectsOracle = await CaptureNetFxOracleAsync(NetFxBindingRedirectsExe);

            NetFxBindingRedirectsClr2Exe = Path.Combine(_repoRoot, "samples", "NetFxBindingRedirects.Clr2",
                "bin", config, "net35", "NetFxBindingRedirects.Clr2.exe");

            // CLR 2 runtime detection: arch-aware. Either Framework64 or Framework slot may host
            // the v2.0.50727 mscorlib. Test the architecture-correct slot first; accept the other
            // as a fallback for x86-only hosts.
            var windir = Environment.GetEnvironmentVariable("WINDIR");
            if (!string.IsNullOrEmpty(windir))
            {
                Clr2RuntimePresent =
                    File.Exists(Path.Combine(windir!, "Microsoft.NET", "Framework64", "v2.0.50727", "mscorlib.dll"))
                    || File.Exists(Path.Combine(windir!, "Microsoft.NET", "Framework", "v2.0.50727", "mscorlib.dll"));
            }

            // Run the runtime oracle only when CLR 2 is installed; otherwise the EXE either
            // shims to CLR 4 (defeating the test) or fails to start. CLR 2 runtime-dependent
            // tests skip when the oracle map is null.
            if (Clr2RuntimePresent)
                NetFxBindingRedirectsClr2Oracle = await CaptureNetFxOracleAsync(NetFxBindingRedirectsClr2Exe);
        }

        var rid = RuntimeInformation.RuntimeIdentifier;
        NativeAotConsoleExe = Path.Combine(_repoRoot, "samples", "NativeAotConsole",
            "bin", releaseConfig, tfm, rid, "publish", $"NativeAotConsole{apphostExt}");

        // ILC sidecars copied next to the exe by the sample's publish target. Null when the
        // toolchain did not produce them, so sidecar tests skip rather than fail.
        var aotPublishDir = Path.GetDirectoryName(NativeAotConsoleExe)!;
        NativeAotConsoleMstat = ExistingPathOrNull(Path.Combine(aotPublishDir, "NativeAotConsole.mstat"));
        NativeAotConsoleDgml =
            ExistingPathOrNull(Path.Combine(aotPublishDir, "NativeAotConsole.codegen.dgml.xml"))
            ?? ExistingPathOrNull(Path.Combine(aotPublishDir, "NativeAotConsole.scan.dgml.xml"));

        // Native symbols land beside the exe per platform: PDB (Windows), .dbg (Linux), or the
        // DWARF file inside the dSYM bundle (macOS).
        NativeAotConsoleSymbols =
            ExistingPathOrNull(Path.Combine(aotPublishDir, "NativeAotConsole.pdb"))
            ?? ExistingPathOrNull(Path.Combine(aotPublishDir, "NativeAotConsole.dbg"))
            ?? ExistingPathOrNull(Path.Combine(
                aotPublishDir, "NativeAotConsole.dSYM", "Contents", "Resources", "DWARF", "NativeAotConsole"));

        NativeAotConsoleDsym = ExistingDirOrNull(Path.Combine(aotPublishDir, "NativeAotConsole.dSYM"));

        // The pre-ILC inputs stay in the intermediate tree — the sidecar probe's territory.
        var aotObjDir = Path.Combine(_repoRoot, "samples", "NativeAotConsole",
            "obj", releaseConfig, tfm, rid);
        NativeAotConsoleManagedDll = ExistingPathOrNull(Path.Combine(aotObjDir, "NativeAotConsole.dll"));
        NativeAotConsoleManagedPdb = ExistingPathOrNull(Path.Combine(aotObjDir, "NativeAotConsole.pdb"));
        NativeAotConsoleIlcRsp = ExistingPathOrNull(
            Path.Combine(aotObjDir, "native", "NativeAotConsole.ilc.rsp"));

        // V2 of the AOT sample: same AssemblyName, so the publish output is also named
        // NativeAotConsole — the project folder is what tells the two builds apart.
        var aotV2PublishDir = Path.Combine(_repoRoot, "samples", "NativeAotConsoleV2",
            "bin", releaseConfig, tfm, rid, "publish");
        NativeAotConsoleV2Exe = ExistingPathOrNull(
            Path.Combine(aotV2PublishDir, $"NativeAotConsole{apphostExt}"));
        NativeAotConsoleV2Mstat = ExistingPathOrNull(
            Path.Combine(aotV2PublishDir, "NativeAotConsole.mstat"));
        NativeAotConsoleV2Dgml =
            ExistingPathOrNull(Path.Combine(aotV2PublishDir, "NativeAotConsole.codegen.dgml.xml"))
            ?? ExistingPathOrNull(Path.Combine(aotV2PublishDir, "NativeAotConsole.scan.dgml.xml"));

        // Artifacts-layout pivot names are SDK-internal; glob for the exe instead of parsing.
        NativeAotArtifactsExe = FindArtifactsPublishOutput(
            "NativeAotArtifactsConsole", $"NativeAotArtifactsConsole{apphostExt}");

        var libPublishDir = Path.Combine(_repoRoot, "samples", "NativeAotLibrary",
            "bin", releaseConfig, tfm, rid, "publish");
        NativeAotLibraryBinary =
            ExistingPathOrNull(Path.Combine(libPublishDir, "NativeAotLibrary.dll"))
            ?? ExistingPathOrNull(Path.Combine(libPublishDir, "NativeAotLibrary.so"))
            ?? ExistingPathOrNull(Path.Combine(libPublishDir, "NativeAotLibrary.dylib"));

        HardwareIntrinsicsExe = ExistingPathOrNull(Path.Combine(_repoRoot, "samples", "HardwareIntrinsics",
            "bin", releaseConfig, tfm, rid, "publish", $"HardwareIntrinsics{apphostExt}"));

        SelfContainedConsoleExe = Path.Combine(_repoRoot, "samples", "SelfContainedConsole",
            "bin", releaseConfig, tfm, rid, "publish", $"SelfContainedConsole{apphostExt}");

        // ReadyToRun publish outputs. Null when crossgen2 did not run for this RID, so R2R tests skip.
        var r2rConsoleDir = Path.Combine(_repoRoot, "samples", "ReadyToRunConsole",
            "bin", releaseConfig, tfm, rid, "publish");
        ReadyToRunConsoleDll = ExistingReadyToRunPathOrNull(
            Path.Combine(r2rConsoleDir, "ReadyToRunConsole.dll"));
        ReadyToRunConsoleExe = ExistingPathOrNull(Path.Combine(r2rConsoleDir, $"ReadyToRunConsole{apphostExt}"));
        ReadyToRunConsoleX86Dll = ExistingReadyToRunPathOrNull(Path.Combine(
            _repoRoot, "samples", "ReadyToRunConsole", "bin", releaseConfig,
            tfm, "win-x86", "publish", "ReadyToRunConsole.dll"));
        ReadyToRunConsoleArm32Dll = ExistingReadyToRunPathOrNull(Path.Combine(
            _repoRoot, "samples", "ReadyToRunConsole", "bin", releaseConfig,
            tfm, "linux-arm", "publish", "ReadyToRunConsole.dll"));
        ReadyToRunConsoleRiscV64Dll = ExistingReadyToRunPathOrNull(Path.Combine(
            _repoRoot, "samples", "ReadyToRunConsole", "bin", releaseConfig,
            tfm, "linux-riscv64", "publish", "ReadyToRunConsole.dll"));
        ReadyToRunConsoleLoongArch64Dll = ExistingReadyToRunPathOrNull(Path.Combine(
            _repoRoot, "samples", "ReadyToRunConsole", "bin", releaseConfig,
            tfm, "linux-loongarch64", "publish", "ReadyToRunConsole.dll"));
        ReadyToRunConsoleWasmNativeWasm = ExistingPathOrNull(Path.Combine(_repoRoot, "samples", "ReadyToRunConsole",
            "bin", releaseConfig, tfm, "browser-wasm", "publish", "dotnet.native.wasm"));
        WasmConsoleNativeWasm = ExistingPathOrNull(Path.Combine(_repoRoot, "samples", "WasmConsole",
            "bin", releaseConfig, tfm, "browser-wasm", "publish", "dotnet.native.wasm"));
        WasmConsoleAotNativeWasm = ExistingPathOrNull(Path.Combine(_repoRoot, "samples", "WasmConsole",
            "bin", releaseConfig, tfm, "browser-wasm-aot", "publish", "dotnet.native.wasm"));
        WasmConsoleWebcilWasm = ExistingPathOrNull(Path.Combine(_repoRoot, "samples", "WasmConsole",
            "bin", releaseConfig, tfm, "browser-wasm", "AppBundle", "_framework", "WasmConsole.wasm"));

        var r2rCompositeDir = Path.Combine(_repoRoot, "samples", "ReadyToRunComposite",
            "bin", releaseConfig, tfm, rid, "publish");
        ReadyToRunCompositeImage = ExistingReadyToRunPathOrNull(
            Path.Combine(r2rCompositeDir, "ReadyToRunComposite.r2r.dll"));
        ReadyToRunCompositeComponent = ReadyToRunCompositeImage is null
            ? null
            : ExistingReadyToRunPathOrNull(Path.Combine(r2rCompositeDir, "ReadyToRunComposite.dll"));
        ReadyToRunComponentLibDll = ReadyToRunCompositeImage is null
            ? null
            : ExistingReadyToRunPathOrNull(Path.Combine(r2rCompositeDir, "ReadyToRunComponentLib.dll"));
        if (ReadyToRunCompositeComponent is not null)
            ReadyToRunCompositeComponentMvid = ReadModuleMvid(ReadyToRunCompositeComponent);
        if (ReadyToRunComponentLibDll is not null)
            ReadyToRunComponentLibMvid = ReadModuleMvid(ReadyToRunComponentLibDll);

        RichLibraryNupkg = Path.Combine(_repoRoot, "samples", "RichLibrary",
            "bin", config, "RichLibrary.2.5.1.nupkg");

        // Create non-.NET binary for BadImageFormatException testing
        NonDotNetBinaryPath = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(NonDotNetBinaryPath, [0xDE, 0xAD, 0xBE, 0xEF]);

        // Verify critical paths exist
        Assert.IsTrue(File.Exists(HelloWorldDll), $"HelloWorld.dll not found at {HelloWorldDll}");
        Assert.IsTrue(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.IsTrue(File.Exists(EmbeddedSourceLibDll),
            $"EmbeddedSourceLib.dll not found at {EmbeddedSourceLibDll}");
        Assert.IsTrue(File.Exists(RichLibraryNupkg), $"RichLibrary.nupkg not found at {RichLibraryNupkg}");
        Assert.IsTrue(File.Exists(AppLocalRollForwardDll),
            $"AppLocalRollForward.dll not found at {AppLocalRollForwardDll}");
        var rollForwardBin = Path.GetDirectoryName(AppLocalRollForwardDll)!;
        Assert.IsTrue(File.Exists(Path.Combine(rollForwardBin, "Microsoft.Diagnostics.NETCore.Client.dll")),
            "AppLocalRollForward must deploy NETCore.Client.dll app-local for the roll-forward probe");
        Assert.IsTrue(File.Exists(Path.Combine(rollForwardBin, "Microsoft.Diagnostics.Tracing.TraceEvent.dll")),
            "AppLocalRollForward must deploy TraceEvent.dll app-local for its stale AssemblyRef to drive the test");
        if (NetFxConsoleExe is not null)
            Assert.IsTrue(File.Exists(NetFxConsoleExe), $"NetFxConsole.exe not found at {NetFxConsoleExe}");
        if (NetFxBindingRedirectsExe is not null)
        {
            Assert.IsTrue(File.Exists(NetFxBindingRedirectsExe),
                $"NetFxBindingRedirects.exe not found at {NetFxBindingRedirectsExe}");
            var binDir = Path.GetDirectoryName(NetFxBindingRedirectsExe)!;
            Assert.IsTrue(File.Exists(Path.Combine(binDir, "NetFxBindingRedirects.exe.config")),
                "NetFxBindingRedirects.exe.config missing — app.config did not deploy");
            Assert.IsTrue(Directory.Exists(Path.Combine(binDir, "lib")),
                "lib/ subdir missing — privatePath helper did not deploy");
            Assert.IsTrue(Directory.Exists(Path.Combine(binDir, "external")),
                "external/ subdir missing — codeBase helper did not deploy");
            Assert.IsTrue(Directory.Exists(Path.Combine(binDir, "fr")),
                "fr/ subdir missing — culture satellite did not deploy");
            Assert.IsNotNull(NetFxBindingRedirectsOracle);
        }
        if (NetFxBindingRedirectsClr2Exe is not null)
        {
            Assert.IsTrue(File.Exists(NetFxBindingRedirectsClr2Exe),
                $"NetFxBindingRedirects.Clr2.exe not found at {NetFxBindingRedirectsClr2Exe}");
            var binDir = Path.GetDirectoryName(NetFxBindingRedirectsClr2Exe)!;
            Assert.IsTrue(File.Exists(Path.Combine(binDir, "NetFxBindingRedirects.Clr2.exe.config")),
                "NetFxBindingRedirects.Clr2.exe.config missing — app.config did not deploy");
            Assert.IsTrue(Directory.Exists(Path.Combine(binDir, "lib")),
                "lib/ subdir missing — Clr2 privatePath helper did not deploy");
            Assert.IsTrue(Directory.Exists(Path.Combine(binDir, "external")),
                "external/ subdir missing — Clr2 codeBase helper did not deploy");
            Assert.IsTrue(Directory.Exists(Path.Combine(binDir, "fr")),
                "fr/ subdir missing — Clr2 culture satellite did not deploy");

            // Identity-based copy-local guard: V1 and V2 emit the same filename, so a path
            // check can't disambiguate. Reject silently re-introduced V1 by reading the staged
            // assembly's version. The redirect collapses on V2; any other value means the
            // wrong build leaked app-local through the project graph.
            var stagedSharedDep = Path.Combine(binDir, "NetFxBindingRedirects.Clr2.SharedDep.dll");
            Assert.IsTrue(File.Exists(stagedSharedDep),
                $"SharedDep V2 was not staged app-local at {stagedSharedDep}");
            var stagedVersion = System.Reflection.AssemblyName.GetAssemblyName(stagedSharedDep).Version?.ToString();
            Assert.AreEqual("2.0.0.0", stagedVersion);
        }
        if (NativeAotConsoleExe is not null)
            Assert.IsTrue(File.Exists(NativeAotConsoleExe), $"NativeAotConsole.exe not found at {NativeAotConsoleExe}");
        if (SelfContainedConsoleExe is not null)
            Assert.IsTrue(File.Exists(SelfContainedConsoleExe), $"SelfContainedConsole not found at {SelfContainedConsoleExe}");
        Assert.IsTrue(File.Exists(HelloWorldExe), $"HelloWorld apphost not found at {HelloWorldExe}");
        Assert.IsTrue(File.Exists(ComplexAppExe), $"ComplexApp apphost not found at {ComplexAppExe}");
        Assert.IsTrue(File.Exists(MinimalApiExe), $"MinimalApi apphost not found at {MinimalApiExe}");
        Assert.IsTrue(File.Exists(DottedNameAppDll), $"Dotted.Name.App.dll not found at {DottedNameAppDll}");
        Assert.IsTrue(File.Exists(DottedNameAppExe), $"Dotted.Name.App apphost not found at {DottedNameAppExe}");
    }

    /// <summary>
    /// Cleans up temporary files created by the fixture.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (File.Exists(NonDotNetBinaryPath))
        {
            try { File.Delete(NonDotNetBinaryPath); }
            catch { /* best effort */ }
        }
        return ValueTask.CompletedTask;
    }

    private string? FindArtifactsPublishOutput(string project, string fileName)
    {
        var artifactsDirectory = TestProcessEnvironment.IsDevelopmentContainer
            ? Path.Combine("artifacts", "devcontainer")
            : "artifacts";
        var publishRoot = Path.Combine(
            _repoRoot, "samples", project, artifactsDirectory, "publish", project);
        if (!Directory.Exists(publishRoot)) return null;

        foreach (var pivotDir in Directory.GetDirectories(publishRoot))
        {
            var candidate = Path.Combine(pivotDir, fileName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private string SamplePath(string project, string config, string tfm, string file)
        => Path.Combine(_repoRoot, "samples", project, "bin", config, tfm, file);

    private static string? ExistingPathOrNull(string path)
        => File.Exists(path) ? path : null;

    /// <summary>
    /// Returns an existing readable PE path and rejects partial publish outputs.
    /// </summary>
    internal static string? ExistingReadyToRunPathOrNull(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            _ = reader.HasMetadata;
            return path;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? ExistingDirOrNull(string path)
        => Directory.Exists(path) ? path : null;

    private async Task PublishNativeAotProject(string relativePath)
    {
        var projectName = Path.GetFileName(relativePath);
        var assemblyName = projectName.Equals("NativeAotConsoleV2", StringComparison.Ordinal)
            ? "NativeAotConsole"
            : projectName;
        var publishDirectory = Path.Combine(
            _repoRoot,
            relativePath,
            "bin",
            TestProcessEnvironment.ReleaseBuildConfiguration,
            "net10.0",
            RuntimeInformation.RuntimeIdentifier,
            "publish");
        var apphostExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
        var expectedOutput = Path.Combine(publishDirectory, $"{assemblyName}{apphostExtension}");
        var expectedMstat = Path.Combine(publishDirectory, $"{assemblyName}.mstat");
        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + ".lock";
        var lockPath = Path.Combine(Path.GetTempPath(), lockName);

        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var sharesOutputWithMcpTests =
                projectName.Equals("NativeAotConsole", StringComparison.Ordinal) ||
                projectName.Equals("NativeAotConsoleV2", StringComparison.Ordinal);
            var projectDirectory = Path.Combine(_repoRoot, relativePath);
            if (sharesOutputWithMcpTests &&
                TestProcessEnvironment.IsFixtureOutputCurrent(
                    expectedOutput,
                    projectDirectory,
                    _repoRoot) &&
                TestProcessEnvironment.IsFixtureOutputCurrent(
                    expectedMstat,
                    projectDirectory,
                    _repoRoot))
            {
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c {TestProcessEnvironment.ReleaseBuildConfiguration} "
                    + $"-r {RuntimeInformation.RuntimeIdentifier} -v q",
                WorkingDirectory = projectDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // NoDefaultCurrentDirectoryInExePath breaks the ILCompiler's
            // findvcvarsall → VsDevCmd toolchain discovery (vswhere.exe
            // is not resolved from the current directory inside FOR /F).
            // Clear it for this process only.
            psi.Environment.Remove("NoDefaultCurrentDirectoryInExePath");
            if (relativePath.EndsWith("NativeAotArtifactsConsole", StringComparison.Ordinal))
            {
                TestProcessEnvironment.ConfigureArtifactsBuild(psi);
            }
            else
            {
                TestProcessEnvironment.ConfigureBuild(psi);
            }

            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet publish failed for {relativePath} (exit {process.ExitCode}):\n{stdout}\n{stderr}");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    /// <summary>
    /// Publishes a ReadyToRun (crossgen2) sample. Tolerant: a crossgen2 failure (an unsupported RID)
    /// leaves the publish outputs absent, so the dependent tests skip rather than the fixture failing.
    /// </summary>
    private Task PublishReadyToRunProject(string relativePath, bool selfContained) =>
        PublishReadyToRunProject(relativePath, RuntimeInformation.RuntimeIdentifier, selfContained);

    private async Task PublishReadyToRunProject(string relativePath, string rid, bool selfContained)
    {
        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + ".lock";
        var lockPath = Path.Combine(Path.GetTempPath(), lockName);

        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var projectDir = Path.Combine(_repoRoot, relativePath);
            var projectName = Path.GetFileName(projectDir);
            var imageName = projectName.Equals("ReadyToRunComposite", StringComparison.Ordinal)
                ? $"{projectName}.r2r.dll"
                : $"{projectName}.dll";
            var imagePath = Path.Combine(
                projectDir,
                "bin",
                TestProcessEnvironment.ReleaseBuildConfiguration,
                "net10.0",
                rid,
                "publish",
                imageName);
            if (ExistingReadyToRunPathOrNull(imagePath) is not null &&
                TestProcessEnvironment.IsFixtureOutputCurrent(imagePath, projectDir, _repoRoot))
            {
                return;
            }

            if (File.Exists(imagePath))
                File.Delete(imagePath);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c {TestProcessEnvironment.ReleaseBuildConfiguration} -r {rid} "
                    + $"--self-contained {(selfContained ? "true" : "false")} -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            TestProcessEnvironment.ConfigureBuild(psi);

            var process = Process.Start(psi)!;
            _ = await process.StandardOutput.ReadToEndAsync();
            _ = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (File.Exists(imagePath) && ExistingReadyToRunPathOrNull(imagePath) is null)
                File.Delete(imagePath);
            // A non-zero exit means crossgen2 is unavailable for this RID; leave the outputs absent.
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    /// <summary>
    /// Publishes a browser Wasm sample without ReadyToRun, which the SDK does not support for Wasm.
    /// The produced <c>dotnet.native.wasm</c> module still contains real runtime Wasm code.
    /// A missing wasm-tools workload leaves the outputs absent so dependent tests can skip.
    /// </summary>
    private async Task PublishWasmProject(string relativePath, bool runAotCompilation = false)
    {
        var wasmRid = runAotCompilation ? "browser-wasm-aot" : "browser-wasm";
        var configuration = TestProcessEnvironment.ReleaseBuildConfiguration;
        var expectedOutput = Path.Combine(_repoRoot, relativePath,
            "bin", configuration, "net10.0", wasmRid, "publish", "dotnet.native.wasm");
        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + $"-{wasmRid}.lock";
        var lockPath = Path.Combine(Path.GetTempPath(), lockName);

        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var projectDirectory = Path.Combine(_repoRoot, relativePath);
            if (TestProcessEnvironment.IsFixtureOutputCurrent(
                expectedOutput,
                projectDirectory,
                _repoRoot))
            {
                return;
            }

            var arguments = $"publish -c {configuration} -r browser-wasm --self-contained true "
                + "-p:PublishReadyToRun=false -p:WasmEmitSymbolMap=true ";
            if (runAotCompilation)
            {
                arguments += "-p:RunAOTCompilation=true "
                    + $"-p:WasmAppDir=bin\\{configuration}\\net10.0\\browser-wasm-aot\\AppBundle "
                    + $"-p:PublishDir=bin\\{configuration}\\net10.0\\browser-wasm-aot\\publish\\ ";
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments + "-v q",
                WorkingDirectory = projectDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            TestProcessEnvironment.ConfigureBuild(psi);

            var process = Process.Start(psi)!;
            _ = await process.StandardOutput.ReadToEndAsync();
            _ = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            // A non-zero exit means wasm-tools is unavailable; leave the outputs absent.
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private static Guid ReadModuleMvid(string path)
    {
        using var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(path);
        var reader = analyzer.GetMetadataReader();
        return reader is null ? Guid.Empty : reader.GetGuid(reader.GetModuleDefinition().Mvid);
    }

    private async Task PublishSelfContainedProject(string relativePath)
    {
        var projectName = Path.GetFileName(relativePath);
        var apphostExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
        var expectedOutput = Path.Combine(
            _repoRoot,
            relativePath,
            "bin",
            TestProcessEnvironment.ReleaseBuildConfiguration,
            "net10.0",
            RuntimeInformation.RuntimeIdentifier,
            "publish",
            $"{projectName}{apphostExtension}");
        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + ".lock";
        var lockPath = Path.Combine(Path.GetTempPath(), lockName);

        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var projectDirectory = Path.Combine(_repoRoot, relativePath);
            if (Dotsider.Core.Analysis.SingleFileBundleReader.IsBundle(expectedOutput, out _) &&
                TestProcessEnvironment.IsFixtureOutputCurrent(
                    expectedOutput,
                    projectDirectory,
                    _repoRoot))
            {
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c {TestProcessEnvironment.ReleaseBuildConfiguration} "
                    + $"-r {RuntimeInformation.RuntimeIdentifier} --self-contained "
                    + "-p:PublishSingleFile=true -v q",
                WorkingDirectory = projectDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            TestProcessEnvironment.ConfigureBuild(psi);

            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet publish failed for {relativePath} (exit {process.ExitCode}):\n{stdout}\n{stderr}");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private async Task BuildProject(string relativePath)
    {
        // Use a file lock to prevent concurrent builds of the same project
        // across test assemblies (e.g. Dotsider.Tests and Dotsider.Mcp.Tests).
        // File locks are cross-platform, unlike named Mutex/Semaphore.
        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + ".lock";
        var lockPath = Path.Combine(Path.GetTempPath(), lockName);

        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var projectDir = Path.Combine(_repoRoot, relativePath);
            var projectName = Path.GetFileName(projectDir);
            var assemblyName = projectName.Equals("RichLibraryV2", StringComparison.Ordinal)
                ? "RichLibrary"
                : projectName;
            var expectedOutput = Path.Combine(
                projectDir,
                "bin",
                TestProcessEnvironment.DebugBuildConfiguration,
                "net10.0",
                $"{assemblyName}.dll");
            if (TestProcessEnvironment.IsFixtureOutputCurrent(
                expectedOutput,
                projectDir,
                _repoRoot))
            {
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build --no-restore -c {TestProcessEnvironment.DebugBuildConfiguration} -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            TestProcessEnvironment.ConfigureBuild(psi);

            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet build failed for {relativePath} (exit {process.ExitCode}):\n{stdout}\n{stderr}");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    /// <summary>
    /// Runs <c>NetFxBindingRedirects.exe --oracle &lt;temp.json&gt;</c> on Windows under the
    /// real .NET Framework CLR, captures the JSON document, and returns it as a typed map.
    /// Used as the runtime ground truth for NetFxBinder oracle-parity tests.
    /// </summary>
    /// <param name="exePath">Absolute path to the built sample executable.</param>
    /// <returns>The parsed oracle map.</returns>
    private static async Task<IReadOnlyDictionary<string, NetFxOracleEntry>> CaptureNetFxOracleAsync(string exePath)
    {
        var oraclePath = Path.Combine(Path.GetTempPath(),
            $"netfx-binder-oracle-{Guid.NewGuid():N}.json");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--oracle \"" + oraclePath + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
            };
            TestProcessEnvironment.RemoveCodeCoverageVariables(psi);
            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !File.Exists(oraclePath))
                throw new InvalidOperationException(
                    $"NetFxBindingRedirects oracle run failed (exit {process.ExitCode}):\n{stdout}\n{stderr}");

            var json = await File.ReadAllTextAsync(oraclePath);
            using var doc = JsonDocument.Parse(json);
            var map = new Dictionary<string, NetFxOracleEntry>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var fullName = prop.Value.GetProperty("fullName").GetString() ?? string.Empty;
                var location = prop.Value.GetProperty("location").GetString() ?? string.Empty;
                var loaded = prop.Value.GetProperty("loaded").GetBoolean();
                var error = prop.Value.GetProperty("error").GetString();
                map[prop.Name] = new NetFxOracleEntry(fullName, location, loaded, error);
            }
            return map;
        }
        finally
        {
            try { File.Delete(oraclePath); } catch { /* best effort */ }
        }
    }
}

/// <summary>
/// One entry in the NetFxBindingRedirects runtime oracle JSON: what the actual .NET Framework
/// CLR loaded for a particular reference.
/// </summary>
/// <param name="FullName">The bound assembly's <c>Assembly.FullName</c>, or empty on failure.</param>
/// <param name="Location">The bound assembly's <c>Assembly.Location</c>, or empty on failure.</param>
/// <param name="Loaded">Whether the load succeeded.</param>
/// <param name="Error">Type-name + message of the load exception, or <see langword="null"/> on success.</param>
internal sealed record NetFxOracleEntry(string FullName, string Location, bool Loaded, string? Error);
