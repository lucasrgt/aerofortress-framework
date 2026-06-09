---
id: 0002
title: One plain TS file is the single token source; scaffolded, owned by the app
type: adr
status: accepted
created: 2026-06-09
supersedes: —
---

# ADR — tokens.ts (plain data, scaffolded, app-owned) is the only token artifact

## Context
Tokens need a home that is: single-source (no drift), platform-neutral (web + RN consume it),
styling-mechanism-neutral (CSS vars, NativeWind, StyleSheet all map FROM it), doctor-removable, and
agent-legible. Candidates: npm package, CSS file, TS file, generated pair.

## Decision
One scaffolded `core/src/design/tokens.ts` — plain object literals typed by the 0001 taxonomy. The
app owns it; values are editable, names are lint-protected. Web kit consumes it via inline `style`
computed from the objects (zero styling deps); an app with Tailwind/NativeWind maps it into its
config by hand (documented, not shipped).

## Alternatives considered
- **`@lazuli/design-tokens` npm package** — rejected: values become framework-versioned (theming via
  package override = MUI), and it violates "ship the standard, not the plugins".
- **tokens.css as a second artifact** — rejected: two sources, guaranteed drift; CSS vars are one
  app-side mapping among several (RN can't read them at all).
- **Generated tokens.css from tokens.ts at build** — rejected: apparatus (a build step the app
  inherits); the kit doesn't need it and apps that want CSS vars write the 20-line mapping once.
- **JSON + codegen** — rejected: codegen of non-code, Lazuli-1 smell; TS literals are already typed.

## Consequences
**We accept:** apps using utility-class styling do a one-time manual mapping; the default palette is
opinionated (someone will dislike the blue — they own the file).
**We gain:** one greppable token truth; dark mode as a value swap proven in-repo; the kit gets a
dependency-free style source that renders identically in jsdom tests.
**We watch:** if every pilot writes the same CSS-vars mapping by hand, promote a documented snippet
in DESIGN-CONVENTIONS.md to a scaffold flag — still emitted code, never a build step.
