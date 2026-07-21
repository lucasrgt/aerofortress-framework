---
description: End-to-end AeroFortress feature workflow — new slice from backend to wired frontend screen, doctor-clean. Use for "add a feature/endpoint/screen" requests in a AeroFortress app.
---

# AeroFortress feature, end to end

The full path from idea to doctor-clean, wired feature. Delegate each phase to the kit's
specialist; keep the order.

1. **Scaffold** (aerofortress-scaffolder): choose acceptance criteria with
   `af criteria suggest <Name>`, then run `af g slice <Module> <Name> --verify <id,id>` (create
   the module first if needed: `af g module <Module>`). The CLI refuses a criterion-free slice.
   For a screen, also `af g view <Name>` later.
2. **Domain** (aerofortress-backend): model VOs/entity changes (always-valid, EnsureValid funnel),
   implement Handle (validation inline → domain method → SaveChanges → Result), declare error
   codes in the `*ErrorCodes` registry, decide authorization on Map, `.WithName("<Name>")`.
3. **Backend proof** (aerofortress-backend): fill the co-located `<Name>.Tests.cs` and every
   generated `[AVP(typeof(Slice), "criterion")]` proof. For a shape-derived write, complete the
   isolated happy and sad `[Journey]` E2E cases; sad proves rejection and unchanged state. Declare
   the persistence concurrency posture. Never classify the slice by annotation or manifest mode.
4. **Contract** : run `af gen client` — the new hook appears named after the slice.
5. **Frontend** (aerofortress-frontend): scaffold/own the triple — ViewModel composes the generated
   hook and exposes loading/error/empty; View renders through `<Resource>`; copy goes to the
   feature's i18n (every locale); error codes get entries in `api-errors`. Declare `@verify` and
   satisfy it in the exact co-located `*.assay.test.*`. Declare two distinct `@e2e` ids and link
   exact happy/sad flows in `flows.json`. If a flow names `backendSlices`, its Playwright case must
   declare `backendContract`, collect page responses with canonical `observeBackend()`, and assert the exact
   operations with `expectBackendSlices()` from `@aerofortress/frontend-sdk/playwright-backend`; global setup
   probes `PW_API_URL` with the same package. Put mocked smoke coverage in another spec and never call the API
   directly from a backend-bound case.
6. **Gate**: run `af gate`. Any AF*/AFFE* finding goes to aerofortress-doctor — fix the shape,
   never suppress, skip, focus, or weaken the manifest.

Report at the end: slice path, endpoints, error codes, AVP proofs, backend journeys, frontend
happy/sad flows, and the `af gate` verdict.
