#!/usr/bin/env node
// Scaffold the design token contract (docs/DESIGN-CONVENTIONS.md) — the frontend parallel of the backend's
// component standards: the taxonomy ships as code the app owns, never as a package it versions against.
// Usage: node tools/design-scaffold.mjs [targetDir]
//   node tools/design-scaffold.mjs core/src/design
// The emitted tokens.ts is the taxonomy with starting values; edit the VALUES (brand, dark mode), never the
// names — the design band (LZFE024-026 + LZFE012) polices the names. Map your styling mechanism (Tailwind
// theme, NativeWind, CSS vars) FROM this file, by hand, once.

import { mkdirSync, writeFileSync, existsSync } from "node:fs";
import { join } from "node:path";
import { renderDesign } from "./generate.mjs";

const [, , targetDir] = process.argv;
const dir = targetDir ?? join("core", "src", "design");
const files = renderDesign();

mkdirSync(dir, { recursive: true });
const written = [];
for (const [filename, contents] of Object.entries(files)) {
  const path = join(dir, filename);
  if (existsSync(path)) {
    console.error(`refusing to overwrite ${path}`);
    process.exit(1);
  }
  writeFileSync(path, contents);
  written.push(path);
}

console.log("scaffolded the design token contract:");
for (const p of written) console.log(`  ${p}`);
console.log(
  "\nnext: replace the starting values with your brand (names stay), map your styling mechanism from tokens.ts, read docs/DESIGN-CONVENTIONS.md.",
);
