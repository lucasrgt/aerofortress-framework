# Decision: journey enforcement must grade *depth*, not just *existence*

**Status:** accepted; Tier A1 (`LZFE-JOURNEY-002`, `frontend-sdk/tools/e2e-doctor.mjs`) and Tier B3
(`LZ0020`, `analyzers/AeroFortress.Framework.Doctor/JourneyAssertionAnalyzer.cs`) implemented (+ tests). Tier A2
(`LZFE-E2E-SKIP-IN-GATE-001`) is pending behind the e2e-support harness home; Tier B4
(`LZFE-JOURNEY-SEAM-001`) pending its feasibility spike; Tier C (mutation) deferred until a critical
journey set exists. Self-graded **8.6 — PASS with notes** (see §Grading). Tracked in
[`docs/PORTBACK-CHECKLIST.md`](../PORTBACK-CHECKLIST.md).
**Date:** 2026-06-08.
**Supersedes/extends:** `CriticalJourneyAnalyzer` (LZ0008) + `JourneyCoversCriticalAnalyzer`
(LZ0010) + the frontend `journey-parity.mjs` (LZFE-JOURNEY). It does not replace them — it adds
the depth rung above the existence rung they already enforce.
**Lineage:** the anti-theater hardening that gave the language rubric Criteria 12 (`spec_polarity`)
and 13 (`HANDLER-SIGNATURE-MISMATCH-001`, `TEST-FAILURE-ONLY-COVERAGE-001`). Same intent — "a
green doctor is not shipping safety" — applied to the **journey** grain instead of the slice grain.

---

## Context

The doctor enforces journeys at two grains today, both **existence-only**:

- **Backend.** A `[Slice]` marked `[Critical]` must carry a happy *and* a sad `[Journey]`
  (`analyzers/AeroFortress.Framework.Doctor/CriticalJourneyAnalyzer.cs:29` — LZ0008), and every `[Journey]` must
  cover a critical slice (`JourneyCoversCriticalAnalyzer.cs:32` — LZ0010). The match is **textual**
  — a regex over `AdditionalFiles` that confirms the `[Journey(typeof(Slice), JourneyPath.Happy|Sad)]`
  attribute *exists* (`CriticalJourneyAnalyzer.cs:86-106`). It never reads the test body.
- **Frontend.** `frontend-sdk/tools/journey-parity.mjs` (LZFE-JOURNEY) name-matches each
  `e2e/flows.json` entry's `backendJourney` against a `*.Tests.cs` filename and back
  (`journey-parity.mjs:45-53`). It reads **zero lines** of the spec.

The hole this leaves was paid for in production. The hostpoint onboarding wizard (a pilot on this
framework) had a journey on **both** sides, and the doctor reported `0 parity gaps` — yet finishing
onboarding bounced the user back to step 0 instead of the dashboard. Root cause: the dashboard
route guard read the role's `lifecycleState` from a **stale query cache** after completion. The
backend journey proved the lifecycle reaches `Complete`; the frontend spec proved only **entry**
into the wizard (`clients/hostpoint-app/e2e/traveler-onboarding.spec.ts:11-21` — asserts the URL is
`/onboarding/traveler` and stops, explicitly delegating the rest to "the backend twin"). Each side
tested half. The bug lived in the **seam** between them — exactly where neither existence check looks.

Two structural facts make this a framework gap, not just a pilot mistake:

1. **The framework already documents the depth contract it does not enforce.** `JourneyAttribute`
   states: *"A sad journey must assert both the failure status **and** that no state changed"*
   (`src/AeroFortress.Framework.Testing/JourneyAttribute.cs:22-26`). LZ0008 checks the attribute is present; nothing
   checks the assertion exists. The prose promises depth; the analyzer delivers existence.
2. **The `backendJourney` link actively *invited* the shallow spec.** Because parity is satisfied by
   a name match, the cheapest way to go green is an entry-only spec that punts depth to the backend
   twin — which is precisely what the pilot did. Breadth parity *read as* depth coverage and gave
   false confidence.

There is a hard ceiling worth stating up front, because it bounds what any analyzer can promise:

| Rung | Claim | Statically enforceable? |
|---|---|---|
| 1. Existence | "a journey exists" | ✅ — LZ0008 / LZFE-JOURNEY today |
| 2. Breadth parity | "every critical journey is linked both ways" | ✅ — LZ0010 / LZFE-JOURNEY today |
| 3. Structural depth | "the journey traverses its declared arc to the terminal post-condition" | ✅ — **if the arc is declared as data** (this proposal) |
| 4. Terminal assertion | "the spec asserts a post-state, not just entry" | ✅ — heuristic AST/textual (this proposal) |
| 5. Semantic adequacy | "the assertion is *meaningful* and would *fail* if the behaviour broke" | ❌ — **undecidable** (Rice); only mutation testing approximates, at runtime |

