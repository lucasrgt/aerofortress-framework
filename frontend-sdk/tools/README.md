# tools — frontend generators + doctors

Beyond the typed client (orval generates that from the backend contracts), these emit the *structure* a feature
needs — the frontend parallel of the backend scaffold — and check the parts a per-file lint rule can't see. Pure
functions (no I/O, unit-tested); the `*.mjs` CLIs wrap them with file writes / reports. The `lazuli` .NET CLI
front-door shells out to these (the way `lazuli doctor` shells out to `npm run lint`) — two engines, one front door.

## The test tiers (how each is enforced)

| Tier | Enforced by | Where |
|---|---|---|
| Unit | `LZFE005` (every ViewModel has a co-located `renderHook` test) | eslint, per-file |
| Integration | `LZFE006` (every View has a co-located `render()` test) | eslint, per-file |
| **E2E** | **`e2e-doctor.mjs`** (a curated `e2e/flows.json` + every listed flow has a spec + a runner) | this dir, **per-project** |

E2E is flow-level and expensive, so it is **not** enforced per component — `checkE2e(root)` enforces a *curated
checklist*: humans declare the critical flows in `e2e/flows.json`, the doctor proves each has a spec. See the
Hostpoint dogfood's `scripts/lzfe-e2e-doctor.mjs` (a thin CLI over `checkE2e`) + `playwright.config.ts`.

## The fullstack loop (journey parity)

`journey-parity.mjs` (`checkJourneyParity`) closes the loop at the **journey grain**: the backend declares critical
journeys (its `Journeys/*.Tests.cs`), the frontend declares them in `flows.json` (linked explicitly via a
`backendJourney` field) — and the doctor proves the two sets agree (no backend journey uncovered on the front, no
front flow pointing at a journey the back lacks). The *endpoint* grain is already closed: `tsc` for front→back (you
can't call a missing endpoint) and **LZFE008** for back→front (every endpoint is consumed by a ViewModel). So:
endpoints by type + coverage, journeys by parity — the same critical path proven on both sides.

## One report — the unified doctor

`doctor.mjs` (`aggregateReport`) is the single front-door that captures the **whole crew** in one pass: `eslint
--format json` (every rule — the LZFE architecture rules, the community kit, expo's set — bucketed) plus the three
script-doctors above (endpoint coverage, e2e, journey parity). The core is pure (feed it the eslint results, each
rule's configured level, and the loop summaries); a consumer CLI does the I/O. It surfaces the **LZFE roster
including clean (0-hit) rules** so a `warn`→`error` promotion is an evidence-backed move — you see, in one place,
which rules gate, which are a revealed backlog, and which are already clean. See the Hostpoint dogfood's
`scripts/lzfe-doctor.mjs` (`npm run doctor` / `doctor:json`) — a thin CLI over this core.

## Scaffold a feature unit

```
npm run scaffold -- <plural-name> [targetDir]
# e.g.  npm run scaffold -- bookings sample/bookings
```

Emits the four co-located files of the canonical unit — `<Feature>.viewModel.ts`, `<Feature>.view.tsx`,
`<Feature>.test.tsx`, `<feature>.i18n.ts` — with names derived from the feature name. The emitted unit is the
blessed `sample/items` shape with substitutions, so it **passes every LZFE rule and typechecks by construction**
(`generate.test.ts` writes a unit to disk and lints it with the real rules to prove the emitter and the harness
agree). Then refine the entity fields, wire the slice in `@/client.gen`, and fill the copy.

## Assemble the i18n resource tree

```
npm run assemble-i18n -- <featuresDir> <outFile>
# e.g.  npm run assemble-i18n -- features src/i18n/resources.generated.ts
```

Discovers every `*.i18n.ts`, derives each namespace from the filename, and emits a generated module that imports the
locale catalogs and composes `resources` (locale → namespace) — what an app otherwise wires by hand. The output
typechecks, so a renamed/removed catalog fails the build; pair it with LZFE011 (key parity *within* each catalog) —
the `lazuli/i18n-completeness` rule in-scope, or `i18n-parity.mjs` below when the catalogs are cross-package.

## i18n parity — cross-package catalogs (LZFE011, the other half)

```
node tools/i18n-parity.mjs <catalogsDir> [...moreDirs]
# e.g.  node tools/i18n-parity.mjs ../examples/sample-app/frontend/core/src
```

LZFE011 has two mechanisms for two layouts. When the catalogs live *inside* the linted source, the
`lazuli/i18n-completeness` eslint rule pins parity per file at lint time — done. But in a **core-split** layout the
catalogs sit in a SEPARATE package, outside the app's eslint scope, so the rule never sees them. `i18n-parity.mjs`
reads them directly and enforces the same invariant (every locale object in a catalog declares the same keys; a key
in one but not its siblings is a silent untranslated string), locale-agnostic like the rule. **Use the rule when
catalogs are in lint scope, the tool when they're cross-package** — same convention, the mechanism follows the
layout. (The Hostpoint dogfood runs the tool: its catalogs live in `@hostpoint/app-core`, out of the app's scope.)
