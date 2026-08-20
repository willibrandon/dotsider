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
exports.buildSizeCheckArguments = buildSizeCheckArguments;
exports.readSizeReport = readSizeReport;
exports.isSizeReport = isSizeReport;
exports.classifyResult = classifyResult;
exports.createStableOutputs = createStableOutputs;
exports.createErrorOutputs = createErrorOutputs;
exports.formatSizeCheckSummary = formatSizeCheckSummary;
const fs = __importStar(require("node:fs/promises"));
const path = __importStar(require("node:path"));
function buildSizeCheckArguments(target, baseline, budgets, budgetFile, top, why, jsonReportPath, markdownReportPath) {
    const args = [
        "size-check",
        target,
        "--format",
        "json",
        "--output",
        jsonReportPath,
        "--summary-file",
        markdownReportPath,
        "--top",
        String(top),
    ];
    if (baseline) {
        args.push("--baseline", baseline);
    }
    for (const budget of budgets) {
        args.push("--budget", budget);
    }
    if (budgetFile) {
        args.push("--budget-file", budgetFile);
    }
    if (why) {
        args.push("--why");
    }
    return args;
}
async function readSizeReport(reportPath) {
    const parsed = JSON.parse(await fs.readFile(reportPath, "utf8"));
    if (!isSizeReport(parsed)) {
        throw new Error(`Dotsider wrote an unsupported JSON report to '${reportPath}'.`);
    }
    return parsed;
}
function isSizeReport(value) {
    if (!value || typeof value !== "object") {
        return false;
    }
    const report = value;
    return report.schemaVersion === 2
        && typeof report.target === "string"
        && !!report.targetArtifacts
        && typeof report.targetArtifacts.mstatPath === "string"
        && typeof report.totalBasis === "string"
        && typeof report.rightTotal === "number"
        && !!report.summary
        && typeof report.summary.delta === "number";
}
function classifyResult(exitCode, report) {
    if (exitCode === 2) {
        return "budget-failed";
    }
    if (exitCode !== 0 || report === undefined) {
        return "error";
    }
    if (report.budgets?.hasWarnings === true || report.budgets?.hasDeferred === true) {
        return "passed-with-warnings";
    }
    return "passed";
}
function createStableOutputs(execution, artifactName, dotsiderVersion, baselineSource, baselineComparison) {
    const report = execution.report;
    const evaluations = report?.budgets?.evaluations ?? [];
    const violationCount = evaluations.reduce((count, evaluation) => count + (evaluation.violations?.length ?? 0), 0);
    return {
        result: resultWithBaselineWarning(execution.result, baselineComparison),
        exitCode: String(execution.exitCode),
        jsonReportPath: path.resolve(execution.jsonReportPath),
        markdownReportPath: path.resolve(execution.markdownReportPath),
        artifactName,
        dotsiderVersion,
        totalBasis: report?.totalBasis ?? "",
        baselineTotal: numberOutput(report?.leftTotal),
        currentTotal: numberOutput(report?.rightTotal),
        delta: numberOutput(report?.summary.delta),
        violationCount: String(violationCount),
        baselineStatus: baselineSource?.status ?? "",
        baselineSourceId: baselineSource?.id ?? "",
        baselineSourceCommit: baselineSource?.commit ?? "",
        baselineSourceUrl: baselineSource?.url ?? "",
        baselineArtifactName: baselineSource?.artifactName ?? "",
        baselineTargetCommit: baselineComparison?.targetCommit ?? "",
        baselineComparisonStatus: baselineComparison?.status ?? "",
        baselineComparisonReason: baselineComparison?.status === "unknown" ? baselineComparison.reason : "",
    };
}
function createErrorOutputs(artifactName, dotsiderVersion, baselineSource, baselineComparison) {
    return {
        result: "error",
        exitCode: "1",
        jsonReportPath: "",
        markdownReportPath: "",
        artifactName,
        dotsiderVersion,
        totalBasis: "",
        baselineTotal: "",
        currentTotal: "",
        delta: "",
        violationCount: "0",
        baselineStatus: baselineSource?.status ?? "",
        baselineSourceId: baselineSource?.id ?? "",
        baselineSourceCommit: baselineSource?.commit ?? "",
        baselineSourceUrl: baselineSource?.url ?? "",
        baselineArtifactName: baselineSource?.artifactName ?? "",
        baselineTargetCommit: baselineComparison?.targetCommit ?? "",
        baselineComparisonStatus: baselineComparison?.status ?? "",
        baselineComparisonReason: baselineComparison?.status === "unknown" ? baselineComparison.reason : "",
    };
}
function resultWithBaselineWarning(result, comparison) {
    return result === "passed" && comparison && comparison.status !== "current"
        ? "passed-with-warnings"
        : result;
}
function formatSizeCheckSummary(outputs) {
    const parts = [];
    if (outputs.currentTotal !== "" && outputs.baselineTotal === "") {
        parts.push("current build (no baseline comparison)");
    }
    else if (outputs.baselineTotal !== "") {
        parts.push("compared with baseline");
    }
    const currentTotal = parseOutputNumber(outputs.currentTotal);
    if (currentTotal !== undefined) {
        parts.push(`${formatBytes(currentTotal)} total${outputs.totalBasis ? ` (${outputs.totalBasis})` : ""}`);
    }
    const delta = parseOutputNumber(outputs.delta);
    if (outputs.baselineTotal !== "" && delta !== undefined) {
        parts.push(`${delta >= 0 ? "+" : "-"}${formatBytes(Math.abs(delta))} from baseline`);
    }
    const violationCount = parseOutputNumber(outputs.violationCount);
    if (violationCount !== undefined && violationCount > 0) {
        parts.push(`${violationCount} budget violation${violationCount === 1 ? "" : "s"}`);
    }
    return parts.join("; ");
}
function numberOutput(value) {
    return value === null || value === undefined ? "" : String(value);
}
function parseOutputNumber(value) {
    if (value.trim() === "") {
        return undefined;
    }
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : undefined;
}
function formatBytes(value) {
    const units = ["B", "KB", "MB", "GB", "TB"];
    let amount = value;
    let unit = 0;
    while (amount >= 1024 && unit < units.length - 1) {
        amount /= 1024;
        unit++;
    }
    return unit === 0 ? `${Math.round(amount)} B` : `${amount.toFixed(1)} ${units[unit]}`;
}
