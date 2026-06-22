# Lazuli — Key decisions (ADR digest)

- **Frontend harness**: MVVM over plain custom hooks (no classes/observables/two-way binding);
  orval stock wrapped, never a bespoke compiler; typed slice-hooks via OpenAPI so completeness
  is enforced by tsc, not lint; ViewModels scaffolded once then owned (no source-gen of
  behavior); the ESLint plugin is optional and doctor-removable. The "wired" guarantee is the
  type system: an invented endpoint is a compile error.

- **Journey depth enforcement**: Tier A — linked flows declare `terminal` in flows.json and the
  spec asserts it after entry (entry-only flagged); skipped gate-class flows fail the CI gate.
  Tier B — `[Journey]` bodies must assert post-conditions (AF0020, heuristic warning).
  Tier B4 (spike) — lifecycle-advancing `[Critical]` slices need a frontend flow proving
  post-transition navigation. Tier C (runtime) — mutation score on critical journeys only.
  Honest ceiling: semantic adequacy of an assertion is undecidable by analyzer.

- **Unmarked domain type (AF0021)**: catch the UNMARKED entity/VO, not just police the marked —
  `DbSet<T>` with unmarked T requires `[Entity]`; complex members of entities require
  `[ValueObject]`. Omission of the mark was the evasion (a pilot shipped an anemic `User`);
  marks become the only way to model state.

- **Monorepo architecture**: `backends/` + `frontends/` with per-product core and platform
  shells (mobile = RN/Expo, os = react-dom admin, consumer web = RN-web ~95% shared + Astro
  for public/SEO). `shared/kernel` for auth/session/spine/ports; `shared/ui-*` promoted only at
  ≥2 products. Ports & adapters for platform capabilities. `Lazuli.toml` single source of
  topology, doctor-validated.

- **Core mission**: scaffolding + the doctor + AI-context discipline (ctx.md). The framework is
  the skeleton and its enforcement — apps bring their own libraries and business logic; vendors
  and integrations live in plugins, not in the framework.
