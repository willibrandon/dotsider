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
exports.getInput = getInput;
exports.escapeVsoProperty = escapeVsoProperty;
exports.escapeVsoMessage = escapeVsoMessage;
const fs = __importStar(require("node:fs/promises"));
const os = __importStar(require("node:os"));
const path = __importStar(require("node:path"));
const acquisition_1 = require("./acquisition");
const input_1 = require("./input");
const process_1 = require("./process");
const report_1 = require("./report");
if (require.main === module) {
    void main();
}
async function main() {
    let artifactName = getInput("artifactName") || "dotsider-size-check";
    let dotsiderVersion = "";
    let errorOutputs = (0, report_1.createErrorOutputs)(artifactName, dotsiderVersion);
    try {
        const defaultRoot = process.env.BUILD_ARTIFACTSTAGINGDIRECTORY
            || process.env.AGENT_TEMPDIRECTORY
            || os.tmpdir();
        const inputs = (0, input_1.createInputs)({
            mode: getInput("mode"),
            target: getInput("target"),
            baseline: getInput("baseline"),
            budgets: getInput("budgets"),
            budgetFile: getInput("budgetFile"),
            top: getInput("top"),
            why: getInput("why"),
            dotsiderVersion: getInput("dotsiderVersion"),
            dotsiderPath: getInput("dotsiderPath"),
            reportDirectory: getInput("reportDirectory"),
            publishSummary: getInput("publishSummary"),
            publishReports: getInput("publishReports"),
            artifactName: getInput("artifactName"),
        }, defaultRoot);
        artifactName = inputs.artifactName;
        const tool = await (0, acquisition_1.prepareTool)(inputs.dotsiderVersion, inputs.dotsiderPath);
        dotsiderVersion = tool.version;
        const executable = await (0, acquisition_1.acquireTool)(tool);
        const execution = await (0, process_1.executeSizeCheck)(executable, inputs);
        const outputs = (0, report_1.createStableOutputs)(execution, inputs.artifactName, tool.version);
        errorOutputs = { ...outputs, result: "error", exitCode: "1" };
        writeStableOutputs(outputs);
        const summary = (0, report_1.formatSizeCheckSummary)(outputs);
        if (summary) {
            process.stdout.write(`Dotsider size check: ${summary}.${os.EOL}`);
        }
        if (inputs.publishSummary && await fileExists(execution.markdownReportPath)) {
            vso("task.uploadsummary", {}, execution.markdownReportPath);
        }
        if (inputs.publishReports
            && await fileExists(execution.jsonReportPath)
            && await fileExists(execution.markdownReportPath)) {
            vso("artifact.upload", { artifactname: inputs.artifactName }, inputs.reportDirectory);
        }
        if (execution.exitCode === 0) {
            complete("Succeeded", execution.result === "passed-with-warnings"
                ? "Dotsider size check passed with warnings."
                : "Dotsider size check passed.");
            return;
        }
        if (execution.exitCode === 2) {
            complete("Failed", `Dotsider size budgets were exceeded${summary ? `: ${summary}` : ""}.`);
        }
        else {
            complete("Failed", `Dotsider size check failed with exit code ${execution.exitCode}.`);
        }
        process.exitCode = 1;
    }
    catch (error) {
        if (errorOutputs.artifactName !== artifactName || errorOutputs.dotsiderVersion !== dotsiderVersion) {
            errorOutputs = (0, report_1.createErrorOutputs)(artifactName, dotsiderVersion);
        }
        writeStableOutputs(errorOutputs);
        complete("Failed", error instanceof Error ? error.message : String(error));
        process.exitCode = 1;
    }
}
function getInput(name) {
    const variants = [
        `INPUT_${name}`,
        `INPUT_${name.toUpperCase()}`,
        `INPUT_${name.replace(/([a-z])([A-Z])/gu, "$1_$2").toUpperCase()}`,
    ];
    for (const variant of variants) {
        const value = process.env[variant];
        if (value !== undefined && value.trim().length > 0) {
            return value.trim();
        }
    }
    return undefined;
}
function escapeVsoProperty(value) {
    return escapeVsoMessage(value).replaceAll(";", "%3B").replaceAll("]", "%5D");
}
function escapeVsoMessage(value) {
    return value
        .replaceAll("%", "%AZP25")
        .replaceAll("\r", "%0D")
        .replaceAll("\n", "%0A");
}
function setOutput(name, value) {
    vso("task.setvariable", { variable: name, isOutput: "true" }, value);
}
function writeStableOutputs(outputs) {
    for (const [name, value] of Object.entries(outputs)) {
        setOutput(name, value);
    }
}
function complete(result, message) {
    if (result === "Failed") {
        vso("task.logissue", { type: "error" }, message);
    }
    vso("task.complete", { result }, message);
}
function vso(command, properties, message) {
    const serialized = Object.entries(properties)
        .map(([key, value]) => `${key}=${escapeVsoProperty(value)};`)
        .join("");
    process.stdout.write(`##vso[${command} ${serialized}]${escapeVsoMessage(message)}${os.EOL}`);
}
async function fileExists(filePath) {
    try {
        return (await fs.stat(path.resolve(filePath))).isFile();
    }
    catch {
        return false;
    }
}
