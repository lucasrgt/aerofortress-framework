import { createRequire } from "node:module";
import tsParser from "@typescript-eslint/parser";

// Lint the canonical sample with the framework's own harness — dogfooding eslint-plugin-lazuli on the blessed
// reference. If the sample ever drifts from the conventions the plugin enforces, this fails (alongside the plugin's
// own RuleTester self-tests). The plugin is CommonJS; load it via createRequire.
const require = createRequire(import.meta.url);
const lazuli = require("./packages/eslint-plugin/index.cjs");

export default [
  { ignores: ["**/node_modules/**", "packages/eslint-plugin/**"] },
  {
    files: ["sample/**/*.{ts,tsx}"],
    languageOptions: {
      parser: tsParser,
      ecmaVersion: 2022,
      sourceType: "module",
      parserOptions: { ecmaFeatures: { jsx: true } },
    },
    plugins: { lazuli },
    rules: {
      "lazuli/view-purity": "error",
      "lazuli/data-door": "error",
      "lazuli/viewmodel-platform-agnostic": "error",
      "lazuli/test-colocated": "error",
      "lazuli/no-mock": "error",
      "lazuli/state-completeness": "error",
      "lazuli/i18n-completeness": "error",
      "lazuli/design-tokens": "error",
    },
  },
];
