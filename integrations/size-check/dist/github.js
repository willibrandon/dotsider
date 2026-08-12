"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
const fs = __importStar(require("node:fs/promises"));
const os = __importStar(require("node:os"));
const path = __importStar(require("node:path"));
const acquisition_1 = require("./acquisition");
const baseline_1 = require("./baseline");
const github_baseline_1 = require("./github-baseline");
const input_1 = require("./input");
const process_1 = require("./process");
const report_1 = require("./report");
void main();
async function main() {
    const commandName = process.argv[2];
    let errorOutputs = (0, report_1.createErrorOutputs)(optional(process.env.DOTSIDER_INPUT_ARTIFACT_NAME) || "dotsider-size-check", optional(process.env.DOTSIDER_PREPARED_VERSION) || "");
    try {
        switch (commandName) {
            case "prepare":
                await prepare();
                break;
            case "run":
                await run(outputs => {
                    errorOutputs = { ...outputs, result: "error", exitCode: "1" };
                });
                break;
            case "discover":
                await discover();
                break;
            case "enforce":
                enforce();
                break;
            default:
                throw new Error("Expected a GitHub adapter command: prepare, discover, run, or enforce.");
        }
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        if (commandName === "run") {
            writeStableOutputs(errorOutputs);
        }
        command("error", {}, message);
        process.exitCode = 1;
    }
}
async function discover() {
    const inputs = githubInputs();
    const discovery = await (0, github_baseline_1.discoverGithubBaseline)(inputs, requiredEnvironment("DOTSIDER_PREPARED_RID"));
    writeOutputs({
        status: discovery.source.status,
        "artifact-name": discovery.artifactName,
        "run-id": discovery.runId ?? "",
        "download-directory": discovery.downloadDirectory ?? "",
        publish: String(discovery.publish),
        identity: JSON.stringify(discovery.identity),
        source: JSON.stringify(discovery.source),
    });
}
async function prepare() {
    const tool = await (0, acquisition_1.prepareTool)(process.env.DOTSIDER_INPUT_VERSION || "latest", optional(process.env.DOTSIDER_INPUT_PATH));
    writeOutputs({
        version: tool.version,
        rid: tool.rid,
        "cache-directory": tool.cacheDirectory,
        "executable-path": tool.executablePath,
        "cache-key": tool.cacheKey,
        explicit: String(tool.explicit),
    });
}
async function run(onOutputs) {
    let inputs = githubInputs();
    const tool = preparedTool();
    const executable = await (0, acquisition_1.acquireTool)(tool);
    const discovery = process.env.DOTSIDER_BASELINE_SOURCE
        ? preparedDiscovery()
        : await (0, github_baseline_1.discoverGithubBaseline)(inputs, tool.rid);
    let source = discovery.source;
    if (source.status === "restored") {
        const directory = requiredEnvironment("DOTSIDER_BASELINE_DOWNLOAD_DIRECTORY");
        const restored = await (0, baseline_1.restoreBaseline)(directory, discovery.identity, source);
        inputs = { ...inputs, baseline: restored.targetPath };
    }
    const execution = await (0, process_1.executeSizeCheck)(executable, inputs, source.status === "not-found");
    if (execution.report && await fileExists(execution.markdownReportPath)) {
        execution.report = await (0, baseline_1.enrichReports)(execution.jsonReportPath, execution.markdownReportPath, source);
    }
    const outputs = (0, report_1.createStableOutputs)(execution, inputs.artifactName, tool.version, source);
    onOutputs(outputs);
    writeStableOutputs(outputs);
    let baselineUploadPath = "";
    let publishBaseline = false;
    if (discovery.publish && execution.report
        && (execution.result === "passed" || execution.result === "passed-with-warnings")) {
        source = currentGithubSource(discovery.artifactName);
        baselineUploadPath = await (0, baseline_1.stageBaseline)(execution.report, discovery.identity, source, pathForBaseline(inputs.reportDirectory));
        publishBaseline = true;
    }
    writeOutputs({
        "baseline-upload-path": baselineUploadPath,
        "publish-baseline": String(publishBaseline),
    });
    const summary = (0, report_1.formatSizeCheckSummary)(outputs);
    if (summary) {
        process.stdout.write(`Dotsider size check: ${summary}.\n`);
    }
    if (inputs.publishSummary && await fileExists(execution.markdownReportPath)) {
        const summaryPath = process.env.GITHUB_STEP_SUMMARY;
        if (summaryPath) {
            const markdown = await fs.readFile(execution.markdownReportPath, "utf8");
            await fs.appendFile(summaryPath, `${markdown.trimEnd()}\n`);
        }
    }
}
function githubInputs() {
    return (0, input_1.createInputs)({
        target: process.env.DOTSIDER_INPUT_TARGET,
        baseline: process.env.DOTSIDER_INPUT_BASELINE,
        baselineKey: process.env.DOTSIDER_INPUT_BASELINE_KEY,
        budgets: process.env.DOTSIDER_INPUT_BUDGETS,
        budgetFile: process.env.DOTSIDER_INPUT_BUDGET_FILE,
        top: process.env.DOTSIDER_INPUT_TOP,
        why: process.env.DOTSIDER_INPUT_WHY,
        dotsiderVersion: process.env.DOTSIDER_INPUT_VERSION,
        dotsiderPath: process.env.DOTSIDER_INPUT_PATH,
        reportDirectory: process.env.DOTSIDER_INPUT_REPORT_DIRECTORY,
        publishSummary: process.env.DOTSIDER_INPUT_PUBLISH_SUMMARY,
        publishReports: process.env.DOTSIDER_INPUT_PUBLISH_REPORTS,
        artifactName: process.env.DOTSIDER_INPUT_ARTIFACT_NAME,
    }, process.env.RUNNER_TEMP || os.tmpdir());
}
function preparedDiscovery() {
    return {
        source: JSON.parse(requiredEnvironment("DOTSIDER_BASELINE_SOURCE")),
        identity: JSON.parse(requiredEnvironment("DOTSIDER_BASELINE_IDENTITY")),
        artifactName: requiredEnvironment("DOTSIDER_BASELINE_ARTIFACT_NAME"),
        downloadDirectory: optional(process.env.DOTSIDER_BASELINE_DOWNLOAD_DIRECTORY),
        publish: process.env.DOTSIDER_BASELINE_PUBLISH === "true",
    };
}
function currentGithubSource(artifactName) {
    const repository = requiredEnvironment("GITHUB_REPOSITORY");
    const runId = requiredEnvironment("GITHUB_RUN_ID");
    return {
        status: "restored",
        provider: "github-actions",
        branch: process.env.GITHUB_REF_NAME,
        commit: process.env.GITHUB_SHA,
        id: runId,
        number: process.env.GITHUB_RUN_NUMBER,
        url: `${process.env.GITHUB_SERVER_URL || "https://github.com"}/${repository}/actions/runs/${runId}`,
        artifactName,
    };
}
function pathForBaseline(reportDirectory) {
    return path.join(reportDirectory, ".baseline");
}
function enforce() {
    const exitCode = Number.parseInt(process.env.DOTSIDER_EXIT_CODE || "1", 10);
    const result = process.env.DOTSIDER_RESULT || "error";
    if (exitCode === 0) {
        return;
    }
    if (exitCode === 2) {
        const summary = (0, report_1.formatSizeCheckSummary)({
            totalBasis: process.env.DOTSIDER_TOTAL_BASIS || "",
            baselineTotal: process.env.DOTSIDER_BASELINE_TOTAL || "",
            currentTotal: process.env.DOTSIDER_CURRENT_TOTAL || "",
            delta: process.env.DOTSIDER_DELTA || "",
            violationCount: process.env.DOTSIDER_VIOLATION_COUNT || "",
        });
        const artifactName = process.env.DOTSIDER_ARTIFACT_NAME || "dotsider-size-check";
        command("error", {}, `Dotsider size budgets were exceeded${summary ? `: ${summary}` : ""}. Full report: job summary and '${artifactName}' artifact.`);
    }
    else {
        command("error", {}, `Dotsider size check failed with exit code ${exitCode} (${result}).`);
    }
    process.exitCode = 1;
}
function preparedTool() {
    const version = requiredEnvironment("DOTSIDER_PREPARED_VERSION");
    const rid = requiredEnvironment("DOTSIDER_PREPARED_RID");
    const cacheDirectory = requiredEnvironment("DOTSIDER_PREPARED_CACHE_DIRECTORY");
    const executablePath = requiredEnvironment("DOTSIDER_PREPARED_EXECUTABLE_PATH");
    return {
        version,
        rid,
        cacheDirectory,
        executablePath,
        cacheKey: requiredEnvironment("DOTSIDER_PREPARED_CACHE_KEY"),
        explicit: process.env.DOTSIDER_PREPARED_EXPLICIT === "true",
    };
}
function writeStableOutputs(outputs) {
    writeOutputs({
        result: outputs.result,
        "exit-code": outputs.exitCode,
        "json-report-path": outputs.jsonReportPath,
        "markdown-report-path": outputs.markdownReportPath,
        "artifact-name": outputs.artifactName,
        "dotsider-version": outputs.dotsiderVersion,
        "total-basis": outputs.totalBasis,
        "baseline-total": outputs.baselineTotal,
        "current-total": outputs.currentTotal,
        delta: outputs.delta,
        "violation-count": outputs.violationCount,
        "baseline-status": outputs.baselineStatus,
        "baseline-source-id": outputs.baselineSourceId,
        "baseline-source-commit": outputs.baselineSourceCommit,
        "baseline-source-url": outputs.baselineSourceUrl,
        "baseline-artifact-name": outputs.baselineArtifactName,
    });
}
function writeOutputs(outputs) {
    const outputPath = requiredEnvironment("GITHUB_OUTPUT");
    const records = Object.entries(outputs).map(([name, value]) => {
        const delimiter = `dotsider_${crypto.randomUUID()}`;
        return `${name}<<${delimiter}\n${value}\n${delimiter}\n`;
    });
    require("node:fs").appendFileSync(outputPath, records.join(""), "utf8");
}
function command(name, properties, message) {
    const serialized = Object.entries(properties)
        .map(([key, value]) => `${key}=${escapeProperty(value)}`)
        .join(",");
    process.stdout.write(`::${name}${serialized ? ` ${serialized}` : ""}::${escapeData(message)}\n`);
}
function escapeData(value) {
    return value.replaceAll("%", "%25").replaceAll("\r", "%0D").replaceAll("\n", "%0A");
}
function escapeProperty(value) {
    return escapeData(value).replaceAll(":", "%3A").replaceAll(",", "%2C");
}
function optional(value) {
    return value?.trim() || undefined;
}
function requiredEnvironment(name) {
    const value = process.env[name];
    if (!value) {
        throw new Error(`Required environment variable ${name} was not provided.`);
    }
    return value;
}
async function fileExists(filePath) {
    try {
        return (await fs.stat(filePath)).isFile();
    }
    catch {
        return false;
    }
}
