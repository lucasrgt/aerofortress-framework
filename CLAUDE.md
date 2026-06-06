# Lazuli (.NET) — Operating manual for AI agents

Lazuli is the **opinionated .NET convention bundle**: a standardized vertical-slice
architecture + a build-time harness (the doctor) + an ai-context discipline, so an LLM has
less to decide and what it writes is enforced. It is the **Rails mindset in .NET** — the
mentality (convention over configuration, quality control, semantic density), **not** the
mechanism (no runtime metaprogramming, no language). Reference codebase: `rails/rails`.

> This is **not** the Lazuli language (the Rust project — parked). Same name, same soul
> (semantic density for the AI + CoC), different body: plain, idiomatic .NET.

Mirrored verbatim at `AGENTS.md` for tooling that loads it (Codex, Aider, etc.).

---

## The two laws (never violate)

1. **Stranger-maintainable.** The output is always plain, idiomatic C# that a .NET dev who
   has never heard of Lazuli can read and maintain.
2. **Doctor-removable.** `dotnet remove` the analyzers and the project still **compiles and
   runs** — you only lose enforcement. The harness is wire, not apparatus.

Any feature that fails both — hidden source-gen of behavior, a DSL, a runtime you inherit
from, magic discovery — is **out, by construction**.

The goal is **not "less code"**. It is **semantic density**: more meaning per token for the
AI (rich types, standardized shapes, co-located context). Token savings follow; they are not
the target.

---

## Repository layout

```
src/Lazuli.Abstractions/      The thin wire: Result<T>, Error, [Slice], [ValueObject], [Entity]. A normal dependency.
analyzers/Lazuli.Doctor/      SHIPPED harness. LZ* rules — run on the USER's code.
analyzers/Lazuli.SelfHarness/ FRAMEWORK-DEV ONLY. LZSELF* rules — run on OUR code. Never shipped.
src/Sample.Api/               The reference app + canonical slice. The template source.
tests/Sample.Tests/           Slice tests, with intent.
build/Lazuli.Library.props    The library standard, declared once.
docs/CONVENTIONS.md           The constitution + slice shape + full rule catalog.
```

Ground every convention fact in `docs/CONVENTIONS.md`, never memory.

---

## The bar for code you write here

Every `Lazuli.*` library file is held to the self-harness. Write to it from the start:

- **File at or under 500 lines.** Past it, extract a concern — do not pack (`LZSELF001`).
- **Gold-standard XML docs on every public member.** Missing docs are a build error
  (`CS1591`). Lead with *why*, not *what*; use `<inheritdoc/>` on overrides.
- **No junk comments.** No `TODO`/`FIXME`/`HACK`/`XXX`, no tracking codes (`WAR-001`,
  `SPEC-001`), no materialized AI thoughts (`LZSELF002`). Only documentation worth reading.
- **Tests with intent**, not `1 + 1 == 2`. A test states a behavior the code must keep.

If the build fails on `LZSELF*`/`CS1591`, **fix the code — never suppress the rule.** The
target is source a Microsoft .NET MVP would read and be proud of.

---

## Build & verify — green before you are done

```
dotnet build Lazuli.slnx     # the doctor + self-harness run inside the build
dotnet test  Lazuli.slnx     # the slice tests
```

A green build means the conventions are held. Never leave the workspace red.

---

## The doctor vs the self-harness — keep them separate

- **`LZ*` (`Lazuli.Doctor`)** — rules on the **user's** code. Shipped. Enforces the slice
  convention (e.g. `LZ0001`: a `[Slice]` is a static class with a nested `Input` and `Output`, a
  `Handle` returning `Task<Result<T>>`, and a `Map`).
- **`LZSELF*` (`Lazuli.SelfHarness`)** — rules on **our own** code. `IsPackable=false`,
  referenced with `ReferenceOutputAssembly="false"`. **Never packaged, never in the
  production CLI or the published `Lazuli.Doctor`.** This is the `lazuli` vs `lazuli-dev`
  split: framework-dev tooling stays out of the published surface, always.

New framework-dev tooling never lands on the published surface.

---

## Scope discipline — the anti-drift guardrails

The two cautionary tales are concrete. **Lazuli-the-language** died from owning a compiler
(gargantuan apparatus, generated non-code, zero adoption). **Aerocoding** died from scope
explosion (a generator that metastasized into a full-SaaS meta-framework + frontend sprawl +
28K LOC of specs for unbuilt features). Do not repeat either:

- **No source-gen of behavior.** Plumbing only, if ever — and not yet. A source generator is
  a mini-compiler: the Lazuli-2 vector. Behavior always stays visible in the slice.
- **No vendor adapters in core.** Ship the *standard* a component follows, not the plugins.
- **No frontend/UI generation, no realtime, no multi-app sprawl.** The aerocoding failure
  modes — designed out.
- **No runtime framework you inherit from.** Conventions + analyzers, not base classes.
- **`[Slice]` stays a pure marker; `.ctx.md` stays prose.** Reject fattening either into a
  mini-language.

When a proposal smells like *capability* instead of *convention + enforcement*, it is a scope
violation. Reject in line — do not defer it to a checklist.

---

## Git discipline

- Stage specific files (`git add <path>`), never `-A`/`.`.
- One commit per concern; lowercase, present-tense imperative messages.
- Workspace green every commit (`dotnet build` + `dotnet test`).
- No `--force`, no history rewrites to escape a failing hook — fix forward.
