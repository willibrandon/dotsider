/// <summary>
/// Checks the files shipped in a Native AOT release directory.
/// Required terminal assets are specific to the product and runtime identifier.
/// CI and release builds use the same validation after separating native symbols.
/// </summary>
internal static class NativeAotPayloadValidator
{
    internal static void Validate(string output, string productName, string rid)
    {
        bool windows = rid.StartsWith("win-", StringComparison.Ordinal);
        StringComparer comparer = windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        StringComparison comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var required = new HashSet<string>(comparer)
        {
            productName + (windows ? ".exe" : ""),
            "LICENSE",
        };
        bool terminal = productName.Equals("dotsider", StringComparison.Ordinal);
        if (terminal)
        {
            required.Add("tracehost/dotsider-tracehost.dll");
            required.UnionWith(RequiredTerminalFiles(rid));
        }

        string[] files = [.. Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(output, path).Replace('\\', '/'))];
        string[] missing = [.. required.Except(files, comparer)];
        if (missing.Length != 0)
        {
            throw new InvalidOperationException(
                $"Missing release files for {productName} on {rid}: {string.Join(", ", missing)}");
        }

        string[] unexpected = [.. files.Where(path => !required.Contains(path)
            && !(terminal && path.StartsWith("tracehost/", comparison)))];
        if (unexpected.Length != 0)
        {
            throw new InvalidOperationException(
                $"Unexpected release files for {productName} on {rid}: {string.Join(", ", unexpected)}");
        }
    }

    internal static string[] RequiredTerminalFiles(string rid) => rid switch
    {
        "win-x64" => ["conpty.dll", "arm64/OpenConsole.exe", "x64/OpenConsole.exe"],
        "win-arm64" => ["conpty.dll", "arm64/OpenConsole.exe"],
        "linux-x64" or "linux-arm64" or "linux-musl-x64" or "linux-musl-arm64" => ["libhex1binterop.so"],
        "osx-x64" or "osx-arm64" => ["libhex1binterop.dylib"],
        _ => throw new ArgumentException($"Unsupported runtime identifier '{rid}'.", nameof(rid)),
    };
}
