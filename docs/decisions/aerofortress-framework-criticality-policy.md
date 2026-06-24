# Decision: criticality is a *policy dial*, not a blanket — and the strict opt-out is a forced, reviewable marker

**Status:** accepted; implemented in the 0.6.0 wave. New marker `[NonCritical]`
(`src/AeroFortress.Framework.Abstractions/NonCriticalAttribute.cs`), shared helper
(`analyzers/AeroFortress.Framework.Doctor/CriticalityPolicy.cs`), new rule **`AF0029`**
(`analyzers/AeroFortress.Framework.Doctor/CriticalityPolicyAnalyzer.cs`), `AF0008`/`AF0010` rewired to the helper, the
dial projected through `analyzers/AeroFortress.Framework.Doctor/buildTransitive/AeroFortress.Framework.Doctor.targets`, the template
manifest section (`templates/aerofortress-app/AeroFortress.toml`), and a light manifest check
(`src/AeroFortress.Framework.Cli/AeroFortressManifest.cs`). Tests green (AF0029 across the three levels + the `AF0008`/`AF0010`
strict interaction + the manifest validator). Self-graded **8.6 — PASS** (see §Grading).
**Date:** 2026-06-16.
**Supersedes/extends:** `CriticalJourneyAnalyzer` (AF0008) + `JourneyCoversCriticalAnalyzer` (AF0010). It
does not replace them — it makes *what counts as critical* a per-workspace policy they both read, and adds
the rung that forces the decision to be made (`AF0029`).
**Lineage:** the same "a decision, never an omission" posture as `AF0022` (every endpoint declares its
authorization). Here the decision is *criticality*, and `[NonCritical]` is to `[Critical]` what an explicit
`.AllowAnonymous()` is to `.RequireAuthorization()` — the visible, reviewable "no."

---

## Context

Criticality has been opt-in since it shipped: a `[Slice]` is high-stakes exactly when an author remembers
to write `[Critical]`, and `AF0008`/`AF0010` enforce the journeys only for those marked classes. That is the
right default — `[Critical]` is meant to be sparing, and the convention is explicit that "if half your slices
are critical, the marker has lost its meaning" (`CriticalAttribute.cs`).

But opt-in-only leaves a hole that some teams cannot tolerate, and it is the *same shape* as the hole
`AF0022` was built to close for authorization:

> The omission and the decision look identical. A slice with no `[Critical]` might be a considered "this
> failure is cheap" or an author who never thought about it. Nothing in the source — and nothing in the
> doctor — tells them apart. On a money/trust surface, "nobody decided" reading as "not critical" is exactly
> the silent failure `AF0022` named for the open endpoint.

Two facts make this a framework concern, not a per-app lint preference:

1. **The criticality bar is already framework-owned and enforced** (`[Critical]`, `[Journey]`, AF0008/10).
   Raising *how strictly it must be decided* is tuning an existing in-boundary mechanism, not inventing a
   capability — the 80/20 line does not move.
2. **There is no neutral way to say "I decided this is not critical."** Today the only signal is an absence,
   and an absence cannot be reviewed in a diff. A reviewer cannot challenge a decision that was never written
   down.

## Decision

Add a **criticality policy** — one dial in `AeroFortress.toml` (`[testing] criticality`) with three levels — and a
new marker `[NonCritical]` that the stricter levels make meaningful.

| Level | Meaning | AF0008 / AF0010 | AF0029 |
|---|---|---|---|
| `"opt-in"` (default, and the meaning of an absent dial) | only a `[Critical]` slice is critical | unchanged — today's behavior | inert |
| `"explicit"` | every slice must *decide*: `[Critical]` or `[NonCritical]` | unchanged (still keyed off `[Critical]`) | **errors** on an undecided slice |
| `"strict"` | an undecided slice is **treated as** `[Critical]` | unmarked slice ⇒ critical ⇒ journeys required; `[NonCritical]` is the opt-out | inert (the obligation lives in AF0008/10) |

