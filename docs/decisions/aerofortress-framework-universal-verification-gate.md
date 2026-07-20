# Decision: verification is universal; criticality controls depth, not coverage

**Status:** accepted and implemented.
**Date:** 2026-07-20.
**Amends:** `aerofortress-framework-criticality-policy.md`.

## Context

Pilot inspection found that acceptance coverage was attached to an opt-in marker: 685 slices included only
95 explicit `[Critical]` slices, while most features could satisfy the framework without declaring an AVP
criterion. The lowest-friction path was therefore also the least-tested path. A second hole let an
`[AVP("id")]` proof pay for every slice that reused the same criterion id, and skipped tests still left
`dotnet test` green.

## Decision

Every feature carries executable evidence, regardless of criticality:

1. Every backend `[Slice]` declares at least one criterion in `<Module>.spec.toml` (`AF0031`).
2. Every obligation is proven by the exact `(module, slice, criterion)` tuple through
   `[AVP(typeof(Slice), "criterion")]` (`AF0030`). Criterion-only AVP markers are legacy and do not satisfy
   the doctor or matrix.
3. Every frontend ViewModel declares at least one `@verify` obligation. Its exact co-located
   `<Feature>.assay.test.tsx` carries `@avp <criterion>` and an executable `defineVerification(...)` (`AFFE033`);
   the `.test` segment is part of the convention because the host Vitest runner must discover the proof.
4. Disabled, conditional, or focused tests are errors (`AF0032`, `AFFE034`), and the gate rejects runtime `NotExecuted` results.
5. `af gate` always runs backend tests plus every manifest-selected frontend harness's unit/integration,
   direct Assay/AVP, E2E-shape, and real E2E scripts. Missing/placeholder scripts are contract failures.
   There is no `[Ephemeral]` tier. Runtime is managed by ordinary CI parallelism and
   sharding, never by silently removing proof from the release gate.

`[Critical]` keeps its useful, narrower meaning: failure can cost money or trust. It adds happy and sad
end-to-end journeys through `AF0008`; it never decides whether the feature is tested at all.

## Consequences

Generators are red by design until the real subject is bound: `af g slice` requires `--verify`, auth and CRUD
declare/prove every emitted slice, and the frontend scaffold emits a real Assay file. Existing applications
adopting this release will surface their unverified slices immediately. That fallout is intentional evidence
debt, not a compatibility reason to preserve a false-green gate.

The policy stays doctor-removable: manifests, attributes, comments, and tests are ordinary source. Removing
the analyzers and CLI still leaves plain C#/TypeScript that compiles and runs; only enforcement disappears.
