# Lazuli.Starter — Operating manual for AI agents

This is a **Lazuli (.NET)** app: an opinionated convention bundle — a vertical-slice .NET backend + an
MVVM React frontend + a build-time harness (the *doctor*) that enforces both. The conventions exist so an
agent has **less to decide** and what it writes is **checked**. Same mindset as Rails (convention over
configuration, semantic density) in plain, idiomatic C# + TypeScript — no DSL, no runtime you inherit from.

> Mirrored verbatim at `AGENTS.md` for tooling that loads it (Codex, Aider, …).

---

## The two laws (never violate)

1. **Stranger-maintainable.** Output is always plain, idiomatic C#/TypeScript that a dev who has never heard
   of Lazuli can read and maintain.
2. **Doctor-removable.** Remove the analyzers / the ESLint plugin and the app still **compiles and runs** —
   you only lose enforcement. The harness is wire, not apparatus.

The goal is **not "less code"** — it is **semantic density**: more meaning per token, in standardized shapes
the doctor can check.

---

## This repo

Topology is declared in `Lazuli.toml` (the single source of truth; `lazuli doctor` validates it):

- **Backend** `src/Lazuli.Starter.Api` — .NET vertical slices; the `LZ*` Roslyn analyzers gate
  `Lazuli.Starter.slnx`.
- **Frontend** — add a React client under `clients/` (`lazuli g`); the `LZFE*` ESLint plugin
  (`clients/eslint-plugin-lazuli`, the downstream mirror of the lazuli-net canonical) gates it.
- **Doctor**: `lazuli doctor` runs both legs.

---

## Backend — the vertical slice (the .NET API, gated by `LZ*`)

One feature = **one file** (maximal locality: read the whole feature in one read). The canonical shape
(`LZ0001`):

```csharp
[Slice]                                    // pure marker; module derived from the namespace
public static class Deposit
{
    public record Input(/* … */);          // contract in  — visible
    public record Output(/* … */);         // contract out — visible

    public static async Task<Result<Output>> Handle(Input input, AppDb db, CancellationToken ct)
    {
        // DbContext direct. Behavior lives here, never hidden. No repository, no unit-of-work, no mapper.
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) => /* thin */ .WithName(nameof(Deposit));
}
```

- **DbContext direct** — no `IRepository` / unit-of-work / mapper profile (`LZ0006`). The endpoint stays thin,
  an expression-bodied handler, never a statement block (`LZ0002`). Raw SQL never splices runtime values as
  text — a `*Raw` EF call with interpolation/concat is flagged (`LZ0024`); use `FromSql`/`ExecuteSql`.
- **Security is a decision, never an omission**: every slice's endpoint declares its authorization —
  `.RequireAuthorization(…)` or an explicit `.AllowAnonymous()`, on its own `Map` chain or the module's route
  group (`LZ0022`); a `Handle` that injects `ICurrentUser` and never reads it is a missing ownership check
  (`LZ0023`). A curated CA* security floor (dropped `CancellationToken`, insecure deserialization, broken
  crypto/TLS) ships with the doctor at error tier (opt-out: `<LazuliSecurityAnalysis>false</…>`).
- **Modules** own both halves of their wiring — `AddServices` + `Map` (`LZ0015/16`); `Program.cs` is only an
  index (`LZ0017`). Each module carries a `<Module>.ctx.md` (`## Boundaries` + `## Design notes`, non-empty and
  kept **fresh** — `LZ0004/05`).
- **Domain is always-valid**: a `[ValueObject]`/`[Entity]` exposes no public constructor or setter and is built
  only through a smart constructor returning `Result<T>` (`LZ0013/14`); a persisted or entity-owned type must
  declare its mark (`LZ0021`). **Write-ownership**: a module writes only its own entities — on a `DbSet` or
  through the untyped `db.Add(entity)` (`LZ0009`). A held `Result<T>` is **checked before unwrapped** —
  `IsSuccess`/`IsFailure`/`Validation.Collect` before `.Value`/`.Error` (`LZ0025`). An entity a `[Critical]`
  slice writes carries a concurrency token (`[Timestamp] RowVersion` — `LZ0026`, warn-tier).
