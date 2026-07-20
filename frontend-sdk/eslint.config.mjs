import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import tsParser from "@typescript-eslint/parser";
import tsPlugin from "@typescript-eslint/eslint-plugin";
import tanstackQuery from "@tanstack/eslint-plugin-query";
import noSecrets from "eslint-plugin-no-secrets";
import sonarjs from "eslint-plugin-sonarjs";
import vitest from "@vitest/eslint-plugin";

// Lint config for the canonical example (examples/sample-app/frontend), executed from the repository root by
// `npm run lint`. The root location keeps the example inside ESLint's base path while dependencies remain owned
// by frontend-sdk; the rule self-tests run as a separate leg.
//
// Curated community kit alongside the AFFE rules (prior art:
// pleiades-os / corbanx both standardize on the same kit). Two of those compose cleanly with the AFFE rules:
//   - @tanstack/eslint-plugin-query — react-query correctness (exhaustive deps, stable keys, no rest-destructure);
//     the AFFE rules cover architecture, this covers RQ usage — complementary, not overlapping.
//   - eslint-plugin-no-secrets — entropy-based hardcoded-secret detection (the .env discipline, enforced in code).
// The AFFE plugin is CommonJS; load it via createRequire.
const require = createRequire(import.meta.url);
const aerofortress = require("./packages/eslint-plugin/index.cjs");
// Accessibility — web (DOM) uses jsx-a11y (alt / aria / href). The mobile RN counterpart (react-native-a11y)
// is dropped until it publishes an eslint-9 peer: its latest release still caps eslint at 8, and we keep the
// install ERESOLVE-clean rather than pin a legacy-peer-deps escape hatch. Restore the mobile block (and a
// toWarn helper) once a maintained eslint-9 RN-a11y plugin exists.
const jsxA11y = require("eslint-plugin-jsx-a11y");

export default [
  { ignores: ["**/node_modules/**", "packages/eslint-plugin/**"] },
  // react-query correctness (parser comes from the sample block below, which these merge onto).
  ...tanstackQuery.configs["flat/recommended"],
  {
    files: ["examples/sample-app/frontend/{core,web}/**/*.{ts,tsx}"],
    languageOptions: {
      parser: tsParser,
      ecmaVersion: 2022,
      sourceType: "module",
      // type-aware lint (projectService) — required by @typescript-eslint/no-floating-promises.
      parserOptions: { ecmaFeatures: { jsx: true }, projectService: true, tsconfigRootDir: fileURLToPath(new URL("..", import.meta.url)) },
    },
    plugins: { aerofortress, "no-secrets": noSecrets, sonarjs, "@typescript-eslint": tsPlugin },
    rules: {
      // promise safety (type-aware) — an unhandled promise is a silent failure; `void p` opts out explicitly.
      "@typescript-eslint/no-floating-promises": "error",
      "aerofortress/view-purity": "error",
      "aerofortress/data-door": "error",
      "aerofortress/viewmodel-platform-agnostic": "error",
      "aerofortress/test-colocated": "error",
      "aerofortress/view-integration-test": "error",
      "aerofortress/no-mock": "error",
      "aerofortress/state-completeness": "error",
      "aerofortress/i18n-completeness": "error",
      "aerofortress/design-tokens": "error",
      "aerofortress/mutation-error-handled": "error",
      "aerofortress/no-hardcoded-copy": "error",
      // The routing harness (AFFE015–019 + 030) — declarative redirects, one session seam, a tri-state guard,
      // guarded params + Back, and no cast on a navigation target (the typed-routes mute button). Error-tier
      // (correctness), router-agnostic (expo + TanStack). The default a generated app gets.
      "aerofortress/no-router-replace-in-effect": "error",
      "aerofortress/session-one-door": "error",
      "aerofortress/guard-tristate": "error",
      "aerofortress/route-param-guard": "error",
      "aerofortress/safe-back": "error",
      "aerofortress/no-cast-navigation": "error",
      // The form-validation pair (AFFE031–032) — every validation failure has a surface: the submit carries its
      // invalid path (the spine's submitOrReveal), and a <Controller> surfaces its field's error. Warn-tier on
      // entry (a single-screen form with all errors visible inline is legitimate); promote together once the
      // primitive absorbs the common case.
      "aerofortress/submit-handles-invalid": "warn",
      "aerofortress/controller-field-state": "warn",
      "aerofortress/no-hardcoded-base-url": "error",
      // The security pair (AFFE021–022) — the XSS door stays behind one sanitizing seam; a URL-supplied value
      // never becomes a navigation target without an allowlist. Error-tier (correctness, same bar as routing).
      "aerofortress/no-raw-html": "error",
      "aerofortress/no-open-redirect": "error",
      // The design band (AFFE024–026, DESIGN-CONVENTIONS.md) — views render @/ui only, spacing/typography from the
      // scale, color by semantic role. Error-tier since the canonical screens landed (the recipes prove the bar).
      "aerofortress/ui-door": "error",
      "aerofortress/scale-only": "error",
      "aerofortress/semantic-colors": "error",
      // The mutation band (AFFE027–028) — the QueryClient carries the write-side defaults (invalidate on success,
      // feedback on error), and the hand-rolled `onSuccess: refetch` ritual those defaults obsolete is revealed.
      "aerofortress/query-client-defaults": "error",
      "aerofortress/no-manual-refetch-ritual": "warn",
      // The session-rotation door (AFFE029) — refresh is consumed by ONE seam (the client's single-flight
      // interceptor / the session seam); a second rotation path trips the backend's theft detection.
      "aerofortress/refresh-one-door": "error",
      // The AVP bridge (AFFE033) — the front-side of the backend's AF0030 and the closing leg of Clockwork: a
      // `@verify <id>` obligation on a View/ViewModel must have a co-located executable Assay proof. Error-tier.
      "aerofortress/verify-has-avp-proof": "error",
      "aerofortress/no-disabled-tests": "error",
      // curated community kit (mirrors pleiades/corbanx)
      "no-secrets/no-secrets": ["error", { tolerance: 4.5 }],
      "sonarjs/no-identical-functions": "warn",
      "sonarjs/no-duplicated-branches": "warn",
      "sonarjs/cognitive-complexity": ["warn", 25],
    },
  },
  // a11y — web (DOM): jsx-a11y at ERROR tier — promoted with the canonical screens (the kit wires the floor:
  // label↔control, role=alert errors, aria-busy, the focus ring), so the exemplar tree holds the bar it preaches.
  // aria-role checks DOM elements only (ignoreNonDOM): the kit's `Text role=` is a TextRole (typography), not an
  // ARIA role — components map their props internally; the host-element half of the check stays on.
  {
    files: ["examples/sample-app/frontend/web/**/*.{ts,tsx}"],
    plugins: { "jsx-a11y": jsxA11y },
    rules: { ...jsxA11y.flatConfigs.recommended.rules, "jsx-a11y/aria-role": ["error", { ignoreNonDOM: true }] },
  },
  // test hygiene — no .only/.skip leaking into the suite (the @vitest recommended set).
  {
    files: ["examples/sample-app/frontend/{core,web}/**/*.test.{ts,tsx}"],
    plugins: { vitest },
    rules: { ...vitest.configs.recommended.rules },
  },
];
