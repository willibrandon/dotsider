import assert from "node:assert/strict";
import { test } from "node:test";
import { detectMuslRuntime, prepareTool } from "../src/acquisition";

test("live Node runtime selects a musl Dotsider release without a distro marker", async () => {
  assert.equal(process.platform, "linux");
  assert.match(process.arch, /^(?:x64|arm64)$/u);
  assert.equal(process.env.DOTSIDER_MUSL, undefined);
  const report = process.report.getReport();
  assert.equal(detectMuslRuntime(process.platform, undefined, report), true);

  const tool = await prepareTool("not-a-release", process.execPath);
  assert.equal(tool.explicit, true);
  assert.equal(tool.version, "custom");
  assert.equal(tool.rid, `linux-musl-${process.arch}`);
});
