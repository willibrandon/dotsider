using System.Diagnostics;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the IL disassembly editor widget with themed selection colors,
/// vim keybindings, and go-to-definition support.
/// </summary>
internal static class IlEditorHost
{
    /// <summary>
    /// Builds the themed IL editor widget with all input bindings for
    /// navigation, vim motions, and go-to-definition.
    /// </summary>
    /// <param name="editorState">The editor state containing the IL document and cursor.</param>
    /// <param name="state">The shared application state for decoration providers and navigation.</param>
    /// <returns>A composed widget tree ready for rendering.</returns>
    internal static Hex1bWidget Build(EditorState editorState, DotsiderState state)
    {
        return new ThemePanelWidget(
            t => t
                .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
            new EditorWidget(editorState)
                .Decorations(state.IlSyntaxProvider)
                .Decorations(state.IlNativeSyntaxProvider)
                .Decorations(state.IlSourceLinkProvider)
                .Decorations(state.IlSearchProvider)
                .Decorations(state.IlYankProvider)
                .Decorations(state.IlNavigationProvider)
                .Decorations(state.IlNativeNavigationProvider)
                .InputBindings(bindings =>
                {
                    // Escape: IL back navigation takes priority over vim cancel.
                    // Must be registered BEFORE TextObjectHelper which also binds Escape.
                    // First match wins in the binding walk.
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        if (state.IlNativeBackStack.Count > 0)
                        {
                            state.RestoreFromNativeBackEntry(state.IlNativeBackStack.Pop());
                        }
                        else if (state.IlBackStack.Count > 0)
                        {
                            var entry = state.IlBackStack.Pop();
                            state.RestoreFromIlBackEntry(entry);
                        }
                        else
                        {
                            // Fall through: reset vim text-object state (matches TextObjectHelper behavior)
                            state.VimPending = VimMotionState.Idle;
                            state.App.Invalidate();
                        }
                    }, "Back");

                    TextObjectHelper.ConfigureReadOnlyEditorBindings(
                        bindings,
                        editorState,
                        () => state.VimPending,
                        () => state.VimPendingEditor,
                        () => state.VimPendingCursorOffset,
                        () => state.VimPendingTimestamp,
                        (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                        state.PerformEditorYank,
                        () => state.App.Invalidate());

                    bindings.Key(Hex1bKey.Enter).Action(_ => PerformGoToDefinition(state), "Go to definition");
                    bindings.Key(Hex1bKey.O).Action(_ => OpenEmbeddedSource(state), "Open embedded source");
                    bindings.Key(Hex1bKey.U).Action(ctx => YankSourceLinkUrl(state, ctx), "Yank source URL");

                    bindings.Key(Hex1bKey.G).Action(_ =>
                    {
                        state.IlGdPending = true;
                        state.IlGdTimestamp = DateTime.UtcNow;
                        state.App.Invalidate();
                    }, "");

                    if (state.IlGdPending)
                    {
                        bindings.Key(Hex1bKey.D).Action(_ =>
                        {
                            state.IlGdPending = false;
                            PerformGoToDefinition(state);
                        }, "Go to definition");
                    }

                    if (state.IlGdPending
                        && (DateTime.UtcNow - state.IlGdTimestamp).TotalSeconds > 1.0)
                        state.IlGdPending = false;
                })
                .FillWidth()
                .FillHeight())
            .FillWidth()
            .FillHeight();
    }

    private static int LineStartOffset(string text, int line)
    {
        if (line <= 1) return 0;
        var current = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            if (++current == line) return i + 1;
        }

        return 0;
    }

    private static void PerformGoToDefinition(DotsiderState state)
    {
        // Native mode: resolve the target of the instruction under the cursor.
        if (state.IlNativeInstructions is { } nativeInstructions
            && state.IlEditorState is { } nativeEditor
            && state.Analyzer.NativeSymbols is { } info)
        {
            var inst = NativeNavigationHelper.GetInstructionAtCursor(nativeEditor, nativeInstructions);
            if (inst?.TargetAddress is not { } target)
                return;

            // An intra-function local label jumps within the current listing, not to another symbol.
            if (inst.TargetKind == NativeTargetKind.LocalLabel)
            {
                if (nativeInstructions.FirstOrDefault(i => i.Address == target)?.DisplayLine is { } line)
                {
                    var offset = LineStartOffset(nativeEditor.Document.GetText(), line);
                    nativeEditor.SetCursorPosition(new DocumentOffset(offset));
                    state.App.Invalidate();
                }

                return;
            }

            if (info.TryFindByAddress(target, out var symbol))
            {
                state.NavigateToNativeSymbol(symbol);
                state.App.Invalidate();
            }

            return;
        }

        if (state.IlInstructions is { } instructions
            && state.IlEditorState is { } es)
        {
            var inst = IlNavigationHelper.GetInstructionAtCursor(
                es, instructions, state.IlHeaderLineCount);
            if (inst?.MetadataToken is not null)
            {
                state.NavigateToIlDefinition(inst.MetadataToken.Value);
                state.App.Invalidate();
            }
        }
    }

    private static void YankSourceLinkUrl(DotsiderState state, InputBindingActionContext ctx)
    {
        if (state.IlInstructions is not { } instructions
            || state.IlEditorState is not { } editorState)
            return;

        var url = IlNavigationHelper.GetSourceLinkUrlAtCursor(editorState, instructions);
        if (url is null)
        {
            state.ShowTransientNotice("No Source Link URL at cursor");
            return;
        }

        ctx.CopyToClipboard(url);
        if (IlNavigationHelper.GetSourceLinkYankRangeAtCursor(editorState, instructions) is { } range)
            FlashSourceLinkMarker(state, range);
        state.ShowTransientNotice("Yanked Source Link URL");
    }

    private static void FlashSourceLinkMarker(
        DotsiderState state,
        (DocumentPosition Start, DocumentPosition End) range)
    {
        state.IlYankProvider.HighlightRange = range;
        state.App.Invalidate();
        _ = Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(_ =>
        {
            state.IlYankProvider.HighlightRange = null;
            state.App.Invalidate();
        }, TaskScheduler.Default);
    }

    private static void OpenEmbeddedSource(DotsiderState state)
    {
        if (state.IlSelectedMethod is null)
            return;

        var source = state.Analyzer.GetEmbeddedSource(state.IlSelectedMethod);
        if (source is null)
        {
            state.ShowTransientNotice("No embedded source for this method");
            return;
        }

        var tempPath = WriteEmbeddedSourceToTemp(state.IlSelectedMethod.Name, source.Document, source.Bytes);
        if (TryLaunchEditor(tempPath))
        {
            state.ShowTransientNotice($"Opened embedded source: {Path.GetFileName(tempPath)}");
            return;
        }

        state.ShowTransientNotice($"Embedded source: {tempPath}");
    }

    private static string WriteEmbeddedSourceToTemp(string methodName, string documentPath, byte[] bytes)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dotsider", "embedded-source");
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(documentPath);
        if (string.IsNullOrEmpty(extension))
            extension = ".txt";

        var documentName = Path.GetFileNameWithoutExtension(documentPath);
        if (string.IsNullOrWhiteSpace(documentName))
            documentName = methodName;

        var fileName = $"{SanitizeFileName(documentName)}-{Guid.NewGuid():N}{extension}";
        var tempPath = Path.Combine(directory, fileName);
        File.WriteAllBytes(tempPath, bytes);
        return tempPath;
    }

    private static bool TryLaunchEditor(string path)
    {
        var editor = Environment.GetEnvironmentVariable("VISUAL");
        if (string.IsNullOrWhiteSpace(editor))
            editor = Environment.GetEnvironmentVariable("EDITOR");

        if (!string.IsNullOrWhiteSpace(editor) && TryStartEditorCommand(editor, path))
            return true;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryStartEditorCommand(string editor, string path)
    {
        try
        {
            using var process = Process.Start(CreateEditorStartInfo(editor, path));
            process?.WaitForExit();
            return process is not null && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessStartInfo CreateEditorStartInfo(string editor, string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add($"{editor} \"{path}\"");
            return startInfo;
        }

        var shellInfo = new ProcessStartInfo("/bin/sh")
        {
            UseShellExecute = false
        };
        shellInfo.ArgumentList.Add("-c");
        shellInfo.ArgumentList.Add($"{editor} \"$1\"");
        shellInfo.ArgumentList.Add("dotsider-editor");
        shellInfo.ArgumentList.Add(path);
        return shellInfo;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string([.. value.Select(ch => invalid.Contains(ch) ? '_' : ch)]);
    }
}
