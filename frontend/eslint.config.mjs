import { createRequire } from "node:module";
import tsParser from "@typescript-eslint/parser";
import tsPlugin from "@typescript-eslint/eslint-plugin";
import tanstackQuery from "@tanstack/eslint-plugin-query";
import noSecrets from "eslint-plugin-no-secrets";
import sonarjs from "eslint-plugin-sonarjs";
import vitest from "@vitest/eslint-plugin";

// Lint config for the canonical example (examples/sample-app/frontend). The example now lives at the repo root
// (a sibling of frontend/), so type-aware linting of it runs once lazuli-net adopts a root npm workspace (shared
// node_modules + a project that spans both) — the same monorepo shape the example itself demonstrates. The LZFE
// rules below are the contract it follows; `npm run lint` verifies the rules via the plugin self-test today.
//
// Curated community kit alongside the LZFE rules (prior art:
// pleiades-os / corbanx both standardize on the same kit). Two of those compose cleanly with the LZFE rules:
//   - @tanstack/eslint-plugin-query — react-query correctness (exhaustive deps, stable keys, no rest-destructure);
//     the LZFE rules cover architecture, this covers RQ usage — complementary, not overlapping.
//   - eslint-plugin-no-secrets — entropy-based hardcoded-secret detection (the .env discipline, enforced in code).
// The LZFE plugin is CommonJS; load it via createRequire.
const require = createRequire(import.meta.url);
const lazuli = require("./packages/eslint-plugin/index.cjs");

export default [
  { ignores: ["**/node_modules/**", "packages/eslint-plugin/**"] },
  // react-query correctness (parser comes from the sample block below, which these merge onto).
  ...tanstackQuery.configs["flat/recommended"],
  {
    files: ["../examples/sample-app/frontend/{core,web}/**/*.{ts,tsx}"],
    languageOptions: {
      parser: tsParser,
      ecmaVersion: 2022,
      sourceType: "module",
      // type-aware lint (projectService) — required by @typescript-eslint/no-floating-promises.
      parserOptions: { ecmaFeatures: { jsx: true }, projectService: true, tsconfigRootDir: import.meta.dirname },
    },
    plugins: { lazuli, "no-secrets": noSecrets, sonarjs, "@typescript-eslint": tsPlugin },
    rules: {
      // promise safety (type-aware) — an unhandled promise is a silent failure; `void p` opts out explicitly.
      "@typescript-eslint/no-floating-promises": "error",
      "lazuli/view-purity": "error",
      "lazuli/data-door": "error",
      "lazuli/viewmodel-platform-agnostic": "error",
      "lazuli/test-colocated": "error",
      "lazuli/view-integration-test": "error",
      "lazuli/no-mock": "error",
      "lazuli/state-completeness": "error",
      "lazuli/i18n-completeness": "error",
      "lazuli/design-tokens": "error",
      "lazuli/mutation-error-handled": "error",
      "lazuli/no-hardcoded-copy": "error",
      // curated community kit (mirrors pleiades/corbanx)
      "no-secrets/no-secrets": ["error", { tolerance: 4.5 }],
      "sonarjs/no-identical-functions": "warn",
      "sonarjs/no-duplicated-branches": "warn",
      "sonarjs/cognitive-complexity": ["warn", 25],
    },
  },
  // test hygiene — no .only/.skip leaking into the suite (the @vitest recommended set).
  {
    files: ["../examples/sample-app/frontend/{core,web}/**/*.test.{ts,tsx}"],
    plugins: { vitest },
    rules: { ...vitest.configs.recommended.rules },
  },
];
