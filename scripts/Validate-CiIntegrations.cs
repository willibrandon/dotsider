#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PackAsTool=false
#:include ScriptSupport.cs

using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

try
{
    return CiIntegrationValidator.Run(args);
}
catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or JsonException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

/// <summary>Validates the distributable GitHub Action and Azure Pipelines extension.</summary>
internal static class CiIntegrationValidator
{
    public static int Run(string[] args)
    {
        string root = ScriptSupport.FindRepositoryRoot();
        (string? vsix, string? expectedVersion) = ParseArguments(args, root);

        ValidateGitHubAction(root);
        ValidateOnDemandWorkflow(root);
        ValidateAzureTask(root);
        ValidatePackageManager(root);
        if (vsix is not null)
            ValidateVsix(vsix, expectedVersion);

        Console.WriteLine(vsix is null
            ? "CI integration manifests are valid."
            : $"CI integration manifests and {Path.GetFileName(vsix)} are valid.");
        return 0;
    }

    private static void ValidateOnDemandWorkflow(string root)
    {
        string workflowPath = Path.Combine(
            root, "integrations", "size-check", "examples", "github-aot-size.yml");
        string source = File.ReadAllText(workflowPath);
        Require(source.Contains("workflow_dispatch:", StringComparison.Ordinal)
            && source.Contains("issue_comment:", StringComparison.Ordinal)
            && source.Contains("pull_request_review_comment:", StringComparison.Ordinal)
            && source.Contains("/aot-size", StringComparison.Ordinal),
            "The on-demand example must support manual and /aot-size pull-request requests.");
        Require(source.Contains("getCollaboratorPermissionLevel", StringComparison.Ordinal)
            && source.Contains("['admin', 'write']", StringComparison.Ordinal),
            "The /aot-size example must require collaborator write access.");
        Require(source.Contains("actions/download-artifact@v8", StringComparison.Ordinal)
            && source.Contains("current-run", StringComparison.Ordinal)
            && source.Contains("base-run", StringComparison.Ordinal),
            "The on-demand example must consume successful PR and base-branch build artifacts.");
        Require(source.Contains("run.event === 'pull_request'", StringComparison.Ordinal)
            && source.Contains("run.event !== 'pull_request'", StringComparison.Ordinal)
            && source.Contains("comment:", StringComparison.Ordinal),
            "The on-demand example must separate PR and branch runs and comment from a dedicated job.");
        Require(!source.Contains("actions/checkout", StringComparison.Ordinal),
            "The write-capable on-demand workflow must not check out pull-request code.");
        Require(source.Contains("<!-- dotsider-aot-size -->", StringComparison.Ordinal),
            "The on-demand example must update one identifiable pull-request comment.");
    }

    private static void ValidateGitHubAction(string root)
    {
        string actionPath = Path.Combine(root, "action.yml");
        string source = File.ReadAllText(actionPath);
        Require(source.Contains("using: composite", StringComparison.Ordinal),
            "action.yml must remain a composite action.");
        Require(source.Contains("actions/setup-node@v7.0.0", StringComparison.Ordinal)
            && source.Contains("node-version: '24'", StringComparison.Ordinal),
            "action.yml must select Node 24 explicitly.");
        int runIndex = source.IndexOf("Run Dotsider size check", StringComparison.Ordinal);
        int discoverIndex = source.IndexOf("Find Dotsider baseline", StringComparison.Ordinal);
        int uploadIndex = source.IndexOf("Upload Dotsider reports", StringComparison.Ordinal);
        int baselineUploadIndex = source.IndexOf("Publish managed Dotsider baseline", StringComparison.Ordinal);
        int enforceIndex = source.IndexOf("Enforce Dotsider result", StringComparison.Ordinal);
        Require(discoverIndex >= 0 && runIndex > discoverIndex && uploadIndex > runIndex
            && baselineUploadIndex > uploadIndex && enforceIndex > baselineUploadIndex,
            "action.yml must discover baselines, publish reports and the next baseline, then enforce.");
        Require(source.Contains(
                "if: always() && inputs.upload-reports == 'true' && steps.run.outputs.result != 'error'",
                StringComparison.Ordinal),
            "action.yml must retain reports for budget failures.");
        Require(source.Contains("actions/cache@v6.1.0", StringComparison.Ordinal),
            "action.yml must cache verified releases by the prepared cache key.");
        Require(source.Contains(
                "if: steps.prepare.outcome == 'success' && steps.prepare.outputs.explicit != 'true'",
                StringComparison.Ordinal),
            "action.yml must not restore the tool cache after preparation fails.");
        Require(source.Contains("id: run\n      if: always()", StringComparison.Ordinal)
            || source.Contains("id: run\r\n      if: always()", StringComparison.Ordinal),
            "action.yml must emit stable run outputs after preparation fails.");
        Require(!source.Contains("inputs.mode", StringComparison.Ordinal)
            && !source.Contains("steps.run.outputs.mode", StringComparison.Ordinal)
            && !source.Contains("DOTSIDER_INPUT_MODE", StringComparison.Ordinal),
            "action.yml must not expose a separate mode; supplying baseline controls comparison.");
        Require(source.Contains("DOTSIDER_INPUT_BASELINE: ${{ inputs.baseline }}", StringComparison.Ordinal),
            "action.yml must pass the optional baseline to Dotsider.");
        Require(source.Contains("actions/download-artifact@v8", StringComparison.Ordinal)
            && source.Contains("steps.baseline.outputs.run-id", StringComparison.Ordinal),
            "action.yml must restore the exact baseline artifact from the discovered successful run.");
        Require(source.Contains("steps.run.outputs.publish-baseline", StringComparison.Ordinal)
            && source.Contains("steps.run.outputs.baseline-upload-path", StringComparison.Ordinal),
            "action.yml must retain the successful branch target as the next managed baseline.");
        Require(source.Contains("${{ steps.run.outputs.json-report-path }}", StringComparison.Ordinal)
            && source.Contains("${{ steps.run.outputs.markdown-report-path }}", StringComparison.Ordinal),
            "action.yml must upload only the reports produced by Dotsider.");
    }

