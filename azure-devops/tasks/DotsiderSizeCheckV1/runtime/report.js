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
    return report.schemaVersion === 1
        && typeof report.target === "string"
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
    if (report.budgets?.hasWarnings === true) {
        return "passed-with-warnings";
    }
    return "passed";
}
function createStableOutputs(execution, artifactName, dotsiderVersion) {
    const report = execution.report;
    const evaluations = report?.budgets?.evaluations ?? [];
    const violationCount = evaluations.reduce((count, evaluation) => count + (evaluation.violations?.length ?? 0), 0);
    return {
        result: execution.result,
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
    };
}
function createErrorOutputs(artifactName, dotsiderVersion) {
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
    };
}
function numberOutput(value) {
    return value === null || value === undefined ? "" : String(value);
}
