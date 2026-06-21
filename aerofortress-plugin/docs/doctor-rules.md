# Lazuli — Doctor rules (LZ* backend, LZFE* frontend)

Never suppress. A firing rule means the shape is wrong; fix the shape.

## Backend (Roslyn)

- LZ0001 slice conformance (static class, Input/Output, Handle→Task<Result<T>>, Map, ordered)
- LZ0002 endpoint thin (expression/method group, never statement block)
- LZ0003 every slice has co-located `<Slice>.Tests.cs`
- LZ0004 every module has `<Module>.ctx.md` with non-empty `## Boundaries` + `## Design notes`
- LZ0005 ctx.md fresh: backticked citations resolve in source (not mtime)
- LZ0006 no IRepository / unit-of-work in slices
- LZ0007 file ≤ 500 LOC (Migrations/ exempt)
- LZ0008 [Critical] has happy AND sad [Journey]
- LZ0009 write-ownership: module writes only its own entities (reads/joins free; Tests exempt)
- LZ0010 a [Journey] must cover a [Critical] slice (journey on non-critical flagged)
- LZ0011 tests live in src/ (tests/<App>.Tests is infra only)
- LZ0012 Map calls `.WithName("<SliceName>")` (operationId = frontend hook name)
- LZ0013 [ValueObject] always-valid (immutable, smart constructor Result<T>)
- LZ0014 [Entity] encapsulation (private ctor/setters, EnsureValid funnel)
- LZ0015 [Module] shape (AddServices + Map)
- LZ0016 every module registered in explicit AddModules/MapModules
- LZ0017 Program.cs is an index (AddAeroFortress/AddPlatform/AddModules + Use/Map only)
- LZ0018 error code is a registry constant on *ErrorCodes (never literal)
- LZ0019 every *ErrorCodes constant is used (no orphans)
- LZ0020 a [Journey] asserts a post-condition (warning tier)
- LZ0021 unmarked domain types: DbSet<T> unmarked → [Entity]; complex member of [Entity] unmarked → [ValueObject]
- LZ0022 every endpoint declares authorization (.RequireAuthorization or .AllowAnonymous)
- LZ0023 injected ICurrentUser must be consulted
- LZ0024 raw SQL never absorbs runtime values as text (FromSql/ExecuteSql parameterized)
- LZ0025 held Result<T> checked before .Value/.Error
- LZ0026 write-side [Critical] declares concurrency posture (warning tier)

Self-harness (framework dev only): LZSELF001 ≤500 lines · LZSELF002 no TODO/FIXME/HACK ·
CS1591 public members documented.

## Frontend (eslint-plugin-aerofortress + ts-morph)

- LZFE001 View purity (no data layer in *.view.tsx; type-only contract imports OK)
- LZFE002 ViewModel data door (only VMs + lib/session + lib/guards import client.gen)
- LZFE003 no mocks/MSW outside *.test.*
- LZFE004 (planned) VM imports no JSX/react-dom
- LZFE005 every VM has sibling test calling renderHook()
- LZFE006 every View consuming a VM has sibling render test (import-based detection)
- LZFE007 (planned) VM exposes loading/error/empty
- LZFE008 endpoint coverage: every app-facing generated hook referenced by ≥1 data door (tool endpoint-coverage.mjs)
- LZFE009 VM platform-agnostic (no react-native/expo-*; ports injected)
- LZFE010 Views route async states through <Resource> (no raw isPending/isError)
- LZFE011 i18n parity: every locale declares the same flattened keys
- LZFE012 no inline hex outside token/theme/palette files
- LZFE013 every mutation surfaces failure (empty onError flagged)
- LZFE014 no hardcoded user-facing copy in Views (t() only)
- LZFE015 no imperative redirect in useEffect (declarative <Redirect/>)
- LZFE016 session one-door: token writes via lib/session seam (+me-cache reset)
- LZFE017 guards read tri-state SessionState, never raw boolean
- LZFE018 required route params via requiredParam() union
- LZFE019 no bare router.back()/history.back() (safeBack/useGoBack)
- LZFE020 no hardcoded API base URL (env/relative/injected)
- LZFE021 no dangerouslySetInnerHTML outside audited lib/html seam
- LZFE022 no open redirect (URL-sourced navigation through in-app allowlist)
- LZFE023 (planned) no orphan placeholders (// wire later, TODO, @ts-expect-error on data call)
- LZFE024 (planned, design band) UI door: Views use @/ui kit only (no lowercase JSX/style/className)
- LZFE025 scale only: no numeric literals in spacing/typography outside ui/tokens/tests
- LZFE026 (planned, design band) semantic colors only (no rgb/hsl/named/raw palette outside ui/)
- LZFE027 QueryClient carries mutation defaults (invalidate + feedback; meta.silent/expectedFailure opt-outs)
- LZFE028 (warn) no onSuccess refetch ritual (defaults already invalidate)
- LZFE029 refresh one-door (only lib/aerofortress-client, lib/session)

E2E/journey: LZFE-JOURNEY (back↔front flow parity) · LZFE-JOURNEY-002 (flow declares terminal
in flows.json and spec asserts it) · LZFE-E2E-SKIP-IN-GATE-001 (skipped gate-class flow fails CI
gate) · LZFE-JOURNEY-SEAM-001 (planned: lifecycle-advancing [Critical] needs frontend flow).
