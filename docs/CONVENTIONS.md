# Lazuli (.NET) — Conventions & Constitution

Lazuli is the **opinionated .NET convention bundle**: a standardized vertical-slice
architecture + a build-time harness (the doctor) + an ai-context discipline, so an LLM has
less to decide and what it writes is enforced. It is the *Rails mindset in .NET* — the
mentality (convention over configuration, quality control, semantic density), **not** the
mechanism (no runtime metaprogramming, no language).

The goal is **not "less code"**. The goal is **semantic density**: each unit carries more
meaning per token for the AI. Token savings follow from that — they are not the target.

---

## The gift, and the boundary

Lazuli's gift is **the scaffolding and the harness** — the opinionated project shape, the generators
(`lazuli new` / `g`), and the doctor that enforces the conventions. That is the whole product. It is
**not** a platform that absorbs every integration, vendor, or business rule.

This is the Rails posture. Rails ships convention + the framework; the rest is **gems** the app
chooses and **business logic** the app writes. Lazuli is the same — it ships the skeleton and the
enforcement; each project brings its own libraries (a hashing lib, a payment SDK, a maps client) and
its own domain rules, in plain idiomatic C#. The framework never grows a feature to meet one app's
specific need.

The boundary is deliberate — the lesson of two failures we will not repeat:

- **Lazuli-1** (the language) grew into a gargantuan apparatus — per-vendor adapters, a runtime, a
  compiler — and shipped to nobody.
- **aerocoding** became a "unification engine," building artifacts for features that did not exist.

Both died of *trying to absorb the world*. So the test for any proposed framework feature is one
question: **is it generic across projects and proven by real use?** If it smells like the framework
conforming to one app — a vendor adapter, a domain rule, a bespoke capability — it is out; it lives in
the app. The framework earns new surface from evidence, never from the ambition to be comprehensive.

---

## The two laws (read before adding anything)

1. **Stranger-maintainable.** The output is always plain, idiomatic C# that a .NET dev who
   has never heard of Lazuli can read and maintain. (Lazuli-the-language died failing this:
   its real code was generated Go.)
2. **Doctor-removable.** `dotnet remove` the analyzer and the project still **compiles and
   runs** — you only lose enforcement. The harness is wire, not apparatus.

Any feature that fails both laws — hidden source-gen of behavior, a DSL, a runtime you
inherit from, magic discovery — is **out**, by construction.

---

## The slice convention

One feature = **one file** (maximal locality: the agent reads the whole feature in one
read). The canonical shape (enforced by `LZ0001`):

```csharp
[Slice]                                  // pure marker; module is derived from the namespace
public static class Deposit
{
    public record Input(/* ... */);                            // contract in — visible
    public record Output(/* ... */);                           // contract out — visible

    public static async Task<Result<Output>> Handle(           // the essence — visible
        Input input, AppDb db, CancellationToken ct)
    {
        // DbContext direct. No repository, no unit-of-work, no mapper profile.
        // Behavior always lives here, never hidden.
    }

    public static void Map(IEndpointRouteBuilder app) =>       // transport — one thin line
        app.MapPost("/deposit", async (Input input, AppDb db, CancellationToken ct) =>
            (await Handle(input, db, ct)).ToHttp());
}
```

- **DbContext direct** in the handler. The repository/UoW layer is the clean-architecture
  bloat we cut; the doctor forbids reintroducing it (planned rule).
- **Handlers are HTTP-agnostic.** They return `Result<T>`; the API boundary maps it to a
  status code (`ResultHttpExtensions.ToHttp`). This keeps them unit-testable without a host.
- **Rich types carry the semantics.** Prefer `Money`, `Cpf`, `Email` over `decimal`,
  `string`. The type *is* the rule — the AI understands it without reading a validator.
- **Endpoint registration is one explicit line** in `Program.cs`. No reflection, no
  discovery.
