#!/usr/bin/env node
// AFFE — framework sync (the frontend half of the package-first law). A pilot carries a MIRROR of
// eslint-plugin-aerofortress (the AFFE rules) because the package isn't published yet; a mirror that drifts from the
// canonical either missed a rule wave or grew a local rule that never went upstream — exactly how framework
// code gets "lost in time" inside a pilot. This compares the mirror against the canonical byte-for-byte
// (line-ending-insensitive) and fails on drift. The backend half (package versions) lives in `af doctor`
// (the .NET CLI's FrameworkSync); pilots wire THIS into their lint chain via a thin delegating wrapper.
//
// Usage: node tools/framework-sync.mjs <app-root> <framework-repo>

import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

/** A line-ending-insensitive fingerprint (the mirror crosses repos with different git eol settings). */
export function fingerprint(text) {
  return createHash("sha256").update(text.replace(/\r\n/g, "\n")).digest("hex");
}

/**
 * Compare the pilot's plugin mirror against the framework canonical. Pure (no I/O).
 * @param {{ mirror: string|null, canonical: string|null }} input - file contents, null when absent
 * @returns {{ status: "ok"|"drifted"|"skipped", messages: string[] }}
 */
export function checkMirror({ mirror, canonical }) {
  if (mirror === null || canonical === null)
    return { status: "skipped", messages: ["framework-sync: mirror or canonical not found — skipping (normal on CI)"] };
  if (fingerprint(mirror) !== fingerprint(canonical))
    return {
      status: "drifted",
      messages: [
        "framework-sync: clients/eslint-plugin-aerofortress/index.cjs differs from the framework canonical — rebase the "
        + "mirror (copy index.cjs + index.test.cjs from frontend-sdk/packages/eslint-plugin, bump its package.json "
        + "version) and adopt any new rules in the app's eslint config. If the difference is a rule you wrote HERE, "
        + "it belongs in aerofortress-framework first (the package-first law) — upstream it, then rebase.",
      ],
    };
  return { status: "ok", messages: ["framework-sync: the eslint-plugin mirror matches the canonical."] };
}

// ── CLI tail (the only I/O) ─────────────────────────────────────────────────────────────────────────────────────
const invokedDirectly =
  process.argv[1] && import.meta.url.endsWith(process.argv[1].replace(/\\/g, "/").split("/").pop());
if (invokedDirectly) {
  const [, , appRoot, frameworkRepo] = process.argv;
  if (!appRoot || !frameworkRepo) {
    console.error("usage: node tools/framework-sync.mjs <app-root> <framework-repo>");
    process.exit(2);
  }
  const read = (p) => (existsSync(p) ? readFileSync(p, "utf8") : null);
  const result = checkMirror({
    mirror: read(join(appRoot, "clients", "eslint-plugin-aerofortress", "index.cjs")),
    canonical: read(join(frameworkRepo, "frontend-sdk", "packages", "eslint-plugin", "index.cjs")),
  });
  for (const m of result.messages) console.log(m);
  process.exit(result.status === "drifted" ? 1 : 0);
}
