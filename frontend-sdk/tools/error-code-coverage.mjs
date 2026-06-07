#!/usr/bin/env node
// LZFE — error-code coverage (cross-package). The backend ships every error as a stable code (the registry consts
// behind LZ0018/LZ0019), enumerated into the OpenAPI `ErrorBody.code`; the front owns the copy. This proves every
// code has a catalog entry, so no error reaches a user untranslated. Pair with i18n-parity (LZFE011), which proves
// that entry exists in EVERY locale: coverage = code → copy, parity = copy → every language. Together they are the
// front end of the LZ0018/LZ0019 discipline.
//
// It reads the GENERATED client's ErrorBody union (orval output) directly — the cross-package analogue of an
// in-source rule — and is a NOTICE until the client is regenerated against the enum-bearing contract, a hard gate
// after (so it never blocks before the codegen has run).
// Usage: node tools/error-code-coverage.mjs <catalog.i18n.ts> <errorBody.ts>
//   node tools/error-code-coverage.mjs ../app-core/src/i18n/api-errors.i18n.ts ../app-core/src/client.gen/model/errorBody.ts

import { readFileSync } from "node:fs";

const [, , catalogFile, errorBodyFile] = process.argv;
if (!catalogFile || !errorBodyFile) {
  console.error("usage: node tools/error-code-coverage.mjs <catalog.i18n.ts> <errorBody.ts>");
  process.exit(2);
}

// Keys across all exported catalog objects (locale-agnostic; LZFE011 guarantees the locales agree, so the union is
// the catalog's key set). Same flat-catalog parsing as i18n-parity: a key is a quoted/bare token after `{` or `,`.
const BLOCK = /export const \w+\s*=\s*(\{[\s\S]*?\n\}(?:\s+as\s+const)?)\s*;/g;
const KEY = /[{,]\s*(?:"([^"]+)"|([A-Za-z_$][\w$]*))\s*:/g;
function catalogKeys(src) {
  const keys = new Set();
  for (const block of src.matchAll(BLOCK)) for (const k of block[1].matchAll(KEY)) keys.add(k[1] ?? k[2]);
  return keys;
}

// The string-literal members of the generated `code: 'a' | 'b' | ...;` union, or null when the client hasn't been
// regenerated against the enum yet (no file, or `code` is still a plain string).
function unionCodes(src) {
  const field = src.match(/\bcode\??:\s*([^;]+);/);
  if (!field) return null;
  const codes = [...field[1].matchAll(/'([^']+)'/g)].map((m) => m[1]);
  return codes.length ? new Set(codes) : null;
}

const catalog = catalogKeys(readFileSync(catalogFile, "utf8"));
let union;
try {
  union = unionCodes(readFileSync(errorBodyFile, "utf8"));
} catch {
  union = null;
}

if (union === null) {
  console.log(`LZFE error-codes: ${catalog.size} code(s) in the catalog.`);
  console.log("  note: regenerate the client against the enum-bearing contract to check ErrorBody.code coverage.");
  process.exit(0);
}

const uncovered = [...union].filter((c) => !catalog.has(c));
const orphan = [...catalog].filter((k) => !union.has(k));
console.log(`LZFE error-codes: ${union.size} code(s) in the contract, ${catalog.size} in the catalog.`);
if (orphan.length) console.log(`  ${orphan.length} catalog key(s) not in the contract (stale?): ${orphan.sort().join(", ")}`);
if (uncovered.length) {
  console.error(`  ${uncovered.length} code(s) with no translation: ${uncovered.sort().join(", ")}`);
  process.exit(1);
}
process.exit(0);
