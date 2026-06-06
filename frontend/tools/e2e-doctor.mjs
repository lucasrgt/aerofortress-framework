// The e2e tier of the frontend doctor (canonical, runner-agnostic + target-aware). E2E is flow-level and
// expensive, so it is NOT enforced per component (that is the integration tier's job, LZFE006). It is enforced at
// the PROJECT level via a CURATED checklist: a project declares its critical flows in `e2e/flows.json`, and the
// doctor proves every listed flow has an implementation file AND a runner for its target is configured. Humans own
// WHICH flows are critical; the doctor only proves the list is covered — "completeness via checklist".
//
// The runner is the swappable slot (the "RSpec"): Playwright is the blessed default for WEB, Maestro/Detox for
// NATIVE — different targets, different tools (a Playwright spec does not run on Maestro). The OFFICIAL,
// tool-agnostic artifacts are the flows.json manifest (the WHAT) + this doctor. A flow declares its `target`
// (web|native) and a `spec` path to its implementation (a `.spec.ts` or a `.yaml`).
//
// `checkE2e(root)` is pure (no process.exit) so a CLI, a test, or `lazuli doctor` decides what to do with it.
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

// Known runners → how to detect each (config file or convention dir). Add a runner here, not in consumers.
const RUNNER_DETECT = {
  playwright: { files: ["playwright.config.ts", "playwright.config.js", "playwright.config.mjs"], dirs: [] },
  maestro: { files: [], dirs: [".maestro", "maestro"] },
  detox: { files: [".detoxrc.js", ".detoxrc.json", ".detoxrc"], dirs: [] },
};
const WEB_RUNNERS = new Set(["playwright", "cypress"]);
const NATIVE_RUNNERS = new Set(["maestro", "detox"]);

/** The e2e runners a project has configured (e.g. ["playwright", "maestro"]). */
export function detectRunners(root) {
  return Object.entries(RUNNER_DETECT)
    .filter(([, { files, dirs }]) => files.some((f) => existsSync(join(root, f))) || dirs.some((d) => existsSync(join(root, d))))
    .map(([name]) => name);
}

/**
 * Inspect a project's e2e tier. Returns `{ bootstrap, runners, flows, gaps, critical, messages }`:
 *   - bootstrap: true when e2e/flows.json doesn't exist yet (nothing to enforce — the pillar isn't set up)
 *   - runners: detected runners; flows: count of curated flows; gaps: enforceable problems; messages: lines
 *   - critical: `{ total, covered, gaps }` — flows flagged `critical: true` are a HARD requirement: a critical flow
 *     missing its spec or its target's runner counts in `critical.gaps`. A consumer SHOULD fail the build on
 *     `critical.gaps > 0` while treating plain `gaps` (non-critical) as a warning. This is how "mark a flow critical"
 *     gets teeth: you cannot flag a flow critical and leave it unimplemented.
 * Each flow: `{ name, area?, target?: "web"|"native", spec, critical?: boolean }`; spec is a path from root to its impl.
 * Target-aware: a target:native flow needs a native runner (.maestro/ or detox); a target:web flow needs Playwright.
 */
export function checkE2e(root) {
  const flowsFile = join(root, "e2e", "flows.json");
  if (!existsSync(flowsFile)) {
    return {
      bootstrap: true,
      runners: detectRunners(root),
      flows: 0,
      gaps: 0,
      critical: { total: 0, covered: 0, gaps: 0 },
      messages: ["no e2e/flows.json yet (bootstrap)"],
    };
  }

  const messages = [];
  let gaps = 0;
  const runners = detectRunners(root);
  const hasWeb = runners.some((r) => WEB_RUNNERS.has(r));
  const hasNative = runners.some((r) => NATIVE_RUNNERS.has(r));
  if (runners.length === 0) {
    messages.push("no e2e runner configured (playwright.config.* / .maestro/ / .detoxrc*)");
    gaps++;
  }

  let flows;
  try {
    flows = JSON.parse(readFileSync(flowsFile, "utf8"));
    if (!Array.isArray(flows)) throw new Error("flows.json must be an array");
  } catch (e) {
    return {
      bootstrap: false,
      runners,
      flows: 0,
      gaps: gaps + 1,
      critical: { total: 0, covered: 0, gaps: 0 },
      messages: [...messages, `flows.json invalid — ${e.message}`],
    };
  }

  let criticalTotal = 0;
  let criticalCovered = 0;
  let criticalGaps = 0;
  for (const flow of flows) {
    const isCritical = flow.critical === true;
    if (isCritical) criticalTotal++;
    const tag = isCritical ? "CRITICAL flow" : "curated flow";
    const spec = String(flow.spec ?? "");
    let ok = true;
    if (!spec || !existsSync(join(root, spec))) {
      messages.push(`${tag} "${flow.name ?? "(unnamed)"}" has no spec (${flow.spec ?? "missing"})`);
      gaps++;
      ok = false;
    } else if (flow.target === "native" && !hasNative) {
      messages.push(`${tag} "${flow.name}" is target:native but no native runner (.maestro/ or detox) is configured`);
      gaps++;
      ok = false;
    } else if (flow.target === "web" && !hasWeb) {
      messages.push(`${tag} "${flow.name}" is target:web but no web runner (playwright) is configured`);
      gaps++;
      ok = false;
    }
    if (isCritical) {
      if (ok) criticalCovered++;
      else criticalGaps++;
    }
  }

  return {
    bootstrap: false,
    runners,
    flows: flows.length,
    gaps,
    critical: { total: criticalTotal, covered: criticalCovered, gaps: criticalGaps },
    messages,
  };
}