Three design choices carry the decision:

### 1. A dial, not a blanket "everything is critical"

The naïve version of "be stricter" is "treat every slice as critical." That is self-defeating: it floods the
journey requirement onto trivial read slices, trains authors to suppress, and destroys the sparing meaning of
`[Critical]` the convention deliberately protects. The dial instead lets a workspace pick *how much decision*
it wants — `explicit` forces a choice without prejudging it; `strict` flips the default for teams whose domain
is mostly high-stakes (a payments core) while still leaving a clean opt-out. The default stays `opt-in`, so
**every existing app and every test is byte-for-byte unchanged** until someone turns the dial.

### 2. `[NonCritical]` is a *forced, reviewable* downgrade — not a rubber-stampable negation

Under `explicit`, the slice cannot stay silent: it must write `[Critical]` or `[NonCritical]`. The downgrade
is a positive marker that appears in the diff, can be questioned in review, and anchors the knowledge graph —
the mirror of an explicit `.AllowAnonymous()`. The alternative (let absence mean "not critical, and that's
fine") is precisely the rubber stamp: nothing is written, so nothing is reviewed. A marker you must add is a
decision a reviewer can see; an absence you may leave is a decision nobody can audit. This is the heart of the
lineage with `AF0022`.

### 3. The analyzer never parses TOML — the dial is projected through MSBuild

The doctor must not learn a config format. Teaching the analyzer to read `AeroFortress.toml` would (a) break Law 2
— the policy would no longer be removable with the package — and (b) couple a Roslyn analyzer to a file
format it has no other reason to know. Instead the doctor's `buildTransitive` targets — already auto-imported
by every consumer and removed with the package — locate `AeroFortress.toml`, read the one key with an MSBuild regex,
and hand the analyzers the resolved level through a `CompilerVisibleProperty`
(`build_property.AeroFortressCriticality`, surfaced in `AnalyzerConfigOptionsProvider.GlobalOptions`). Absent file
or key ⇒ `opt-in`. The analyzer sees a single projected string; the TOML stays the CLI's concern. `af
doctor`'s manifest leg adds a light textual check that the value, if present, names one of the three levels —
so a typo (`"strickt"`) is caught at the doctor instead of silently degrading to `opt-in` at build time.

### Canonical form (determinism)

The dial lives in **exactly one** place — `[testing] criticality` in the workspace `AeroFortress.toml` — and is
read **once** per compilation. The three rules share **one** definition of "critical under policy"
(`CriticalityPolicy.IsCriticalUnderPolicy`), so they cannot drift: a slice is critical for AF0008, AF0010, and
the strict-mode effect by the identical test. There is no per-rule or per-author fork.

## New diagnostics

Net-new, none exists today (honest per the rubric's Criterion 8.5):

- `AF0029` — under the `explicit` policy, a `[Slice]` declaring neither `[Critical]` nor `[NonCritical]`.
  Next free id; AF0001–AF0028 are taken (AF0028 = `PageOrderTiebreakerAnalyzer`).

Existing, cited as-is and rewired (not renumbered): `AF0008` (`CriticalJourneyAnalyzer.cs`), `AF0010`
(`JourneyCoversCriticalAnalyzer.cs`) — both now read the policy via `CriticalityPolicy` instead of the inline
`Slice && Critical` check. Under `opt-in`/`explicit` the helper reduces to that exact check, so their behavior
is identical to before for every workspace that has not turned the dial.

## Scope / boundary

Generic, in-boundary. Every AeroFortress app has slices, `[Critical]`, and journeys; the dial tunes an existing
framework mechanism rather than moving the 80/20 line. No source-gen of behavior (`[NonCritical]` is a pure
marker, like `[Critical]`), no runtime you inherit from, no vendor anything. Law 2 is preserved by
construction: the policy projection ships in the doctor's `buildTransitive` targets and leaves with the
package; remove the doctor and the app still compiles and runs, dial or no dial.

## What it removes

The conflation of "decided not critical" with "never decided." Before, the cheapest path to a green journey
bar was to *not mark* a slice — enforcement was opt-in and the opt-out was silent. After, a workspace that
wants to can demand the decision be made and written (`explicit`), or default the dangerous direction and
require an explicit downgrade (`strict`). It adds one diagnostic, one marker, one TOML key, and **zero** new
behavior for the default — the surface is paid for only by the teams that opt into the stricter bar.

## Grading (self-assessment, cruel-first)

Gate: ≥ 8.5 weighted **and** no criterion < 7 → PASS; any < 6 → BLOCK; boundary violations always BLOCK.

| # | Criterion | Score | Best evidence | Weakest spot |
|---|---|---|---|---|
| 1 | Cold-read legibility of the resulting convention | 9.0 | the level table + the `[NonCritical]` ↔ `.AllowAnonymous()` analogy land in one read | a reader must learn three level names; mitigated by the default being "do nothing" |
| 2 | Scope / boundary discipline (80/20, no runtime invasion) | 9.5 | tunes existing marks + doctor; pure markers; projection removable with the package | the MSBuild TOML read is logic outside the analyzer — held in-boundary by *shipping with* and *removing with* the doctor |
| 3 | Determinism — one dial, one definition of "critical" | 9.0 | single key, read once; `IsCriticalUnderPolicy` is the one shared test for all three rules | the dial is read in MSBuild *and* (lightly) re-validated in the CLI — two readers, but the CLI only checks legality, never decides |
| 4 | Doctor enforcement named + severity path | 8.5 | `AF0029` Error, implemented + tested across all three levels; AF0008/10 strict interaction tested | severities not yet mapped across `production`/`strict`/`prototype` profiles |
| 5 | Diagnostic-ID truthfulness (rubric C8.5) | 9.5 | `AF0029` genuinely net-new; the id-collision check (AF0028 taken) is in the code comment | none material |
| 6 | Testability | 8.5 | AF0029 ×3 levels, AF0008/AF0010 strict cases, manifest legal+typo cases — all green | the MSBuild projection itself is not exercised by the solution build (the targets import only on package consumption); verified by reasoning, not a test |
| 7 | Anti-theater honesty — does it close the hole it claims? | 8.0 | `explicit` makes "nobody decided" impossible to ship; `strict` defaults the safe direction | it cannot judge whether a `[NonCritical]` is *honest* — a lazy author can stamp `[NonCritical]` to silence AF0029, the same Rice-ceiling every marker rule hits |
| 8 | Smallness / what it removes | 8.5 | +1 diagnostic, +1 marker, +1 key, 0 behavior change for the default | three new level names are real surface, justified by the decision they force |

Weighted ≈ **8.6**. No criterion < 7 → **PASS.**

**Cruel summary.** The load-bearing claim is C7, and it has a ceiling: `[NonCritical]` can be rubber-stamped
just as `[Critical]` can be forgotten — no analyzer proves a downgrade is *honest* (Rice). What the dial
genuinely buys is making the decision **visible and required**, moving it from an unauditable absence to a
reviewable marker in the diff. That is a real improvement over opt-in-only, and it is the same improvement
`AF0022` made for authorization — but the proposal must not oversell it as proving the decision is *correct*,
only that one was made. C6 is the other soft floor: the MSBuild projection is not covered by the solution
build (the targets only import when the doctor is consumed as a package), so its correctness rests on careful
authoring plus the analyzer-side tests that drive the same `build_property` the projection emits.

### Tracked cuts (PASS)

- Map `AF0029` to `production`/`strict`/`prototype` severities (it is `Error` by default today).
- Cover the MSBuild projection end-to-end once a package-consuming fixture exists (today only the analyzer
  side — reading the projected `build_property` — is tested).
- Consider whether `af g slice` should emit `[NonCritical]` when scaffolding under a non-`opt-in`
  workspace, so a generated non-critical slice is born conformant. Deferred — the generator change is
  orthogonal to the policy and can follow once a pilot adopts a stricter level.
