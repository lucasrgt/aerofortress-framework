// The e2e tier of the frontend doctor (canonical). E2E is flow-level and expensive, so it is NOT enforced per
// component (that is the integration tier's job, LZFE006). Instead it is enforced at the PROJECT level via a
// CURATED checklist: a project declares its critical flows in `e2e/flows.json`, and the doctor proves every listed
// flow has an e2e spec + a runner is configured. Humans own WHICH flows are critical; the doctor only proves the
// list is covered — "completeness via checklist", avoiding both per-component e2e theater and silent under-coverage.
//
// `checkE2e(root)` is pure (no process.exit) so a CLI, a test, or `lazuli doctor` can decide what to do with the
// report. An app wires a thin CLI over it (see the Hostpoint dogfood's scripts/lzfe-e2e-doctor.mjs).
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

const RUNNERS = ["playwright.config.ts", "playwright.config.js", "playwright.config.mjs"];

/**
 * Inspect a project's e2e tier. Returns `{ bootstrap, flows, specs, gaps, messages }`:
 *   - bootstrap: true when e2e/ + flows.json don't exist yet (nothing to enforce — the pillar isn't set up)
 *   - flows/specs: counts of curated flows and spec files found
 *   - gaps: enforceable problems (a curated flow with no spec, a missing runner, an invalid manifest)
 *   - messages: human-readable lines describing each gap
 */
export function checkE2e(root) {
  const e2eDir = join(root, "e2e");
  const flowsFile = join(e2eDir, "flows.json");

  if (!existsSync(e2eDir) || !existsSync(flowsFile)) {
    return { bootstrap: true, flows: 0, specs: 0, gaps: 0, messages: ["no e2e/ + flows.json yet (bootstrap)"] };
  }

  const messages = [];
  let gaps = 0;
  if (!RUNNERS.some((c) => existsSync(join(root, c)))) {
    messages.push("no playwright.config.* — configure the runner");
    gaps++;
  }

  let flows;
  try {
    flows = JSON.parse(readFileSync(flowsFile, "utf8"));
    if (!Array.isArray(flows)) throw new Error("flows.json must be an array");
  } catch (e) {
    return { bootstrap: false, flows: 0, specs: 0, gaps: gaps + 1, messages: [...messages, `flows.json invalid — ${e.message}`] };
  }

  const specs = new Set(readdirSync(e2eDir).filter((f) => /\.spec\.[tj]sx?$/.test(f)));
  for (const flow of flows) {
    const spec = String(flow.spec ?? "").replace(/^e2e[\\/]/, "");
    if (!spec || !specs.has(spec)) {
      messages.push(`curated flow "${flow.name ?? "(unnamed)"}" has no spec (${flow.spec ?? "missing"})`);
      gaps++;
    }
  }

  return { bootstrap: false, flows: flows.length, specs: specs.size, gaps, messages };
}
