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
exports.runProcess = runProcess;
exports.executeSizeCheck = executeSizeCheck;
const node_child_process_1 = require("node:child_process");
const fs = __importStar(require("node:fs/promises"));
const path = __importStar(require("node:path"));
const report_1 = require("./report");
async function runProcess(fileName, args) {
    return await new Promise((resolve, reject) => {
        const child = (0, node_child_process_1.spawn)(fileName, [...args], {
            shell: false,
            windowsHide: true,
            stdio: ["ignore", "pipe", "pipe"],
        });
        let stdout = "";
        let stderr = "";
        child.stdout.setEncoding("utf8");
        child.stderr.setEncoding("utf8");
        child.stdout.on("data", (data) => {
            stdout += data;
            process.stdout.write(data);
        });
        child.stderr.on("data", (data) => {
            stderr += data;
            process.stderr.write(data);
        });
        child.on("error", reject);
        child.on("close", (exitCode, signal) => {
            if (exitCode === null) {
                reject(new Error(`${fileName} terminated by signal ${signal ?? "unknown"}.`));
                return;
            }
            resolve({ exitCode, stdout, stderr });
        });
    });
}
async function executeSizeCheck(executablePath, inputs) {
    await fs.mkdir(inputs.reportDirectory, { recursive: true });
    const jsonReportPath = path.join(inputs.reportDirectory, "dotsider-size-check.json");
    const markdownReportPath = path.join(inputs.reportDirectory, "dotsider-size-check.md");
    await Promise.all([
        fs.rm(jsonReportPath, { force: true }),
        fs.rm(markdownReportPath, { force: true }),
    ]);
    const args = (0, report_1.buildSizeCheckArguments)(inputs.target, inputs.baseline, inputs.budgets, inputs.budgetFile, inputs.top, inputs.why, jsonReportPath, markdownReportPath);
    const processResult = await runProcess(executablePath, args);
    let report;
    try {
        report = await (0, report_1.readSizeReport)(jsonReportPath);
    }
    catch (error) {
        if (processResult.exitCode === 0 || processResult.exitCode === 2) {
            const message = error instanceof Error ? error.message : String(error);
            return {
                result: "error",
                exitCode: 1,
                jsonReportPath,
                markdownReportPath,
                stderr: `${processResult.stderr}${message}`,
            };
        }
    }
    return {
        result: (0, report_1.classifyResult)(processResult.exitCode, report),
        exitCode: processResult.exitCode,
        jsonReportPath,
        markdownReportPath,
        report,
        stderr: processResult.stderr,
    };
}
