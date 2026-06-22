// The e2e tier of the frontend doctor (canonical, runner-agnostic + target-aware). E2E is flow-level and
// expensive, so it is NOT enforced per component (that is the integration tier's job, AFFE006). It is enforced at
// the PROJECT level via a CURATED checklist: a project declares its journeys in `e2e/flows.json`, and the doctor
// proves every listed journey has an implementation file AND a runner for its target. Humans own WHICH journeys
// to curate; the doctor only proves the list is covered — "completeness via checklist".
//
// SINGLE TIER, by design: being in `flows.json` IS the bar — there are no priority sub-tiers (no `critical` flag).
// The hard teeth live on the BACKEND, where a slice marked `[Critical]` MUST have happy + sad journeys or the build
// fails (AF0008), and journeys exist ONLY for critical slices (AF0012). Criticality is decided ONCE, on the slice;
// the frontend list mirrors those journeys. So this doctor is the curated-coverage REVEAL: a listed journey with no
// spec yet is a gap (the e2e roadmap — declare now, implement over time), surfaced, not a second class of flow.
//
// The runner is the swappable slot (the "RSpec"): Playwright is the blessed default for WEB, Maestro/Detox for
// NATIVE. A flow declares its `target` (web|native) and a `spec` path to its implementation (a `.spec.ts` / `.yaml`).
// `checkE2e(root)` is pure (no process.exit) so a CLI, a test, or `af doctor` decides what to do with it.
//
// DEPTH, not just existence (AFFE-JOURNEY-002). A spec existing does NOT mean the journey is covered — a spec can
// stop at the door (assert the entry screen and return), punting the rest to the backend twin. That is the exact
// shape that let a pilot's onboarding ship a "complete -> back to step 0" bug under a green doctor. So a LINKED flow
// (one with a `backendJourney`) must also declare `terminal`: the marker — a testID or route — its spec asserts
// AFTER entry to prove the journey reached its end. The doctor reads the spec and flags it when the terminal is
// undeclared or never referenced. These are reported as `depthGaps` (warn-tier), kept SEPARATE from the hard
// existence `gaps`, so a consumer can warn while flows adopt `terminal` and promote to a hard gate once they have.
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
 * Inspect a project's e2e tier. Returns `{ bootstrap, runners, flows, gaps, depthGaps, messages }`:
 *   - bootstrap: true when e2e/flows.json doesn't exist yet (nothing to enforce — the pillar isn't set up)
 *   - runners: detected runners; flows: count of curated journeys; gaps: hard existence problems; messages: lines
 *   - depthGaps: warn-tier journey-depth findings (AFFE-JOURNEY-002) — a linked flow with no `terminal`, or a spec
 *     that never asserts its declared `terminal`. Separate from `gaps` so consumers can warn now, gate later.
 * Each flow: `{ name, area?, target?: "web"|"native", spec, backendJourney?, terminal? }` where spec is a path from
 * root to its impl file and `terminal` is the testID/route the spec must assert to prove the journey reached its end.
 * Target-aware: a target:native flow needs a native runner (.maestro/ or detox); a target:web flow needs Playwright.
 */
export function checkE2e(root) {
  const flowsFile = join(root, "e2e", "flows.json");
  if (!existsSync(flowsFile)) {
    return { bootstrap: true, runners: detectRunners(root), flows: 0, gaps: 0, depthGaps: 0, messages: ["no e2e/flows.json yet (bootstrap)"] };
  }

  const messages = [];
  let gaps = 0;
  let depthGaps = 0;
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
    return { bootstrap: false, runners, flows: 0, gaps: gaps + 1, depthGaps: 0, messages: [...messages, `flows.json invalid — ${e.message}`] };
  }

  for (const flow of flows) {
    const name = flow.name ?? "(unnamed)";
    const spec = String(flow.spec ?? "");
    const specPath = join(root, spec);
    if (!spec || !existsSync(specPath)) {
      messages.push(`curated journey "${name}" has no spec (${flow.spec ?? "missing"})`);
      gaps++;
      continue;
    }
    if (flow.target === "native" && !hasNative) {
      messages.push(`journey "${name}" is target:native but no native runner (.maestro/ or detox) is configured`);
      gaps++;
    } else if (flow.target === "web" && !hasWeb) {
      messages.push(`journey "${name}" is target:web but no web runner (playwright) is configured`);
      gaps++;
    }

    // Depth (AFFE-JOURNEY-002): a linked flow must declare its `terminal`, and any flow that declares one must
    // actually assert it in the spec — otherwise "covered" only proves the journey STARTS. Warn-tier (depthGaps),
    // so existence stays the hard gate while terminals are adopted. The assert-check is a string-presence heuristic:
    // it catches the door-stopper (a spec that never names its end marker) without parsing the runner's AST.
    const terminal = typeof flow.terminal === "string" ? flow.terminal.trim() : "";
    if (flow.backendJourney && !terminal) {
      messages.push(
        `linked journey "${name}" declares no \`terminal\` — add the marker (testID or route) its spec must assert ` +
          `after entry to prove the journey reaches its end; without it, "linked" only proves entry`,
      );
      depthGaps++;
    } else if (terminal && !readFileSync(specPath, "utf8").includes(terminal)) {
      messages.push(
        `journey "${name}" never asserts its terminal \`${terminal}\` in ${spec} — the spec may stop at entry ` +
          `instead of proving the journey reaches its end`,
      );
      depthGaps++;
    }
  }

  return { bootstrap: false, runners, flows: flows.length, gaps, depthGaps, messages };
}
