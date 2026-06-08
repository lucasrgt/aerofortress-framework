# Port-back checklist — what the hostpoint pilot earned that the framework must own

**Created:** 2026-06-08. **Status:** in progress (branch `framework/portback-audit`).

Lazuli grew by dogfooding in the **hostpoint** pilot
(`c:/Users/lucas/dev/dotnet-projects/hostpoint-monorepo`) without a disciplined port-back. This
checklist is the audit of every generic mechanism the pilot accumulated that the framework should own
but doesn't — plus the framework claims that aren't actually shipped. App-specific material (per the
80/20 scope discipline) is listed under "Correctly app-owned" and is deliberately NOT ported.

Headline: the backend **core** is real and complete (17 `LZ00xx` analyzers + base runtime lib). What
never came back is the **workspace/monorepo layer**, the **frontend harness foundation** (the "wired
guarantee"), and a few **claimed-but-absent** rules. The pilot is carrying the framework.

Legend: `[ ]` todo · `[~]` partial · `[x]` done · scope = FRAMEWORK-GAP / AMBIGUOUS / APP-SPECIFIC.

---

## P1 — broken promise / claimed-but-absent

- [ ] **Scaffold a real project + `Lazuli.toml`.** `lazuli new` emits a backend-only project with no
  `Lazuli.toml`, no monorepo/frontend/workspaces/turbo/lefthook. The manifest is a phantom — the CLI
  never reads, validates, or generates it; the only validator is a copy inside the pilot
  (`hostpoint/scripts/lazuli-doctor.mjs`, whose header says canonical home = the CLI). _FRAMEWORK-GAP._
  - hostpoint: `Lazuli.toml`, root `package.json`, `turbo.json`, `lefthook.yml`
  - framework: only `examples/sample-app/Lazuli.toml`; `src/Lazuli.Cli/` has no manifest reader
- [ ] **`lazuli gen client` (the "wired guarantee").** The frontend harness rests on a generated
  typed client so an invented endpoint is a `tsc` error — but **orval is not even a dependency**, there
  is no `orval.config.ts`, no `lazuli gen client`. The LZFE rules police a client nothing generates.
  _FRAMEWORK-GAP._ (`docs/decisions/lazuli-net-frontend-harness.md` §3 vs `frontend-sdk/tools/generate.mjs:47`)
- [ ] **`LZFE008` endpoint-coverage — claimed "shipped", absent.** Documented shipped at
  `docs/FRONTEND-CONVENTIONS.md:268`; the implementation exists only in the pilot
  (`hostpoint/clients/hostpoint-app/scripts/lzfe-coverage.mjs`). _FRAMEWORK-GAP (truthfulness)._
- [ ] **`LZFE015` no-router-replace-in-effect — real rule, not ported + ID collision.** A
  battle-tested rule in the pilot (`hostpoint/clients/eslint-plugin-lazuli/index.cjs:438`, born from a
  shipped infinite-navigation bug) is absent from the framework plugin, and its ID collides with a
  *planned, different* LZFE015 ("no orphan placeholder") at `docs/FRONTEND-CONVENTIONS.md:275`.
  _FRAMEWORK-GAP._
- [ ] **e2e harness has no framework home.** `requireBackend`/`requireSeed`/`global-setup`/
  `playwright.config` are generic, yet absent from the framework — even though `e2e-doctor.mjs:67`
  *reads* those function names. _FRAMEWORK-GAP._ (`hostpoint/clients/hostpoint-app/e2e/support/*`)

## P2 — generic mechanism carried by the pilot, should graduate

- [ ] **The 5 `lzfe-*.mjs` doctor scripts are reimplementations, not SDK consumers.** They re-derive
  `walk`/regex/`bucket`/`aggregateReport` inline; the framework already exposes the pure cores
  (`checkJourneyParity`, `checkE2e`, `aggregateReport`, `i18n-parity`, `error-code-coverage`).
  _FRAMEWORK-GAP (anti-drift)._ (blocked on `@lazuli/*` being consumable)
- [ ] **Publish `@lazuli/react`.** Unpublished → the pilot forks `AsyncState`/`Resource` locally and
  they are drifting. _FRAMEWORK-GAP._ (npm publish is an outward release step — prepare, don't publish)
- [ ] **Harden the tenancy scaffold (`lazuli g auth`).** The shipped template ships
  `OrgId { get; set; }` + stamps via the CLR setter; the pilot already had to fix this (encapsulated
  setter, stamp via EF metadata, skip non-tenant rows). _FRAMEWORK-GAP (correctness, no boundary move)._
  (`src/Lazuli.Cli/Templates/auth/Tenancy/*.cstmpl` vs `hostpoint/.../Tenancy/TenantDbContext.cs`)
- [ ] **`lazuli` CLI conductor + `lazuli g view` front-door.** `MONOREPO-ARCHITECTURE.md` promises
  `build`/`gen:client` reading `[tasks]`; the ViewModel scaffold exists as a `.mjs` but isn't wired
  into the .NET CLI. _FRAMEWORK-GAP._
- [ ] **`OrderedLifecycle<TState>` helper.** Step-ordered enum + `StepReached`/`AdvancePast`
  (no-skip/no-regress) is duplicated byte-for-byte across Traveler + Host in the pilot. _FRAMEWORK-GAP._
- [ ] **`LZ0006` no-repository + `LZ0007` file ≤500 (user apps).** Both documented `planned` in
  `docs/CONVENTIONS.md:321-322`, neither implemented. _FRAMEWORK-GAP (documented commitment)._

## Journey-depth (decision `lazuli-net-journey-depth-enforcement.md`)

- [x] **Tier A1 `LZFE-JOURNEY-002`** — terminal-depth in `e2e-doctor.mjs` (+ tests). _done this session._
- [ ] **Decision-doc fixes** — `LZ0011` collides with `TestInfraPurityAnalyzer` → renumber to `LZ0020`;
  refresh the stale "today" baseline (A1 is now implemented).
- [ ] **Tier A2 `LZFE-E2E-SKIP-IN-GATE-001`** — a skipped gate-class flow must fail in gate mode.
- [ ] **Tier B3 `LZ0020`** (was LZ0011) — `[Journey]` body asserts its post-condition.
- [ ] **Tier B4 `LZFE-JOURNEY-SEAM-001`** — lifecycle-advancing `[Critical]` slice needs a frontend
  flow proving the post-transition navigation. (spike feasibility first, per the grade)
- [ ] **Tier C** — Stryker mutation-score lane (doctor consumes the artifact). _deferred — needs a
  `[Critical]` journey set to be meaningful._

## P3 / AMBIGUOUS — wait for ≥3-pilot evidence (per the framework's own rule)

- [ ] _(hold)_ `IUserScoped` (global user-owned data) — generic in shape, 1-pilot evidence.
- [ ] _(hold)_ blanket `Money`→bigint converter + a VO↔EF-converter analyzer — 1 instance each.
- [ ] _(hold)_ CI workflow template / starter Dockerfile / `dotnet-tools.json` in scaffold — judgment.

## Correctly app-owned (NOT gaps — must stay in the pilot)

`Cpf`/`Cnpj`/`Cep`/`BrazilianPhone`, `Address`/`Gender`/PostGIS `Geo`, `fly.toml`/`deploy.ps1`,
`e2e/support/actors.ts` (personas + pt-BR labels), the role-switch resolution policy, and the
`flows.json` contents (the curated list is per-app; the schema is framework-owned).

---

## Working order

1. Decision-doc fixes (LZ0011→LZ0020, baseline) — cheap correctness.
2. Truthfulness: LZFE008 tool + LZFE015 port — stop the framework lying about what it ships.
3. Contained analyzers: LZ0006, LZ0007, the journey-body LZ0020.
4. Tenancy scaffold hardening + OrderedLifecycle helper.
5. e2e harness templates + skip-in-gate.
6. Foundation: `Lazuli.toml` scaffold + manifest reader; `lazuli gen client`.
7. `@lazuli/react` publish-readiness; then rewrite the pilot's 5 scripts as thin SDK CLIs.
