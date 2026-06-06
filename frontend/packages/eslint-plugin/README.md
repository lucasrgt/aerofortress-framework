# eslint-plugin-lazuli

The frontend harness — the front-side parallel of the backend's Roslyn analyzers (`Lazuli.Doctor`). It polices the
MVVM seam of a lazuli-net screen so React Native + web stays **wired, not mocked**. Doctor-removable: delete the
plugin and the app still builds; you only lose enforcement.

## Rules

| Rule | Code | Polices |
|---|---|---|
| `view-purity` | LZFE001 | A `*.view.tsx` renders only — no generated client / axios / react-query import (contract **types** are fine). |
| `data-door` | LZFE002 | The generated client has exactly two doors: a screen's `*.viewModel.ts`, and the auth/routing infra (`lib/session`, `lib/guards`). |
| `viewmodel-platform-agnostic` | LZFE009 | A `*.viewModel.ts` imports no `react-native` / `expo` (value **or** type) — the core stays shareable web↔mobile and testable in jsdom. |
| `test-colocated` | LZFE005 | Every `*.viewModel.ts` has a co-located `*.test.tsx` that `renderHook()`s it (unit tier — proof the data door mounts). |
| `view-integration-test` | LZFE006 | Every `*.view.tsx` has a co-located test that `render()`s the View (integration tier — proof the screen composes + mounts). |
| `no-mock` | LZFE003 | No mock/fixture/MSW import in production code (only under `*.test.*`). |
| `state-completeness` | LZFE010 | A `*.view.tsx` routes loading/error/empty through `<Resource>` — no raw `isPending`/`isError`/… (the booleans are the ViewModel's). |
| `i18n-completeness` | LZFE011 | Every locale catalog in a `*.i18n.ts` declares the same keys (a key in one but not its siblings is a silent untranslated string). |
| `design-tokens` | LZFE012 | No inline hex color in production code — colors come from a token; only the `theme`/`tokens`/`palette` definition files may hold hex. |
| `mutation-error-handled` | LZFE013 | A `*.viewModel.ts` mutation (`.mutate`/`.mutateAsync`) passes an `onError` — no silent failure (front-side of the backend's error_handling). |

## Self-proving

`npm test` runs `index.test.cjs` — RuleTester cases that pin every rule on **both** edges: it must FIRE on the
violation it polices and PASS on the shapes it allows. A rule isn't done until both are pinned (the discipline the
framework applies to its own backend rules). CI runs this beside `dotnet build` and the spine's vitest suite.

## Consuming it in an app

An app registers the plugin in its flat ESLint config and turns the rules on for its feature tree — see the
`frontend/eslint.config.mjs` in this repo, which lints the canonical `sample/`. Until this package is published to
npm, a consuming app (e.g. the Hostpoint dogfood) installs it from this repo; once published it is a normal devDep.