    private static void ValidateAzureTask(string root)
    {
        string taskPath = Path.Combine(root, "azure-devops", "tasks", "DotsiderSizeCheckV1", "task.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(taskPath));
        JsonElement rootElement = document.RootElement;
        Require(rootElement.GetProperty("name").GetString() == "DotsiderSizeCheck",
            "The Azure task name must remain DotsiderSizeCheck.");
        Require(rootElement.GetProperty("version").GetProperty("Major").GetInt32() == 1,
            "The Azure task major version must remain 1 for DotsiderSizeCheck@1.");
        Require(rootElement.GetProperty("minimumAgentVersion").GetString() == "3.230.2",
            "The Azure task must require an agent with the Node20_1 fallback handler.");
        JsonElement execution = rootElement.GetProperty("execution");
        Require(execution.TryGetProperty("Node24", out JsonElement node24)
            && node24.GetProperty("target").GetString() == "runtime/azure.js",
            "The Azure task must provide its Node 24 handler.");
        Require(execution.TryGetProperty("Node20_1", out JsonElement node20)
            && node20.GetProperty("target").GetString() == "runtime/azure.js",
            "The Azure task must retain its Node 20 handler for older agents.");
        Dictionary<string, JsonElement> inputs = rootElement.GetProperty("inputs")
            .EnumerateArray()
            .ToDictionary(input => input.GetProperty("name").GetString() ?? string.Empty);
        Require(!inputs.ContainsKey("mode"),
            "The Azure task must not expose a separate mode; supplying baseline controls comparison.");
        JsonElement target = inputs["target"];
        Require(target.GetProperty("type").GetString() == "filePath"
            && target.GetProperty("required").GetBoolean(),
            "The Azure task target must remain required.");
        foreach (string name in new[] { "baseline", "baselineKey", "budgetFile", "dotsiderPath" })
        {
            JsonElement input = inputs[name];
            Require(input.GetProperty("type").GetString() == "string"
                && input.GetProperty("defaultValue").GetString() == string.Empty
                && !input.GetProperty("required").GetBoolean(),
                $"The optional Azure task input '{name}' must remain an empty string; "
                    + "Azure roots empty filePath inputs at the working directory.");
        }
        string[] stableOutputs =
        [
            "result",
            "exitCode",
            "jsonReportPath",
            "markdownReportPath",
            "artifactName",
            "dotsiderVersion",
            "totalBasis",
            "baselineTotal",
            "currentTotal",
            "delta",
            "violationCount",
            "baselineStatus",
            "baselineSourceId",
            "baselineSourceCommit",
            "baselineSourceUrl",
            "baselineArtifactName",
            "baselineTargetCommit",
            "baselineFreshness",
        ];
        string[] outputVariables =
        [
            .. rootElement.GetProperty("outputVariables")
                .EnumerateArray()
                .Select(output => output.GetProperty("name").GetString() ?? string.Empty),
        ];
        Require(outputVariables.SequenceEqual(stableOutputs, StringComparer.Ordinal),
            "The Azure task output contract must remain stable and must not expose mode.");
        string[] settableVariables =
        [
            .. rootElement.GetProperty("restrictions")
                .GetProperty("settableVariables")
                .GetProperty("allowed")
                .EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty),
        ];
        Require(settableVariables.SequenceEqual(stableOutputs, StringComparer.Ordinal),
            "The Azure task may set only its documented output variables.");

        string extensionPath = Path.Combine(root, "azure-devops", "vss-extension.json");
        using JsonDocument extension = JsonDocument.Parse(File.ReadAllText(extensionPath));
        Require(extension.RootElement.GetProperty("publisher").GetString() == "willibrandon",
            "The Azure extension publisher must be willibrandon.");
        Require(extension.RootElement.GetProperty("galleryFlags")
            .EnumerateArray().Any(value => value.GetString() == "Public"),
            "The Azure extension must be public.");
    }

