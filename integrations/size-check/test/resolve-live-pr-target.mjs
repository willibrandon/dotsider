import { createRequire } from "node:module";
import * as path from "node:path";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const { resolveGithubTargetCommit } = require(path.join(
  repository,
  "artifacts/size-check-tests/src/github-baseline.js",
));
const { resolveLocalMergeTargetCommit } = require(path.join(
  repository,
  "artifacts/size-check-tests/src/azure-baseline.js",
));

const resolution = await resolveGithubTargetCommit(process.env);
if (resolution.status !== "resolved") {
  throw new Error(`Expected an open pull-request test merge; resolution was ${JSON.stringify(resolution)}.`);
}
const event = JSON.parse(await (await import("node:fs/promises")).readFile(process.env.GITHUB_EVENT_PATH, "utf8"));
const local = await resolveLocalMergeTargetCommit(
  process.env.GITHUB_WORKSPACE,
  process.env.GITHUB_SHA,
  event.pull_request?.head?.sha,
);
if (local.status !== "resolved" || local.targetCommit !== resolution.targetCommit) {
  throw new Error(`Local PR merge resolution disagreed: ${JSON.stringify(local)}.`);
}
process.stdout.write(`${resolution.targetCommit}\n`);
