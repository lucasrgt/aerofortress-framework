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
- **Vitest** (jsdom) — the test runner for the **platform-agnostic core**: ViewModels (render-agnostic hooks,
  tested with `@testing-library/react`'s `renderHook` — no RN runtime), the generated client, types, schemas.
  *Not* jest-expo: rendering RN components is out of scope (the View is thin by convention), and Vitest is
  faster, one runner, and the same tests cover a future web client.

expo-router (the route tree) and the feature triple compose: a route file under `app/` is a thin shell that
renders one feature's `*.view.tsx`; the feature folder holds the view/model/test. Navigation is the router's
job; data is the ViewModel's. Two seams, never crossed.

### The shared core — the View is the only platform-specific layer

Everything below the View — the ViewModel (a render-agnostic hook), the generated client, the contract types,
the schemas — is **pure TypeScript with no platform dependency** (`LZFE009` enforces it). So the core is
**shareable web↔mobile**: a future web client (React + react-dom) and this mobile app (RN) consume the *same*
ViewModels + client + types; only the Views differ. And it is **tested once, in Vitest** (jsdom), since none of
it touches a native runtime. Platform capabilities (storage, navigation, push, camera) are **injected ports**
(the frontend's `IFileStorage`), wired by each platform's View/shell — never imported into the ViewModel. This
is the old lazuli's "audience/platform projection" done as plain shared TS + thin per-platform Views, not a
bespoke compiler. Physically extracting a `core` package is a `move` when the web client lands (YAGNI until
then); the discipline holds from day one.

---

## The MVVM convention — one feature, one shape

One screen = a co-located triple. The suffixes are the analyzer anchor, the way `.Tests.cs` and
`.ctx.md` are on the backend:

```
features/<zone>/<name>/
  <Name>.view.tsx        # the View — pure render; consumes exactly one ViewModel
  <Name>.viewModel.ts    # the ViewModel — the only data door; composes generated slice-hooks
  <Name>.test.tsx        # co-located test, as on the backend (LZ0003)
  <name>.i18n.ts         # the feature's i18n namespace (ptBR/esES/enUS)
  panels/ | steps/       # for multi-panel/multi-step features (sub-views, same harness rules)
```

- **Group features by `<zone>` = audience, not by backend domain.** When an app has distinct personas,
  the feature tree mirrors the **route tree** and how the product is *experienced* — e.g. Hostpoint:
  `features/{host,traveler,account,shared}/<name>/`. The domain axis (Catalog/Operations/…) is already
  carried by the generated client; re-mirroring the backend's modules on the front would scatter a single
  audience across domains. The harness is depth-agnostic (`LZFE*` match by filename, not folder), so the
  grouping is a free organizational choice. Single-persona apps can stay flat (`features/<name>/`).

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
- **Audience filters the client at the generator, not the rule.** orval is configured to include only
  endpoints tagged for *this* frontend's audience (`app`). Webhooks, internal/server-to-server, and
  other-audience endpoints carry a different `[Endpoint(...)]` kind (below), are tagged accordingly, and
  never enter `client.gen/`. So `LZFE008` (loose-endpoint coverage) is high-signal by construction — the
  noise is removed in the plumbing (config of a stock tool), not papered over by the rule. This is the old
  lazuli's "audience SDK projection", done by tool config instead of a bespoke compiler.

---

## Forms — react-hook-form, validation grounded in the contract

Multi-field forms (the property/service editors) use **react-hook-form**, not a hand-rolled
`useState` draft. The same "wire, not reinvention" law that keeps us off a bespoke OpenAPI
compiler keeps us off a hand-rolled form engine: RHF gives uncontrolled inputs + field-level
subscriptions (no whole-form re-render per keystroke), dirty/touched/validation state, and one
submit path. Reimplementing that is the gesture the framework forbids.

- **The `useForm` lives in the ViewModel.** It is form *logic*, not rendering, and RHF's core
  imports no `react-native` — so the ViewModel stays platform-agnostic (`LZFE009`) and the same
  form would back a web client. The ViewModel exposes `control` + a `submit`; panels bind their
  slice with `<Controller>`. The View layer stays the only platform-specific piece.
- **Validation is grounded in the contract.** The field *shape* and closed enums are already
  enforced at compile time by the form type (built from the generated enums) and at runtime by the
  controlled pickers — an invalid enum value cannot be produced. The zod resolver adds only what
  the type system can't: required fields and documented `@pattern`s (e.g. the coordinate regex,
  lifted verbatim from the contract). Small, hand-authored, contract-grounded — not free-invented.
- **Why not generate the zod from the contract.** orval's `client: "zod"` output is the ideal
  (schema straight from OpenAPI), but v7 emits invalid `zod.number().regex(...)` for numeric fields
  carrying a `pattern` — it does not compile. Until coordinates are typed as `string` server-side
  (or orval fixes it), forms hand-author the compact schema. Revisit when the generator can.
- **Plain `useState` is fine for trivial input.** A one- or two-field reply/search box does not
  need RHF; the convention is for genuine multi-field forms.

Big forms decompose **panel-per-tab**: a hand-written spine (the ViewModel + the tab-shell View
with a panel registry) plus one pure `panels/<X>Panel.view.tsx` per tab, each a function of the
shared `control`. The panels are independent, so they migrate as panel-granularity fan-out.

---

## Endpoint kinds — the wiring vocabulary

Not every endpoint should have a frontend wiring, and that is not a rare exception (webhooks, internal
server-to-server, OAuth redirects, other-audience admin panels). So the framework classifies an endpoint's
**nature** with a closed-vocabulary marker — `[Endpoint(...)]` — pulled from the same `[Slice]` shape that
drives everything else. This is **classification, not suppression**: it does not say "ignore the rule here",
it says "this endpoint *is* a webhook", and the harness derives that a webhook has no UI wiring.

- **Opt-out, not opt-in.** The default is `App` — app-facing, *must* be wired (`LZFE008`). The dangerous
  case (forgot to wire) must be loud by default; the legitimate exception (a webhook) costs one marker. (This
  is right *because* a Lazuli app is UI-first/mobile — app-facing is dominant. An API-first product would
  reconsider.)
- **One mark, many derivations** — the `[Critical]` pattern again. A single marker on the slice feeds: orval
  (audience filter → non-app endpoints leave the client), `LZFE008` (covers only app-facing), and a future
  backend doctor rule (a `Webhook` must verify its signature / be idempotent). One declaration, several
  enforcements; intent flowing back→front.
- **Closed enum of natures, zero behavior params** — the guard-rail against the mini-language the constitution
  forbids. The marker says *what it is*; the `Handle` says *what it does*.
  - `(default)` → `App` — app-facing; must be wired.
  - `[Endpoint(Webhook)]` → third-party callback; never UI.
  - `[Endpoint(Internal)]` → server-to-server; outside any client.
  - `[Endpoint(Audience = "admin")]` → wired by *another* frontend, not this one.
  - Forbidden: `[Webhook(Retries = 3, Signature = "hmac")]` — config-as-annotation is the fattening that
    killed earlier scenarios. Retry/signature/idempotency live in the `Handle`, visibly, never in the mark.
- **The .NET spelling is a builder call, not a class attribute.** A minimal-API handler is a lambda; a `[Endpoint]`
  attribute on the slice class can't reach the endpoint metadata. So the marker is `.WithEndpointKind(EndpointKind.Webhook)`
  on the slice's `Map` (framework extension, `Lazuli.AspNetCore`) — it tags the endpoint, and `AddLazuliOpenApi` /
  the orval audience filter carry the nature into the client. `App` is the default and needs no call (opt-out).

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
| `LZFE001` | View purity — a `*.view.tsx` imports no data layer (generated hooks, the client, `fetch`/`axios`); it consumes its ViewModel. Type-only imports of the contract are exempt | **shipped** | the wired-only seam — keeps the View mock-free |
| `LZFE002` | ViewModel is the only data door — only `*.viewModel.ts` may (value-)import the generated client | **shipped** | one data path, one policed surface |
| `LZFE003` | **No mock in production code** — no import from `**/__mocks__`/`**/fixtures`/MSW outside `*.test.*` | **shipped** | hostpoint: `WAR-*` storybook fixtures shipped as data |
| `LZFE004` | ViewModel is render-agnostic — a `*.viewModel.ts` imports no JSX/`react-dom` | planned | keeps the ViewModel unit-testable without rendering |
| `LZFE005` | **Co-located test that exercises the ViewModel** — every `*.viewModel.ts` has a sibling `*.test.tsx` that imports it and calls `renderHook()`. Existence alone is not enough: mounting `useXModel()` compiles the ViewModel against the real generated client and proves the hook is callable. Behavior assertions stay per-screen judgment (no test-theater) | **shipped** | mirror of `LZ0003` — the triple's third leg; "renders + has a data door but no test" is not done |
| `LZFE006` | No orphan placeholder — `// wire later`, `TODO`/`FIXME`, `WAR-*`, or `@ts-expect-error` on a data call | planned | mirror of `LZSELF002` — "almost done" is not done |
| `LZFE007` | Mandatory states — a ViewModel exposing server data exposes `loading` + `error` + `empty` | planned | the sad-path discipline of `[Critical]` slices |
| `LZFE008` | **Endpoint coverage (back→front)** — every app-facing generated hook (`use<Slice>`) is referenced by ≥1 ViewModel; an unreferenced hook is a **warning** ("loose endpoint"). Non-app endpoints leave by audience tag and never enter the client, so they never warn | **shipped** (fs pass in `npm run lint`) | back→front completeness — catches "backend done, UI not wired" |
| `LZFE009` | **ViewModel is platform-agnostic** — a `*.viewModel.ts` imports no `react-native`/`expo-*` (value *or* type); platform capabilities are injected ports | **shipped** | keeps the ViewModel + core shareable web↔mobile and Vitest-testable |

The two directions are asymmetric, and that sets the severity: **front→back** (the UI calls an
endpoint that doesn't exist) is never valid → a hard **error**, free from `tsc` (the hook isn't
generated, so it can't compile). **back→front** (the endpoint exists, nothing wired it yet) is a
legitimate intermediate state → a **warning** (`LZFE008`). Failing the build there would be wrong;
revealing it is the point. The completeness gate — "does this call a real endpoint?" — is **not** a
rule. It is `tsc` against the generated client. Lean on the type system; the harness only forbids the
bypass and surfaces the loose ends.

---

## Code comments — the code speaks for itself

Comments are **English**, and they earn their place. The default is **no comment**: a well-named
ViewModel/hook/component + the types say what the code does. A comment exists only to say what the
code *can't* — a non-obvious **why**, a **gotcha**, an invariant, a contract quirk. Rails-style:
prose that adds signal, never restating the line below it.

Explicitly **out** (this is the junk an LLM tends to emit — strip it on sight):
- Migration play-by-play / thinking-out-loud (`// Faithful clone of the old screen…`, `// re-skinned
  onto…`, `// Step 1: …`). The git history is the narrative; the file is not.
- Restating the obvious (`// the name field` over `name:`, `// loading state` over `loading`).
- Mixed PT/EN. Comments are English; **only user-facing copy is pt-BR, and that lives in i18n**, not
  in comments or string literals.

Keep: a non-obvious gotcha (e.g. *why* a value is coerced, *why* an effect is gated), a contract
caveat, an invariant. If you're unsure whether a comment earns its place, delete it.

## i18n — react-i18next, per-feature namespaces

User-facing copy is **never inlined** in a View; it goes through `react-i18next`. One i18next instance
(`src/i18n`), pt-BR today. Each feature owns a **namespace** = its folder name, in a co-located
`src/features/<feat>/<feat>.i18n.ts` (the `ptBR` export), assembled in `src/i18n/resources.ts`; shared
copy (nav, generic actions) lives in the `common` namespace. A View reads `const { t } =
useTranslation("<feat>")` and renders `t("some.key")`. Adding a locale is a second key in `resources`
+ a language switch — the feature namespaces don't change. (Like styling, i18n is the app's choice, not
a framework mechanism; this is Hostpoint's.)

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
- **No prescribed styling system.** The blessed shape pins what touches the seam (router, query
  layer, generator, form lib, test runner — all relevant to *wired/tested*) and stays silent on pure
  paint: the styling library (StyleSheet / NativeWind / Tamagui / Unistyles), the component kit, the
  icon set, the design tokens are **the app's choice**. The `LZFE*` harness is styling-neutral by
  construction — it polices the data seam (`LZFE001` no data layer in a View, `LZFE002` one data door,
  `LZFE009` platform-agnostic ViewModel), never the paint above it. (Hostpoint picks NativeWind + its
  own design system; another app picks differently — the convention and harness are unchanged.)
- **No TS decorators (`@Slice`/`@Journey`/`@Critical`).** The backend's `[Slice]` is a first-class
  C# attribute the Roslyn doctor reads natively; React function components have no idiomatic decorator
  seam, and bolting one on (babel `experimentalDecorators`, wrapper indirection) *adds* LLM decision
  space — the opposite of the goal. Symmetry of **concept** (the slice), not of **mechanism**: on the
  front the **folder/file convention is the annotation**, discovered structurally
  (`features/<x>/<X>.view.tsx`), exactly as `[Slice]` is on the back. When `@Critical`/`@Journey`
  earn their place (a concrete multi-screen flow needing flow-level coverage), they arrive as a plain
  `export const meta = { critical, journey } satisfies FeatureMeta` the doctor reads — never a
  decorator. Deferred until a real journey lands (YAGNI).
- **No multi-app sprawl.** One frontend shape, enforced — sprawl was aerocoding's *N* apps, not
  one blessed convention.
- **No frontend in core.** The harness ships as a separate, optional, doctor-removable package —
  the `lazuli`/`lazuli-dev` split, applied again. It never enters `Lazuli.Abstractions` or
  `Lazuli.Doctor`.

When a proposal smells like capability instead of convention + enforcement, it is a scope
violation. Reject in line.
