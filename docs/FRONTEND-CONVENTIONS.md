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
  The spine (`@lazuli/react`) carries the primitives: the ViewModel projects each query through
  `toAsyncState` into the closed `AsyncState<T>` union (a multi-query screen folds them with
  `combineAsyncStates` — precedence `error > loading > empty > ready`, combined retry), and the
  View renders it through `<Resource>` (`LZFE010`). Routes project raw params through
  `requiredParam` (`missing | ready`) before rendering (`LZFE018`).

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
  in between. (`LZ0012` enforces it: endpoint name = slice name.)
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
| `LZFE002` | ViewModel is the only data door — only `*.viewModel.ts` (plus the auth/routing infra seams, `lib/session`/`lib/guards`) may (value-)import the generated client. Re-exporting it (`export … from "client.gen"`) outside the doors is the laundering bypass, also flagged; type re-exports stay free | **shipped** | one data path, one policed surface |
| `LZFE003` | **No mock in production code** — no import from `**/__mocks__`/`**/fixtures`/MSW outside `*.test.*` | **shipped** | hostpoint: `WAR-*` storybook fixtures shipped as data |
| `LZFE004` | ViewModel is render-agnostic — a `*.viewModel.ts` imports no JSX/`react-dom` | planned | keeps the ViewModel unit-testable without rendering |
| `LZFE005` | **Co-located test that exercises the ViewModel** — every `*.viewModel.ts` has a sibling `*.test.tsx` that imports it and calls `renderHook()`. Existence alone is not enough: mounting `useXModel()` compiles the ViewModel against the real generated client and proves the hook is callable. Behavior assertions stay per-screen judgment (no test-theater) | **shipped** | mirror of `LZ0003` — the triple's third leg; "renders + has a data door but no test" is not done |
| `LZFE006` | **Co-located integration test for every screen** — a `*.view.tsx` with a sibling `*.viewModel.ts` has a `*.test.tsx` that `render()`s it through the shared Providers harness. Presentational fragments (no viewModel) are out of scope — covered via their shell | **shipped** | the integration tier — "renders but untested" is not done |
| `LZFE007` | Mandatory states — a ViewModel exposing server data exposes `loading` + `error` + `empty` | planned | the sad-path discipline of `[Critical]` slices |
| `LZFE008` | **Endpoint coverage (back→front)** — every app-facing generated hook (`use<Slice>`) is referenced by ≥1 ViewModel; an unreferenced hook is a **warning** ("loose endpoint"). Non-app endpoints leave by audience tag and never enter the client, so they never warn | **shipped** (`tools/endpoint-coverage.mjs`) | back→front completeness — catches "backend done, UI not wired" |
| `LZFE009` | **ViewModel is platform-agnostic** — a `*.viewModel.ts` imports no `react-native`/`expo-*` (value *or* type); platform capabilities are injected ports | **shipped** | keeps the ViewModel + core shareable web↔mobile and Vitest-testable |
| `LZFE010` | **State completeness** — a `*.view.tsx` routes loading/error/empty through `<Resource>` (the spine), not raw `isPending`/`isError` | **shipped** | every async state handled by construction, not a hand-rolled branch that forgets one |
| `LZFE011` | **i18n parity** — every locale object in a `*.i18n.ts` declares the same keys, compared as **flattened paths** (`empty.title`) so a key missing inside a nested group is caught too; a key in one language but not its siblings is a silent untranslated string. Two mechanisms by layout: the `i18n-completeness` eslint rule when catalogs are in lint scope, `tools/i18n-parity.mjs` when they are cross-package | **shipped** | no string ships untranslated in any language |
| `LZFE012` | **Design tokens** — no inline hex color outside the token/theme/palette files; color comes from the theme | **shipped** | one palette; theming (dark mode, white-label) survives |
| `LZFE013` | **Mutation surfaces its error** — a react-query `.mutate(...)`/`.mutateAsync(...)` in a ViewModel routes its failure somewhere (inline `onError`, a read `.isError` state, a try/catch or `.catch()` on `mutateAsync`, or a propagated return). An **empty** `onError: () => {}` is flagged too — the silent failure with paperwork | **shipped** | the front-side of the backend's `error_handling` — no silent failure, no `onError` theater |
| `LZFE014` | **No hardcoded copy** — user-facing JSX text + copy props (`placeholder`, `label`, `accessibilityLabel`…) in a View go through i18n (`t()`), not literals | **shipped** | feeds the catalog that `LZFE011` then keeps complete |
| `LZFE015` | **No imperative redirect inside `useEffect`** — a redirect-on-state is declarative (`if (terminal) return <Redirect/Navigate … />`), never `router.replace`/`router.navigate`/a `useNavigate()` call in an effect: it runs after paint and re-fires every render (a flash on TanStack; on expo-router web the router freezes the source screen → an infinite navigation/refetch loop). `push`/`back` on a user action stay allowed. Scoped to the navigating layer (views + routes) | **shipped** | the pilot shipped this loop twice (Splash, then ChooseRole + 5 screens) before the rule existed |
| `LZFE016` | **Session one door** — the bearer token is written through one seam (`lib/session`, where the write is paired with a `me`-cache reset); a `*.viewModel`/`*.view` importing the token setter (`setAccessToken`…) directly — **or writing a token-ish key straight to storage** (`localStorage`/`AsyncStorage`/`SecureStore.setItem("…token…", …)`) — is the scattered write that forgets the reset | **shipped** | pauta: a forgotten reset after registration bounced the new user back to `/login` |
| `LZFE017` | **Guard tri-state** — a route guard redirects on a `SessionState` (`loading \| authenticated \| anonymous`), never a raw `isAuthenticated` boolean (which reads "still loading" as "signed out"). The read-side twin of `LZFE010` | **shipped** | the bounce-to-login root cause: a boolean collapses the still-loading case |
| `LZFE018` | **Route param guard** — a route reading a required id param (expo-router `useLocalSearchParams`) guards its absence with a declarative redirect, so a param-less hit (bookmark / stale link) can't render a ghost screen on an empty id. The spine's `requiredParam()` union (`missing \| ready`) is the blessed guard shape (`if (id.status === "missing") return <Redirect/>`), recognized beside the bare `!id` form | **shipped** | hostpoint: a param-less `/messaging/chat` rendered an empty "ghost" thread |
| `LZFE019` | **Safe back** — no bare `router.back()`/`history.back()`; Back goes through a guarded helper (the spine's `safeBack` / an app `useGoBack`) that falls back to a parent when there's no in-app history | **shipped** | hostpoint: deep-linked screens had a dead "Voltar" button (~13 screens migrated) |
| `LZFE020` | **No hardcoded API base URL** — the base URL comes from configuration (env `VITE_API_URL`/`EXPO_PUBLIC_API_URL`, a relative base, or an injected default), never a host baked into `axios.create({ baseURL: "http://…" })`. The backend pins its dev port in `launchSettings`, so the two agree by construction | **shipped** | pauta: the front baked `:8080` while the API ran on the .NET default `:5000` → `me` 404'd → the registered user bounced to login |
| `LZFE021` | **No raw HTML** — no `dangerouslySetInnerHTML` outside the one audited seam (`lib/html`). JSX escapes by construction; raw HTML is the XSS door, and if the app renders rich HTML (a CMS body) the sanitizer lives in that seam, reviewable | **shipped** | the single React opt-out of escaping must not scatter across screens |
| `LZFE022` | **No open redirect** — never navigate to a value that arrived in the URL (`router.replace(returnTo)` / `location.href = next` off `useLocalSearchParams`/`useSearch`/`useSearchParams`); map the param through an **allowlist** of known in-app routes first | **shipped** | the phishing primitive: a crafted link sends the session-carrying browser anywhere the attacker chose |
| `LZFE023` | No orphan placeholder — `// wire later`, `TODO`/`FIXME`, `WAR-*`, or `@ts-expect-error` on a data call | planned | mirror of `LZSELF002` — "almost done" is not done (renumbered as shipped rules claimed the lower slots) |
| `LZFE024` | **UI door** — a `*.view.tsx` renders no host element (no lowercase JSX) and carries no `style`/`className` attribute; everything visual comes from `@/ui` (the app-owned kit). A missing primitive is extended in `ui/`, never inlined. The `LZFE002` one-door pattern applied to paint — the design band, [DESIGN-CONVENTIONS.md](DESIGN-CONVENTIONS.md) | planned (design band) | the sample's pre-kit `ui.tsx` leaked `className` — one passthrough reopened every visual decision |
| `LZFE025` | **Scale only** — outside `ui/`, token files, and tests: no numeric literal in spacing/typography style keys (`padding*`/`margin*`/`gap`/`rowGap`/`columnGap`/`borderRadius`/`fontSize`/`lineHeight`; `0` allowed), no Tailwind arbitrary value (`[13px]`) in `className` | planned (design band) | off-scale values are how rhythm dies one screen at a time |
| `LZFE026` | **Semantic colors** — outside token files: no `rgb()/hsl()/oklch()` literals, no CSS named colors in color-ish style keys, no value-import of a raw palette export outside `ui/`. Completes `LZFE012`: color is a role, or it does not ship | planned (design band) | a forked palette defeats theming silently; hex was only one spelling of the leak |

The two directions are asymmetric, and that sets the severity: **front→back** (the UI calls an
endpoint that doesn't exist) is never valid → a hard **error**, free from `tsc` (the hook isn't
generated, so it can't compile). **back→front** (the endpoint exists, nothing wired it yet) is a
legitimate intermediate state → a **warning** (`LZFE008`). Failing the build there would be wrong;
revealing it is the point. The completeness gate — "does this call a real endpoint?" — is **not** a
rule. It is `tsc` against the generated client. Lean on the type system; the harness only forbids the
bypass and surfaces the loose ends.

**Contract freshness — the mirror is pinned to its spec.** Every loop above reads the generated client
as truth, but nothing re-checks the mirror after generation: a backend shape change leaves the front
compiling happily against a stale client. `tools/contract-freshness.mjs` closes that: the codegen script
ends with `--stamp` (writes `client.gen/.spec-hash`, a whitespace-insensitive fingerprint of the OpenAPI
document), and the doctor leg compares the stamp against the live spec — a mismatch is a build-time "the
contract moved; regenerate", not a runtime 404. A **notice** until the first stamp exists, a hard gate
after.

---

## E2E journeys — `flows.json` + depth

E2E is flow-level and expensive, so it is enforced at the **project** level, not per component: a
project curates its journeys in `e2e/flows.json` and `tools/e2e-doctor.mjs` proves the list is
covered. Each entry is `{ name, target?: "web"|"native", spec, backendJourney?, terminal? }`:

- **Existence** (hard `gaps`): the `spec` file exists and a runner for its `target` is configured
  (Playwright for web, Maestro/Detox for native).
- **Set parity** (`tools/journey-parity.mjs`, LZFE-JOURNEY): a `backendJourney` (the
  `Journeys/<key>.Tests.cs` twin) links the flow to its backend journey, checked both directions — so
  no critical journey is half-built (tested on the back but never end-to-end on the front, or vice-versa).
- **Depth** (`depthGaps`, warn-tier, **LZFE-JOURNEY-002**): a spec *existing* is not coverage — it can
  stop at the door. A **linked** flow must declare `terminal` (the testID or route its spec asserts
  *after* entry, to prove the journey reaches its end), and the spec must actually reference it; a spec
  that asserts only the entry screen is flagged. *Why this exists:* a pilot's onboarding shipped a
  "complete → back to step 0" bug under a green doctor because the backend journey proved the lifecycle
  reached `Complete` while the frontend spec proved only entry — the bug lived in the **seam** between
  them. `terminal` forces the traversal across that seam to be asserted. Warn-tier first; promotes to a
  hard gate once flows declare their terminals. See
  [`docs/decisions/lazuli-net-journey-depth-enforcement.md`](decisions/lazuli-net-journey-depth-enforcement.md).

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

**Error codes — translated in every language, enforced.** The backend ships every error as a stable code
(`ErrorBody.code`, the registry constants behind `LZ0018`/`LZ0019`); the front owns the copy. Two gates guarantee no
error reaches a user untranslated: **coverage** — every code in the generated `ErrorBody.code` union has an
`api-errors` catalog entry (`lzfe-error-codes`; a notice until the client is regenerated against the enum-bearing
OpenAPI, a hard gate after) — and **parity** (`LZFE011`) — that entry exists in every locale. Composed: code → copy
→ in every language. This is the front end of the same full-stack discipline `LZ0018`/`LZ0019` enforce on the back.

## Accessibility — enforced, ecosystem-specific

a11y is part of the harness, but unlike the architecture rules it has **no cross-ecosystem parity to
share**: the web speaks DOM (`alt`, `aria-*`, `href`), React Native speaks accessibility props
(`accessibilityRole`, `accessible`, `accessibilityLabel`). So it is a **mirrored exclusive** — same
intent, one plugin per ecosystem, wired in the ESLint config (not the LZFE plugin, which owns
architecture):

- **web** → [`eslint-plugin-jsx-a11y`](https://www.npmjs.com/package/eslint-plugin-jsx-a11y) (the `flat/recommended` set).
- **mobile (RN)** → [`eslint-plugin-react-native-a11y`](https://github.com/FormidableLabs/eslint-plugin-react-native-a11y) (the full set; runs clean on ESLint 9 despite its peer cap at 8).

Both are **warn-first** — a revealed backlog promoted to error per-rule once cleared — with
`has-accessibility-hint` **off**: a hint is supplementary (only for non-obvious actions), and on by
default it buries the high-signal rules under noise. This is the same posture as the curated
community kit (`sonarjs`, `no-secrets`, `@tanstack/query`): external rules wired *alongside* the LZFE
plugin, never reinvented inside it. The design layer raises this bar exactly once: when the
canonical screens land (the recipes — [DESIGN-CONVENTIONS.md](DESIGN-CONVENTIONS.md)), **web
jsx-a11y promotes to error** for the sample tree — the exemplar proves green is reachable, so the
bar rises then, not before.

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
- **No prescribed styling *mechanism* — but the design *vocabulary* is conventional.** The blessed
  shape pins what touches the seam (router, query layer, generator, form lib, test runner) and stays
  neutral on the paint *mechanism*: the styling library (StyleSheet / NativeWind / Tamagui /
  Unistyles / CSS vars) and the icon set remain **the app's choice**, mapped by hand from the tokens,
  once. What is no longer free-invented is the **vocabulary**: the token taxonomy (names + types),
  the closed kit shape (the app-owned `ui/`), and the ui-door discipline are the convention,
  constitutionalized in [DESIGN-CONVENTIONS.md](DESIGN-CONVENTIONS.md) and enforced by the design
  band (`LZFE024–026`, beside `LZFE012`). Token **values** stay the app's — that is the entire
  theming story. (Hostpoint keeps NativeWind + its own finished components; if it ever adopts, it is
  by aliasing values onto the taxonomy with zero visual delta — the mechanism choice is untouched.)
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
