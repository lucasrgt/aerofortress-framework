---
id: 428224ad-621c-494f-9770-0c7462b72f0d
slug: arch
type: fact
title: Lazuli 0.4.0 numeric-pure OpenAPI: pilot fallout is missing client-side parses, not redundant coercions
tags: lazuli-0.4.0, openapi, orval, typed-client, lz0028, pilot-bump
provenance: observado
evidence: pauta-monorepo commits 32b262b + a177bf5; tsc errors in AgencySettings.viewModel.ts:63-65, CustomerEdit.viewModel.ts:116-118, PaymentTermsForm.view.tsx:175/186, ServiceCatalog.view.tsx:67-68
decay: seasonal
created: 2026-06-11T03:56:46.710627600+00:00
updated: 2026-06-11T03:56:46.710627600+00:00
validated: 2026-06-11T03:56:46.710627600+00:00
links: 
---

Lazuli 0.4.0 pins all numerics in the OpenAPI document as pure `number` (the `AllowReadingFromString` JsonNumberHandling no longer leaks `["integer","string"]` unions; nullability preserved as `number | null`).

**Observed fallout in the Pauta pilot (re-bump 0.3.2 → 0.4.0):**
- Orval deletes the per-field union alias models (`*OffsetDays.ts`, `*UnitPrice.ts`, …) for non-nullable fields (inlined as `number`) and collapses nullable aliases to `null | number` — ~157 generated files churn, −1.4k LOC.
- The typecheck breakage is the **inverse** of the intuitive guess: not redundant `Number(x)` coercions (those still typecheck fine — `Number(number)` is valid), but **form seams that sent raw strings** relying on the server's lenient read. In Pauta: 4 seams (AgencySettings/CustomerEdit percent ViewModels, PaymentTermsForm installments, ServiceItemForm "wire-shaped decimal string" payload).
- Fix pattern (codebase idiom): keep drafts as strings for controlled inputs, parse at the submit boundary — `v ? Number(v) : null` for nullable percents, `Number(e.target.value) || 0` for `type="number"` onChange, `Number(value.trim().replace(",", "."))` for pt-BR comma forms.
- Pre-existing read-side coercions (`Number(row.count ?? 0)` over response fields) become redundant but harmless; cleanup is optional polish, not required for green.

**LZ0028** (warn: ToPageAsync ordering needs unique key in chain) shipped in the same wave; a pilot already migrated with `ThenBy(Id)` on every list slice gets 0 hits — confirmed 0 warnings on Pauta's 34 slices.

Commit shape that keeps every commit green: (1) csproj bump alone; (2) regenerated client + the form-boundary fixes **together** (the regen alone leaves tsc red, so the fallout rides the regen commit).
