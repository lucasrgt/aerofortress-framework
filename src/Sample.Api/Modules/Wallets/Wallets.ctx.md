# Wallets — module context

## Boundaries

Owns a `Wallet` and its running `Balance`. Two slices: `GetBalance` (read) and `Deposit` (the only
inflow that grows a balance). Persistence is the shared `AppDb` — slices talk to it directly, no repository
or unit-of-work layer. The module is a logical bounded context: it writes only its own `Wallet`, so it
stays liftable later even though it shares the one store. The entity and its slices live here, never in a
root `Domain/` folder.

## Design notes

- **`Money` owns the amount rule, once.** "amount >= 0" lives in the `Money` value object; slices only
  `Collect` its failure and `AppDb` only re-checks at the DB↔domain boundary (the converter's
  `Money.From(d).Value`). The rule is never restated in a slice or in `OnModelCreating`.
- **Balance is authoritative server-side.** `Deposit` recomputes from the stored value and never trusts
  a client-sent total.
- **`Deposit` is `[Critical]` and not yet idempotent** — a retried request double-deposits; idempotency
  keys are planned. The "why" lives in the slice header because the slice is self-contained.
- **`WalletsDb.OnModelCreating` carries storage facts only** (precision 18,2; the `Money`↔decimal
  converter), never domain invariants.
