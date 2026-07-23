using Dotsider.Views;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Dotsider.Tests;

/// <summary>
/// Tests editor process construction and production launch orchestration.
/// </summary>
/// <param name="testContext">The current test context.</param>
[TestClass]
public sealed class EditorLauncherTests(TestContext testContext)
{
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies direct launch information uses an absolute executable and discrete source argument.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CreateDirectStartInfo_ConfiguredEditor_UsesDiscreteArguments()
    {
        var executable = Path.GetFullPath(
            OperatingSystem.IsWindows() ? @"C:\Tools\editor.exe" : "/usr/bin/editor");
        var sourcePath = Path.GetFullPath(
            OperatingSystem.IsWindows() ? @"C:\Temp\source file.cs" : "/tmp/source file.cs");

        var startInfo = EditorLauncher.CreateDirectStartInfo(
            executable,
            ["--wait", "profile name"],
            sourcePath);

        Assert.AreEqual(executable, startInfo.FileName);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.AreEqual(Path.GetDirectoryName(executable), startInfo.WorkingDirectory);
        string[] expectedArguments = ["--wait", "profile name", sourcePath];
        Assert.AreSequenceEqual(
            expectedArguments,
            startInfo.ArgumentList);
    }

    /// <summary>
    /// Verifies association start information accepts only an inert text path.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CreateAssociationStartInfo_TxtPath_UsesPlatformAssociation()
    {
        var sourcePath = Path.GetFullPath(Path.Combine("temp", "Source.txt"));

        var startInfo = EditorLauncher.CreateAssociationStartInfo(sourcePath);

        Assert.AreEqual(sourcePath, startInfo.FileName);
        Assert.IsTrue(startInfo.UseShellExecute);
        Assert.AreEqual(Path.GetDirectoryName(sourcePath), startInfo.WorkingDirectory);
        Assert.ThrowsExactly<ArgumentException>(() =>
            EditorLauncher.CreateAssociationStartInfo(Path.ChangeExtension(sourcePath, ".cs")));
    }

