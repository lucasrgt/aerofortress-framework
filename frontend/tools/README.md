# tools — frontend generators

Beyond the typed client (orval generates that from the backend contracts), these emit the *structure* a feature
needs — the frontend parallel of the backend scaffold. Pure render functions in `generate.mjs` (no I/O, unit-tested
in `generate.test.ts`); the `*.mjs` CLIs wrap them with file writes. The `lazuli` .NET CLI front-door shells out to
these (the way `lazuli doctor` shells out to `npm run lint`) — two engines, one front door.

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
