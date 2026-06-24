# AeroFortress Framework — frontend

The frontend side of aerofortress-framework. Where the .NET packages under `src/` give the backend its spine (`[Slice]`,
`Result<T>`, the Roslyn analyzers), this gives the React Native + web frontend an equally tight spine — **as
idiomatic React/TS primitives the app composes, not a DSL** (the "poisoned frontend": real React, enriched by
convention + tooling, never a second language). It is multi-target by construction: the same code serves web
(react-native-web) and native (iOS/Android) via Expo; only genuinely divergent bits (auth persistence, maps,
OAuth) are platform seams (`*.web.ts` / `*.native.ts`).

## Layout

```
frontend/
  package.json        # npm workspace root — `npm run check` = typecheck + test (the gate CI runs)
  tsconfig.base.json  # strict TS config every package/sample extends
  vitest.config.ts    # jsdom + the @/… aliases that let the sample run wired (not mocked)
  packages/
    aerofortress-react/     # @aerofortress/react — the spine: AsyncState, Resource (+ guards, session as they graduate)
    eslint-plugin/    # eslint-plugin-aerofortress — the AFFE harness (rules + RuleTester self-tests)   [planned home]
  sample/
    items/            # the canonical feature unit — the blessed reference an app/agent copies (kept pristine)
    harness/          # stand-ins for the app's @/ui, @/i18n, @/client.gen — so the sample compiles + tests
  tools/              # generators: scaffold a feature unit + assemble the i18n resource tree (npm run scaffold / assemble-i18n)
```

The gate (`npm run check`, also run by `.github/workflows/ci.yml` next to `dotnet build`): `tsc --noEmit` over
the spine + sample, then `vitest run`. The spine has unit tests (every `toAsyncState` branch, every `<Resource>`
state); the sample's test mounts its data door against the harness's real react-query hook — **wired, not mocked**,
the same shape a real generated client would have.

Apps (e.g. the Hostpoint dogfood) consume `@aerofortress/react` + the eslint plugin as packages; the `af` .NET CLI
scaffolds them in and `af doctor` shells out to `npm run lint` for the frontend slice (Roslyn in-proc for the
backend slice — two native engines, one front door).

## The spine — `@aerofortress/react`

The read-side analogue of `Result<T>`. A screen's **ViewModel** (the data door) exposes its resource as an
`AsyncState<T>` discriminated union; the **View** renders it through `<Resource>`, so loading / error / empty are
handled **by construction** (exhaustive switch), the way an exhaustive `Result` match forces the sad path.

```ts
type AsyncState<T> =
  | { status: "loading" }
  | { status: "error"; message: string; retry?: () => void }
  | { status: "empty" }
  | { status: "ready"; data: T };
```

`toAsyncState(query, { errorMessage, isEmpty? })` projects a react-query result into the union — **wire over
react-query, not a replacement** (react-query still owns fetch/cache/retry). `<Resource>` is design-system-
agnostic; the app injects its loading/error/empty visuals via slots.

## The canonical unit (the frontend `[Slice]`)

A feature is four co-located files, mapped to the backend:

| Backend `[Slice]` | Frontend unit |
|---|---|
| `Handle` (logic, the data door) | `<Feature>.viewModel.ts` — the only client importer; platform-agnostic; exposes `AsyncState` + commands |
| `Output` (typed result) | `AsyncState<T>` |
| route `Map` | `<Feature>.view.tsx` — render only; consumes the ViewModel via `<Resource>` |
| co-located `*.Tests.cs` | `<Feature>.test.tsx` — mounts the door against the real client (wired, not mocked) |
| — | `<feature>.i18n.ts` — per-feature copy, all locales |

See [`sample/items`](./sample/items) for the blessed reference.

## The harness (the frontend self-audit)

`eslint-plugin-aerofortress` (the AFFE rules) is the front-side parallel of the backend's Roslyn/`AeroFortress.Framework.Doctor`
self-audit. It polices the unit: View renders only (AFFE001), the ViewModel is the one data door (AFFE002, with
`lib/session` + `lib/guards` as the sanctioned infra doors), the ViewModel is platform-agnostic (AFFE009), every
ViewModel has a co-located test (AFFE005), no mocks in production (AFFE003). The plugin **self-proves** with
RuleTester (`npm test`) — a rule isn't done until a test pins that it fires on the violation and passes on the
allowed shape.

## Roadmap (closing the gap with the backend)

0. **Toolchain/CI** — workspace + strict TS + vitest + a CI gate beside `dotnet build` (done); the sample runs
   wired against a harness.
1. **Spine** — `AsyncState`/`Resource` + the routing/session primitives (`SessionState`/`toSessionState`, `safeBack`)
   graduated from the pilots (done).
2. **Rules** — grow AFFE into categories the way the backend has: state-completeness (screens use `<Resource>`),
   i18n-completeness (keys in every locale), design-tokens (no inline hex), and the **routing harness**
   (`AFFE015–019`: declarative redirects, one session seam, a tri-state guard, guarded params + Back) — **done**;
   still ahead: the "no hardcoded string" half of i18n, a11y, error-handling (every mutation surfaces its error).
3. **Generators** — beyond the typed client: scaffold a feature unit + assemble the i18n resource tree (`tools/`,
   **done**); still ahead: wire route guards from declared policy, and the `af` .NET CLI front-door that shells
   out to these.
