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
| `test-colocated` | LZFE005 | Every `*.viewModel.ts` has a co-located `*.test.tsx` that `renderHook()`s it (proof the wiring mounts). |
| `no-mock` | LZFE003 | No mock/fixture/MSW import in production code (only under `*.test.*`). |

## Self-proving

`npm test` runs `index.test.cjs` — RuleTester cases that pin every rule on **both** edges: it must FIRE on the
violation it polices and PASS on the shapes it allows. A rule isn't done until both are pinned (the discipline the
framework applies to its own backend rules). CI runs this beside `dotnet build` and the spine's vitest suite.

## Consuming it in an app

An app registers the plugin in its flat ESLint config and turns the rules on for its feature tree — see the
`frontend/eslint.config.mjs` in this repo, which lints the canonical `sample/`. Until this package is published to
npm, a consuming app (e.g. the Hostpoint dogfood) installs it from this repo; once published it is a normal devDep.
