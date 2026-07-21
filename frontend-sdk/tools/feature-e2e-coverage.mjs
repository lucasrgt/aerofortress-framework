#!/usr/bin/env node
// AFFE-FEATURE-E2E — visible feature -> executable journey coverage.
//
// e2e-doctor proves every flow declared by a surface has an executable spec. This tool proves the inverse:
// every ViewModel (the convention's user-visible feature boundary) declares `@e2e <flow-id>`, and every id
// resolves to a flow owned by one of the product's executable surfaces. Shared core ViewModels are checked
// against the union of all surface manifests, so a core never pretends to own a browser while still owing a
// real consumer journey.
//
// Complete depth is universal: every ViewModel owns linked happy and sad flows, and each flow names the exact
// feature(s) it proves. A backend slice with no frontend consumer remains backend-only; once its hook enters a
// ViewModel or infrastructure data door, the real surface journeys must name it.
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { basename, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const IGNORED_DIRECTORIES = new Set([
  ".git",
  ".aerofortress",
  "bin",
  "node_modules",
  "obj",
  "output",
  "test-results",
  "tmp",
]);

/** Stable flow ids declared by a ViewModel through `@e2e <id>`. */
export function extractE2eObligations(source) {
  return [...new Set(
    [...source.matchAll(/@e2e\s+([a-z0-9][a-z0-9._-]*)\b/gi)].map((match) => match[1]),
  )];
}

function sliceDeclarations(sources) {
  const declarations = [];
  for (const source of sources) {
    const classes = source.matchAll(
      /((?:^\s*\[[^\]\r\n]+\]\s*(?:\/\/[^\r\n]*)?\r?\n)+)\s*public\s+static\s+class\s+([A-Za-z_]\w*)/gm,
    );
    for (const match of classes) {
      const attributes = match[1];
      if (!/\bSlice(?:Attribute)?\b/.test(attributes)) continue;
      declarations.push({ name: match[2] });
    }
  }
  return declarations;
}

/** Every backend slice class name from ordinary C# source files. */
export function extractSlices(sources) {
  return [...new Set(sliceDeclarations(sources).map(({ name }) => name))].sort();
}

/** Backend hooks consumed directly by one frontend data door. */
export function sliceHooks(source, slices) {
  const imports = new Set();
  const declarations = source.matchAll(
    /\bimport\s+(?!type\b)\{([^}]*)\}\s+from\s+["'][^"']*client\.gen(?:\/[^"']*)?["']/g,
  );
  for (const declaration of declarations) {
    for (const raw of declaration[1].split(",")) {
      const specifier = raw.trim();
      if (!specifier || specifier.startsWith("type ")) continue;
      imports.add(specifier.split(/\s+as\s+/)[0].trim());
    }
  }
  return slices.filter((slice) => {
    const operation = slice.charAt(0).toLowerCase() + slice.slice(1);
    return imports.has(`use${slice}`) || imports.has(operation);
  }).sort();
}

/**
 * Check the complete feature -> flow mapping.
 * @param {{ path: string, source: string }[]} viewModels
 * @param {{ id?: string, path?: string, features?: string[], backendSlices?: string[], __surface?: string }[]} flows
 * @param {string[]} slices
 * @param {{ path: string, source: string }[]} infrastructure
 */
