# Sample — the canonical feature unit

The blessed reference an app (or an agent) copies when building a screen. `items/` is one feature shown end to
end, demonstrating every part of the unit and the spine:

- **`Items.viewModel.ts`** — the data door. Imports the generated client (only place that may), stays
  platform-agnostic, and exposes its resource as `AsyncState<Item[]>` via `toAsyncState`.
- **`Items.view.tsx`** — render only. A thin `<Resource>` gate: loading / error / empty handled by construction,
  the list body runs only with resolved data. No `isPending`/`isError` in the View.
- **`Items.test.tsx`** — mounts the door against the real client (wired, not mocked) and asserts the resource
  starts `loading`. The uniform shape of every screen test.
- **`items.i18n.ts`** — per-feature copy in all three locales; no hardcoded strings leak into the View/ViewModel.

The imports (`@/client.gen/sample`, `@/ui`, `@/i18n`) are illustrative — they resolve inside a real lazuli-net
app (scaffolded by `lazuli new`), where `@lazuli/react`, the design system, the generated client and i18n are
wired. This sample is the **shape**; copy it, swap the slice + fields, and you have a conformant feature.
