---
id: 0004
title: AFFE design band — ui-door, scale-only, semantic-colors
type: prd
stage: 4 of 7
status: ready
created: 2026-06-09
---

# PRD — AFFE design band (AFFE024–026)

## Problem
The kit closes the front door but lint guards no side doors: a view can still render `<div
style={{ padding: 13 }}>`, a Tailwind `[13px]`, an `rgb(...)`, or import the raw palette. AFFE012
catches exactly one leak (hex). Without the band, kit adoption is voluntary — which for agent-built
code means inconsistent.

## Why now (or why ever)
The band is what makes the vocabulary enforced rather than suggested — the difference between this
wave and a style guide nobody runs. 0005 promotes it to error; 0007 runs it against hostpoint to
harvest real-world gaps.

## Outcome — done means
- Three rules in `eslint-plugin-lazuli` with tests, mapped in `tools/doctor.mjs`, registered
  warn-tier in `eslint.config.mjs`, cataloged per the AFFE012 format:
  - AFFE024 `ui-door` — views render `@/ui` only: no host elements, no style/className attrs.
  - AFFE025 `scale-only` — no off-scale numeric spacing/typography literals outside `ui/`+tokens+tests.
  - AFFE026 `semantic-colors` — no rgb/hsl/oklch/named colors or raw-palette value-imports outside tokens (completing AFFE012).
- Plugin version bumped (0.4.0 → 0.5.0): the mirror-rebase signal for pilots.

## Non-goals
- No "interactive-states" or "form-anatomy" lint — those are enforced by construction in the kit
  (Button owns its states; Field owns the anatomy). Linting them would be theater on top of the API.
- No a11y rules inside the AFFE plugin — jsx-a11y / rn-a11y stay the wired-alongside standard;
  promotion to error happens in 0005, in config, per the existing posture.
- No error-tier enablement here (warn-first; 0005 promotes once the sample is exemplar-clean).
- No autofix (the fix is "use the kit/tokens" — a design decision, not a mechanical rewrite).

## User stories
- As an agent that just wrote `style={{ marginTop: 7 }}` in a view, I get a message naming the rule,
  the door (`@/ui`), and the token to reach for — and the diff never lands red-free without the fix.

## Constraints
- House plugin style: rules in `index.cjs` (kebab-case keys), tests in `index.test.cjs` using its
  existing harness, message format `AFFE0NN: <what> — <use instead>`.
- Exemption boundaries come from 0001 §C3 verbatim (`ui/` path, the AFFE012 token-file regex, tests).
- Zero false positives on the current sample tree at warn (it must not drown the backlog signal).

## Open questions
None.