- **Co-located `<Module>.ctx.md`** carries the business "why" — the rules that are not in the
  control flow. One per module (not per slice). Shape + rationale in
  [The ctx.md schema](#the-ctxmd-schema); presence + spine are gated by `LZ0004`.
- **LAW — validation is always inline at the top of the Handle; never extracted to a method.**
  Build the value objects, accumulate with `Validation` (`Check` for an inline condition,
  `Collect` for a value object's verdict), then `if (validation.Failed) return
  validation.ToError();`. There is no per-slice judgment and no "extract when it grows": if the
  inline validation ever feels too large, the fix is to push rules into value objects (where the
  rule belongs), never to extract a `Validate` method. Validation's complexity lives in the
  types, so the inline part stays a short list — there is nothing big to extract.

---

## Project layout

```
src/<App>.Api/
  Program.cs                       # composition root: wire DI, map modules, seed, run. Zero logic.
  GlobalUsings.cs
  AppDb.cs                         # one DbContext for every module — the modular monolith's store
  Modules/<Module>/                # a logical bounded context (owns + writes only its own entities)
    <Module>Module.cs             #   groups the module's slices under one route prefix
    <Module>.ctx.md               #   the module's "why" — Boundaries + Design notes (see schema)
    <Entity>.cs                   #   entities live at the module root — domain, not operations
    Slices/<Name>.cs              #   one slice = one operation (Input/Output/Handle/Map)
    Slices/<Name>.Tests.cs        #   tests co-located with the slice they exercise
  BuildingBlocks/                  # shared value objects (Money, Cpf) — generic, owned by no module
  Journeys/<Name>Journey.Tests.cs  # cross-module E2E journeys (required for [Critical] slices)
tests/<App>.Tests/                 # thin runner: globs the co-located *.Tests.cs, provides TestApp
```

- **Slices live in `Slices/`; the domain (entities, DbContext) lives at the module root.** The split
  keeps a module with ten operations from swallowing what is domain. The folder is `Slices/`, not
  `Features/` — one term, matching `[Slice]`, `lazuli g slice`, and the rules.
- **Value objects go in `BuildingBlocks/`** when generic (a leaf type couples nothing), or inside the
  module when module-specific.
- **The namespace is `<App>.Api.Modules.<Module>` — the `Slices/` subfolder is not in it.** Folders
  organize files; the namespace is the module.
- **Modular monolith: one `AppDb`, modules are bounded contexts by convention.** Every module shares one
  DbContext, so a read can join across modules in-process — a dashboard (the host home) is one query, not a
  composition. What keeps a module liftable later is cheap discipline, not isolation: it **writes only its
  own entities** (LZ0009) and references other modules **by id, never an EF foreign key**. Reads / joins /
  in-process calls across modules are free; a cross-module *effect* goes through the owner's service or a
  job, and domain events + an outbox are reserved for genuinely-async external integrations (a payment
  webhook), not internal decoupling. Extracting a module later re-platforms only its cross-module reads — a
  move, not a rewrite. (See `lazuli-net-modular-monolith` in the decisions archive.)
- The scaffold (`lazuli new`) and generators (`lazuli g module` / `g slice`) produce exactly this shape.

---

## Real-time — hubs (opt-in)

Real-time is **opt-in**, the way Rails 8 dropped the default `channels/` folder: a fresh app has no hub, so
it carries no transport it doesn't use. When a feature needs server-pushed liveness — live messages, typing,
presence — `lazuli g hub <Module> <Name>` scaffolds a SignalR hub under `Modules/<Module>/Realtime/<Name>Hub.cs`.

- **A hub is wire, not logic — the same law as an endpoint.** A hub method persists nothing itself: it calls
  the matching slice (the one source of the write + its rules) and then fans the result out to the room.
  Ephemeral signals (typing, presence) ride the hub and never touch the database. SignalR is first-party
  ASP.NET Core — wire, not a vendor; the harness stays doctor-removable.
- **The caller comes from `Context.User` via `ClaimsCurrentUser`**, not the request-scoped `ICurrentUser` — a
  hub method runs outside the HTTP pipeline, where `IHttpContextAccessor.HttpContext` is not reliably set. The
  token rides the query string for hub paths (a WebSocket can't send an `Authorization` header); the generator
  prints the `Program.cs` wiring.
- **Webhook ≠ hub.** An inbound provider callback (a payment webhook) is a normal slice with a route, not a
  hub. A hub pushes to *your* connected users; it never receives third-party callbacks.
- **Ephemeral state is not an entity.** Presence and typing are in-memory (a singleton registry) on a single
  instance; scaling to several instances swaps in a backplane + shared store (Redis, TTL heartbeat) — one line
  at the composition root, the only place that changes. No database write per heartbeat.

---

## The ctx.md schema

Every module carries one `<Module>.ctx.md` — the home for the business *why* the code cannot show. It
is **not** a mirror of the code: anything recoverable from the types, the tests, or the routes is
duplication, and duplication rots. (corbanx's 16-section feature doc was the reference; we graded its
sections against that one test and kept about a third.)

**Spine — required, gated by `LZ0004`:**

- `# <module>` + a 1–3 line purpose paragraph.
- `## Boundaries` — Inside / Outside (and non-goals). Where the module's seams are, and where *not* to
  add code. The highest-value section: it stops scope leak.
- `## Design notes` — the invariants and their rationale: the non-obvious rules and *why* they hold (the
  "gems"). Performance, security, and cross-module side-effects fold in here when they carry a why.

**Optional — add when earned:**

- `## Wiring` — integration dependencies not obvious from the imports (e.g. "org provides tenancy").
- `## Not yet ported` — deliberate absences, so a reader knows they are intentional, not forgotten.

**Excluded by law** (recoverable → would duplicate and drift): data models (the entities *are* the
model), DTO/contracts (the `Input`/`Output` records *are* the contract), route tables (the `Map` *is*
the route), error tables (`ToHttp` owns the mapping), test matrices (the `*.Tests.cs` *are* the matrix),
request/response examples, code-pointer file lists, and change logs (procedence is narrative, not
normative — decisions go to an ADR + git, never the ctx). **No Mermaid / flow diagrams** either: a
non-linear flow is explained in prose in the Design notes.

`LZ0005` keeps it honest by **freshness as citation-resolution**, not mtime: a ctx that names a slice or
type which no longer exists is stale. An mtime rule would be wrong here — because the ctx does not
duplicate the code, adding a field must *not* force a ctx edit.

---

## Testing — co-located, categorized, host-provided

Tests live **next to the slice** (`Deposit.Tests.cs` beside `Deposit.cs`): the app assembly
excludes them (`<Compile Remove>`) so no test dependency ships; a thin per-app test project
compiles them via glob. No mirror-the-architecture test tree.

- **Categories are a closed vocabulary** (`Lazuli.Testing`): `[Unit]` (fast, no infrastructure),
  `[Integration]` (real infrastructure — database, HTTP), `[E2E]` (cross-module journey). Each
  maps to the xUnit trait `Category=<kind>`, so `dotnet test --filter Category=Unit` and the
  doctor read the same single signal — the attribute on the test.
- **The integration host is `LazuliWebTest<TProgram>`.** It boots the real app and hands you one
  hook, `SwapStores(IServiceCollection)`, to reconfigure services for the test. The base holds
  **no database opinion and drags no provider dependency**. Two paths, both the app's to choose:
  - *Fast and isolated* — reference `Lazuli.Testing.InMemory`, call
    `services.UseIsolatedInMemory<WalletsDb>()`.
  - *A real database (e.g. Testcontainers Postgres)* — register your own provider in `SwapStores`
    and manage its lifetime with xUnit's `IAsyncLifetime`; the in-memory package never enters the
    graph.
- **Assertions and mocking are the app's free choice** — the kit ships and mandates none
  (FluentAssertions, Shouldly, NSubstitute, xUnit-native all work). The one deliberate coupling is
  the **runner: xUnit** (the categories are xUnit traits).
- `LZ0003` makes every slice carry a co-located test; the build fails without one.
- **`[Critical]` slices must prove failure, not just success.** A `[Slice] [Critical]` operation —
  one where failure costs money or trust — must be covered end-to-end on **both** paths: a happy
  journey and at least one sad journey, each declared with `[Journey(typeof(Slice),
  JourneyPath.Happy|Sad)]` on the `[E2E]` test. `LZ0008` enforces both exist. The sad journey asserts
  the failure status **and** that no state changed — the no-partial-state property only an end-to-end
  test proves. Mark `[Critical]` sparingly (the few high-stakes operations); the broader set of flows
  stays human-curated, since "which flow matters" lives in the domain, not the code.
- **`[Journey]` is not a category — it is the proof of a `[Critical]` slice, both ways.** `[Unit]` /
  `[Integration]` / `[E2E]` are the categories (pick one); `[Journey]` is a *marker on an `[E2E]` test*
  binding it to the critical slice it covers. The relation is enforced in both directions: `LZ0008`
  (critical slice ⟹ has happy + sad journeys) and `LZ0010` (a journey ⟹ covers a critical slice). So a
  `[Journey]` exists exactly for a critical operation. A cross-module `[E2E]` that proves a whole *flow*
  rather than one critical slice carries **no** `[Journey]` — e.g. the onboarding traversal. Naming follows:
  **`*Journey.Tests.cs`** = critical-slice proofs, **`*Flow.Tests.cs`** = plain `[E2E]` flows.
- **Criticality tracks the dangerous *sad path*, not complexity.** The test for `[Critical]` is: would
  the failure path *failing silently* cause real harm — an auth bypass, money moving wrong, a takeover, a
  boundary leak? `Login` is trivial code but critical (a one-character inversion is an auth bypass);
  `CompleteOnboarding`, which only flips an `IsActive` flag with no guard, is **not** — its only failure is
  not-found. Don't mark a slice critical because it sounds important or sits on an important flow.
- **A generator that scaffolds a `[Critical]` slice ships its journeys too.** `lazuli g auth`
  (`Login`/`Refresh`/`Register`) and every augment (`g auth:otp` → `VerifyPhone`) emit the matching
  `Journeys/*.Tests.cs` beside the slice — otherwise the generated app fails `LZ0008` on its first
  `doctor`. The rule is self-enforcing: `LZ0008` is exactly what makes "the augment forgot the journey"
  impossible to ship. When a journey can't be driven purely over HTTP (an SMS or email code never crosses
  the wire), the generated journey layers a **capturing provider** over the booted app through the
  `SwapStores` / `WithWebHostBuilder` hook — the same test seam, with zero production change.

---

## The doctor — rule catalog

Every rule is born from observed pain (the corbanx pilot + the Rails-repo discipline),
never speculation. Keep it minimal; add only on real drift.

| Rule | Enforces | Status | Origin |
|------|----------|--------|--------|
| `LZ0001` | Slice conformance (static class; nested `Input` and `Output`; `Handle → Task<Result<T>>`; `Map`; ordered Input → Output → Handle → Map) | **shipped** | corbanx: "deviated from architecture" |
| `LZ0002` | Endpoint stays thin (a route handler is an expression-bodied lambda or method group, never a statement block) | **shipped** | corbanx: "business logic in routes" |
| `LZ0003` | Every slice has a co-located `<Slice>.Tests.cs` | **shipped** | corbanx: "didn't write tests" |
| `LZ0004` | Every module has a `<Module>.ctx.md` with the spine (`## Boundaries` + `## Design notes`, non-empty) | **shipped** | corbanx: "forgot to write ai.context" |
| `LZ0005` | `.ctx.md` is fresh — a backticked identifier it cites resolves in source or a reference (not mtime) | **shipped** | corbanx: "ai.context drifted" |
| `LZ0006` | No `IRepository` / unit-of-work abstraction in a slice | planned | clean-arch bloat cut |
| `LZ0007` | File ≤ 500 LOC | planned | Rails-repo discipline |
| `LZ0008` | A `[Critical]` slice has a happy **and** a sad journey covering it | **shipped** | high-stakes ops must prove failure E2E |
| `LZ0009` | **Write-ownership**: a module writes only its own entities — a `DbSet` write (Add/Update/Remove/…) on another module's entity is flagged; reads/joins/calls across modules are free. `.Tests.cs` exempt | **shipped** | modular monolith — write-ownership keeps a context carvable later |
| `LZ0010` | A `[Journey]` covers a `[Critical]` slice — the inverse of `LZ0008`. A journey on a non-critical slice (inert metadata) is flagged: mark it `[Critical]` or use a plain `[E2E]` | **shipped** | journeys were silently inert off a critical slice |
| `LZ0011` | **Tests live in `src/`**: a test method (`[Fact]`/`[Theory]`/`[Unit]`/`[Integration]`/`[E2E]`/`[Journey]`) authored outside a `src/` directory is flagged. The `tests/<App>.Tests` project is infrastructure only (WebApplicationFactory, DB harness, shared bootstrap); unit tests sit next to their slice, journeys under `src/.../Journeys` | **shipped** | keeps tests co-located + doctor-visible; the runner project stays pure infra |

The doctor catches **structural drift**, not logic correctness. Correctness is tests +
review. Expect it to reclaim the *structural* fraction of drift, not 100%.

---

## The self-harness — framework-dev only, never shipped

The libraries hold *themselves* to a standard, the way the Rails repo does. This is the
**self-harness** (`Lazuli.SelfHarness`): analyzers that run only on lazuli-net's own source.

It is **closed to this repo**: `IsPackable=false`, referenced with
`ReferenceOutputAssembly="false"`, never packaged, and **never part of the production CLI or
the user-facing `Lazuli.Doctor`**. It is the `lazuli` vs `lazuli-dev` split — framework-dev
tooling stays out of the published surface, always.

| Rule | Enforces | Applies to |
|------|----------|------------|
| `LZSELF001` | File at or under 500 lines | `Lazuli.*` libraries |
| `LZSELF002` | No tracking codes or scratch markers in comments (TODO, FIXME, HACK, XXX, capital-letter/number codes) | `Lazuli.*` libraries |
| `CS1591` (built-in) | Every public member carries XML documentation | `Lazuli.*` libraries |

Settings live once in `build/Lazuli.Library.props`, imported by every `Lazuli.*` project. The
bar is code a Microsoft .NET MVP would read and be proud of: gold-standard docs, no junk,
small files. The **user's app is not** held to these — `LZSELF*` never touches a generated
user project.

---

## Scope — and non-goals

**In:** the standard project shape, the doctor, the ai-context discipline, the thin wire
(`Result<T>`, `[Slice]`), a slice scaffold (`rails g`-style, planned), a knowledge-graph
dump for the LLM (planned).

**Out (non-goals), by decision:**
- **No source-gen of behavior.** Plumbing only, if ever — and not in v0. (It is a
  mini-compiler: the Lazuli-2 vector.)
- **No vendor adapters in the core.** MercadoPago/Twilio/etc. are written *following* the
  component standard, in separate repos — the kit ships the standard, not the plugins.
- **No frontend/UI generation, no realtime, no multi-app sprawl.** (The aerocoding-2
  failure modes — designed out.)
- **No runtime framework you inherit from.** Conventions + analyzer, not base classes.

When a proposal smells like capability instead of convention+enforcement, it is a scope
violation. Reject in line.
