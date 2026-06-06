import { createRequire } from "node:module";
import tsParser from "@typescript-eslint/parser";
import tanstackQuery from "@tanstack/eslint-plugin-query";
import noSecrets from "eslint-plugin-no-secrets";

// Lint the canonical sample with the framework's harness PLUS a curated slice of community plugins (prior art:
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
    files: ["sample/**/*.{ts,tsx}"],
    languageOptions: {
      parser: tsParser,
      ecmaVersion: 2022,
      sourceType: "module",
      parserOptions: { ecmaFeatures: { jsx: true } },
    },
    plugins: { lazuli, "no-secrets": noSecrets },
    rules: {
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
    },
  },
];
