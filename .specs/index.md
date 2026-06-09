# .specs — living map

Current wave: **Design SDK** — the deterministic design layer for agent-built UI.
Strategy: closed vocabulary (tokens + kit API) > mechanical enforcement (LZFE design band) >
copyable exemplars (canonical screens) > context loading (skill) > pilot harvest (pauta).
Determinism comes from removing decision space, not from adding instructions.

## Active

| id   | slug                  | status | depends_on       | parallel_safe | test_gate |
|------|-----------------------|--------|------------------|---------------|-----------|
| 0001 | design-constitution   | done¹  | —                | true          | maker approves taxonomy (one-time, explicit) |
| 0002 | token-contract        | done   | 0001             | true          | `npm --prefix frontend-sdk run check` ✓ |
| 0003 | ui-kit-primitives     | done   | 0001, 0002       | true          | `npm --prefix frontend-sdk run check` ✓ |
| 0004 | lzfe-design-band      | done   | 0001             | true          | `npm --prefix frontend-sdk run check` ✓ + 0 findings on sample |
| 0005 | canonical-screens     | done   | 0003, 0004       | true          | `npm --prefix frontend-sdk run check` ✓ (band + web jsx-a11y at error, 0 findings) |
| 0006 | design-skill          | done   | 0005             | true          | files exist + byte-identical ✓ |
| 0007 | pauta-design-dogfood  | done¹  | 0003, 0004, 0005 | true (pauta repo) | pauta lint 0 errors + 456 tests ✓ + harvest in PORTBACK-CHECKLIST.md |

¹ executed 2026-06-09 (all automated gates green); the maker's one-time visual check is pending:
0001 — read the taxonomy + kit API once; 0007 — eyeball the relifted screen + Dialog. Archive
(`changes/ → archive/` + As-Built) after that nod. Notable as-built deltas: the form recipe is
`deposit/` (mirrors the real backend slice; `itemForm` had no backend), `Text` gained `alert?:
boolean` (C2 superseded — the command-error surface), `ColorRole` gained `scrim` (pauta Dialog
harvest, inner loop), and renderUiKitWeb lives in `tools/ui-kit-web.mjs` (not generate.mjs).

## Dependency graph

```
0001 ──┬── 0002 ──── 0003 ──┐
       │                    ├── 0005 ──┬── 0006
       └── 0004 ────────────┘          └── 0007 (pauta repo)
```

Dispatch waves: `[0001]` → `[0002, 0004]` → `[0003]` → `[0005]` → `[0006, 0007]`.

## Next wave (cut from this one, by decision)

- **Pauta full relift** — "todas as telas": per-feature cells dispatched from 0007's ranked
  worklist (`pauta-web/frontend/docs/design-relift-worklist.md`), only after 0007 proves the
  pattern on the modal + exemplar screen.
- **Hostpoint adoption — conditional, aesthetics-frozen.** Hostpoint's UI is finished. It adopts
  the layer only as a pure token-aliasing refactor with ZERO visual delta (estética e estrutura
  inalteradas — maker's condition, verbatim), specced only after the pauta harvest is digested.
  If zero-delta can't be guaranteed, it never adopts; the taxonomy serves new apps, not finished ones.

## Archived

(none yet)

## Killed — named so it stays dead

- **Published `@lazuli/ui` npm component library** — a versioned component lib is the MUI/aerocoding
  vector: theming API surface, breaking releases, platform sprawl. The kit is scaffolded code the app owns.
- **Pilot-facing React Native kit** — hostpoint keeps NativeWind + its own components. The sample's
  mobile `ui/` mirror exists solely to keep the agnostic-View seam honest.
- **Dialog / Select / Tabs / Toast / DataTable in kit v1** — overlay a11y is a deep well; apps extend
  their own `ui/` per the constitution. Pauta's rebuilt Dialog (0007) is the evidence-gathering for a
  kit-v2 decision — evidence first, primitive later.
- **Theme runtime / dark-mode switcher** — dark *values* ship (proves the semantic layer); the
  switching mechanism is the app's.
- **Full LZFE harness adoption in pauta** — the design band is separable by construction; the MVVM/
  i18n/routing harness adoption is its own wave, not a rider on this one.
- **Icon set, Figma sync, visual-regression CI, TOML/JSON design spec, CSS-in-JS runtime** — capability,
  not convention + enforcement. The TOML spec is specifically the mini-language vector CLAUDE.md forbids.
