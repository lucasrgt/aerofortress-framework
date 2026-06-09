---
id: 0006
title: The skill is a pointer-only loader with a fixed exit ritual
type: adr
status: accepted
created: 2026-06-09
supersedes: —
---

# ADR — lazuli-design is a thin loader (pointers + ritual), never a second constitution

## Context
Agent context loading is the last unreliable link: constitution, kit, and recipes work only when
present in the working context. A skill can fix that — but skills that carry their own content
desynchronize from the truth they summarize (the framework's own docs warn: the ctx discipline keeps
prose co-located, single-sourced). Also: "lindo" is not lintable; some verification must be visual.

## Decision
The skill is a router with three jobs and no knowledge: (1) load — read the relevant
DESIGN-CONVENTIONS.md sections + the recipe the index maps to the task; (2) constrain — build by
instantiating the recipe, extend the app's `ui/` when a primitive is missing (never inline paint);
(3) verify — run the lint gate, then render the screen and self-review a screenshot against a fixed
five-point checklist (hierarchy, spacing rhythm, the five states, contrast pairs, form anatomy).
One copy in this repo, one in the template, byte-identical; the repo copy is the canonical one.

## Alternatives considered
- **Skill embeds the taxonomy + kit API ("saves a read")** — rejected: second source of truth;
  drifts on the first 0001-superseding ADR and silently mis-trains every agent after.
- **Hook-based hard enforcement (block edits to *.view.tsx until docs read)** — rejected: hooks are
  harness-config, per-app and per-user; a skill ships in the repo/template and degrades gracefully.
- **Screenshot-diff CI as the beauty gate** — rejected (and killed in the index): visual-regression
  infra is apparatus; the band + recipes guarantee consistency mechanically, and the screenshot
  self-review covers the judgment slice at zero infra cost.
- **No skill (rely on CLAUDE.md pointers)** — rejected: CLAUDE.md is always-loaded context for THIS
  repo's agents, but pilots' agents and template users need the on-demand, task-shaped loading.

## Consequences
**We accept:** the ritual costs tokens per UI task (reading the doc + recipe each time); the
screenshot review is judgment, not proof — it catches ugly, not guarantees beauty.
**We gain:** every UI task starts from the same loaded context and ends at the same bar; recipes
become self-distributing (index-routed, no skill edits per recipe).
**We watch:** skill length creeping past 60 lines or gaining rule text — that's the constitution
leaking in; cut it back to pointers.
