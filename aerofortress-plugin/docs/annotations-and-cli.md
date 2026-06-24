# AeroFortress — Annotations & CLI

## Backend markers (pure attributes; inert if the doctor is removed)

- `[Slice]` — operation: nested `Input`/`Output` records, `Handle(Input, AppDb, CancellationToken) → Task<Result<Output>>`, `Map(IEndpointRouteBuilder)`, all four in order.
- `[Module]` — static class exposing `AddServices(IServiceCollection, IConfiguration)` + `Map(IEndpointRouteBuilder)`.
- `[ValueObject]` — immutable, no public ctor/setter, smart constructor returning `Result<T>`, always-valid.
- `[Entity]` — private ctor (EF rehydration), private setters, private `EnsureValid() → Result<T>` funnel on every create/mutate path.
- `[Critical]` — high-stakes slice: requires happy AND sad `[Journey]`, and a concurrency token on written entities (`[Timestamp] byte[]? RowVersion` or `[ConcurrencyCheck]`).
- `[Journey(typeof(Slice), JourneyPath.Happy|Sad)]` — on an `[E2E]` test; binds it to a `[Critical]` slice.
- `[Unit]` / `[Integration]` / `[E2E]` — xUnit traits categorizing co-located tests.
- `[JsonConverter(typeof(ScalarJsonConverter<TVo, TPrim>))]` — scalar VO crosses the wire as its primitive; OpenAPI schema mirrored by `AddAeroFortressOpenApi`.
- `[Endpoint(...)]` — endpoint nature: default `App`; `[Endpoint(Webhook)]`, `[Endpoint(Internal)]`, `[Endpoint(Audience = "admin")]`. Drives the orval audience filter.

## Frontend markers (file/folder conventions)

- `<Name>.view.tsx` — pure render, exactly one ViewModel.
- `<Name>.viewModel.ts` — hook `use<Name>Model()`, render- and platform-agnostic.
- `<Name>.test.tsx` — co-located test (renderHook for VMs, Providers render for Views).
- `<name>.i18n.ts` — per-feature locale namespace.
- `features/<zone>/<name>/` — screens grouped by audience, not domain.

## CLI

- `af new [name]` — full project (AeroFortress.toml, src/<App>.Api, Program.cs index, sample module, Modules.cs).
- `af g module <Name>` — module + `<Name>.ctx.md` + entities placeholder.
- `af g slice <Module> <Name>` — slice file + co-located `<Name>.Tests.cs`.
- `af g auth` — auth module (Login/Refresh/Register + journeys), Identity entity, session seam.
- `af g hub <Module> <Name>` — SignalR hub at `Modules/<Module>/Realtime/<Name>Hub.cs` (hub is wire: calls the slice, fans out).
- `af g view <Slice>` — frontend triple typed from the contract.
- `af gen client` — orval (react-query) → `client.gen/` hooks + mutator. Hook name = slice name via `.WithName(...)`.
- `af doctor` — Roslyn AF* + AeroFortress.toml validation + frontend AFFE*.
- `af build` / `af test` — dotnet + turbo per `[tasks]` in AeroFortress.toml.
