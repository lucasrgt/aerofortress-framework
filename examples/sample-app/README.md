# sample-app — the canonical Lazuli example

Demonstrates the Lazuli monorepo shape end to end (the same architecture in `docs/MONOREPO-ARCHITECTURE.md`):

- **`Lazuli.toml`** — the workspace manifest: one source of truth for the topology (backend + core + per-platform apps).
- **`backend/`** — the .NET API (`Sample.Api`) + its co-located tests (`Sample.Tests`), vertical slices under the
  `LZ*` Roslyn analyzers.
- **`frontend/core/`** — the **platform-agnostic** layer: the ViewModel (the single data door), the
  design-system-driven View (it reaches the UI only through `@/ui`, never `react-native` directly), i18n, and the
  generated client. It has **no** `react-native` / `react-dom` dependency — that is what makes it shareable.
- **`frontend/web/`** — the **web** design-system impl of `@/ui` (react-dom primitives).
- **`frontend/mobile/`** — the **mobile** design-system impl of `@/ui` (React Native primitives).

The View is written **once** (agnostic) in `core`; only the `@/ui` implementation differs per platform.

## Verification

- **Backend** — `dotnet test examples/sample-app/backend/Sample.Tests` (21 tests, green).
- **Frontend core + web** — the framework's vitest (run `npm run check` in `frontend-sdk/`) mounts the core's View
  against the **web** `@/ui` in jsdom — wired, not mocked.
- **Mobile** — built with the consumer's Expo/Metro toolchain (which provides `react-native`), like a real app's
  mobile target; it is not part of the framework's web-only check.
- **Full static typecheck + LZFE lint across `core`/`web`** runs once lazuli-net adopts a root npm workspace (a
  shared `node_modules` so the example — a sibling of `frontend/` — resolves the spine + deps). The example's
  `frontend/tsconfig.json` is already wired for it. That root workspace is itself the monorepo this example shows.
