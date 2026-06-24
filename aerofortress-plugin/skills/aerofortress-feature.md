---
description: End-to-end AeroFortress feature workflow — new slice from backend to wired frontend screen, doctor-clean. Use for "add a feature/endpoint/screen" requests in a AeroFortress app.
---

# AeroFortress feature, end to end

The full path from idea to doctor-clean, wired feature. Delegate each phase to the kit's
specialist; keep the order.

1. **Scaffold** (aerofortress-scaffolder): `af g slice <Module> <Name>` (create the module first
   if needed: `af g module <Module>`). For a screen, also `af g view <Name>` later.
2. **Domain** (aerofortress-backend): model VOs/entity changes (always-valid, EnsureValid funnel),
   implement Handle (validation inline → domain method → SaveChanges → Result), declare error
   codes in the `*ErrorCodes` registry, decide authorization on Map, `.WithName("<Name>")`.
3. **Tests** (aerofortress-backend): fill the co-located `<Name>.Tests.cs`. If the operation is
   high-stakes, mark `[Critical]` and write happy + sad `[Journey]` (sad: failure AND state
   unchanged), declare the concurrency token.
4. **Contract** : run `af gen client` — the new hook appears named after the slice.
5. **Frontend** (aerofortress-frontend): scaffold/own the triple — ViewModel composes the generated
   hook and exposes loading/error/empty; View renders through `<Resource>`; copy goes to the
   feature's i18n (every locale); error codes get entries in `api-errors`.
6. **Gate**: `af doctor` + `af test`. Any AF*/AFFE* finding goes to aerofortress-doctor —
   fix the shape, never suppress.

Report at the end: slice path, endpoints, error codes added, journeys written, doctor status.
