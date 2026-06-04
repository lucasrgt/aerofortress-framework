# lazuli-net

An opinionated .NET convention framework — Rails mindset in C#: minimal decision space, plain
stranger-maintainable code, and a "doctor" of Roslyn analyzers that enforce the conventions at build.

- **Slices** — one operation = one `[Slice]` (`Input` / `Output` / `Handle` / `Map`), thin endpoints.
- **Modular monolith** — modules are logical bounded contexts sharing one `AppDb`; a module writes only
  its own entities (LZ0009) and references others by id, so a context stays carvable into its own service.
- **The doctor** — LZ0001‑LZ0010 catch structural drift (slice shape, co-located tests, `ctx.md`
  freshness, write-ownership, `[Critical]` journeys) at build time.
- **Generators** — `lazuli new`, `lazuli g module / slice / crud / auth` scaffold exactly the convention.

See [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md).
