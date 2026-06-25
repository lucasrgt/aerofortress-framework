#!/usr/bin/env node
// Release guard — the enforcement half of "a published version is immutable". Run at publish time (tag push),
// it fails when a publishable unit's shipped content changed since the last release tag without its version
// moving: the "0.10.0 published != 0.10.0 canonical" drift, where source slid under a number that was already
// out (the publish step then silently skips it, so the change never ships and local diverges from the registry
// forever). The fix is always a bump, never a re-skip. The decision is pure (`violations`); the CLI feeds it git.

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

// The publishable surface. A unit "changed" if any shipped path differs since the tag; `version` is the file
// whose number must move when that happens. The NuGet packages share one number (the library props); each npm
// package carries its own. SelfHarness is framework-dev-only and never shipped, so it is not a unit here.
export const RELEASE_UNITS = Object.freeze([
  {
    name: "AeroFortress.Framework.* (nuget)",
    version: "build/AeroFortress.Framework.Library.props",
    paths: ["src", "analyzers/AeroFortress.Framework.Doctor"],
  },
  {
    name: "@aerofortress/react",
    version: "frontend-sdk/packages/aerofortress-react/package.json",
    paths: ["frontend-sdk/packages/aerofortress-react/src", "frontend-sdk/packages/aerofortress-react/package.json"],
  },
  {
    name: "eslint-plugin-aerofortress",
    version: "frontend-sdk/packages/eslint-plugin/package.json",
    paths: ["frontend-sdk/packages/eslint-plugin/index.cjs"],
  },
  {
    name: "@aerofortress/frontend-sdk",
    version: "frontend-sdk/package.json",
    paths: ["frontend-sdk/tools"],
  },
]);

/**
 * The pure decision: a unit that changed since the last release without bumping its version is a violation.
 * @param {ReadonlyArray<{name: string, changed: boolean, versionBumped: boolean}>} units
 * @returns {string[]} one message per violation (empty array = clean to publish)
 */
export function violations(units) {
  return units
    .filter((unit) => unit.changed && !unit.versionBumped)
    .map((unit) =>
      `release-guard: ${unit.name} changed since the last release but its version did not move — bump it before `
      + "tagging. A published version is immutable; shipping new content under the same number is the "
      + "'0.x published != 0.x canonical' drift.",
    );
}

/** @param {string} content @param {string} versionPath */
function versionOf(content, versionPath) {
  if (versionPath.endsWith(".props")) return content.match(/<Version>([^<]+)<\/Version>/)?.[1] ?? "";
  try {
    return JSON.parse(content).version ?? "";
  } catch {
    return "";
  }
}

/** The file's content at a git ref, or "" when it did not exist there. @param {string} ref @param {string} path */
function fileAt(ref, path) {
  try {
    return execFileSync("git", ["show", `${ref}:${path}`], { encoding: "utf8" });
  } catch {
    return "";
  }
}

/** True when any of `paths` differs between `ref` and HEAD. @param {string} ref @param {string[]} paths */
function changedSince(ref, paths) {
  try {
    execFileSync("git", ["diff", "--quiet", ref, "HEAD", "--", ...paths]);
    return false; // exit 0 = no differences
  } catch {
    return true; // non-zero exit = differences
  }
}

const invokedDirectly = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (invokedDirectly) {
  // Compare against the most recent release tag reachable from HEAD's parent, so a freshly-pushed v* tag at HEAD
  // measures against the PREVIOUS release rather than itself.
  let base = process.argv[2];
  if (!base) {
    try {
      base = execFileSync("git", ["describe", "--tags", "--abbrev=0", "--match", "v*", "HEAD^"], { encoding: "utf8" }).trim();
    } catch {
      console.log("release-guard: no prior v* tag to compare against — first release, nothing to guard.");
      process.exit(0);
    }
  }

  const units = RELEASE_UNITS.map((unit) => ({
    name: unit.name,
    changed: changedSince(base, unit.paths),
    versionBumped: versionOf(fileAt(base, unit.version), unit.version) !== versionOf(readFileSync(unit.version, "utf8"), unit.version),
  }));

  const messages = violations(units);
  for (const message of messages) console.error(message);
  if (messages.length === 0) console.log(`release-guard: every changed publishable unit was bumped since ${base}.`);
  process.exit(messages.length ? 1 : 0);
}