- **Validation inline** at the top of `Handle`, accumulated with `Validation` — `Check`/`Collect` plus the
  shorthands `Require(guid, field, code)`, `NotBlank`, `InRange`.
- **Errors are registry constants** on a `*ErrorCodes` class (`LZ0018/19`) — the OpenAPI + i18n seam.
  `.WithName(nameof(Slice))` (`LZ0012`) is what the typed client turns into the `use<Slice>` hook.
- **Tests**: every slice has a co-located `<Slice>.Tests.cs` (`LZ0003`); a `[Critical]` slice has a happy **and**
  a sad `[Journey]` that asserts its post-condition (`LZ0008/10/20`). Files ≤ 500 LOC (`LZ0007`).

---

## Frontend — MVVM + the spine (the React clients, gated by `LZFE*`)

A screen is a triple: a **View** that renders, a **ViewModel** that owns data, a test that mounts it.

- **View renders only** (`LZFE001`); the **ViewModel is the one data door** to the generated client (`LZFE002`)
  and is **platform-agnostic** — no `react-native`/`expo` (`LZFE009`), so the core is shared web↔mobile.
- Async state flows through the spine's `AsyncState` + `<Resource>` (`LZFE010`), never raw `isPending`/`isError`
  (multi-query screens fold with `combineAsyncStates`). Mutations surface their error — and an empty
  `onError: () => {}` doesn't count (`LZFE013`). No mocks in production (`LZFE003`); co-located unit +
  integration tests (`LZFE005/06`). Copy goes through i18n, with locale-key parity checked as flattened nested
  paths (`LZFE011/14`); color through design tokens (`LZFE012`). The API base URL comes from config, never a
  hardcoded host (`LZFE020`).
- **Security** (`LZFE021–022`): no `dangerouslySetInnerHTML` outside the one audited `lib/html` seam (the XSS
  door); never navigate to a value that arrived in the URL — allowlist it first (open redirect).
