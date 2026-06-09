---
id: 0006
title: Design skill — the context loader for UI work
type: prd
stage: 6 of 7
status: ready
created: 2026-06-09
---

# PRD — Design skill (lazuli-design)

## Problem
The constitution, kit, and recipes only determinize an agent that has them in context. Nothing today
guarantees that: an agent asked to "add a screen" may never open DESIGN-CONVENTIONS.md or the recipe,
and lint feedback arrives after the bad composition is already written. The last unreliable link is
context loading itself.

## Why now (or why ever)
Layers 1–3 exist after 0005; without the loader, their effect depends on the agent remembering to
look. The skill is what makes "agent builds UI" reliably start from the vocabulary and end with the
verification ritual — closing the loop the user asked for ("o mais deterministico possivel").

## Outcome — done means
- `.claude/skills/lazuli-design/SKILL.md` exists in this repo (dogfood) and in
  `templates/lazuli-app/.claude/skills/lazuli-design/SKILL.md` (shipped to new apps), byte-identical.
- Triggered by UI work (creating/modifying `*.view.tsx`, `ui/`, screen requests), it: loads the
  relevant constitution sections + the matching recipe, instructs build-by-instantiation, and ends
  with the exit ritual — lint green + render + screenshot self-review against a fixed checklist.
- The skill contains zero rules of its own — pointers only.

## Non-goals
- No second source of truth: the skill never restates token values, kit props, or rule text.
- No screenshot-diff CI / visual regression infra (killed) — the screenshot step is agent eyes at
  build time, nothing persisted or gated mechanically.
- No auto-invocation machinery beyond the skill description/triggers (hooks are the app's choice).
- No pilot installation here (pauta gets it in 0007; future apps via the template).

## User stories
- As a coding agent told "make the settings screen", the skill routes me: constitution § → nearest
  recipe → instantiate → check → render → self-review — and I never compose a screen from a blank context.

## Constraints
- Skill body ≤ 60 lines — it's a loader, not a manual; budget forces pointer discipline.
- Paths it references must exist (the structural gate); recipe references go through the
  DESIGN-CONVENTIONS.md index, not hardcoded per-screen paths, so new recipes need no skill edit.
- Pilot variant note: in a pilot, the constitution lives in the framework checkout declared by
  `Lazuli.toml [framework] repo` — the skill states how to resolve it.

## Open questions
None.
