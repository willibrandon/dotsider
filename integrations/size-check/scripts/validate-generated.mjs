import { readFile, readdir, stat } from "node:fs/promises";
import path from "node:path";
import process from "node:process";

const packageRoot = path.resolve(import.meta.dirname, "..");
const repositoryRoot = path.resolve(packageRoot, "../..");
const roots = [
  path.join(packageRoot, "dist"),
  path.join(repositoryRoot, "azure-devops/tasks/DotsiderSizeCheckV1/runtime"),
];
const expectedByRoot = new Map([
  [roots[0], new Set(["acquisition.js", "github.js", "input.js", "process.js", "report.js", "types.js"])],
  [roots[1], new Set(["acquisition.js", "azure.js", "input.js", "process.js", "report.js", "types.js"])],
]);
const bundleMarkers = ["__webpack_require__", "webpackBootstrap", "parcelRequire", "__commonJS("];
let totalBytes = 0;
let totalLines = 0;

for (const root of roots) {
  const entries = (await readdir(root)).sort();
  const expected = expectedByRoot.get(root);
  if (!expected || entries.length !== expected.size || entries.some(entry => !expected.has(entry))) {
    throw new Error(`Unexpected generated files in ${path.relative(repositoryRoot, root)}: ${entries.join(", ")}`);
  }

  for (const entry of entries) {
    const filePath = path.join(root, entry);
    const metadata = await stat(filePath);
    const source = await readFile(filePath, "utf8");
    const lines = source.split(/\r?\n/u).length;
    if (metadata.size > 50 * 1024) {
      throw new Error(`${entry} is ${metadata.size} bytes; generated modules are limited to 50 KiB.`);
    }
    if (lines > 750) {
      throw new Error(`${entry} is ${lines} lines; generated modules are limited to 750 lines.`);
    }
    const marker = bundleMarkers.find(candidate => source.includes(candidate));
    if (marker) {
      throw new Error(`${entry} contains bundler marker '${marker}'. Commit plain tsc output only.`);
    }
    totalBytes += metadata.size;
    totalLines += lines;
  }
}

if (totalBytes > 150 * 1024) {
  throw new Error(`Generated JavaScript totals ${totalBytes} bytes; the combined limit is 150 KiB.`);
}
if (totalLines > 2_000) {
  throw new Error(`Generated JavaScript totals ${totalLines} lines; the combined limit is 2,000 lines.`);
}

process.stdout.write(`Validated ${totalBytes} bytes and ${totalLines} lines of plain tsc output.\n`);
