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
typechecks, so a renamed/removed catalog fails the build; pair it with LZFE011 (key parity *within* each catalog).