Rung 5 is the wall. No analyzer proves an assertion is meaningful — a test can call everything and
assert `true`. This proposal pushes enforcement from rung 2 to rungs 3-4 and names rung 5 honestly
as out of analyzer reach (Tier C, runtime).

## Decision

Add a **depth rung** to journey enforcement, tiered by cost and by where it sits against the rung-5
ceiling. Tier A ships first; B and C are sequenced behind it.

### Tier A — stop reading "linked" as "covered" (ship first)

1. **A linked flow declares its terminal, and the doctor verifies the spec asserts it.** Extend the
   `flows.json` schema: a flow with a `backendJourney` must also carry `terminal` — the testID (or
   route) the spec must assert **after** entry. `journey-parity.mjs` (→ LZFE-JOURNEY v2) parses the
   spec and fails when (a) the spec asserts only the entry marker, or (b) the declared `terminal`
   marker never appears in an assertion. Warn-only first, then hard-exit once parity holds — the
   promotion path already sketched at `journey-parity.mjs:63`. **New diagnostic:
   `LZFE-JOURNEY-002`.** This alone would have failed the hostpoint entry-only spec.

2. **A skip is not a pass in the gate.** `frontend-sdk/tools/e2e-doctor.mjs`, in CI-gate mode (the
   no-seed critical set), fails when a gate-class flow *skips* — today `requireBackend()` /
   `requireSeed()` skip silently and the run reads green (`clients/hostpoint-app/e2e/support/backend.ts:11-29`).
   The spec's own comment already insists "a skipped spec is NOT a passed spec"; this makes the gate
   honour it. **New diagnostic: `LZFE-E2E-SKIP-IN-GATE-001`.**

### Tier B — structural depth (sequenced behind A)

3. **Enforce the journey's already-documented assertion contract (backend).** A new analyzer reads
   the `[Journey]` test body: a `JourneyPath.Sad` test must assert a failure (a non-2xx / `IsFailure`)
   **and** a re-read showing no state changed; a `JourneyPath.Happy` test must assert the observable
   effect (a re-read of the mutation or the terminal status). This is LZ0008's prose promise
   (`JourneyAttribute.cs:22-26`) finally checked — the journey-grain mirror of the language rubric's
   C13 Probe R-D (`_test.go` polarity). Heuristic; warning-default. **New diagnostic: `LZ0020`** (the
   first draft said `LZ0011`, which already belongs to `TestInfraPurityAnalyzer` — renumbered to the
   next free id; LZ0001–LZ0019 are taken).

4. **The seam rule — the one that catches *this* bug class precisely.** When a `[Critical]` slice's
   effect is a **state transition** (it advances a lifecycle the framework already models) and a
   **frontend route guards on that lifecycle**, the journey's terminal must be the *post-transition
   navigation*, proven by one real-UI traversal — not the backend twin alone. Declared via the
   `flows.json` `terminal` route + the backend journey's terminal state; the doctor cross-checks that
   a flow asserts the post-transition destination. **New diagnostic: `LZFE-JOURNEY-SEAM-001`.**
   *Risk, stated plainly:* this requires the doctor to know a frontend guard reads a backend lifecycle
   — cross-language, cross-artifact reasoning. If that proves infeasible to detect cheaply, the rule
   degrades to "any `[Critical]` lifecycle-advancing slice must have a frontend flow whose `terminal`
   is a route, not a wizard step" — coarser, but still forcing the traversal to exist.

### Tier C — the rung-5 ceiling (runtime, not an analyzer)

5. **Mutation score as the only real depth metric, on critical journeys only.** A periodic /
   critical-path CI lane runs Stryker.NET (backend) + Stryker (TS) scoped to `[Critical]` slices and
   their journeys, and the doctor **consumes the score artifact** to gate against a threshold. AeroFortress
   does **not** invoke the mutation runner — same boundary as the rubric's C12 (`view_e2e_pair` checks
   file existence; `handler_go` parses coverprofile; CI runs the tools, AeroFortress reads the output). This
   is the only rung that answers "is the assertion meaningful?", and it is explicitly *not* a
   per-build compiler rule.

### Canonical form (determinism)

A journey's terminal is declared in **exactly one** place per side and the two are *linked*, never
duplicated: the **backend** journey owns the terminal *state* (the `JourneyPath` outcome it asserts);
the **frontend** flow owns the terminal *surface* (`flows.json.terminal` = the testID/route). The
seam rule (B4) is the cross-check between them. There is no third spelling and no "author preference"
fork — if a flow has a `backendJourney`, `terminal` is required; if it has none (UI-only flow),
`terminal` is forbidden.

## New diagnostics

