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
exports.parseBoolean = parseBoolean;
exports.parseTop = parseTop;
exports.parseBudgets = parseBudgets;
exports.parseMode = parseMode;
exports.createInputs = createInputs;
const path = __importStar(require("node:path"));
function parseBoolean(value, fallback) {
    if (value === undefined || value.trim() === "") {
        return fallback;
    }
    switch (value.trim().toLowerCase()) {
        case "true":
        case "1":
        case "yes":
            return true;
        case "false":
        case "0":
        case "no":
            return false;
        default:
            throw new Error(`Expected a boolean value but received '${value}'.`);
    }
}
function parseTop(value) {
    const candidate = (value ?? "10").trim();
    if (!/^\d+$/u.test(candidate)) {
        throw new Error(`top must be a non-negative integer; received '${value ?? ""}'.`);
    }
    const parsed = Number(candidate);
    if (!Number.isSafeInteger(parsed)) {
        throw new Error(`top must be a non-negative integer; received '${value ?? ""}'.`);
    }
    return parsed;
}
function parseBudgets(value) {
    return (value ?? "")
        .split(/\r?\n/u)
        .map(line => line.trim())
        .filter(line => line.length > 0);
}
function parseMode(value, baseline) {
    const mode = required(value, "mode").toLowerCase();
    if (mode === "current") {
        if (baseline) {
            throw new Error("baseline must not be supplied when mode is 'current'.");
        }
        return mode;
    }
    if (mode === "compare") {
        if (!baseline) {
            throw new Error("baseline is required when mode is 'compare'.");
        }
        return mode;
    }
    throw new Error(`mode must be 'current' or 'compare'; received '${value ?? ""}'.`);
}
function createInputs(values, defaultReportRoot) {
    const target = required(values.target, "target");
    const baseline = optionalPath(values.baseline);
    const requestedDirectory = values.reportDirectory?.trim();
    const reportDirectory = path.resolve(requestedDirectory && requestedDirectory.length > 0
        ? requestedDirectory
        : path.join(defaultReportRoot, "dotsider-size-check"));
    return {
        mode: parseMode(values.mode, baseline),
        target: path.resolve(target),
        baseline,
        budgets: parseBudgets(values.budgets),
        budgetFile: optionalPath(values.budgetFile),
        top: parseTop(values.top),
        why: parseBoolean(values.why, false),
        dotsiderVersion: values.dotsiderVersion?.trim() || "latest",
        dotsiderPath: optionalPath(values.dotsiderPath),
        reportDirectory,
        publishSummary: parseBoolean(values.publishSummary, true),
        publishReports: parseBoolean(values.publishReports, true),
        artifactName: values.artifactName?.trim() || "dotsider-size-check",
    };
}
function required(value, name) {
    if (!value || value.trim().length === 0) {
        throw new Error(`${name} is required.`);
    }
    return value.trim();
}
function optionalPath(value) {
    const candidate = value?.trim();
    return candidate ? path.resolve(candidate) : undefined;
}
