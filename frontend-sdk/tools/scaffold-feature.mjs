#!/usr/bin/env node
// Scaffold a feature unit (ViewModel + View + test + i18n) — the frontend parallel of the backend scaffold.
// Usage: node tools/scaffold-feature.mjs <plural-name> [targetDir]
//   node tools/scaffold-feature.mjs bookings sample/bookings
// The emitted unit is the blessed shape with names substituted; it passes the LZFE rules + typechecks by
// construction (the sample it is templated from does). Refine the entity fields, the copy, and the slice after.

import { mkdirSync, writeFileSync, existsSync } from "node:fs";
import { join } from "node:path";
import { renderFeature, pascal } from "./generate.mjs";

const [, , name, targetDir] = process.argv;
if (!name) {
  console.error("usage: node tools/scaffold-feature.mjs <plural-name> [targetDir]");
  process.exit(2);
}

const dir = targetDir ?? join("sample", name.toLowerCase());
const files = renderFeature(name);

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

console.log(`scaffolded ${pascal(name)} feature unit:`);
for (const p of written) console.log(`  ${p}`);
console.log(`\nnext: refine the ${pascal(name)} entity fields, wire the slice in @/client.gen, fill the copy, run \`npm run check\`.`);