export function checkFeatureE2e(viewModels, flows, slices, infrastructure = []) {
  const messages = [];
  let gaps = 0;
  const byId = new Map();
  const featureNames = viewModels.map((viewModel) => basename(viewModel.path).replace(/\.viewModel\.tsx?$/i, ""));
  for (const duplicate of featureNames.filter((feature, index) => featureNames.indexOf(feature) !== index)) {
    messages.push(`ViewModel feature id "${duplicate}" is duplicated; filenames must identify one proof subject`);
    gaps += 1;
  }

  for (const flow of flows) {
    const id = typeof flow?.id === "string" ? flow.id.trim() : "";
    if (!id) continue; // e2e-doctor owns malformed flow shape; unresolved obligations still fail below.
    if (byId.has(id)) {
      messages.push(`flow id "${id}" is duplicated across frontend surfaces`);
      gaps += 1;
      continue;
    }
    byId.set(id, flow);
    if (flow.features !== undefined && (!Array.isArray(flow.features)
      || flow.features.length !== 1
      || flow.features.some((feature) => typeof feature !== "string" || !feature.trim()))) {
      messages.push(`flow id "${id}" must own exactly one ViewModel through features:["FeatureName"]`);
      gaps += 1;
    }
    for (const feature of flow.features ?? []) {
      if (featureNames.includes(feature)) continue;
      messages.push(`flow id "${id}" links unknown ViewModel feature "${feature}"`);
      gaps += 1;
    }
    for (const slice of flow.backendSlices ?? []) {
      if (slices.includes(slice)) continue;
      messages.push(`flow id "${id}" links unknown backend slice "${slice}"`);
      gaps += 1;
    }
  }

  let linked = 0;
  let complete = 0;
  for (const viewModel of viewModels) {
    const feature = basename(viewModel.path).replace(/\.viewModel\.tsx?$/i, "");
    const obligations = extractE2eObligations(viewModel.source);
    if (obligations.length === 0) {
      messages.push(`${viewModel.path}: ${feature} declares no \`@e2e <flow-id>\` browser/device journey`);
      gaps += 1;
      continue;
    }

    const resolvedFlows = [];
    for (const id of obligations) {
      const flow = byId.get(id);
      if (!flow) {
        messages.push(`${viewModel.path}: @e2e ${id} resolves to no surface e2e/flows.json entry`);
        gaps += 1;
      } else {
        resolvedFlows.push(flow);
      }
    }
    if (resolvedFlows.length === 0) continue;
    const subjectFlows = resolvedFlows.filter((flow) => flow.features?.includes(feature));
    for (const flow of resolvedFlows) {
      if (flow.features?.includes(feature)) continue;
      messages.push(`${viewModel.path}: linked flow "${flow.id}" does not declare features:["${feature}"]`);
      gaps += 1;
    }
    if (subjectFlows.length > 0) linked += 1;

    const paths = new Set(subjectFlows.map((flow) => flow.path));
    for (const required of ["happy", "sad"]) {
      if (paths.has(required)) continue;
      messages.push(`${viewModel.path}: complete verification requires a subject-bound @e2e flow with path:${required}`);
      gaps += 1;
    }
    if (paths.has("happy") && paths.has("sad")) complete += 1;

    const hooks = sliceHooks(viewModel.source, slices);
    for (const hook of hooks) {
      if (subjectFlows.some((flow) => flow.backendSlices?.includes(hook))) continue;
      messages.push(
        `${viewModel.path}: use${hook} is UI-consumed but none of its linked @e2e flows declares `
          + `backendSlices:["${hook}"]`,
      );
      gaps += 1;
    }

  }

  for (const dataDoor of infrastructure) {
    for (const hook of sliceHooks(dataDoor.source, slices)) {
      const proofs = flows.filter((flow) => flow.backendSlices?.includes(hook));
      if (proofs.length === 0) {
        messages.push(`${dataDoor.path}: use${hook} is UI infrastructure with no E2E flow declaring that backend slice`);
        gaps += 1;
        continue;
      }
      const paths = new Set(proofs.map((flow) => flow.path));
      for (const required of ["happy", "sad"]) {
        if (paths.has(required)) continue;
        messages.push(`${dataDoor.path}: infrastructure use${hook} requires an E2E flow with path:${required}`);
        gaps += 1;
      }
    }
  }

  return { features: viewModels.length, linked, complete, gaps, messages };
}

function walk(root, predicate) {
  const files = [];
  let entries;
  try {
    entries = readdirSync(root, { withFileTypes: true });
  } catch {
    return files;
  }
  for (const entry of entries) {
    if (entry.isDirectory() && IGNORED_DIRECTORIES.has(entry.name)) continue;
    const path = join(root, entry.name);
    if (entry.isDirectory()) files.push(...walk(path, predicate));
    else if (entry.isFile() && predicate(path)) files.push(path);
  }
  return files;
}

function readFlows(surface) {
  const path = join(surface, "e2e", "flows.json");
  if (!existsSync(path) || !statSync(path).isFile()) return [];
  const parsed = JSON.parse(readFileSync(path, "utf8"));
  if (!Array.isArray(parsed)) throw new Error(`${path}: flows manifest must be an array`);
  return parsed.map((flow) => ({ ...flow, __surface: surface }));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const [, , workspaceArgument, ...packageArguments] = process.argv;
  if (!workspaceArgument || packageArguments.length === 0) {
    console.error("usage: affe-feature-e2e <workspace-root> <core=path|surface=path> [...more-packages]");
    process.exit(2);
  }

  try {
    const workspace = resolve(workspaceArgument);
    const packages = packageArguments.map((argument) => {
      const separator = argument.indexOf("=");
      if (separator < 1) throw new Error(`invalid package argument: ${argument}`);
      const role = argument.slice(0, separator);
      if (role !== "core" && role !== "surface") throw new Error(`unknown frontend role: ${role}`);
      return { role, path: resolve(argument.slice(separator + 1)) };
    });
    const frontendFiles = packages.flatMap(({ path }) =>
      walk(join(path, "src"), (file) => /\.[cm]?[jt]sx?$/i.test(file)
        && !/[\\/]client\.gen[\\/]/i.test(file)));
    const viewModels = frontendFiles
      .filter((file) => /\.viewModel\.tsx?$/i.test(file))
      .map((file) => ({ path: file, source: readFileSync(file, "utf8") }));
    const infrastructure = frontendFiles
      .filter((file) => !/\.viewModel\.tsx?$/i.test(file) && !/\.(?:test|spec)\.[cm]?[jt]sx?$/i.test(file))
      .map((file) => ({ path: file, source: readFileSync(file, "utf8") }));
    const backendSources = walk(workspace, (file) => file.endsWith(".cs"))
      .map((file) => readFileSync(file, "utf8"));
    const slices = extractSlices(backendSources);
    const flows = packages
      .filter(({ role }) => role === "surface")
      .flatMap(({ path }) => readFlows(path));
    const result = checkFeatureE2e(viewModels, flows, slices, infrastructure);
    console.log(
      `AFFE-FEATURE-E2E: ${result.linked}/${result.features} ViewModel feature(s) linked; `
        + `${result.complete} complete happy+sad feature(s); ${result.gaps} coverage gap(s).`,
    );
    for (const message of result.messages) console.log(`  - ${message}`);
    process.exit(result.gaps > 0 ? 1 : 0);
  } catch (error) {
    console.log(`AFFE-FEATURE-E2E: invalid coverage input — ${error.message}`);
    process.exit(1);
  }
}
