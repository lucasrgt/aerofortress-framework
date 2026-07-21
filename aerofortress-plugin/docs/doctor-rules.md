# AeroFortress — Doctor rules (AF* backend, AFFE* frontend)

Never suppress. A firing rule means the shape is wrong; fix the shape.

## Backend (Roslyn)

- AF0001 slice conformance (static class, Input/Output, Handle→Task<Result<T>>, Map, ordered)
- AF0002 endpoint thin (expression/method group, never statement block)
- AF0003 every slice has co-located `<Slice>.Tests.cs`
- AF0004 every module has `<Module>.ctx.md` with non-empty `## Boundaries` + `## Design notes`
- AF0005 ctx.md fresh: backticked citations resolve in source (not mtime)
- AF0006 no IRepository / unit-of-work in slices
- AF0007 file ≤ 500 LOC (Migrations/ exempt)
- AF0008 every shape-derived write has happy AND sad [Journey] proofs; ambiguity fails closed
- AF0009 write-ownership: module writes only its own entities (reads/joins free; Tests exempt)
- AF0010 a [Journey] must cover a shape-derived write slice
- AF0011 tests live in src/ (tests/<App>.Tests is infra only)
- AF0012 Map calls `.WithName("<SliceName>")` (operationId = frontend hook name)
- AF0013 [ValueObject] always-valid (immutable, smart constructor Result<T>)
- AF0014 [Entity] encapsulation (private ctor/setters, EnsureValid funnel)
- AF0015 [Module] shape (AddServices + Map)
- AF0016 every module registered in explicit AddModules/MapModules
- AF0017 Program.cs is an index (AddAeroFortress/AddPlatform/AddModules + Use/Map only)
- AF0018 error code is a registry constant on *ErrorCodes (never literal)
- AF0019 every *ErrorCodes constant is used (no orphans)
- AF0020 a [Journey] asserts its terminal post-condition; sad also proves unchanged state
- AF0021 unmarked domain types: DbSet<T> unmarked → [Entity]; complex member of [Entity] unmarked → [ValueObject]
- AF0022 every endpoint declares authorization (.RequireAuthorization or .AllowAnonymous)
- AF0023 injected ICurrentUser must be consulted
- AF0024 raw SQL never absorbs runtime values as text (FromSql/ExecuteSql parameterized)
- AF0025 held Result<T> checked before .Value/.Error
- AF0026 every persisted write declares concurrency posture (warning tier)
- AF0030 every declared criterion has an exact subject-bound [AVP] proof
- AF0031 every slice declares at least one criterion in its module spec manifest
- AF0032 backend tests cannot be skipped, conditional, explicit, or not executed
- AF0033 every write journey is an isolated executable E2E Fact/Theory in *Journey.Tests.cs

Self-harness (framework dev only): AFSELF001 ≤500 lines · AFSELF002 no TODO/FIXME/HACK ·
CS1591 public members documented.

## Frontend (eslint-plugin-aerofortress + ts-morph)

- AFFE001 View purity (no data layer in *.view.tsx; type-only contract imports OK)
- AFFE002 ViewModel data door (only VMs + lib/session + lib/guards import client.gen)
- AFFE003 no mocks/MSW outside *.test.*
- AFFE004 (planned) VM imports no JSX/react-dom
- AFFE005 every VM has sibling test calling renderHook()
- AFFE006 every View consuming a VM has sibling render test (import-based detection)
- AFFE007 (planned) VM exposes loading/error/empty
- AFFE008 endpoint coverage: every app-facing generated hook referenced by ≥1 data door (tool endpoint-coverage.mjs)
- AFFE009 VM platform-agnostic (no react-native/expo-*; ports injected)
- AFFE010 Views route async states through <Resource> (no raw isPending/isError)
- AFFE011 i18n parity: every locale declares the same flattened keys
- AFFE012 no inline hex outside token/theme/palette files
- AFFE013 every mutation surfaces failure (empty onError flagged)
- AFFE014 no hardcoded user-facing copy in Views (t() only)
- AFFE015 no imperative redirect in useEffect (declarative <Redirect/>)
- AFFE016 session one-door: token writes via lib/session seam (+me-cache reset)
- AFFE017 guards read tri-state SessionState, never raw boolean
- AFFE018 required route params via requiredParam() union
- AFFE019 no bare router.back()/history.back() (safeBack/useGoBack)
- AFFE020 no hardcoded API base URL (env/relative/injected)
- AFFE021 no dangerouslySetInnerHTML outside audited lib/html seam
- AFFE022 no open redirect (URL-sourced navigation through in-app allowlist)
- AFFE023 (planned) no orphan placeholders (// wire later, TODO, @ts-expect-error on data call)
- AFFE024 (planned, design band) UI door: Views use @/ui kit only (no lowercase JSX/style/className)
- AFFE025 scale only: no numeric literals in spacing/typography outside ui/tokens/tests
- AFFE026 (planned, design band) semantic colors only (no rgb/hsl/named/raw palette outside ui/)
- AFFE027 QueryClient carries mutation defaults (invalidate + feedback; meta.silent/expectedFailure opt-outs)
- AFFE028 (warn) no onSuccess refetch ritual (defaults already invalidate)
- AFFE029 refresh one-door (only lib/aerofortress-client, lib/session)
- AFFE030 no cast on navigation targets
- AFFE031 submit handles the invalid form path
- AFFE032 Controller surfaces fieldState validation errors
- AFFE033 every ViewModel declares `@verify` and has an exact co-located executable Assay proof
- AFFE034 frontend tests cannot be skipped, focused, todo, or conditionally excluded
- AFFE035 every ViewModel links distinct subject-bound happy and sad E2E flows

E2E/journey: AFFE-JOURNEY (back↔front flow parity) · AFFE-JOURNEY-002 (flow declares terminal
in flows.json and spec asserts it) · AFFE-E2E-SKIP-IN-GATE-001 (skipped gate-class flow fails CI
gate). A web flow naming backendSlices uses the canonical `requireBackend` import and a successful
`PW_API_URL` global-setup probe; its spec cannot intercept network requests or invoke API mocks.
Mocked UI smoke cases live in separate front-only specs.
