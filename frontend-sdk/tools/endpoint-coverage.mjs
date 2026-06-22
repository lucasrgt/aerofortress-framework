// AFFE008 — back->front endpoint coverage. Every app-facing generated hook (`use<Slice>`) should be consumed by at
// least one ViewModel; one with no consumer is a "loose endpoint" — the backend slice exists but no screen wired it.
// This is a WARNING, never a build failure: the front->back direction (a UI calling an endpoint that does not exist)
// is the hard gate and it is free from `tsc` (the hook isn't generated, so it can't compile). back->front only
// reveals "backend done, UI not wired." Non-app endpoints never reach the client (orval's audience filter drops
// them), so the metric is high-signal by construction. See docs/FRONTEND-CONVENTIONS.md (AFFE008).
//
// The core (`extractHooks` + `checkEndpointCoverage`) is pure (no I/O) so it is unit-testable; the CLI tail wires it
// to the filesystem. Same split as journey-parity.mjs.
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

/** App-facing generated hook names (`use<Name>`) declared in the generated client source. */
export function extractHooks(clientText) {
  return [...clientText.matchAll(/export\s+(?:function|const)\s+(use[A-Z]\w*)/g)].map((m) => m[1]);
}

/**
 * Whether a file is a legal data door whose hook references count as "wired": a screen's `*.viewModel.ts`, or
 * the auth/routing infra seams (`lib/session*`, `lib/guards*`) that AFFE002 already blesses as client consumers.
 * Without the doors, the session hooks (`useMe`/`useRefresh`/`useLogout`) report loose from day one — wallpaper
 * that buries the real gaps the metric exists to reveal.
 */
export function isDataDoor(filePath) {
  const p = filePath.replace(/\\/g, "/");
  return p.endsWith(".viewModel.ts") || /(^|\/)lib\/(session|guards)($|[./])/.test(p);
}

/**
 * Back->front coverage of the generated hooks against the ViewModel layer (the only legal data door).
 * @param {string[]} hooks - hook names (from extractHooks)
 * @param {string} wiredText - concatenated source of every *.viewModel.ts
 * @returns {{ total: number, wired: number, loose: string[], messages: string[] }}
 */
export function checkEndpointCoverage(hooks, wiredText) {
  const unique = [...new Set(hooks)];
  const loose = unique.filter((h) => !new RegExp(`\\b${h}\\b`).test(wiredText)).sort();
  const messages = loose.length
    ? [`${loose.length} loose endpoint(s) — a backend slice with no screen wired yet (warning):`, ...loose.map((h) => `  - ${h}`)]
    : [];
  return { total: unique.length, wired: unique.length - loose.length, loose, messages };
}

/** Recursively collect files under `dir` matching `pred` (missing dir -> []). */
function walk(dir, pred) {
  const out = [];
  let entries;
  try {
    entries = readdirSync(dir);
  } catch {
    return out;
  }
  for (const e of entries) {
    const p = join(dir, e);
    if (statSync(p).isDirectory()) out.push(...walk(p, pred));
    else if (pred(p)) out.push(p);
  }
  return out;
}

// CLI: node tools/endpoint-coverage.mjs <client.gen file> <srcDir>
// Pass the source ROOT (not only features/): the scan collects every data door under it — the ViewModels plus
// the lib/session|guards infra seams — so legally-consumed session hooks never pollute the loose list.
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const [, , clientFile, srcDir] = process.argv;
  if (!clientFile || !srcDir) {
    console.error("usage: node tools/endpoint-coverage.mjs <client.gen file> <srcDir>");
    process.exit(2);
  }
  if (!existsSync(clientFile)) {
    console.log(`AFFE008 endpoint coverage: no generated client at ${clientFile} (bootstrap) — nothing to check yet.`);
    process.exit(0);
  }
  const hooks = extractHooks(readFileSync(clientFile, "utf8"));
  const wiredText = walk(srcDir, isDataDoor)
    .map((p) => readFileSync(p, "utf8"))
    .join("\n");
  const r = checkEndpointCoverage(hooks, wiredText);
  console.log(`AFFE008 endpoint coverage: ${r.wired}/${r.total} app-facing hooks wired by a ViewModel.`);
  for (const m of r.messages) console.log(`  ${m}`);
  process.exit(0); // warning-only — front->back (tsc) is the hard gate
}