    /// <summary>
    /// Verifies the Windows batch route carries only fixed environment references in command text.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void CreateWindowsBatchStartInfo_ConfiguredShim_UsesFixedCommandTemplate()
    {
        var script = Path.GetFullPath(@"C:\Editor Tools\code.cmd");
        var source = Path.GetFullPath(@"C:\Temp Folder\source.cs");

        var startInfo = EditorLauncher.CreateWindowsBatchStartInfo(
            script,
            ["--wait", "profile name"],
            source);

        Assert.AreEqual(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            startInfo.FileName);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.AreEqual(Path.GetDirectoryName(script), startInfo.WorkingDirectory);
        Assert.AreEqual(
            "/d /s /v:off /c \"\"%DOTSIDER_EDITOR_SCRIPT:~1%\" " +
            "\"%DOTSIDER_EDITOR_ARGUMENT_0000:~1%\" " +
            "\"%DOTSIDER_EDITOR_ARGUMENT_0001:~1%\" " +
            "\"%DOTSIDER_EDITOR_SOURCE:~1%\"\"",
            startInfo.Arguments);
        Assert.IsEmpty(startInfo.ArgumentList);
        Assert.DoesNotContain(script, startInfo.Arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(source, startInfo.Arguments, StringComparison.Ordinal);
        Assert.AreEqual($".{script}", startInfo.Environment["DOTSIDER_EDITOR_SCRIPT"]);
        Assert.AreEqual($".{source}", startInfo.Environment["DOTSIDER_EDITOR_SOURCE"]);
    }

    /// <summary>
    /// Verifies an unrepresentable literal quote in a Windows batch argument fails closed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void CreateWindowsBatchStartInfo_LiteralQuoteArgument_Throws()
    {
        var script = Path.GetFullPath(@"C:\Editor Tools\code.cmd");
        var source = Path.GetFullPath(@"C:\Temp Folder\source.cs");

        Assert.ThrowsExactly<ArgumentException>(() =>
            EditorLauncher.CreateWindowsBatchStartInfo(
                script,
                ["quote\"value"],
                source));
    }

    /// <summary>
    /// Verifies a real Windows batch shim receives spaces and command metacharacters literally.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public async Task WindowsBatchShim_HostileLiteralArguments_ReceivesExactCommandLine()
    {
        var directory = Directory.CreateTempSubdirectory(
            "dotsider-editor & % ! ^ (-");
        try
        {
            var script = Path.Combine(directory.FullName, "shim.cmd");
            var capture = Path.Combine(directory.FullName, "captured.txt");
            var source = Path.Combine(directory.FullName, "source file.cs");
            File.WriteAllText(
                script,
                "@echo off\r\n" +
                "set \"DOTSIDER_RECEIVED_0000=value:%~1\"\r\n" +
                "set \"DOTSIDER_RECEIVED_0001=value:%~2\"\r\n" +
                "set \"DOTSIDER_RECEIVED_0002=value:%~3\"\r\n" +
                "set \"DOTSIDER_RECEIVED_0003=value:%~4\"\r\n" +
                "set \"DOTSIDER_RECEIVED_0004=value:%~5\"\r\n" +
                "set \"DOTSIDER_RECEIVED_0005=value:%~6\"\r\n" +
                "set \"DOTSIDER_RECEIVED_0006=value:%~7\"\r\n" +
                "set \"DOTSIDER_RECEIVED_0007=value:%~8\"\r\n" +
                "set \"DOTSIDER_RECEIVED_SOURCE=value:%~9\"\r\n" +
                "> captured.txt (\r\n" +
                "set DOTSIDER_RECEIVED_0000\r\n" +
                "set DOTSIDER_RECEIVED_0001\r\n" +
                "set DOTSIDER_RECEIVED_0002\r\n" +
                "set DOTSIDER_RECEIVED_0003\r\n" +
                "set DOTSIDER_RECEIVED_0004\r\n" +
                "set DOTSIDER_RECEIVED_0005\r\n" +
                "set DOTSIDER_RECEIVED_0006\r\n" +
                "set DOTSIDER_RECEIVED_0007\r\n" +
                "set DOTSIDER_RECEIVED_SOURCE\r\n" +
                ")\r\n");
            File.WriteAllText(source, "");
            var arguments = new[]
            {
                "value with spaces",
                "%PATH%",
                "!bang!",
                "^caret",
                "a&b|c(paren)",
                "semi;colon",
                @"back\slash",
                ""
            };
            var startInfo = EditorLauncher.CreateWindowsBatchStartInfo(
                script,
                arguments,
                source);

            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);
            await process.WaitForExitAsync(_testContext.CancellationToken);

            Assert.AreEqual(0, process.ExitCode);
            var expected = arguments
                .Select((value, index) =>
                    $"DOTSIDER_RECEIVED_{index:D4}=value:{value}")
                .Append($"DOTSIDER_RECEIVED_SOURCE=value:{source}");
            Assert.AreSequenceEqual(expected, File.ReadAllLines(capture));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a real Unix executable editor receives exact argv without a shell.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task DirectUnixShim_LiteralArguments_ReceivesExactArgv()
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-unix-shim-");
        try
        {
            var script = Path.Combine(directory.FullName, "editor");
            var capture = Path.Combine(directory.FullName, "captured.txt");
            var source = Path.Combine(directory.FullName, "source file.cs");
            File.WriteAllText(
                script,
                "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$DOTSIDER_CAPTURE\"\n");
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.WriteAllText(source, "");
            var arguments = new[] { "--wait", "value with spaces", "$HOME", "a&b", "" };
            var startInfo = EditorLauncher.CreateDirectStartInfo(script, arguments, source);
            startInfo.Environment["DOTSIDER_CAPTURE"] = capture;

            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);
            await process.WaitForExitAsync(_testContext.CancellationToken);

            Assert.AreEqual(0, process.ExitCode);
            Assert.AreSequenceEqual(
                arguments.Append(source),
                File.ReadAllLines(capture));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies VISUAL is started before EDITOR and preserves the allowlisted source extension.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_VisualResolves_StartsVisualOnly()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var editor = CreateResolvedEditorFile(out var directory);
        try
        {
            var starts = new List<ProcessStartInfo>();

            var status = EditorLauncher.Launch(
                store,
                source,
                editor,
                editor,
                [],
                [".EXE"],
                startInfo =>
                {
                    starts.Add(startInfo);
                    return new MemoryStream();
                },
                out var openedPath);

            Assert.AreEqual(EditorLaunchStatus.Started, status);
            Assert.HasCount(1, starts);
            Assert.AreEqual(source, openedPath);
            Assert.EndsWith(".cs", starts[0].ArgumentList[^1]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies successful launch disposes the returned handle without waiting for process exit.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_ProcessStarts_DisposesHandleAndReturnsWithoutWaiting()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var editor = CreateResolvedEditorFile(out var directory);
        try
        {
            Process? process = null;

            var status = EditorLauncher.Launch(
                store,
                source,
                editor,
                null,
                [],
                [".EXE"],
                _ =>
                {
                    process = Process.GetCurrentProcess();
                    return process;
                },
                out _);

            Assert.AreEqual(EditorLaunchStatus.Started, status);
            Assert.IsNotNull(process);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = process.Handle);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a starter that creates no process is reported as a launch failure.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_ProcessStarterReturnsNull_ReturnsFailed()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var editor = CreateResolvedEditorFile(out var directory);
        try
        {
            var status = EditorLauncher.Launch(
                store,
                source,
                editor,
                null,
                [],
                [".EXE"],
                _ => null,
                out var openedPath);

            Assert.AreEqual(EditorLaunchStatus.Failed, status);
            Assert.AreEqual(source, openedPath);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a literal quote remains a discrete argument for directly executable editors.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_DirectArgumentContainsLiteralQuote_PreservesExactArgument()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var editor = CreateResolvedEditorFile(out var directory);
        try
        {
            ProcessStartInfo? captured = null;
            var configured = $"\"{editor}\" \"quote\\\"value\"";

            var status = EditorLauncher.Launch(
                store,
                source,
                configured,
                null,
                [],
                [".EXE"],
                startInfo =>
                {
                    captured = startInfo;
                    return new MemoryStream();
                },
                out var openedPath);

            Assert.AreEqual(EditorLaunchStatus.Started, status);
            Assert.IsNotNull(captured);
            Assert.AreEqual("quote\"value", captured.ArgumentList[0]);
            Assert.AreEqual(source, captured.ArgumentList[1]);
            Assert.AreEqual(source, openedPath);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a missing VISUAL permits EDITOR to start.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_VisualNotFound_StartsEditor()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var editor = CreateResolvedEditorFile(out var directory);
        try
        {
            ProcessStartInfo? captured = null;

            var status = EditorLauncher.Launch(
                store,
                source,
                "missing-dotsider-editor",
                editor,
                [],
                [".EXE"],
                startInfo =>
                {
                    captured = startInfo;
                    return new MemoryStream();
                },
                out _);

            Assert.AreEqual(EditorLaunchStatus.Started, status);
            Assert.IsNotNull(captured);
            Assert.AreEqual(Path.GetFullPath(editor), captured.FileName);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies Windows native targets launch directly while batch targets use the system interpreter.
    /// </summary>
    /// <param name="extension">The configured editor target extension.</param>
    /// <param name="usesSystemCommandInterpreter">
    /// Whether the resolved target requires the system command interpreter.
    /// </param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(".bat", true)]
    [DataRow(".cmd", true)]
    [DataRow(".com", false)]
    [DataRow(".exe", false)]
    public void Launch_WindowsTarget_UsesExpectedStartPath(
        string extension,
        bool usesSystemCommandInterpreter)
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-kind-");
        try
        {
            var editor = Path.Combine(directory.FullName, $"editor{extension}");
            File.WriteAllText(editor, "");
            ProcessStartInfo? captured = null;

            var status = EditorLauncher.Launch(
                store,
                source,
                editor,
                null,
                [],
                [".EXE"],
                startInfo =>
                {
                    captured = startInfo;
                    return new MemoryStream();
                },
                out _);

            Assert.AreEqual(EditorLaunchStatus.Started, status);
            Assert.IsNotNull(captured);
            Assert.AreEqual(
                usesSystemCommandInterpreter
                    ? Path.Combine(Environment.SystemDirectory, "cmd.exe")
                    : Path.GetFullPath(editor),
                captured.FileName,
                ignoreCase: true);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a literal quote that cannot round-trip through a batch shim fails before launch.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void Launch_WindowsBatchArgumentContainsLiteralQuote_ReturnsFailed()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-quote-");
        try
        {
            var editor = Path.Combine(directory.FullName, "editor.cmd");
            File.WriteAllText(editor, "");
            var configured = $"\"{editor}\" \"quote\\\"value\"";
            var startCount = 0;

            var status = EditorLauncher.Launch(
                store,
                source,
                configured,
                null,
                [],
                [".CMD"],
                _ =>
                {
                    startCount++;
                    return new MemoryStream();
                },
                out var openedPath);

            Assert.AreEqual(EditorLaunchStatus.Failed, status);
            Assert.AreEqual(0, startCount);
            Assert.AreEqual(source, openedPath);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies unresolved configured editors use only a text association path.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_BothEditorsNotFound_UsesTxtAssociation()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        ProcessStartInfo? captured = null;

        var status = EditorLauncher.Launch(
            store,
            source,
            "missing-visual",
            "missing-editor",
            [],
            [".EXE"],
            startInfo =>
            {
                captured = startInfo;
                return new MemoryStream();
            },
            out var openedPath);

        Assert.AreEqual(EditorLaunchStatus.Started, status);
        Assert.IsNotNull(captured);
        Assert.IsTrue(captured.UseShellExecute);
        Assert.EndsWith(".txt", captured.FileName);
        Assert.AreEqual(captured.FileName, openedPath);
        Assert.IsFalse(File.Exists(source));
        Assert.IsTrue(File.Exists(openedPath));
    }

    /// <summary>
    /// Verifies malformed configured editor syntax stops without trying another application.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_MalformedVisual_ReturnsFailedWithoutStartingProcess()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var editor = CreateResolvedEditorFile(out var directory);
        try
        {
            var startCount = 0;

            var status = EditorLauncher.Launch(
                store,
                source,
                "editor && other",
                editor,
                [],
                [".EXE"],
                _ =>
                {
                    startCount++;
                    return new MemoryStream();
                },
                out var openedPath);

            Assert.AreEqual(EditorLaunchStatus.Failed, status);
            Assert.AreEqual(0, startCount);
            Assert.AreEqual(source, openedPath);
            Assert.IsTrue(File.Exists(source));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a configured-editor launch failure does not fall through to another application.
    /// </summary>
    /// <param name="nativeErrorCode">The simulated native process-start error.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(5)]
    [DataRow(193)]
    public void Launch_ResolvedVisualFails_ReturnsFailedWithoutFallback(int nativeErrorCode)
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);
        var editor = CreateResolvedEditorFile(out var directory);
        try
        {
            var startCount = 0;

            var status = EditorLauncher.Launch(
                store,
                source,
                editor,
                editor,
                [],
                [".EXE"],
                _ =>
                {
                    startCount++;
                    throw new Win32Exception(nativeErrorCode);
                },
                out var openedPath);

            Assert.AreEqual(EditorLaunchStatus.Failed, status);
            Assert.AreEqual(1, startCount);
            Assert.AreEqual(source, openedPath);
            Assert.IsTrue(File.Exists(source));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies other expected process-start failures stop without falling through.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_ExpectedStartExceptions_ReturnFailedWithoutFallback()
    {
        Exception[] exceptions =
        [
            new InvalidOperationException("invalid start"),
            new IOException("I/O failure"),
            new UnauthorizedAccessException("access denied")
        ];

        foreach (var exception in exceptions)
        {
            using var store = new EmbeddedSourceTempFileStore();
            var source = store.Write("Method", "Source.cs", [0x2A]);
            var editor = CreateResolvedEditorFile(out var directory);
            try
            {
                var startCount = 0;

                var status = EditorLauncher.Launch(
                    store,
                    source,
                    editor,
                    editor,
                    [],
                    [".EXE"],
                    _ =>
                    {
                        startCount++;
                        throw exception;
                    },
                    out var openedPath);

                Assert.AreEqual(EditorLaunchStatus.Failed, status);
                Assert.AreEqual(1, startCount);
                Assert.AreEqual(source, openedPath);
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies association failure reports the already moved safe text path.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Launch_AssociationFails_ReturnsMovedTxtPath()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var source = store.Write("Method", "Source.cs", [0x2A]);

        var status = EditorLauncher.Launch(
            store,
            source,
            null,
            null,
            [],
            [".EXE"],
            _ => throw new Win32Exception(2),
            out var openedPath);

        Assert.AreEqual(EditorLaunchStatus.Failed, status);
        Assert.EndsWith(".txt", openedPath);
        Assert.IsTrue(File.Exists(openedPath));
        Assert.IsFalse(File.Exists(source));
    }

    private static string CreateResolvedEditorFile(out DirectoryInfo directory)
    {
        directory = Directory.CreateTempSubdirectory("dotsider-editor-launch-");
        var path = Path.Combine(
            directory.FullName,
            OperatingSystem.IsWindows() ? "editor.exe" : "editor");
        File.WriteAllText(path, "");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }
}