- **Routing & session** (`LZFE015–019`, `LZFE030`) — the navigation harness, born from real pilot bugs and
  router-agnostic (recognizes expo-router ↔ TanStack):
  - **`LZFE015`** — a redirect-on-state is declarative (`return <Redirect/Navigate …/>`), never `router.replace`
    / `router.navigate` / a `useNavigate()` call inside a `useEffect`.
  - **`LZFE016`** — the bearer token is written through **one seam** (`lib/session`) that pairs the write with a
    `me`-cache **reset**; a scattered write — importing the setter directly **or** writing a token-ish key to
    `localStorage`/`AsyncStorage`/`SecureStore` — forgets the reset and bounces a just-authenticated user to login.
  - **`LZFE017`** — a guard branches on a **tri-state** `SessionState` (`loading | authenticated | anonymous`),
    never an `isAuthenticated` boolean (which reads "still loading" as "signed out").
  - **`LZFE018`** — a route reading a required id param guards its absence with a declarative redirect (no ghost
    screen on an empty id); the spine's `requiredParam()` union (`missing | ready`) is the blessed guard shape.
  - **`LZFE019`** — no bare `router.back()`/`history.back()`; Back goes through a guarded helper
    (`safeBack`/`useGoBack`) that falls back to a parent when there is no in-app history.
  - **`LZFE030`** — no `as never`/`as any`/`as unknown` on a navigation target (a `router.push`/`replace`/
    `navigate` argument, a `useNavigate()` call, or the `href`/`to` of `<Redirect>`/`<Navigate>`/`<Link>`).
    The cast silences typed routes; silenced, a drifted route literal compiles clean and 404s in prod. Keep
    typed routes ON (expo-router `experiments.typedRoutes` / TanStack's route tree) — the rule's config pair.
  - When the **backend drives a navigation** (a pending card, a CTA), the contract carries a **closed kind
    enum**, never a route string — the client owns the `Record<Kind, Href>` map over the generated enum, so a
    new kind is a compile error until mapped and every target is a typed route.
- **Forms & validation** (`LZFE031–032`, warn-tier) — a validation failure always has a surface:
  - **`LZFE031`** — a one-argument `handleSubmit(onValid)` in a ViewModel swallows validation failures (the
    mute submit button: the failure happens *before* the mutation, so `LZFE013/027` never see it). Use the
    spine's `submitOrReveal(form.handleSubmit, onValid, { onInvalid })` — it forces the surface and resolves
    the first invalid field so a tab/step shell can navigate to it — or pass `onInvalid` by hand.
  - **`LZFE032`** — a `<Controller>` render must read `fieldState` and surface the field's error
    (`error={fieldState.error?.message}`); a render that only takes `{ field }` leaves the error invisible.
  - The spine `@lazuli/react` ships the primitives these steer toward: `SessionState`/`toSessionState`,
    `AsyncState`/`Resource`/`combineAsyncStates`, `safeBack`, `requiredParam`, `submitOrReveal`.
- **Contract freshness** — the typed client is pinned to the spec it was generated from: the codegen tail stamps
  `client.gen/.spec-hash` and the doctor compares it against the live OpenAPI document. A moved contract is a
  build-time "regenerate", never a runtime 404.

Routing rules are **error**-tier (correctness), beside the architecture rules — not the warn-tier polish rules.
A badly-wired route **fails the build**.

---

## Build & verify — green before you are done

```
lazuli doctor      # validates Lazuli.toml, then runs the backend (LZ*) + each client's lint/typecheck/test (LZFE*)
```

(Or the explicit commands in `Lazuli.toml [tasks]`.) Never leave the workspace red. If the doctor is red,
**fix the code — never suppress a rule.** A rule fires on a real defect class; the fix *is* the convention.

---

## The boundary (anti-drift — the Rails posture)

The framework ships the **skeleton + enforcement**; this app brings its own **libraries** (a hashing lib, a
payment SDK, a maps client) and its **business logic**, in plain code. No source-gen of behavior, no vendor
adapters in core, no runtime you inherit from. When a need smells like *capability* rather than
*convention + enforcement*, it lives in the app — not the framework.

---

## The package-first law (anti-desync)

This app consumes the framework **only as versioned `Lazuli*` packages and a rebased `eslint-plugin-lazuli`
mirror** — never as source copies. If a need here is framework-shaped (a rule, a spine primitive, a harness
mechanism, anything another Lazuli app would also want), it does **not** get implemented in this repo:
it lands in **lazuli-net first**, ships through the package feed, and arrives here as a version bump whose
doctor fallout you then fix. Writing it here "for now" is how framework code gets lost in time.

Enforcement: declare the framework checkout in `Lazuli.toml` (`[framework] repo = "…"`) and `lazuli doctor`
fails on a stale package version or a drifted plugin mirror; the frontend lint chain re-checks the mirror on
every commit. App-specific code (your domain, your vendors, your copy) stays here, obviously — the law is
about *generic* mechanisms only.

---

## Git discipline

- Stage specific files (`git add <path>`), never `-A`/`.`. One commit per concern; lowercase, present-tense
  imperative messages.
- Workspace green every commit. No `--force`, no history rewrites to escape a failing hook — fix forward.

---

## Canonical conventions (the full constitution)

This file is the distilled operating manual. The complete catalog + rationale lives in the **lazuli-net**
framework repo: `docs/CONVENTIONS.md` (backend) and `docs/FRONTEND-CONVENTIONS.md` (frontend). Ground every
convention fact there, never memory.