Net-new, none exist today (honest per the rubric's Criterion 8.5):

- `LZFE-JOURNEY-002` — linked flow's spec does not assert its declared `terminal` (Tier A1).
- `LZFE-E2E-SKIP-IN-GATE-001` — a gate-class flow skipped in CI-gate mode (Tier A2).
- `LZ0020` — a `[Journey]` test body does not assert its path's post-condition (Tier B3). (Renumbered
  from the draft's `LZ0011`, which collides with the shipped `TestInfraPurityAnalyzer`.)
- `LZFE-JOURNEY-SEAM-001` — a lifecycle-advancing `[Critical]` slice has no frontend flow proving the
  post-transition navigation (Tier B4).

Existing, cited as-is: `LZ0008` (`CriticalJourneyAnalyzer.cs:29`), `LZ0010`
(`JourneyCoversCriticalAnalyzer.cs:32`), LZFE-JOURNEY (`journey-parity.mjs`).

## Scope / boundary

Generic, in-boundary. Every AeroFortress app with journeys benefits; the mechanism (`[Critical]` /
`[Journey]` attributes, the doctor, `flows.json`, the e2e-doctor) is already framework-owned. This is
**not** moving the 80/20 boundary (which needs ≥3-pilot evidence) — it is hardening an existing
in-boundary mechanism, so the one-pilot hostpoint incident + the C12/C13 lineage is sufficient
justification for Tier A. No provider names, no DI/transport mechanics, no runtime invasion (Tier C
consumes an artifact; it does not run the runner).

## What it removes

The false confidence that `0 parity gaps` means "covered." After this, the doctor distinguishes
three states a single boolean hid: **unlinked / linked-but-entry-only / terminal-covered**. "Linked"
stops being printable as "covered." It adds no namespace and no new authoring surface beyond one
required `flows.json` field; the `terminal` field makes the previously-implicit arc explicit.

## Grading (self-assessment, cruel-first)

Gate: ≥ 8.5 weighted **and** no criterion < 7 → PASS; any < 6 → BLOCK; boundary violations always
BLOCK. Anchored `path:line`, strongest + weakest.

| # | Criterion (adapted to a lazuli-net analyzer proposal) | Score | Best evidence | Weakest spot |
|---|---|---|---|---|
| 1 | Cold-read legibility of the resulting convention | 9.0 | the rung table + canonical-form §make the arc explicit | a second required `flows.json` field every author must learn |
| 2 | Scope / boundary discipline (80/20, no runtime invasion) | 9.5 | reuses framework-owned attributes + doctor; Tier C consumes, never runs | Tier C's mutation lane is adjacent to the "no external runner" line — held by *consume-artifact* framing |
| 3 | Determinism — one way to declare the terminal | 7.5 | canonical-form § fixes terminal to one place per side | two *sides* (state + surface) is inherently two declarations linked by B4 — a residual fork if B4 slips |
| 4 | Doctor enforcement named + severity path | 8.5 | four codes named, warn→hard-exit promotion reuses `journey-parity.mjs:63` | severities are proposed, not yet profile-mapped |
| 5 | Diagnostic-ID truthfulness (rubric C8.5) | 9.0 | all four under §New diagnostics as net-new; existing three cited at `path:line` | none material |
| 6 | Testability of the proposal itself | 8.5 | each analyzer gets a twin test (mirrors `CriticalJourneyAnalyzerTests`) | the seam rule's twin needs a cross-artifact fixture not yet sketched |
| 7 | Anti-theater honesty — does it catch the bug it claims? | 7.5 | Tier A catches "stops at the door" generically | the *precise* seam bug needs Tier B4, which is deferred **and** the hardest to make static |
| 8 | Smallness / what it removes | 8.5 | removes the linked-as-covered conflation; +1 field, 0 namespaces | adds four diagnostics — real surface, justified by the rung it buys |

Weighted ≈ **8.6**. No criterion < 7 → **PASS with notes.**

**Cruel summary.** The load-bearing rule for the triggering incident is **B4 (the seam rule)**, and
B4 is simultaneously the most valuable and the least obviously static-feasible — it asks the doctor
to reason that a *frontend guard* reads a *backend lifecycle*. If B4 reduces to its coarse fallback,
the headline ("enforce depth") is delivered mostly by Tier A (generic "don't stop at the door") + the
honest Tier C ceiling, with B4 as best-effort. That is still a strict improvement over today, but the
proposal must not oversell B4 as guaranteed. Determinism (C3, 7.5) is the other soft floor: depth has
two legitimate sides (state + surface), and keeping them from drifting *is* B4's job — so C3 and C7
share a root and rise together.

### Tracked cuts (PASS-with-notes)

- Map the four diagnostics to `production`/`strict`/`prototype` severities before Tier A lands.
- Spike B4 static-feasibility on the hostpoint onboarding case **before** committing the full seam
  rule; fall back to the coarse "terminal must be a route" form if cross-artifact detection is too
  costly.
- Define the cross-artifact fixture for the seam-rule twin test.

## Rollout

Tier A behind the existing warn-only LZFE-JOURNEY posture → promote to hard-exit once the pilot's
flows declare `terminal`. Tier B after the B4 feasibility spike. Tier C as a separate critical-path
CI lane, deferred until a `[Critical]` journey set exists to make the mutation score meaningful.
