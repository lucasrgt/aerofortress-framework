# Lazuli — Monorepo Architecture (back + front, 3 platforms)

> Status: **decided 2026-06-06**, grade **9.5/10** (the residual 0.5 is earned in code — the kernel boundary +
> ports surface only fully reveal themselves during the first extraction). This is the canonical layout for a
> Lazuli project that spans a .NET backend + React Native (mobile) + React (web) + an Astro marketing site.

## Identity (read first)

Lazuli is a **Rails-style meta-framework for .NET** — **no language** (`.lzi`/`.lzx` retired). The "compiler" is
the analyzers: **`LZ*`** (Roslyn, backend) + **`LZFE*`** (ESLint, frontend) that fail the build. One thing, called
**Lazuli** (the "Lazurite" distro name is retired); the workspace manifest is **`Lazuli.toml`**.

## The shape

```
Lazuli.toml                         # the manifest = single source of truth; the `lazuli` CLI reads it and delegates
backends/
  hostpoint-app-api/                # .NET — MSBuild + LZ* analyzers (native, no package.json shim)
  hostpoint-os-api/                 # (when the os product lands)
  hostpoint-barcos-api/             # (when barcos lands)
frontends/
  hostpoint-app/   core/  mobile/  web/    # consumer product (RN mobile; web = RN-web; Astro owns public SEO)
  hostpoint-os/    core/  web/             # admin (react-dom Vite+TanStack); core = admin VMs + os api client
  hostpoint-barcos/ core/ mobile/ web/     # (when it lands)
  shared/          kernel/  ui-web/  ui-mobile/   # cross-product, promoted by evidence (≥2 products)
  website/         # Astro (SEO/marketing), standalone
pnpm-workspace.yaml  turbo.json     # generated from / validated against Lazuli.toml
```

npm scope `@hostpoint`; packages `<product>-<layer>` (`@hostpoint/app-core`, `@hostpoint/os-web`, `@hostpoint/kernel`, …).

## The six decisions (each closes a real trap)

1. **Platform split.** `mobile` = React Native (Expo). `os` = **react-dom** (Vite + TanStack — admin, web-only, no
   SEO). consumer `web` = **RN-web** (≈95% shared with mobile) + **Astro owns public/SEO** pages, so RN-web's SEO
   weakness is moot (the app web is authenticated). *Reversible*: a separate react-dom consumer web is a later
   `move`, not a rewrite. **os being react-dom is the canary that keeps `core` honest** — it can't consume RN-isms.

2. **No `os→app` edge — a thin `shared/kernel`.** Both products depend on `@hostpoint/kernel` (auth/session, the
   generated client + types for shared endpoints, the spine `@lazuli/react`, the ports). `app` = kernel + consumer
   VMs; `os` = kernel + admin VMs + its own api client. Neither depends on the other (kills the coupling trap).
   Not speculative — both obviously need auth/client/ports.

3. **Ports & adapters (hexagonal).** Every platform capability is an interface in `core/ports/` (or `kernel/ports/`)
   — `IStorage, INavigator, IPush, IFilePicker, ILinking, IMaps` — implemented per shell (RN impls in `mobile`,
   web impls in `web`/`os`), wired at the shell, **never imported into a ViewModel**. This is what makes `core`
   genuinely platform-agnostic (the package boundary enforces it structurally; LZFE009 lints it).

4. **`Lazuli.toml` is the single source of truth.** It **generates** `pnpm-workspace.yaml` + the turbo pipeline,
   and a **`lazuli doctor` check validates** that the declared topology (products, cores, deps) matches the real
   `package.json` workspace deps + folders. No hand-maintained parallel; drift = a build error.

5. **`core` per product; `shared/` by evidence.** A product's `core` = its integration+logic (its orval client +
   ViewModels + i18n + ports). Cross-product code is promoted to `shared/` **only when ≥2 products need it** (a
   `move`, not upfront — YAGNI). Don't scaffold `os`/`barcos` folders before those products exist.

6. **The View↔ViewModel split becomes a package boundary.** `core` literally has no `react-native`/`react-dom`
   dependency, so platform-agnosticism is *structural*, stronger than the lint rule.

## Harness change this requires (lazuli-net)

`LZFE005`/`LZFE006` today assume **co-location** (a `*.view.tsx` with a sibling `*.viewModel.ts`). With the split,
the ViewModel is in `core` and the View in `mobile`/`web` — different packages. So the rules must detect the link by
**import**, not sibling file: a `*.view.tsx` is a "screen" if it **imports a ViewModel**; its co-located test must
render it; the ViewModel's `renderHook` test lives with the ViewModel in `core`. (This is the one framework change
the migration depends on — do it first.)

## `Lazuli.toml` schema (orchestrator)

```toml
[products.hostpoint-app]
backend = "hostpoint-app-api"
core    = "frontends/hostpoint-app/core"      # @hostpoint/app-core
apps    = ["mobile", "web"]                    # web = RN-web

[products.hostpoint-os]
backend = "hostpoint-os-api"
core    = "frontends/hostpoint-os/core"        # @hostpoint/os-core
deps    = ["@hostpoint/kernel"]                # NOT @hostpoint/app-core
apps    = ["web"]                              # react-dom

[shared]
packages = ["@hostpoint/kernel", "@hostpoint/ui-web", "@hostpoint/ui-mobile"]

[generate.client]                              # orval per product: contract -> typed client
"hostpoint-app" = { from = "hostpoint-app-api", to = "frontends/hostpoint-app/core/src/client.gen" }

[doctor]
backend  = "tdd-iron-hand"                     # LZ* Roslyn rules
frontend = "tdd-iron-hand"                     # LZFE* ESLint + the doctors

[tasks]                                        # the `lazuli` CLI delegates (wire, not reimplement)
build = ["dotnet:build", "turbo:build"]
test  = ["dotnet:test",  "turbo:test"]
```

The `lazuli` CLI is the conductor (`lazuli build/test/new/doctor/gen:client`); it **delegates** to `dotnet` + `turbo`
+ `pnpm` + `orval` — it never reimplements their caching/build (the founding "wire, not reimplementation" principle).

## Migration sequence (big-bang, but in green-verified milestones)

1. **(this doc)** capture — done.
2. **lazuli-net**: evolve `LZFE005/006` to import-based detection (+ self-tests). *Prereq for the split to stay enforced.*
3. **hostpoint**: introduce `Lazuli.toml` + `backends/` + `frontends/` + `pnpm-workspace.yaml` + `turbo.json`;
   verify the .NET 374 tests + the front still build/lint/test.
4. **front**: extract `@hostpoint/app-core` (ViewModels + `client.gen` + i18n + model); rewire the app to consume it.
5. **front**: extract `@hostpoint/kernel` (auth/session/spine/ports interfaces); convert `expo-*` direct imports to
   ports + wire impls in the shell. (RN-web for web; Astro stays.)
6. **`Lazuli.toml` doctor**: validate topology == real deps (and/or generate the workspace config from it).
7. **lazuli-net canon**: rewrite the identity (meta-framework, no language) + fold this doc into the convention set.

`hostpoint-os` and `hostpoint-barcos` are **convention, applied when those products are real** — no empty scaffolds.

Each milestone is a commit; each commit is green (build + tests + lint). A stop at any milestone is a clean,
resumable checkpoint — the value is never lost.
