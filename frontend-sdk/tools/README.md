# tools — frontend generators + doctors

Beyond the typed client (orval generates that from the backend contracts), these emit the *structure* a feature
needs — the frontend parallel of the backend scaffold — and check the parts a per-file lint rule can't see. Pure
functions (no I/O, unit-tested); the `*.mjs` CLIs wrap them with file writes / reports. The `af` .NET CLI
front-door shells out to these (the way `af doctor` shells out to `npm run lint`) — two engines, one front door.

## The test tiers (how each is enforced)

| Tier | Enforced by | Where |
|---|---|---|
| Unit | `AFFE005` (every ViewModel has a co-located `renderHook` test) | eslint, per-file |
| Integration | `AFFE006` (every View has a co-located `render()` test) | eslint, per-file |
| AVP | `AFFE033` (every ViewModel declares `@verify`; the exact co-located `*.assay.test.tsx` registers the matching `@avp` verification) | eslint + `npm run test:avp` |
| **E2E** | `AFFE035` + **`feature-e2e-coverage.mjs`** (every ViewModel links a flow; every UI-consumed backend slice is named by that flow) + **`e2e-doctor.mjs`** (every flow has an enabled case, terminal assertion, and runner) | eslint + workspace gate + surface gate |

E2E remains flow-level, but coverage is now enforced in both directions. Every ViewModel declares one or more
`@e2e <flow-id>` obligations; `affe-feature-e2e` resolves them against the union of the product surfaces and
requires each generated slice hook consumed by that ViewModel (or by session/guard infrastructure) to appear in
the flow's `backendSlices`. Every flow owns exactly one ViewModel, and every ViewModel needs subject-bound
`happy` and `sad` flows. A web flow naming backend slices imports `requireBackend` from
`@aerofortress/frontend-sdk/playwright-backend` and calls it in its own exact case. A Playwright global setup
calls `probeBackend()` or `createBackendGlobalSetup()` from the same export against `PW_API_URL`; only a
successful HTTP response sets `PW_API_READY=1`. A local namesake cannot impersonate the guard, another case
cannot lend it, and a backend-bound spec cannot install request interception or import mock/stub support.
`checkE2e(root)` then
proves every declared flow has an enabled spec/case, terminal assertion, and real runner. A missing/empty manifest
is red. See the
Hostpoint dogfood's `scripts/affe-e2e-doctor.mjs` (a thin CLI over `checkE2e`) + `playwright.config.ts`.

Minimal real-backend setup:

```ts
// e2e/global-setup.ts
import { createBackendGlobalSetup } from "@aerofortress/frontend-sdk/playwright-backend";
export default createBackendGlobalSetup({ path: "/health" });

// e2e/account.spec.ts
import { requireBackend } from "@aerofortress/frontend-sdk/playwright-backend";
test("signs in", async ({ page }) => {
  requireBackend();
  // Drive the visible UI; configure the app's API base URL to PW_API_URL without interception.
});
```

## The fullstack loop (journey parity)