    private static void ValidatePackageManager(string root)
    {
        string integrationDirectory = Path.Combine(root, "integrations", "size-check");
        string[] directories =
        [
            integrationDirectory,
            Path.Combine(root, "azure-devops"),
        ];
        foreach (string directory in directories)
        {
            Require(File.Exists(Path.Combine(directory, "pnpm-lock.yaml")),
                $"{Path.GetRelativePath(root, directory)} must commit a pnpm lockfile.");
            Require(!File.Exists(Path.Combine(directory, "package-lock.json")),
                $"{Path.GetRelativePath(root, directory)} must not contain an npm lockfile.");
        }

        string workspacePath = Path.Combine(integrationDirectory, "pnpm-workspace.yaml");
        Require(File.Exists(workspacePath)
            && File.ReadLines(workspacePath).Any(line =>
                string.Equals(line.Trim(), "nodeLinker: hoisted", StringComparison.Ordinal)),
            "The size-check integration must use pnpm's symlink-free hoisted linker.");

        string editorProjectPath = Path.Combine(integrationDirectory, "tsconfig.json");
        using JsonDocument editorProject = JsonDocument.Parse(File.ReadAllText(editorProjectPath));
        JsonElement editorRoot = editorProject.RootElement;
        Require(editorRoot.GetProperty("compilerOptions").GetProperty("noEmit").GetBoolean(),
            "The size-check editor project must not emit runtime files.");
        HashSet<string> editorIncludes = editorRoot.GetProperty("include")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        Require(editorIncludes.SetEquals(["src/**/*.ts", "test/**/*.ts"]),
            "The size-check editor project must cover every source and test file.");
    }

    private static void ValidateVsix(string vsixPath, string? expectedVersion)
    {
        Require(File.Exists(vsixPath), $"VSIX not found: {vsixPath}");
        using ZipArchive archive = ZipFile.OpenRead(vsixPath);
        HashSet<string> entries = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] required =
        [
            "extension.vsixmanifest",
            "extension.vsomanifest",
            "tasks/DotsiderSizeCheckV1/task.json",
            "tasks/DotsiderSizeCheckV1/runtime/azure.js",
            "tasks/DotsiderSizeCheckV1/runtime/azure-baseline.js",
            "tasks/DotsiderSizeCheckV1/runtime/baseline.js",
            "README.md",
            "LICENSE",
            "PRIVACY.md",
        ];
        foreach (string entry in required)
            Require(entries.Contains(entry), $"VSIX is missing required entry '{entry}'.");
        Require(!entries.Any(entry => entry.Contains("node_modules/", StringComparison.OrdinalIgnoreCase)),
            "VSIX must not contain node_modules; the task runtime uses Node built-ins only.");
        Require(!entries.Any(entry => entry.EndsWith("github.js", StringComparison.OrdinalIgnoreCase)),
            "VSIX must not contain the GitHub adapter.");
        if (expectedVersion is not null)
        {
            ZipArchiveEntry manifestEntry = archive.GetEntry("extension.vsixmanifest")
                ?? throw new InvalidDataException("VSIX is missing extension.vsixmanifest.");
            using Stream stream = manifestEntry.Open();
            XDocument manifest = XDocument.Load(stream, LoadOptions.None);
            string? actualVersion = manifest.Descendants()
                .Single(element => element.Name.LocalName == "Identity")
                .Attribute("Version")?.Value;
            Require(string.Equals(actualVersion, expectedVersion, StringComparison.Ordinal),
                $"VSIX version is '{actualVersion}', expected '{expectedVersion}'.");
        }
    }

    private static (string? Vsix, string? ExpectedVersion) ParseArguments(string[] args, string root)
    {
        if (args.Length == 0)
            return (null, null);
        if (args.Length is not (2 or 4)
            || !string.Equals(args[0], "-Vsix", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Usage: dotnet run --file ./scripts/Validate-CiIntegrations.cs -- "
                + "[-Vsix <path> [-Version <version>]]");
        }
        string? version = null;
        if (args.Length == 4)
        {
            if (!string.Equals(args[2], "-Version", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(args[3]))
            {
                throw new ArgumentException("-Version requires a nonempty version after -Vsix.");
            }
            version = args[3];
        }
        return (Path.GetFullPath(args[1], root), version);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
