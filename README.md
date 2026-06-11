# lazuli-net

An opinionated .NET convention framework — Rails mindset in C#: minimal decision space, plain
stranger-maintainable code, and a "doctor" of Roslyn analyzers that enforce the conventions at build.

- **Slices** — one operation = one `[Slice]` (`Input` / `Output` / `Handle` / `Map`), thin endpoints.
- **Modular monolith** — modules are logical bounded contexts sharing one `AppDb`; a module writes only
  its own entities (LZ0009) and references others by id, so a context stays carvable into its own service.
- **The doctor** — the `LZ####` analyzers catch structural drift (slice shape, co-located tests, `ctx.md`
  freshness, write-ownership, `[Critical]` journeys, registry error codes…) at build time. Full catalog in
  [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md).
- **Generators** — `lazuli new`, `lazuli g module / slice / entity / vo / crud / auth` scaffold exactly the convention.

Two laws hold it together: the output is always **stranger-maintainable** (plain, idiomatic C#), and the harness is
**doctor-removable** — `dotnet remove` the analyzers and the app still compiles and runs; you only lose enforcement.

## Getting started

Install the framework — one meta-package brings the whole runtime **and** the doctor analyzer:

```bash
dotnet add package Lazuli
```

A minimal app — `Program.cs` reads as a thin index:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLazuli();   // slice-aware OpenAPI + enum-as-name JSON
var app = builder.Build();
app.UseLazuli();                // serves the typed contract at /openapi/v1.json
app.Run();
```

Install the CLI (a .NET global tool) to scaffold the conventions:

```bash
dotnet tool install -g lazuli-cli       # the command is `lazuli`
lazuli new MyApp                        # scaffold a project
lazuli g slice Billing CreateInvoice    # a slice + its co-located test
lazuli g auth                           # the auth module (register/login/refresh/logout/me)
lazuli doctor                           # run the conventions over back + front
```

## Packages (nuget.org)

| Package | What it is |
|---|---|
| **`Lazuli`** | the meta — the whole runtime framework + the doctor analyzer (one reference; Rails-omakase) |
| `Lazuli.Abstractions` | the spine: `Result<T>`, `Error`, `[Slice]`, `[ValueObject]`, `[Entity]`, `[Module]` |
| `Lazuli.AspNetCore` | the ASP.NET wiring: `AddLazuli`/`UseLazuli`, slice-aware OpenAPI, `Result`→HTTP |
| `Lazuli.Auth` / `Lazuli.Identity` / `Lazuli.Mail` / `Lazuli.Sms` / `Lazuli.Storage` | the standard ports (no vendor SDK in core — the adapter is the app's choice) |
| `Lazuli.Doctor` | the `LZ####` Roslyn analyzers (ships with the meta; reference directly for analyzer-only) |
| `Lazuli.Testing` / `Lazuli.Testing.InMemory` | test helpers — add to a **test** project |
| `lazuli-cli` | the `lazuli` CLI, as a `dotnet tool` |

The focused packages are à la carte; the `Lazuli` meta is the front door. (The harness is removable: drop the
`Lazuli.Doctor` analyzer and the app still builds — you only lose the build-time enforcement.)

## Docs

- [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md) — the constitution: the slice shape + the full `LZ####` rule catalog.
- [`docs/FRONTEND-CONVENTIONS.md`](docs/FRONTEND-CONVENTIONS.md) — the React Native + web harness (`LZFE*`).
- [`docs/MONOREPO-ARCHITECTURE.md`](docs/MONOREPO-ARCHITECTURE.md) — how the pieces fit.
