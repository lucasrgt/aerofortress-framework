#!/usr/bin/env node
// LZFE011 (cross-package) — locale parity for *.i18n.ts catalogs, as a TOOL. The eslint rule
// (`aerofortress/i18n-completeness`) is the in-scope mechanism: when the catalogs sit inside the linted source, it pins
// parity per file at lint time. But in a core-split layout the catalogs live in a SEPARATE package, outside the
// app's eslint scope, so the rule can never see them — this tool does, by reading the files directly. Same
// invariant (every locale object in a catalog declares the same keys; a key in one but not its siblings is a
// silent untranslated string), enforced where the rule can't reach. Use the rule OR this tool, by layout.
// Usage: node tools/i18n-parity.mjs <catalogsDir> [...moreDirs]
//   node tools/i18n-parity.mjs ../examples/sample-app/frontend/core/src

import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

const dirs = process.argv.slice(2);
if (dirs.length === 0) {
  console.error("usage: node tools/i18n-parity.mjs <catalogsDir> [...moreDirs]");
  process.exit(2);
}

/** Recursively collect every *.i18n.ts under dir (matches assemble-i18n's discovery). */
function findCatalogs(dir) {
  const out = [];
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return out;
  }
  for (const entry of entries) {
    const p = join(dir, entry.name);
    if (entry.isDirectory()) out.push(...findCatalogs(p));
    else if (entry.name.endsWith(".i18n.ts")) out.push(p);
  }
  return out;
}

// Each exported locale object: `export const <name> = { ... } [as const];`. Catalogs are flat (no nesting), so a
// non-greedy match to the first line-starting `}` is the catalog close. Locale-agnostic by design — like the eslint
// rule, it compares whatever objects the file exports, not a fixed pt/es/en set.
const BLOCK = /export const (\w+)\s*=\s*(\{[\s\S]*?\n\}(?:\s+as\s+const)?)\s*;/g;
// A key is a quoted OR bare identifier immediately after `{` or `,` (the property boundary), then a colon. Anchoring
// to that boundary is what keeps a value — which always follows a `:` — from ever being mistaken for a key, so a
// value containing a colon (or packed several entries to a line) parses correctly.
const KEY = /[{,]\s*(?:"([^"]+)"|([A-Za-z_$][\w$]*))\s*:/g;

function catalogsIn(src) {
  const cats = [];
  for (const block of src.matchAll(BLOCK)) {
    const keys = new Set();
    for (const k of block[2].matchAll(KEY)) keys.add(k[1] ?? k[2]);
    cats.push({ name: block[1], keys });
  }
  return cats;
}

const files = dirs.flatMap(findCatalogs);
if (files.length === 0) {
  console.error(`no *.i18n.ts catalogs found under ${dirs.join(", ")}`);
  process.exit(1);
}

let failed = false;
let checked = 0;
for (const file of files) {
  const cats = catalogsIn(readFileSync(file, "utf8"));
  if (cats.length < 2) continue; // need >= 2 locales to compare
  checked++;
  const rel = file.replace(/\\/g, "/");
  const union = new Set();
  for (const c of cats) for (const k of c.keys) union.add(k);
  for (const c of cats) {
    const missing = [...union].filter((k) => !c.keys.has(k));
    if (missing.length) {
      failed = true;
      console.error(`LZFE011 ${rel}: locale \`${c.name}\` missing ${missing.length} key(s): ${missing.sort().join(", ")}`);
    }
  }
}

console.log(`LZFE011 i18n-parity: ${checked} multi-locale catalog(s) checked.`);
process.exit(failed ? 1 : 0);
