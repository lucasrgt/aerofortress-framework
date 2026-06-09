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
                (await Handle(input, db, ct)).ToHttp())
            .WithName(nameof(Deposit));                        // the operationId the typed client hooks from (LZ0012)
}
```

- **DbContext direct** in the handler. The repository/UoW layer is the clean-architecture
  bloat we cut; the doctor forbids reintroducing it (`LZ0006`).
- **Handlers are HTTP-agnostic.** They return `Result<T>`; the API boundary maps it to a
  status code (`ResultHttpExtensions.ToHttp`). This keeps them unit-testable without a host.
- **Errors carry a code, not just copy.** An `Error` is `(Kind, Code, Message[, Fields])`. `Kind` is the closed
  category that maps to the HTTP status; **`Code`** is a stable, language-neutral, namespaced key — `<module>.<reason>`
  (e.g. `wallets.insufficient_funds`), value objects use `<vo>.<reason>`, field errors `<field>.<reason>` — that the
  **frontend localizes from**; `Message` is a developer hint (English, like a log line), never the copy a user reads.
  The factories require it — `Error.NotFound(code, message)`, `Validation.Check(ok, field, code, message)` — while
  `Collect` inherits the value object's own code.
- **A code is a registry constant, not a literal.** Each module owns a `<Module>ErrorCodes` static class of
  `const string` codes — the readable catalog of what can go wrong there — and every `Error`/`Check`/`FieldError`
  references one (`WalletsErrorCodes.NotFound`), never a bare string. The doctor (`LZ0018`) enforces it, which keeps
  the set **discoverable**: `AddLazuliOpenApi` reflects over the registries and enumerates them into the
  `ErrorBody.code` schema, and `ToHttp` advertises the `ErrorBody` envelope on every endpoint — so the generated
  client is typed on the closed set of codes and the frontend's i18n can be checked for an exhaustive translation of
  each (localization stays the frontend's job — copy has one owner — the backend ships the keys).
- **Rich types carry the semantics.** Prefer `Money`, `Cpf`, `Email` over `decimal`,
  `string`, and let entities own their invariants (see **The domain** below). The type *is*
  the rule — the AI understands it without reading a validator.
- **A module owns both halves of its wiring** — `AddServices(IServiceCollection, IConfiguration)` (its own DI)
  and `Map(IEndpointRouteBuilder)` (its routes) — and is marked `[Module]`. An explicit registry (`Modules.cs`,
  with `AddModules` / `MapModules`) lists every module on both sides. No reflection, no discovery: the registry
  is plain code, and the doctor (`LZ0015` / `LZ0016`) checks the shape and that every `[Module]` is registered.
- **The composition root is three named layers, not a dumping ground.** `Program.cs` stays a thin index —
  `AddLazuli()` + `AddPlatform(config)` + `AddModules(config)`, then the matching `UseLazuli()` /
  `UsePlatform()` / `MapModules()`. **`AddLazuli`** is the framework's universal conventions (OpenAPI,
  enum-as-name JSON), shipped by Lazuli.AspNetCore. **`AddPlatform` / `UsePlatform`** is the app's own
  cross-cutting infrastructure — its `DbContext`, auth, CORS, the framework ports it shares: app-owned (the
  framework can't know your store or your vendors), a *conventional name* so every Lazuli backend reads the
  same, and simply absent when there is nothing cross-cutting to share. It splits **by concern** — one
  `Platform/<Concern>.cs` per concern (recommended vocabulary: Persistence, Security, Observability, Web),
  each a partial of one `Platform` class, composed explicitly (no discovery); a single-concern app is just one
  `Platform.cs`. It grows by adding a concern file, never by fattening `Program.cs`. **`AddModules`** is the
  registry above. A vendor or domain service belongs in the module that owns it (its `AddServices`), never the
  platform. The doctor (`LZ0017`) keeps the index pure: any service registration, pipeline step, or endpoint
  mapping that leaks into `Program.cs` is a build error, redirected to the platform or a module — so the index
  can't rot back into a dumping ground.
- **Authorization is a decision, never an omission.** Every slice's endpoint carries an explicit posture —
  `.RequireAuthorization(…)` or `.AllowAnonymous()` — on its own `Map` chain or on the module's route group
  (`app.MapGroup("/wallets").RequireAuthorization()`); an endpoint with neither is a build error (`LZ0022`).
  Inside the handler, the caller arrives as an injected `ICurrentUser` (claims-based, from `Lazuli.Auth`) and
  the slice does its own ownership/role/org check — a `Handle` that injects `ICurrentUser` and never reads it
  is flagged (`LZ0023`): the signature would claim a check the body doesn't make.
- **Co-located `<Module>.ctx.md`** carries the business "why" — the rules that are not in the
  control flow. One per module (not per slice). Shape + rationale in
  [The ctx.md schema](#the-ctxmd-schema); presence + spine are gated by `LZ0004`.
- **LAW — validation is always inline at the top of the Handle; never extracted to a method.**
  Build the value objects, accumulate with `Validation` (`Check` for an inline condition,
  `Collect` for a value object's verdict, and the shorthands for the recurring shapes —
  `Require(guid, field, code)`, `NotBlank(text, field, code)`, `InRange(value, min, max, field, code)`),
  then `if (validation.Failed) return validation.ToError();`. There is no per-slice judgment and no "extract when it grows": if the
  inline validation ever feels too large, the fix is to push rules into value objects (where the
  rule belongs), never to extract a `Validate` method. Validation's complexity lives in the
  types, so the inline part stays a short list — there is nothing big to extract.

---

## The domain — entities & value objects

A slice is the *operation*; the **entity** and its **value objects** are the *domain* it operates on. Lazuli
keeps the tactical-DDD half that earns its keep — rich, self-validating types — and leaves the apparatus
(repositories, a base class you inherit, an event bus for internal decoupling) out, because each fails one of
the two laws. The result is plain C# that happens to be hard to misuse.

- **Value objects (`[ValueObject]`) are always-valid by construction.** An identity-less domain value
  (`Money`, `Cpf`, `Email`) is immutable, has no public constructor, and is built only through a static smart
  constructor returning `Result<T>` — the `Money.From` shape. Because an invalid instance can never come to
  exist, there is no "validate afterwards" step to forget: any `Money` in the system is already valid. `LZ0013`
  enforces the shape. Value objects live in `BuildingBlocks/` when generic, inside the module when specific.

- **Entities (`[Entity]`) encapsulate state and guard invariants.** An entity has identity and a lifecycle. It
  exposes no public constructor (it is born through a static factory like `Wallet.Open`, and EF rehydrates it
  through a private parameterless one), no public setter (state changes only through intention-revealing
  methods like `Deposit` / `Withdraw`), and a single private invariant funnel — `EnsureValid` returning
  `Result<T>` — that every create and mutate path returns through. So the entity can never be observed or
  persisted broken, and the invariant cannot be bypassed by a slice that forgets to check. `LZ0014` enforces
  the shape. The required private parameterless constructor is exactly the one EF Core materialises through —
  the convention and the ORM ask for the same thing, so encapsulation costs nothing at the storage boundary.
  An entity that a `[Critical]` slice writes also declares its **concurrency posture**: a `[Timestamp]
  public byte[]? RowVersion { get; private set; }` (or `[ConcurrencyCheck]` on a domain field), so two
  concurrent requests can't silently last-write-win each other on exactly the high-stakes operations —
  `LZ0026` (warning-tier) watches for the missing token.

- **Scalar value objects are transparent on the wire.** A scalar `[ValueObject]` that crosses the API
  boundary subclasses `ScalarJsonConverter<TVo, TPrimitive>` (next to the type, pointed at by
  `[JsonConverter]`): it serializes as the primitive it wraps (`Money` as its number, `Slug` as its string),
  invalid wire input fails through the smart constructor as a 400, and `AddLazuliOpenApi` mirrors the
  primitive in the contract schema automatically — so the generated client types it as the primitive, never
  an empty object. The richness is a backend guarantee, not a contract change. (Earned in the hostpoint
  pilot, where every wire-crossing scalar VO needed this by hand.)

- **Where behaviour lives — the split with the slice.** The slice owns *orchestration* and *input validation*
  (inline at the top of `Handle`); the entity and its value objects own *invariants* and *state transitions*.
  A change that cannot fail stays a `void` method (`Wallet.Deposit` — `Money` already guarantees a non-negative
  amount); a change that can violate a rule returns `Result<T>` and funnels through `EnsureValid`
  (`Wallet.Withdraw` refusing an overdraw). This is the validation LAW carried from value objects to entities —
  *push the rule into the type where it belongs* — and none of it is hidden: the entity sits at the module
  root, one read from the slice, in plain C#.

The markers are **pure markers** (like `[Slice]`): no base class, nothing to inherit, no EF semantics. Delete
the doctor and they become inert decoration — the domain still compiles and runs (Law 2).

- **The mark is not optional where the type is persisted or owned.** `LZ0013`/`LZ0014` grade a type that is
  *already* marked — so leaving the mark off used to be a silent way to skip enforcement entirely (the pauta
  port shipped an anemic `User` table this way). `LZ0021` closes that on-ramp from below: a type that is a
  `DbSet<T>` (a table) must be `[Entity]`, and a complex member of an `[Entity]` must be `[ValueObject]` —
  the two places where "what this is" is structurally certain. It does not guess beyond those two signals: a
  DTO, an options bag, or a dead unused record is not forced to wear a mark. See
  [the decision](decisions/lazuli-net-unmarked-domain-type.md).

---

## Project layout

```
src/<App>.Api/
  Program.cs                       # composition root, a thin index: AddLazuli + AddPlatform + AddModules (+ the matching Use*/Map*)
  GlobalUsings.cs
  AppDb.cs                         # one DbContext for every module — the modular monolith's store
  Platform.cs                      # the app's cross-cutting infra: AddPlatform / UsePlatform — app-owned, optional
  Platform/<Concern>.cs            #   ...or a folder, one concern per file (Persistence/Security/Observability/Web): partials of Platform
  Modules/Modules.cs               # the module registry: AddModules + MapModules wire each [Module] (explicit)
  Modules/<Module>/                # a logical bounded context (owns + writes only its own entities)
    <Module>Module.cs             #   the module's wiring root ([Module]): AddServices (its DI) + Map (its routes)
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
| `LZ0006` | No `IRepository` / unit-of-work abstraction in a slice | **shipped** | clean-arch bloat cut |
| `LZ0007` | File ≤ 500 LOC | **shipped** | Rails-repo discipline |
| `LZ0008` | A `[Critical]` slice has a happy **and** a sad journey covering it | **shipped** | high-stakes ops must prove failure E2E |
| `LZ0009` | **Write-ownership**: a module writes only its own entities — a write (Add/Update/Remove/…) on another module's entity is flagged, on a `DbSet` *or* through the untyped `DbContext.Add(entity)` form; reads/joins/calls across modules are free. `.Tests.cs` exempt | **shipped** | modular monolith — write-ownership keeps a context carvable later |
| `LZ0010` | A `[Journey]` covers a `[Critical]` slice — the inverse of `LZ0008`. A journey on a non-critical slice (inert metadata) is flagged: mark it `[Critical]` or use a plain `[E2E]` | **shipped** | journeys were silently inert off a critical slice |
| `LZ0011` | **Tests live in `src/`**: a test method (`[Fact]`/`[Theory]`/`[Unit]`/`[Integration]`/`[E2E]`/`[Journey]`) authored outside a `src/` directory is flagged. The `tests/<App>.Tests` project is infrastructure only (WebApplicationFactory, DB harness, shared bootstrap); unit tests sit next to their slice, journeys under `src/.../Journeys` | **shipped** | keeps tests co-located + doctor-visible; the runner project stays pure infra |
| `LZ0012` | **Endpoint named after the slice**: a `[Slice]`'s `Map` must call `.WithName("<SliceName>")` (or `nameof`). That name is the OpenAPI `operationId` the typed client generates its hook from (`use<SliceName>`), keeping backend↔frontend 1:1. A missing `Map` is LZ0001's concern, not this rule's | **shipped** | the back→front naming seam — a forgotten name drifts the generated client |
| `LZ0013` | **Value object always-valid**: a `[ValueObject]` is immutable, exposes no public constructor and no public setter, and is built only through a static smart constructor returning `Result<T>` (the `Money.From` shape) — so an invalid instance can never exist | **shipped** | anemic domain — a value must be unconstructable when invalid, not validated after the fact |
| `LZ0014` | **Entity encapsulation + invariant funnel**: an `[Entity]` exposes no public constructor (born via a factory, rehydrated by EF via a private one) and no public setter, and declares a private `EnsureValid()` (or `Validate()`) returning `Result<T>` that every create/mutate path returns through | **shipped** | anemic domain — invariants must live on the entity, unbypassable (the sample's own `Wallet` was a setter bag) |
| `LZ0015` | **Module shape**: a `[Module]` is a static class declaring a public static `AddServices(IServiceCollection, IConfiguration)` (its own DI) and a public static `Map(IEndpointRouteBuilder)` (its routes) — it owns both halves of its wiring | **shipped** | the composition root drifts into a dumping ground; a module's DI scatters across `Program.cs` |
| `LZ0016` | **Module registered**: every `[Module]`'s `AddServices` and `Map` are actually called in the explicit registry (`AddModules` / `MapModules`) — a compile-time reachability check, no reflection | **shipped** | generating a module and forgetting to wire it — a silent 404 instead of a build error |
| `LZ0017` | **Composition root is an index**: the top-level statements (`Program.cs`) wire only `AddLazuli` / `AddPlatform` / `AddModules` and the matching `UseLazuli` / `UsePlatform` / `MapModules`; any other service registration (`IServiceCollection`), pipeline step, or endpoint mapping (`Use*`/`Map*`) there is flagged and redirected to the platform or a module | **shipped** | the index rots into a dumping ground — infra creeps back into `Program.cs`, drifts, and bit-rots there unwatched |
| `LZ0018` | **Error code is a registry constant**: the `code` passed to an `Error` factory / `Validation.Check` / `Validation.Add` / `FieldError` must reference a `const` on a class named `*ErrorCodes` (e.g. `WalletsErrorCodes.NotFound`), never an inline literal | **shipped** | a code invisible to reflection can't be enumerated into the OpenAPI contract — it would reach a user untranslated; the registry keeps the set discoverable + typed end-to-end |
| `LZ0019` | **Error code constant is used**: every `const` on an `*ErrorCodes` registry must be referenced by an `Error`/`Validation` call somewhere in the compilation — the reverse of `LZ0018` | **shipped** | drop a flow and leave its code behind → a dead code still ships in the OpenAPI enum + the i18n catalog; this keeps the registry the exact, live set (no orphans) |
| `LZ0020` | **A `[Journey]` asserts its post-condition**: the journey test body must contain an assertion (xUnit `Assert`, FluentAssertions `Should`, or a `Verify`/`Expect` helper) — the depth rung above `LZ0008` (which proves the journey exists). Warning-tier; textual over the journey AdditionalFiles | **shipped** | an assertion-free journey is theater — it runs the path and proves nothing |
| `LZ0021` | **A persisted or entity-owned type declares its mark**: a `DbSet<T>` whose `T` is unmarked must be `[Entity]`; a complex member of an `[Entity]` (after one nullable/collection layer) that is neither `[ValueObject]`, `[Entity]`, nor an enum must be `[ValueObject]`. The rung *beneath* LZ0013/LZ0014, which only fire on already-marked types — so an unmarked domain type would otherwise escape every encapsulation check. Does not flag dead/unused types or framework types | **shipped** | the pauta port shipped an anemic `User` (a table, no `[Entity]`) past a green doctor — the omission of the mark was the evasion |
| `LZ0022` | **An endpoint's authorization is a decision, never an omission**: every `[Slice]` carries an explicit posture — `.RequireAuthorization(…)` or `.AllowAnonymous()` — on its own `Map` chain or on the route group the module mounts it on (`app.MapGroup("/x").RequireAuthorization()`). `.AllowAnonymous()` is not a loophole: it is the same decision, made visible and reviewable. A missing `Map` is LZ0001's concern | **shipped** | the classic silent failure — a new endpoint ships open because nobody decided anything |
| `LZ0023` | **An injected `ICurrentUser` is consulted**: a `[Slice]` `Handle` that takes an `ICurrentUser` parameter and never reads it is flagged — the caller was wired in to scope the operation, so an unread parameter is a missing ownership/role/org check, not dead code. Consult the caller or remove the parameter | **shipped** | the signature claims an authorization posture the body doesn't have — the check was meant and then silently dropped |
| `LZ0024` | **Raw SQL never absorbs runtime values as text**: a `*Raw` EF call (`FromSqlRaw`, `ExecuteSqlRaw`/`Async`, `SqlQueryRaw`) whose SQL argument interpolates or concatenates a non-literal is flagged — the SQL-injection shape. The fix is one token: `FromSql`/`ExecuteSql`/`SqlQuery` take the same interpolated string and turn every hole into a `DbParameter`. Constant SQL through `*Raw` stays legal | **shipped** | injection hides in the one raw query an app eventually needs — the safe twin costs nothing |
| `LZ0025` | **A held `Result<T>` is checked before it is unwrapped**: reading `.Value`/`.Error` on a result stored in a local or parameter with no earlier outcome consult in the same member (`IsSuccess`/`IsFailure`, an `is { IsSuccess: … }` pattern, or a `Validation.Collect` fold) is flagged — on the wrong outcome the access throws. Unwrapping *inline* on a fresh construction (`Money.From(10m).Value` in a seed/test) stays legal: it is the deliberate known-valid idiom | **shipped** | the type's number-one misuse — `result.Value` straight through, an exception where an `Error` was supposed to flow |
| `LZ0026` | **A `[Critical]` write declares its concurrency posture**: a `[Critical]` slice whose `Handle` saves changes against an entity with no visible concurrency token (no `[Timestamp]`/`[ConcurrencyCheck]` member, no `RowVersion` property) is flagged — concurrent requests are last-write-wins on exactly the operations marked high-stakes. Warning-tier: fluent-only configuration is invisible to the doctor; name the property `RowVersion` or tune the severity | **shipped** | the sample's own `Deposit` raced: two concurrent deposits, one balance silently lost |

The doctor catches **structural drift**, not logic correctness. Correctness is tests +
review. Expect it to reclaim the *structural* fraction of drift, not 100%.

Beside the LZ* rules the doctor ships a **security floor** for the built-in .NET analyzers
(`buildTransitive/lazuli.globalconfig`): a curated CA* set — dropped `CancellationToken`
(CA2016), insecure deserialization (CA23xx), broken crypto / disabled cert validation /
deprecated TLS (CA53xx) — raised to error tier. Same removability story: opt out per-project
with `<LazuliSecurityAnalysis>false</LazuliSecurityAnalysis>`, or override a single rule from
your own `.globalconfig` at a `global_level` above 50. The libraries hold themselves to the
same floor via `build/Lazuli.Library.props`.

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
(`Result<T>`, `[Slice]`, `[ValueObject]`, `[Entity]`), a slice scaffold (`rails g`-style,
planned), a knowledge-graph dump for the LLM (planned).

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
