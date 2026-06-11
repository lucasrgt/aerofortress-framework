This project uses **Lazuli**: an opinionated .NET convention bundle (Rails mindset in .NET) —
vertical slices, marked domain types, a Roslyn/ESLint "doctor" (LZ*/LZFE* rules), and an MVVM
frontend harness (React Native + RN-web) wired to the backend via generated typed hooks.

Route through the kit's specialists:
- **lazuli-scaffolder**: creating anything new — projects, modules, slices, auth, hubs,
  frontend view triples, client generation. Knows every `lazuli` CLI command and what each
  generates. Call it BEFORE hand-writing boilerplate.
- **lazuli-backend**: domain modeling and slice implementation — entities ([Entity]),
  value objects ([ValueObject]), Result<T>/error registries, module boundaries
  (write-ownership), [Critical]+[Journey] testing. The authority on backend conventions.
- **lazuli-frontend**: the MVVM triple (view/viewModel/test + i18n), data doors, generated
  client wiring, session rotation, mandatory loading/error/empty states, design tokens.
- **lazuli-doctor**: interpreting and fixing `lazuli doctor` output — any LZ00xx or LZFExxx
  violation. Give it the exact rule id + file; it knows what each rule enforces and the
  idiomatic fix (never suppress, fix the shape).

Hard rules the orchestrator must respect:
- Never add repository/unit-of-work layers (LZ0006). Handlers use AppDb directly.
- One feature = one slice file with Input/Output/Handle/Map. Tests co-located in src/.
- A module writes only its own entities; cross-module references by id, never EF FK.
- Frontend: Views never touch data (LZFE001); only ViewModels import the generated client.
- Error codes are registry constants, copy lives in i18n — never literals.

Deep reference (annotations, CLI, all doctor rules, conventions, decisions) lives in this
plugin's docs — query the network (slug `lazuli-net`) before assuming.
