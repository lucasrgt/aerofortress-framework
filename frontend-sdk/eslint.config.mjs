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
// Accessibility — the ecosystem-specific half of the harness, mirrored across targets: web uses jsx-a11y
// (alt / aria / href on the DOM), mobile uses react-native-a11y (accessibilityRole / accessible / label on RN
// primitives). Same intent, no shared parity, so each ecosystem gets its own block below. Warn-first — a revealed
// backlog promoted to error per-rule once cleared. (rn-a11y caps its eslint peer at 8 but runs clean on 9.)
const jsxA11y = require("eslint-plugin-jsx-a11y");
const rnA11y = require("eslint-plugin-react-native-a11y");
const toWarn = (rules) =>
  Object.fromEntries(Object.entries(rules).map(([id, val]) => [id, Array.isArray(val) ? ["warn", ...val.slice(1)] : "warn"]));

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
      // The routing harness (LZFE015–019) — declarative redirects, one session seam, a tri-state guard, guarded
      // params + Back. Error-tier (correctness), router-agnostic (expo + TanStack). The default a generated app gets.
      "lazuli/no-router-replace-in-effect": "error",
      "lazuli/session-one-door": "error",
      "lazuli/guard-tristate": "error",
      "lazuli/route-param-guard": "error",
      "lazuli/safe-back": "error",
      "lazuli/no-hardcoded-base-url": "error",
      // The security pair (LZFE021–022) — the XSS door stays behind one sanitizing seam; a URL-supplied value
      // never becomes a navigation target without an allowlist. Error-tier (correctness, same bar as routing).
      "lazuli/no-raw-html": "error",
      "lazuli/no-open-redirect": "error",
      // The design band (LZFE024–026, DESIGN-CONVENTIONS.md) — views render @/ui only, spacing/typography from the
      // scale, color by semantic role. Warn-first (the house posture); the canonical-screens stage promotes to error.
      "lazuli/ui-door": "warn",
      "lazuli/scale-only": "warn",
      "lazuli/semantic-colors": "warn",
      // curated community kit (mirrors pleiades/corbanx)
      "no-secrets/no-secrets": ["error", { tolerance: 4.5 }],
      "sonarjs/no-identical-functions": "warn",
      "sonarjs/no-duplicated-branches": "warn",
      "sonarjs/cognitive-complexity": ["warn", 25],
    },
  },
  // a11y — web (DOM): jsx-a11y. Inherits the type-aware parse setup from the {core,web} block above.
  {
    files: ["../examples/sample-app/frontend/web/**/*.{ts,tsx}"],
    plugins: { "jsx-a11y": jsxA11y },
    rules: toWarn(jsxA11y.flatConfigs.recommended.rules),
  },
  // a11y — mobile (RN): react-native-a11y. The {core,web} block doesn't cover mobile, so this carries its own
  // (type-free) parse setup; a11y rules are AST-based and need no type info. has-accessibility-hint is off
  // (supplementary, not required — keeps the backlog high-signal), matching the hostpoint dogfood.
  {
    files: ["../examples/sample-app/frontend/mobile/**/*.{ts,tsx}"],
    languageOptions: { parser: tsParser, ecmaVersion: 2022, sourceType: "module", parserOptions: { ecmaFeatures: { jsx: true } } },
    plugins: { "react-native-a11y": rnA11y },
    rules: { ...toWarn(rnA11y.configs.all.rules), "react-native-a11y/has-accessibility-hint": "off" },
  },
  // test hygiene — no .only/.skip leaking into the suite (the @vitest recommended set).
  {
    files: ["../examples/sample-app/frontend/{core,web}/**/*.test.{ts,tsx}"],
    plugins: { vitest },
    rules: { ...vitest.configs.recommended.rules },
  },
];
