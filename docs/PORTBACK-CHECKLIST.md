# Port-back checklist — what the hostpoint pilot earned that the framework must own

**Created:** 2026-06-08. **Status:** in progress (branch `framework/portback-audit`).

AeroFortress grew by dogfooding in the **hostpoint** pilot
(`c:/Users/lucas/dev/dotnet-projects/hostpoint-monorepo`) without a disciplined port-back. This
checklist is the audit of every generic mechanism the pilot accumulated that the framework should own
but doesn't — plus the framework claims that aren't actually shipped. App-specific material (per the
80/20 scope discipline) is listed under "Correctly app-owned" and is deliberately NOT ported.

Headline: the backend **core** is real and complete (17 `AF00xx` analyzers + base runtime lib). What
never came back is the **workspace/monorepo layer**, the **frontend harness foundation** (the "wired
guarantee"), and a few **claimed-but-absent** rules. The pilot is carrying the framework.

Legend: `[ ]` todo · `[~]` partial · `[x]` done · scope = FRAMEWORK-GAP / AMBIGUOUS / APP-SPECIFIC.

## Progress — 2026-06-08 (branch `framework/portback-audit`)

**Shipped + tested this session (9 gaps closed; full solution green — Doctor 67, Cli 22, SelfHarness 5,
Abstractions 4, Sample 21, SDK tools 28, eslint plugin all):**
AFFE015 (no-router-replace-in-effect, ported) · AFFE008 (endpoint-coverage tool, was claimed-but-absent) ·
AFFE-JOURNEY-002 (e2e terminal-depth) · tenancy scaffold hardening (encapsulated OrgId + metadata stamp) ·
AF0006 (no-repository) · AF0007 (file ≤500) · AF0020 (journey asserts its post-condition) ·
`OrderedLifecycle<TState>` helper · `AeroFortress.toml` scaffolded by `af new` + read/validated by the doctor.

**Deferred follow-up (each blocked on a real dependency, not skipped):**
the frontend generator (unblocks monorepo scaffold, `af gen client`, the e2e-support harness home +
Tier A2 skip-in-gate) · the Tier B4 seam-rule feasibility spike · `@aerofortress/react` publish (needs a build
pipeline + an outward npm/registry step) · Tier C mutation lane (needs a `[Critical]` journey set). The
AMBIGUOUS items (IUserScoped, etc.) stay parked per the framework's own ≥3-pilot rule.

---

## P1 — broken promise / claimed-but-absent

- [~] **Scaffold a real project + `AeroFortress.toml`.** _Done:_ `af new` now scaffolds `AeroFortress.toml`
  (`templates/aerofortress-app/AeroFortress.toml`, substituted from the project name), and the CLI finally READS it —
  `src/AeroFortress.Framework.Cli/AeroFortressManifest.cs` validates `[workspace]`/name + that declared backend/core paths exist,
  wired into `af doctor` (missing = notice, broken = failure) with 4 tests. _Still pending (tied to the
  deferred frontend generator):_ scaffolding the monorepo plumbing (`package.json` workspaces, `turbo.json`,
  `lefthook.yml`, `clients/`) and making `AeroFortress.toml` *generate* the workspace/turbo config.
  - hostpoint: `AeroFortress.toml`, root `package.json`, `turbo.json`, `lefthook.yml`
- [ ] **`af gen client` (the "wired guarantee").** The frontend harness rests on a generated
  typed client so an invented endpoint is a `tsc` error — but **orval is not even a dependency**, there
  is no `orval.config.ts`, no `af gen client`. The AFFE rules police a client nothing generates.
  _FRAMEWORK-GAP._ (`docs/decisions/aerofortress-framework-frontend-harness.md` §3 vs `frontend-sdk/tools/generate.mjs:47`)
- [x] **`AFFE008` endpoint-coverage — claimed "shipped", absent.** _Done:_ implemented as
  `frontend-sdk/tools/endpoint-coverage.mjs` (pure `extractHooks` + `checkEndpointCoverage` core + CLI
  tail) with a vitest twin; the doc claim now points at the real tool. _was FRAMEWORK-GAP (truthfulness)._
- [x] **`AFFE015` no-router-replace-in-effect — real rule, not ported + ID collision.** _Done:_ ported
  to `frontend-sdk/packages/eslint-plugin/index.cjs` (+ self-test), claimed AFFE015 for the navigation
  rule, moved the planned "no orphan placeholder" to AFFE016 (`docs/FRONTEND-CONVENTIONS.md`). A
  battle-tested rule in the pilot (`hostpoint/clients/eslint-plugin-aerofortress/index.cjs:438`, born from a
  shipped infinite-navigation bug) is absent from the framework plugin, and its ID collides with a
  *planned, different* AFFE015 ("no orphan placeholder") at `docs/FRONTEND-CONVENTIONS.md:275`.
  _FRAMEWORK-GAP._
- [ ] **e2e harness has no framework home.** `requireBackend`/`requireSeed`/`global-setup`/
  `playwright.config` are generic, yet absent from the framework — even though `e2e-doctor.mjs:67`
  *reads* those function names. _FRAMEWORK-GAP._ (`hostpoint/clients/hostpoint-app/e2e/support/*`)

## P2 — generic mechanism carried by the pilot, should graduate

- [ ] **The 5 `affe-*.mjs` doctor scripts are reimplementations, not SDK consumers.** They re-derive
  `walk`/regex/`bucket`/`aggregateReport` inline; the framework already exposes the pure cores
  (`checkJourneyParity`, `checkE2e`, `aggregateReport`, `i18n-parity`, `error-code-coverage`).
  _FRAMEWORK-GAP (anti-drift)._ (blocked on `@aerofortress/*` being consumable)
- [ ] **Publish `@aerofortress/react`.** Unpublished → the pilot forks `AsyncState`/`Resource` locally and
  they are drifting. _FRAMEWORK-GAP._ (npm publish is an outward release step — prepare, don't publish)
- [x] **Harden the tenancy scaffold (`af g auth`).** _Done:_ `ITenantScoped.OrgId` is now `{ get; }`
  (read-only); `TenantDbContext` stamps via EF property metadata (`entry.Property(...).CurrentValue`),
  not the CLR setter, + exposes `CurrentOrgId`; `User.OrgId` is `{ get; private set; }`. The shipped
  `TenantIsolation.Tests` already assumed this (seeds without OrgId, asserts the stamp), so the change
  aligns the entity/interface with the test. Did NOT port `IParticipation` (1-pilot, AMBIGUOUS).
  _Follow-up:_ the `crud` test scaffold seeds `new() { OrgId = org }` (cross-org isolation), which needs
  a settable OrgId — a seed-helper is required before crud entities can also go `private set`.
- [ ] **`af` CLI conductor + `af g view` front-door.** `MONOREPO-ARCHITECTURE.md` promises
  `build`/`gen:client` reading `[tasks]`; the ViewModel scaffold exists as a `.mjs` but isn't wired
  into the .NET CLI. _FRAMEWORK-GAP._
- [x] **`OrderedLifecycle<TState>` helper.** _Done:_ `src/AeroFortress.Framework.Abstractions/OrderedLifecycle.cs` —
  `Reached` (cursor ≥ step) + `Advance` (no-skip/no-regress), generic over `TState : struct, Enum`, with a
  new `tests/AeroFortress.Framework.Abstractions.Tests` project (4 green, wired into `AeroFortress.Framework.slnx`). Replaces the
  byte-for-byte Host/Traveler duplication.
- [x] **`AF0006` no-repository + `AF0007` file ≤500 (user apps).** _Done:_ both shipped as Roslyn
  analyzers (`analyzers/AeroFortress.Framework.Doctor/NoRepositoryAnalyzer.cs`, `FileSizeAnalyzer.cs`) with twin tests
  (7 green); `docs/CONVENTIONS.md` flipped planned→shipped. _was FRAMEWORK-GAP (documented commitment)._

## Journey-depth (decision `aerofortress-framework-journey-depth-enforcement.md`)

- [x] **Tier A1 `AFFE-JOURNEY-002`** — terminal-depth in `e2e-doctor.mjs` (+ tests). _done this session._
- [ ] **Decision-doc fixes** — `AF0011` collides with `TestInfraPurityAnalyzer` → renumber to `AF0020`;
  refresh the stale "today" baseline (A1 is now implemented).
- [x] **Tier B3 `AF0020`** (was AF0011) — _Done:_ `analyzers/AeroFortress.Framework.Doctor/JourneyAssertionAnalyzer.cs`
  flags a `[Journey]` whose body asserts nothing (warning-tier, textual over the journey AdditionalFiles,
  lenient on custom asserters); 4 tests; `CONVENTIONS.md` lists it shipped.
- [ ] **Tier A2 `AFFE-E2E-SKIP-IN-GATE-001`** — a skipped gate-class flow must fail in gate mode.
  _Deferred:_ couples to the e2e-support harness home (requireBackend/requireSeed), which is frontend-
  scaffold territory (the frontend generator is deferred). Cheap once that home exists.
- [ ] **Tier B4 `AFFE-JOURNEY-SEAM-001`** — lifecycle-advancing `[Critical]` slice needs a frontend
  flow proving the post-transition navigation. _Deferred:_ the grade itself flagged this needs a
  cross-artifact feasibility spike (does a frontend guard read a backend lifecycle?) before committing.
- [ ] **Tier C** — Stryker mutation-score lane (doctor consumes the artifact). _Deferred — needs a
  `[Critical]` journey set to be meaningful._

## Re-sweep 2026-06-09 — new findings (not previously tracked)

A second audit pass over the pilot, after the AF0022–26 / AFFE021–22 wave. Claims verified against the
framework source (several apparent gaps turned out already owned: `ClaimsCurrentUser` ships in
`AeroFortress.Framework.Auth`; refresh rotation + theft detection and `TenantDbContext` ship as `af g auth`
scaffold templates — the AeroFortress way, app-owned by construction).

**Progress — 2026-06-09 (same day):** all six findings attacked. Scalar VO transparency →
`ScalarJsonConverter<TVo,TPrim>` (Abstractions) + automatic schema mirroring in `AddAeroFortressOpenApi`,
dogfooded on the sample's `Money`. Postgres harness → `AeroFortress.Framework.Testing.Postgres` (`PostgresTestDatabase`).
Rate-limit bridge → `RejectAsAeroFortressError()` + the framework's `PlatformErrorCodes`. Session seam →
`createSessionSeam` + `useSession` in `@aerofortress/react` (cache reset paired by construction). Error-copy
bridge → `apiErrorCode`/`apiErrorCopy` in the spine (structural i18n). Mutator → `tools/client-scaffold.mjs`
(mutator + orval config, AFFE020-conformant). Pilot mirror rebased to plugin 0.4.0 with AFFE021/022 adopted —
zero new errors, full lint chain + typecheck + 125 tests green. Still open from the reverse-drift item: the
app adopting the spine unions themselves (blocked on `@aerofortress/react` publish, tracked in P2).

### Backend

- [x] **Scalar VO wire transparency has no framework mechanism.** Every scalar `[ValueObject]` the
  pilot adds needs a hand-written `[JsonConverter]` + a per-type branch in the `Web.cs` OpenAPI
  schema transformer (`hostpoint/src/Hostpoint.Api/Platform/Web.cs:52`, `Money.cs:54`, `Slug.cs:46`)
  or the contract emits an empty object and the generated client breaks. The mechanism is generic:
  a `AeroFortress.Framework.AspNetCore` schema-transformer that maps a `[ValueObject]` with a primitive-writing
  converter to its primitive schema (`Money`→`int64`, `Slug`→`string`). _FRAMEWORK-GAP — this is the
  pilot's most repeated per-feature toll._
- [x] **Testcontainers Postgres harness with template-database cloning.**
  (`hostpoint/tests/Hostpoint.Tests/TestDatabase.cs`) One container, one migration into a template DB,
  then `CREATE DATABASE … TEMPLATE` per test/keyed group, pooling off. `AeroFortress.Framework.Testing` ships only the
  WebApplicationFactory harness + InMemory; the real-database leg every serious pilot needs lives in
  the app. Candidate: `AeroFortress.Framework.Testing.Postgres`. _FRAMEWORK-GAP._
- [x] **Rate-limiting wired to the error envelope.** The pilot wires ASP.NET's limiter and renders 429
  as the framework's `ErrorBody` with a `platform.rate_limited` registry code
  (`hostpoint/src/Hostpoint.Api/Platform/RateLimiting.cs`). The framework owns `ErrorKind.RateLimit` and
  the envelope but ships no limiter↔envelope bridge; each app re-derives the `OnRejected` glue. Port the
  bridge (policies stay app-owned). _FRAMEWORK-GAP (the glue), policies APP-SPECIFIC._
- [x] _(convention, doc-only)_ **`PlatformErrorCodes` registry** — platform-tier codes (rate limit, etc.)
  live in one `*ErrorCodes` class so AF0018/19 + the OpenAPI enum pick them up like module codes.
  Document in CONVENTIONS (platform layer section); no code needed.
- [ ] _(hold, 1-pilot)_ JSON-list `ValueConverter`+`ValueComparer` helper; sandbox env-gated vendor
  tests (`Sandbox.cs`); reflection seed-helper for encapsulated entities (`TestUser.cs`); presigned-URL
  memoization in an eventual `AeroFortress.Framework.Storage.S3`.

### Frontend

- [x] **The session seam has no framework home.** `onAuthenticated` / `bootstrapSession` /
  `clearSession` + the `useSession` boot hook + the `refresh-token.ts`/`.web.ts` platform-seam pair
  (`hostpoint/clients/app-core/src/lib/session/*`) are the generic mechanics AFFE016/017 *steer
  toward*, yet the spine ships only the read-side (`SessionState`). The write-side trio (token write
  paired with `me`-cache reset by construction) belongs in `@aerofortress/react` (storage injected as a
  port). _FRAMEWORK-GAP — the harness polices a seam the framework doesn't ship._
- [x] **`apiErrorCopy()` — the error-code→copy bridge.**
  (`hostpoint/clients/app-core/src/lib/api-error.ts`) Reads `ErrorBody.code` off an axios error, looks
  up the `api-errors` i18n namespace, falls back to a generic key. It is the runtime half of the
  `error-code-coverage` loop (the tool proves the catalog is complete; this consumes it). Generic —
  graduate to the spine or ship in the scaffold. _FRAMEWORK-GAP._
- [x] **Mutator/`configureClient` template.** The orval mutator (`aerofortress-client.ts`: auth injection,
  base-URL port, `Result` envelope) + boot-time `configureClient()` exist only in the pilot; the SDK's
  `generate.mjs` scaffolds features against a client whose mutator nothing scaffolds. Folds into the
  tracked **`af gen client`** item — listed here so the mutator template isn't forgotten when that
  lands.
- [~] **Reverse drift (pilot behind framework).** The hostpoint `eslint-plugin-aerofortress` mirror is
  v0.3.0 — missing AFFE021/022 and the hardened AFFE002/011/013/016/018 — and the app forks
  `AsyncState` locally while not using the spine's `SessionState`/`requiredParam`/`combineAsyncStates`
  at all (guards still branch on a raw `session.ready` boolean — exactly what AFFE017 exists to
  prevent). Both are symptoms of the tracked "@aerofortress/react publish" gap; flagged so the next pilot
  sync rebases the mirror + adopts the spine unions.

## Design dogfood 2026-06-09 — pauta (the Design SDK wave, `.specs/` 0007)

The design layer (tokens + closed kit + AFFE024–026) wired into `pauta-web/frontend` (Next 15 +
Tailwind, aerocoding-generated, zero prior AeroFortress wiring). Mirror at 0.6.0, band warn-first globally,
error on `src/ui/**` + the relifted exemplar (`billing-type-edit`, the form recipe instantiated on a
real screen; `window.confirm` replaced by the app-owned `Dialog`). Gate: lint 0 errors, 456 tests green
(98 files, 8 new). Per-rule verdicts:

- [ ] **The band is blind to Tailwind utility classes.** _portback, HIGH._ Baseline: **0 findings over
  533 files** — yet the visual rot is everywhere, living in `className` utilities (`bg-red-100`,
  `text-blue-600`, `border-red-400`). AFFE026 checks style-object keys + string literals; AFFE025's
  only className leg is the arbitrary-value `[Npx]` regex (never fired — aerocoding used stock classes).
  A Tailwind-classes web app — the most common web stack — escapes the band almost entirely. Needed:
  a className leg for AFFE026 (flag `(bg|text|border|ring|fill|stroke)-(red|blue|…)-N` palette-family
  utilities outside `ui/` once tokens exist) and a decision on whether stock-scale spacing utilities are
  conformant (they are A scale — arguably fine) vs palette utilities (never fine).
- [x] **`scrim` color role.** _portback — shipped inline (inner loop)._ The Dialog's backdrop had no
  semantic role; raw `rgba()` in `ui/` would trip AFFE026. Added to the taxonomy + default themes
  (aerofortress-framework commit `37785aa`), re-scaffolded into both token instances.
- [ ] **`'use client'` banners on the hooky kit primitives.** _portback, MEDIUM._ Next App Router needs
  them on Button/Input/Field (useState); added app-side in pauta. Harmless on non-Next — candidate for
  the `renderUiKitWeb` template (+ Dialog when it graduates).
- [ ] **`Input` has no date/datetime kind.** _portback, LOW._ The generated forms used
  `type="datetime-local"`; the kit's closed `kind` union stops at text/email/password/number. Wait for a
  second real need before widening the union.
- [ ] **Dialog — kit-v2 candidate evidence, pilot #1.** Built app-side per the constitution (scrim +
  overlay tokens, focus in/return, Esc/backdrop, sentinel trap; 4 tests). A second pilot needing it =
  graduate into the scaffold.
- **AFFE024 inertia outside the blessed naming.** _observation, wave-order._ ui-door anchors on
  `*.view.tsx`; pauta's screens are `page.tsx` + `components/*Form.tsx`, so the rule only bites surfaces
  as they adopt the blessed shape (the relift exemplar does). Adoption order: naming first, then the
  rule has teeth — the full-harness wave's concern, not the band's.
- **Audit fields as editable form inputs.** _app-owned._ The generated forms expose
  `deletedAt`/`deletedBy`/`createdBy`/`updatedBy` as text inputs; the relift drops them (the partial
  update schema makes the narrower payload legal). A relift-pattern note in pauta's worklist, not a
  framework mechanism.
- **Relift fan-out is staged, not done.** "Todas as telas" = the next wave: per-feature cells dispatched
  from `pauta-web/frontend/docs/design-relift-worklist.md` (3 generated patterns × ~36 entity pairs),
  now that the pattern is proven on the worst offender.

## P3 / AMBIGUOUS — wait for ≥3-pilot evidence (per the framework's own rule)

- [ ] _(hold)_ `IUserScoped` (global user-owned data) — generic in shape, 1-pilot evidence.
- [ ] _(hold)_ blanket `Money`→bigint converter + a VO↔EF-converter analyzer — 1 instance each.
- [ ] _(hold)_ CI workflow template / starter Dockerfile / `dotnet-tools.json` in scaffold — judgment.
- [ ] **Scaffold interceptor: adopt hostpoint's injected-refresher shape.** The scaffolded mutator's 401
  interceptor (plugin-0.7.0 wave) bakes a cookie-mode `REFRESH_PATH` post — web-only. Hostpoint's
  generalization is better: the client exposes `setTokenRefresher(fn)` and the session seam registers its
  `bootstrapSession` (single-flight AT THE DOOR — several callers share one in-flight rotation), so the same
  interceptor serves cookie AND body modes and the rotation logic stays in the seam. 2-pilot evidence
  (pauta = direct-post shape, hostpoint = injected shape); graduate the injected shape into
  `client-scaffold.mjs` when touched next.

## Correctly app-owned (NOT gaps — must stay in the pilot)

`Cpf`/`Cnpj`/`Cep`/`BrazilianPhone`, `Address`/`Gender`/PostGIS `Geo`, `fly.toml`/`deploy.ps1`,
`e2e/support/actors.ts` (personas + pt-BR labels), the role-switch resolution policy, and the
`flows.json` contents (the curated list is per-app; the schema is framework-owned).

---

## Working order

1. Decision-doc fixes (AF0011→AF0020, baseline) — cheap correctness.
2. Truthfulness: AFFE008 tool + AFFE015 port — stop the framework lying about what it ships.
3. Contained analyzers: AF0006, AF0007, the journey-body AF0020.
4. Tenancy scaffold hardening + OrderedLifecycle helper.
5. e2e harness templates + skip-in-gate.
6. Foundation: `AeroFortress.toml` scaffold + manifest reader; `af gen client`.
7. `@aerofortress/react` publish-readiness; then rewrite the pilot's 5 scripts as thin SDK CLIs.
