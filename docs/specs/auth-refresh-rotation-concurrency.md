# Spec — refresh-rotation concurrency (RowVersion + theft grace window)

**Status:** SPEC, not implemented. Gated by the round-2 auth audit (finding #7). Implement only with
the owner's go-ahead — it reworks a `[Critical]` security journey and one of its paths is verifiable
only against Postgres.

## Problem

`Refresh` reads a `UserSession` slot, checks `UsedAt == null`, then writes (marks the slot used, adds a
new slot). That read-then-write has no concurrency guard, so two refreshes of the **same** token that
race are last-write-wins:

- Trigger #1 — a single client double-firing (React StrictMode / double-bootstrap) — is already mitigated
  upstream by `@lazuli/react` 0.5.0 single-flight rotation (the two calls collapse into one).
- The residual is a genuine **cross-tab** race: two tabs sharing the one httpOnly refresh cookie each
  present the same live token at the same instant. Today both can pass the `UsedAt == null` check and
  succeed, forking the family; the theft-detection (`UsedAt != null` ⇒ burn) is not atomic with the write.

The doctor already flags this: `LZ0026` (warn) fires on `Login`/`Register`/`Refresh` because
`UserSession`/`User` carry no concurrency token.

## Agreed design

1. **`[Timestamp] public byte[] RowVersion { get; set; }`** on `UserSession` (and the optimistic-
   concurrency check it enables). EF then makes the losing racer's `SaveChanges` throw
   `DbUpdateConcurrencyException` instead of silently winning.
2. **A 10-second theft grace window** keyed off `UsedAt`:
   - reuse of a rotated token **within 10s** of its `UsedAt` ⇒ **benign** — return a transient "retry"
     error, do **not** burn the family. The client re-reads the cookie the winner rotated and retries.
   - reuse **after** the window ⇒ **theft** — burn the whole family (today's behavior, preserved).
3. **Catch `DbUpdateConcurrencyException`** on the rotation `SaveChanges` and map it to the same benign
   "retry" error — the racer that lost the optimistic-concurrency check is a benign concurrent refresh,
   not a thief.

## Why it is not shipped in round 2

The render+compile+test smoke (`tools/auth-smoke.sh`) is green, but two aspects of #7 are not safely
verifiable there, and one changes critical semantics:

- **It reworks the `[Critical]` sad journey.** `AuthJourney.Replayed_refresh_token_burns_the_whole_family`
  replays the spent token **immediately** and asserts the family is burned. Under the grace window an
  immediate replay is now *benign*, so that journey would have to advance the clock past 10s before the
  replay (to still prove theft-burn) **and** gain a new sibling test for the within-window benign case.
  Changing what a `[Critical]` security journey proves is an owner decision, not a silent refactor.
- **The concurrency-exception path needs Postgres.** The EF InMemory provider does not enforce optimistic
  concurrency, so it never raises `DbUpdateConcurrencyException`. The catch in (3) cannot be exercised by
  the in-memory smoke; it needs `Lazuli.Testing.Postgres` (Testcontainers → Docker). The grace-window
  logic in (2) *is* deterministically testable in-memory with a `TimeProvider`.

The compile harness now exists, so a follow-up can implement #7 and verify (1)+(2) compile and pass,
and (3) under a Postgres `[Integration]` test.

## Documented tests (to add when #7 lands)

```csharp
// (2) Grace window — deterministic, in-memory, via a controllable clock.
[Unit, Fact]
public async Task Reuse_within_the_grace_window_is_a_benign_retry_not_theft()
{
    // login → rotate (first→second) at T. Replay `first` at T+5s.
    // Expect: a transient/retry failure, the family is NOT burned, `second` still refreshes.
}

[Unit, Fact]
public async Task Reuse_after_the_grace_window_burns_the_family()
{
    // login → rotate at T. Replay `first` at T+11s.
    // Expect: Unauthorized AND the whole family dead (today's theft behavior). Replaces the immediate
    // replay in AuthJourney.Replayed_refresh_token_burns_the_whole_family.
}

// (3) Optimistic concurrency — Postgres only (Docker / Testcontainers).
[Integration, Fact]
public async Task Two_concurrent_refreshes_of_the_same_token_yield_one_winner_and_one_benign_retry()
{
    // Two parallel Refresh.Handle calls on the same live token against a real Postgres store.
    // Expect: exactly one rotates; the loser surfaces the benign retry (DbUpdateConcurrencyException
    // caught), never a false theft-burn, never a forked family.
}
```

## Migration note

Adding `RowVersion` is a schema change: consuming apps need an EF migration (`AddColumn RowVersion`,
`rowversion`/`bytea` + concurrency token). Call it out in the package release notes — it is the "alto
raio" the owner flagged.
