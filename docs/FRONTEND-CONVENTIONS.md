# Lazuli (.NET) — Frontend Conventions & Harness

The frontend harness is the **same soul as the doctor, a different body**. Same mentality —
convention over configuration, semantic density, enforcement that an LLM cannot drift past —
but the body is plain, idiomatic **React Native / TypeScript** (the app is **mobile**), not C#,
and the harness is a separate, optional, TS-world tool, not the Roslyn doctor.

It exists to kill one class of failure the backend already designed out: **the AI says "it's
done" and ships a screen rendering mocked data.** That gaffe is documented in our own history
(hostpoint's `WAR-*` workarounds — screens inlining storybook fixtures instead of wired data).
The harness makes "wired" the only legal shape and "mock" structurally visible.

Ground every frontend convention here, never memory. The backend constitution is
[CONVENTIONS.md](CONVENTIONS.md); the decision that birthed this file is
[`lazuli-net-frontend-harness`](decisions/lazuli-net-frontend-harness.md).

---

## The two laws — restated for the frontend

1. **Stranger-maintainable.** The output is always plain, idiomatic React Native that an RN dev
   who has never heard of Lazuli can read and maintain. This is why MVVM lives here as a
   *naming discipline over custom hooks*, never as a framework (no classes, no observables, no
   two-way binding — that is Angular/WPF idiom imported into React, and it fails this law).
2. **Doctor-removable.** Remove the harness (the ESLint plugin + the wrapped generator) and the
   app still **builds and runs** — you only lose enforcement. The generated client is committed
   TypeScript that stays; the convention is plain React that stays. The harness is wire, not
   apparatus.

Any feature that fails both — a bespoke compiler, source-gen of behavior, a ViewModel framework
you inherit from — is **out, by construction**. Both are the lessons of Lazuli-1 (died owning a
compiler) and aerocoding (died generating artifacts for features that did not exist).

---

## The stack — opinionated, mobile (the "envenenada")

The body is **React Native**, because Hostpoint is a mobile app. The choices are pre-made so the AI decides
less (Rails-in-RN):

- **Expo** — the opinionated RN distribution (the managed toolchain, OTA, native modules without ejecting).
  Blazor/MAUI were weighed and rejected as too immature for this surface.
- **expo-router** — file-based navigation under `app/`, convention over configuration. It is the **only**
  router; screens are routes, a route renders exactly one View.
- **TanStack Query** — all server state (RN-native; cache, mutation, status). It is the Model.
- **orval** (target `react-query`) — generates the typed slice-hooks from the .NET API's OpenAPI
  (`/openapi/v1.json`). RN-agnostic output; the same wire works on device.
- **zod** + **react-hook-form** — input schemas + form state.
- **TypeScript strict** — `tsc` is part of the gate; it is what makes "wired" decidable.

expo-router (the route tree) and the feature triple compose: a route file under `app/` is a thin shell that
renders one feature's `*.view.tsx`; the feature folder holds the view/model/test. Navigation is the router's
job; data is the ViewModel's. Two seams, never crossed.

---

## The MVVM convention — one feature, one shape

One screen = a co-located triple. The suffixes are the analyzer anchor, the way `.Tests.cs` and
`.ctx.md` are on the backend:

```
features/<name>/
  <Name>.view.tsx        # the View — pure render; consumes exactly one ViewModel
  <Name>.viewModel.ts    # the ViewModel — the only data door; composes generated slice-hooks
  <Name>.test.tsx        # co-located test, as on the backend (LZ0003)
```

- **The ViewModel is a plain custom hook**, never a class: `useDepositModel(params) → { state,
  ...commands }`. Custom hooks *are* React's idiomatic way to extract logic — you get the MVVM
  seam without betraying the grain.
- **The ViewModel is render-agnostic** — no JSX, no `react-dom`. It is unit-testable without
  rendering, exactly as a backend `Handle` is HTTP-agnostic and testable without a host.
- **The View is pure render.** It owns no data access at all; it is a function of the
  ViewModel's return. This makes the View **mock-free by construction** — the harness never has
  to police it for data discipline.
- **TanStack Query is the Model.** The ViewModel *composes* Query (the generated slice-hooks); it
  never hides it behind a wrapper. A ViewModel that re-embeds every query in ceremony is the
  frontend's `IRepository`/unit-of-work — the clean-arch bloat the backend cuts (`LZ0006`).
  **One ViewModel per screen, never per query.**
- **Mandatory states.** A ViewModel exposing server data exposes `loading`, `error`, and `empty`
  as explicit state — never improvised in the View. This is the sad-path discipline of the back.

The parallel to the slice is near-exact — the payoff is semantic density: the AI reads one
ViewModel and knows the feature, as it reads one slice:

| Backend (slice) | Frontend (feature) |
|---|---|
| `Handle(Input) → Task<Result<Output>>` | `useModel(params) → { state, ...commands }` |
| HTTP-agnostic → testable without a host | render-agnostic → testable without JSX |
| `DbContext` direct, no repository (`LZ0006`) | TanStack Query direct, no wrapper |
| `Map` is the thin wire of transport | the `View` is the thin wire of render |
| `Input`/`Output` records *are* the contract | the generated slice-hook *is* the contract |

---

## The data layer — generated wire, hand-owned behavior

The "wired" guarantee is the type system, not a heuristic. The backend's `Input`/`Output`
records are the contract; ASP.NET emits them as OpenAPI; a generated typed client turns each
slice into one typed TanStack hook. If the AI invents an endpoint that does not exist, the hook
is not exported and **`tsc` does not compile** — the completeness gate is the compiler, not a
rule (the frontend analog of the old `API-HANDLER-UNWIRED-001`: no silent 404).

```
client.gen/              # GENERATED — never edited by hand, committed verbatim
  <slice>.gen.ts         #   one typed TanStack hook per slice (useDeposit, …)
lazuli.client.ts         # the orval mutator — unwraps Result<T>, injects auth, maps error→state
orval.config.ts          # the shipped convention config — the "poison" lives here
```

- **The generator is stock, wrapped — never bespoke.** `lazuli gen client` runs **orval**
  (target `react-query`) under our config. We do not own a compiler; we wire an existing one.
  Building our own OpenAPI→TS→TanStack generator *is* the Lazuli-1 gesture — re-solving parsing
  and emission that orval already maintains, tests, and edge-cases for free.
- **The opinion lives in config + convention, not in a fork.** The "envenenada" is two things:
  the `orval.config.ts` we ship and the **mutator** (`lazuli.client.ts` — the typed Lazuli
  client: `Result<T>` unwrap, auth, error mapping). This mirrors the back exactly — we do not
  fork EF Core; we use it stock and direct, and the opinion is the slice convention + the doctor.
- **The generated layer is boring on purpose.** All semantic density lives *above* it, in the
  hand-written ViewModel. Never poison the generated hooks.
- **One backend micro-convention makes the 1:1 clean.** The slice's `Map` names its endpoint —
  `MapPost("/deposit", …).WithName("Deposit")` → the OpenAPI `operationId` → orval emits
  `useDeposit`. The contract of the `Handle` becomes the name of the wire, with nothing bespoke
  in between. (Candidate doctor rule: endpoint name = slice name.)

---

## The bright line — generate vs scaffold (the law)

This is the whole game. Cross it wrong and the harness becomes the Lazuli-2 vector.

| | **Generate** (re-emits every build; you never edit) | **Scaffold** (`g`, runs once; you own it after) |
|---|---|---|
| Contract types (`Input`/`Output` → TS) | ✅ plumbing — the wire | |
| Typed slice-hook (`useDeposit()`) | ✅ plumbing — a wrap of the client | |
| ViewModel body (state, commands, derived, UX) | ❌ **source-gen of behavior — the Lazuli-2 vector** | ✅ a visible skeleton you write |

The test that separates them: **scaffold** runs on demand, writes visible code you edit and
own, and is doctor-removable (deleting the generator does not touch existing files) — the output
*is* the source. **Source-gen** runs every build, owns its output, clobbers your edits, and the
behavior lives in the generator, not the file.

So `lazuli g view <Slice>` scaffolds the `view`/`viewModel`/`test` triple with the **types fiber
from the contract** and the **behavior as a TODO you write** — a starting point, never an owner.
Explicitly **out**, on the same law: the old lazuli's "smart stubs" that pre-filled the body
with the "correct" runtime call. That delegates behavior; it is the frontend twin of the
"runtime framework you inherit from" the back rejects.

If the contract changes, the generated `*.gen.ts` regenerates (plumbing) and `tsc` breaks the
ViewModel where it is now wrong — you fix it by hand. The type enforces the drift; you own the
behavior.

---

## The harness — rule catalog (`LZFE*`)

The frontend doctor is an **ESLint custom plugin** (`eslint-plugin-lazuli`) for in-file rules,
plus a thin `ts-morph` pass for the cross-file shape, invoked alongside `lazuli doctor`. ESLint
is the mature path for custom semantic rules — hostpoint reached for Biome and had to hand-roll
a `.mjs` scanner for exactly this, the tell that Biome's custom plugins are not yet there.

With the MVVM seam, the policed surface collapses to **the ViewModel** — the View is mock-free
by construction, and completeness is the compiler. Every rule is born from observed pain
(hostpoint's port + the old-lazuli wiring rules), never speculation.

| Rule | Enforces | Status | Origin |
|------|----------|--------|--------|
| `LZFE001` | View purity — a `*.view.tsx` imports no data layer (generated hooks, the client, `fetch`/`axios`); it consumes exactly one ViewModel | planned | the wired-only seam — keeps the View mock-free |
| `LZFE002` | ViewModel is the only data door — only `*.viewModel.ts` may import the generated client hooks | planned | one data path, one policed surface |
| `LZFE003` | **No mock in production code** — no import from `**/__mocks__`/`**/fixtures`/MSW and no inline data-literal above threshold outside `*.test.*`; opt out a genuinely static surface with an explicit marker | planned | hostpoint: `WAR-*` storybook fixtures shipped as data |
| `LZFE004` | ViewModel is render-agnostic — a `*.viewModel.ts` imports no JSX/`react-dom` | planned | keeps the ViewModel unit-testable without rendering |
| `LZFE005` | Every feature carries a co-located `*.test.tsx` | planned | mirror of `LZ0003` |
| `LZFE006` | No orphan placeholder — `// wire later`, `TODO`/`FIXME`, `WAR-*`, or `@ts-expect-error` on a data call | planned | mirror of `LZSELF002` — "almost done" is not done |
| `LZFE007` | Mandatory states — a ViewModel exposing server data exposes `loading` + `error` + `empty` | planned | the sad-path discipline of `[Critical]` slices |

The completeness gate — "does this call a real endpoint?" — is **not** a rule. It is `tsc`
against the generated client. Lean on the type system; the harness only forbids the bypass.

---

## Scope — and non-goals

**In:** the MVVM feature convention, the `LZFE*` harness, a `g view` scaffold, and `lazuli gen
client` (stock orval, wrapped) with the shipped config + mutator. One blessed frontend shape.

**Out (non-goals), by decision:**
- **No bespoke generator.** orval stock, wrapped — never a Lazuli OpenAPI→TS compiler. (The
  Lazuli-1 vector.)
- **No source-gen of behavior.** The ViewModel body is scaffolded once and owned, never
  re-emitted. No "smart stubs" that pre-fill logic. (The Lazuli-2 vector.)
- **No MVVM framework.** Plain custom hooks, not classes/observables/two-way binding. (The
  stranger-maintainable law.)
- **No multi-app sprawl.** One frontend shape, enforced — sprawl was aerocoding's *N* apps, not
  one blessed convention.
- **No frontend in core.** The harness ships as a separate, optional, doctor-removable package —
  the `lazuli`/`lazuli-dev` split, applied again. It never enters `Lazuli.Abstractions` or
  `Lazuli.Doctor`.

When a proposal smells like capability instead of convention + enforcement, it is a scope
violation. Reject in line.
