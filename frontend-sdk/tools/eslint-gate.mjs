#!/usr/bin/env node

import { createRequire } from "node:module";
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);

/** Build a closed ESLint invocation that cannot lose the plugin or an AFFE rule through consumer configuration. */
export function eslintGateArguments(sourcePaths, ruleNames) {
  const arguments_ = [
    ...sourcePaths,
    "--max-warnings=0",
    "--no-inline-config",
    "--plugin",
    "aerofortress",
  ];
  for (const rule of [...ruleNames].sort()) {
    arguments_.push("--rule", `aerofortress/${rule}:warn`);
  }
  return arguments_;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const sourcePaths = process.argv.slice(2);
  if (sourcePaths.length === 0) {
    console.error("usage: affe-eslint-gate <source-path> [more-source-paths...]");
    process.exit(2);
  }

  let plugin;
  try {
    plugin = require("eslint-plugin-aerofortress");
  } catch {
    console.error(
      "AFFE release gate: eslint-plugin-aerofortress is unavailable; install the framework-pinned frontend packages.",
    );
    process.exit(1);
  }

  let eslint;
  try {
    eslint = join(dirname(require.resolve("eslint/package.json")), "bin", "eslint.js");
  } catch {
    console.error("AFFE release gate: ESLint is unavailable; install the framework-pinned frontend packages.");
    process.exit(1);
  }
  const result = spawnSync(
    process.execPath,
    [eslint, ...eslintGateArguments(sourcePaths, Object.keys(plugin.rules ?? {}))],
    { cwd: process.cwd(), stdio: "inherit" },
  );
  if (result.error) {
    console.error(`AFFE release gate: could not start ESLint (${result.error.message}).`);
    process.exit(1);
  }
  process.exit(result.status ?? 1);
}
