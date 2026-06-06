// The e2e tier of the frontend doctor (canonical, runner-agnostic). E2E is flow-level and expensive, so it is NOT
// enforced per component (that is the integration tier's job, LZFE006). It is enforced at the PROJECT level via a
// CURATED checklist: a project declares its critical flows in `e2e/flows.json`, and the doctor proves every listed
// flow has an implementation file + an e2e runner is configured. Humans own WHICH flows are critical; the doctor
// only proves the list is covered — "completeness via checklist".
//
// The runner is the swappable "RSpec slot": Playwright is the blessed default (web), Maestro/Detox for native. The
// OFFICIAL, tool-agnostic artifacts are the flows.json manifest (the WHAT) + this doctor. A flow's `spec` is just a
// path to its implementation — a Playwright `.spec.ts` OR a Maestro `.yaml` — so the manifest is portable even
// though the spec files are not (a Playwright spec does not run on Maestro; you pick the runner per target).
//
// `checkE2e(root)` is pure (no process.exit) so a CLI, a test, or `lazuli doctor` decides what to do with it.
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

// Known e2e runners — config file OR convention dir. Add a runner here, not in consumers.
const RUNNER_FILES = ["playwright.config.ts", "playwright.config.js", "playwright.config.mjs", ".detoxrc.js", ".detoxrc.json", ".detoxrc"];
const RUNNER_DIRS = [".maestro", "maestro"];

/** Which e2e runner a project has configured, or null. */
export function detectRunner(root) {
  if (RUNNER_FILES.slice(0, 3).some((f) => existsSync(join(root, f)))) return "playwright";
  if (RUNNER_DIRS.some((d) => existsSync(join(root, d)))) return "maestro";
  if (RUNNER_FILES.slice(3).some((f) => existsSync(join(root, f)))) return "detox";
  return null;
}

/**
 * Inspect a project's e2e tier. Returns `{ bootstrap, runner, flows, gaps, messages }`:
 *   - bootstrap: true when e2e/flows.json doesn't exist yet (nothing to enforce — the pillar isn't set up)
 *   - runner: the detected runner ("playwright" | "maestro" | "detox") or null
 *   - flows: count of curated flows; gaps: enforceable problems; messages: human-readable lines
 * A flow's `spec` is a path from the project root to its implementation file (any runner's format).
 */
export function checkE2e(root) {
  const flowsFile = join(root, "e2e", "flows.json");
  if (!existsSync(flowsFile)) {
    return { bootstrap: true, runner: detectRunner(root), flows: 0, gaps: 0, messages: ["no e2e/flows.json yet (bootstrap)"] };
  }

  const messages = [];
  let gaps = 0;
  const runner = detectRunner(root);
  if (!runner) {
    messages.push("no e2e runner configured (playwright.config.* / .maestro/ / .detoxrc*)");
    gaps++;
  }

  let flows;
  try {
    flows = JSON.parse(readFileSync(flowsFile, "utf8"));
    if (!Array.isArray(flows)) throw new Error("flows.json must be an array");
  } catch (e) {
    return { bootstrap: false, runner, flows: 0, gaps: gaps + 1, messages: [...messages, `flows.json invalid — ${e.message}`] };
  }

  for (const flow of flows) {
    const spec = String(flow.spec ?? "");
    if (!spec || !existsSync(join(root, spec))) {
      messages.push(`curated flow "${flow.name ?? "(unnamed)"}" has no spec (${flow.spec ?? "missing"})`);
      gaps++;
    }
  }

  return { bootstrap: false, runner, flows: flows.length, gaps, messages };
}