`journey-parity.mjs` (`checkJourneyParity`) closes the loop at the **journey grain**: the backend declares write
journeys (its `Journeys/*.Tests.cs`), the frontend declares them in `flows.json` (linked explicitly via a
`backendJourney` field) — and the doctor proves the two sets agree (no backend journey uncovered on the front, no
front flow pointing at a journey the back lacks). The *endpoint* grain is already closed: `tsc` for front→back (you
can't call a missing endpoint) and **AFFE008** for back→front (every endpoint is consumed by a ViewModel). So:
endpoints by type + coverage, journeys by parity — the same write path proven on both sides.

When those endpoints are shared by multiple frontend surfaces, endpoint coverage also consumes the union of their
source roots:

```
affe-endpoint-coverage app-core/src/client.gen/api.ts app-core/src operator/src partner/src
```

When one backend serves multiple executable surfaces, pass every independently-gated manifest; parity is computed
over their union without pretending operator or partner journeys belong to the consumer app:

```
affe-journey-parity ../api/Journeys app/e2e/flows.json operator/e2e/flows.json partner/e2e/flows.json
```

## One report — the unified doctor

`doctor.mjs` (`aggregateReport`) is the single front-door that captures the **whole crew** in one pass: `eslint
--format json` (every rule — the AFFE architecture rules, the community kit, expo's set — bucketed) plus the three
script-doctors above (endpoint coverage, e2e, journey parity). The core is pure (feed it the eslint results, each
rule's configured level, and the loop summaries); a consumer CLI does the I/O. It surfaces the **AFFE roster
including clean (0-hit) rules** so a `warn`→`error` promotion is an evidence-backed move — you see, in one place,
which rules gate, which are a revealed backlog, and which are already clean. See the Hostpoint dogfood's
`scripts/affe-doctor.mjs` (`npm run doctor` / `doctor:json`) — a thin CLI over this core.

## Scaffold a feature unit

```
npm run scaffold -- <plural-name> [targetDir]
# e.g.  npm run scaffold -- bookings sample/bookings
```

Emits the five co-located files of the canonical unit — `<Feature>.viewModel.ts`, `<Feature>.view.tsx`,
`<Feature>.test.tsx`, `<Feature>.assay.test.tsx`, `<feature>.i18n.ts` — with names derived from the feature name.
The Assay subject is red by design until it mounts the real View and endpoint; the other four files are the
blessed `sample/items` shape with substitutions, so their structure **passes every AFFE rule and typechecks by construction**
(`generate.test.ts` writes a unit to disk and lints it with the real rules to prove the emitter and the harness
agree). Its generated `@e2e <feature>-happy` is deliberately unresolved until the owning surface adds the real
flow, spec, and terminal proof. Then refine the entity fields, wire the slice in `@/client.gen`, and fill the copy.

## Assemble the i18n resource tree

```
npm run assemble-i18n -- <featuresDir> <outFile>
# e.g.  npm run assemble-i18n -- features src/i18n/resources.generated.ts
```

Discovers every `*.i18n.ts`, derives each namespace from the filename, and emits a generated module that imports the
locale catalogs and composes `resources` (locale → namespace) — what an app otherwise wires by hand. The output
typechecks, so a renamed/removed catalog fails the build; pair it with AFFE011 (key parity *within* each catalog) —
the `aerofortress/i18n-completeness` rule in-scope, or `i18n-parity.mjs` below when the catalogs are cross-package.

## i18n parity — cross-package catalogs (AFFE011, the other half)

```
node tools/i18n-parity.mjs <catalogsDir> [...moreDirs]
# e.g.  node tools/i18n-parity.mjs ../examples/sample-app/frontend/core/src
```

AFFE011 has two mechanisms for two layouts. When the catalogs live *inside* the linted source, the
`aerofortress/i18n-completeness` eslint rule pins parity per file at lint time — done. But in a **core-split** layout the
catalogs sit in a SEPARATE package, outside the app's eslint scope, so the rule never sees them. `i18n-parity.mjs`
reads them directly and enforces the same invariant (every locale object in a catalog declares the same keys; a key
in one but not its siblings is a silent untranslated string), locale-agnostic like the rule. **Use the rule when
catalogs are in lint scope, the tool when they're cross-package** — same convention, the mechanism follows the
layout. (The Hostpoint dogfood runs the tool: its catalogs live in `@hostpoint/app-core`, out of the app's scope.)

## Error-code coverage — every code has copy

```
node tools/error-code-coverage.mjs <catalog.i18n.ts> <errorBody.ts>
# e.g.  node tools/error-code-coverage.mjs ../app-core/src/i18n/api-errors.i18n.ts ../app-core/src/client.gen/model/errorBody.ts
```

The backend ships every error as a stable code (the registry constants behind `AF0018`/`AF0019`), enumerated into the
OpenAPI `ErrorBody.code`. This proves every code in the generated union has a catalog entry — so no error reaches a
user untranslated. It's the **coverage** half (code → copy); `i18n-parity.mjs` is the **parity** half (copy → every
language). It reads the orval-generated `errorBody.ts` union directly, and is a **notice until the client is
regenerated** against the enum-bearing contract, a hard gate after — so it never blocks before the codegen has run.

## Contract freshness — the client mirrors the live spec

```
node tools/contract-freshness.mjs <openapi.json> <client.gen dir>            # check (the doctor leg)
node tools/contract-freshness.mjs <openapi.json> <client.gen dir> --stamp    # stamp (run as the codegen tail)
```

The typed client is a **mirror** of the backend's OpenAPI document, and nothing re-checks the mirror after
generation — a backend shape change leaves the front compiling happily against a stale client, and every other
loop (endpoint coverage, error-code coverage, journey parity) inherits the drift because they read the client as
truth. This pins the client to the exact spec it was generated from: the codegen script ends with `--stamp`
(writes `client.gen/.spec-hash`, a whitespace-insensitive fingerprint), and the doctor compares the stamp against
the live spec. A mismatch is a build-time "the contract moved; regenerate", not a runtime 404. **Notice until the
first stamp exists**, a hard gate after — so it never blocks before the codegen has run.

## Client scaffold — the mutator + orval config (the hand-owned half of the wired guarantee)

```
node tools/client-scaffold.mjs <client-name> <contract-path> [target-dir]
# e.g.  node tools/client-scaffold.mjs shop ./contract/Shop.Api.json packages/app-core
```

orval generates the hooks, but the **mutator** they all call through (auth injection, the base-URL port, the
`X-Client: web` header that turns on the cookie session) and the **orval config** are hand-owned files nothing
scaffolded — every pilot re-derived them. This renders both, conformant by construction: the base URL is an
injectable default overridden at boot via `configureClient()` (the AFFE020-blessed shape), the token sink is the
session seam's `setAccessToken`, the 401 rotation is the seam's single-flight `bootstrapSession` injected via
`setTokenRefresher` (one door, cookie AND body — never a cookie-only fork baked into the transport file), and the
audience filter keeps webhooks/internal endpoints out of the client (so endpoint coverage stays high-signal).
Existing files are never overwritten — they are hand-owned after birth.
