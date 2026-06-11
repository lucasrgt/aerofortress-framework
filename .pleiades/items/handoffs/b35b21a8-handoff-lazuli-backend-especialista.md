---
id: b35b21a8-225f-406b-b4a9-f0e0a0fb92d0
slug: handoffs
type: doc
title: Handoff — lazuli-backend (especialista)
tags: lazuli-backend, echo
provenance: observado
evidence: echo 2bf4150d — ask_session: 2bf4150d-b646-4103-80df-5468ee43946a
decay: seasonal
created: 2026-06-11T02:40:35.788632600+00:00
updated: 2026-06-11T02:40:35.788632600+00:00
validated: 2026-06-11T02:40:35.788632600+00:00
links: 
---

# HANDOFF — especialista lazuli-backend (paginação canônica: design → framework → pilots)

## 1. O que eu estava fazendo e o estado atual

Três missões encadeadas, **todas concluídas**:

1. **Investigação de fronteira** (pedido do Lucas, via piloto hostpoint): veredito de que paginação offset É convention+enforcement (não capability) e merece entrar no framework. Shape decidido e gravado na rede de conhecimento (slug `decisions`, MCP `knowledge`/`rede` — o nome do server oscila entre sessões).
2. **Implementação .NET no lazuli-net** (`c:\Users\lucas\dev\lazuli-net`): shipped na wave **0.3.0**, depois **0.3.1** e **0.3.2** (dois refinamentos da regra LZ0027 forçados pela adoção do pauta). 12 commits no main no total. 13–14 nupkgs por versão em `local-feed/` (não git-versionado).
3. **Adoção no pauta** (`C:\Users\lucas\dev\dotnet-projects\pauta-monorepo`): 34 slices paginados, 5 commits (`bd6f0b7..cecfc88`), backend doctor **verde** (794/794 testes vs Postgres real/Testcontainers), frontend **vermelho de propósito** (71 erros TS em 37 ViewModels — o especialista lazuli-frontend assume; eslint/framework-sync do frontend passam).

**O hostpoint ainda está em 0.2.0** — quando bumpar, pula direto pra 0.3.2 e migra os 2 slices hand-rolled (`Trust/ListPublicPointReviews` — que ainda tem o bug de tiebreaker ausente em `OrderByDescending(CreatedAt)`, linha ~38 — e `Catalog/ListOperatorPublicPoints`) + os 2 ViewModels (`clients/hostpoint-os/src/features/points/PointsList.viewModel.ts`, `clients/app-core/src/features/traveler/home/PublicPointReviews.viewModel.ts`).

## 2. Decisões do domínio e arquivos exatos

**Shape canônico** (rationale: análogo de coleção do `Result<T>`; sem shape canônico o orval gera tipos ad-hoc e o spine não compõe):
- `Page<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)` + **`Page<T>.Select(selector)`** (adendo meu ao shape pedido — sem ele todo slice re-embrulha os 4 campos ao projetar; preserva o metadata) → `c:\Users\lucas\dev\lazuli-net\src\Lazuli.Abstractions\Page.cs`. `PageNumber` e não `Page` (CS0542).
- `ToPageAsync<T>(this IOrderedQueryable<T>, pageNumber, pageSize, maxPageSize = 100, ct)` → pacote satélite novo `src\Lazuli.EntityFrameworkCore\QueryablePageExtensions.cs`. **`IOrderedQueryable` é enforcement pelo type system** (sobrevive sem o doctor). Clamp embutido ecoa efetivos; count+page no MESMO queryable (mata count-antes-do-filtro-de-tenant). É o **único pacote runtime com dep EF**; **fora do meta-package `Lazuli` de propósito** (comentário gravado em `src\Lazuli\Lazuli.csproj`).
- SEM `PageRequest` no wire (MaxPageSize é policy do servidor); Input flat (`int Page = 1, int PageSize = N`); agregados extras por **composição** (`Output(Page<X> Xs, ...)`), nunca herança de record.
- Seam OpenAPI em `src\Lazuli.AspNetCore\OpenApiExtensions.cs`: (a) schema id de `Page<T>` qualificado pelo slice do item (`PageOfListWalletsWalletView` — o default usava nome curto e colidia); (b) transformer pina os 4 campos required + **integer puro** — o default vazava `type: ["integer","string"]` (NumberHandling dos Web defaults), que é o motivo histórico dos `Number(x) || 0` nos ViewModels dos pilots. Cirúrgico no Page; o resto do contrato continua com a union (conversa futura app-wide, não iniciada).
- **LZ0027** (warn) `analyzers\Lazuli.Doctor\UnboundedMaterializationAnalyzer.cs` + testes em `tests\Lazuli.Doctor.Tests\UnboundedMaterializationAnalyzerTests.cs` (12 testes): materialização DbSet-rooted sem `Take`/`ToPageAsync`/`GroupBy`, escopada a `[Slice]`, com rastreio de locals via assignments. **Exemptions (história em 2 patches)**: parent-scoped `Where` com `==`/`Contains` em membro `*Id` **enraizado no parâmetro do lambda** (entity-side only — o lado do valor `input.AgencyId` NÃO conta; esse vazamento foi o patch 0.3.2), com `OrgId`/`TenantId` blocklisted (são o tenant scope, o alvo da regra); `GroupBy` é bound próprio (rollups).
- Dogfood no sample: `examples\sample-app\backend\Sample.Api\Modules\Wallets\Slices\ListWallets.cs` (slice paginado canônico) + `OpenApiPage.Tests.cs` (pina o schema).
- Docs: `docs\CONVENTIONS.md` seção "Pagination — the canonical page" + linha LZ0027 no catálogo. **Warn de tiebreaker (última chave não-única sem ThenBy) é FASE 2 — anotado lá, não implementado.** Cursor pagination = futura `CursorPage<T>` separada, nunca unificada com offset.
- Versão mora em `build\Lazuli.Library.props` (`<Version>0.3.2</Version>`); pack: `dotnet pack Lazuli.slnx -c Release -o local-feed`. **Repack da mesma versão exige purge** do cache do consumidor (`~/.nuget/packages/<pkg>/<ver>`) — por isso fui 0.3.1→0.3.2 em vez de repack.

**No pauta**: Output property = substantivo plural do slice (ex.: `result.Value.Teams.Items`); page sizes originais preservados como defaults do Input + fallback no Map (`page ?? 1, pageSize ?? N`); `global using Lazuli.EntityFrameworkCore;` em `api\src\Pauta.Api\GlobalUsings.cs`. Gabaritos de migração: `Modules\Agency\Slices\ListTeams.cs` (simples) e `Modules\Account\Slices\UsersInTenant.cs` (projeção movida pra memória).

## 3. Pendências e armadilhas que só eu sei

- **Armadilha EF crítica (custou 4 testes)**: `Select(x => new Row(...)).OrderBy(...)` **compila mas não traduz** — EF inlina o construtor do record posicional no OrderBy e lança `InvalidOperationException` em runtime. Pós-`Select` só funciona com tipos anônimos. O idioma confiável é: paginar a ENTIDADE ordenada → `page.Select(...)` em memória → agregados juntam pelos ids da página (`GroupBy`/`ToDictionaryAsync` — ver `pauta...\Modules\AdminPanel\Slices\ListAllAgencies.cs`, que ficou até mais barato). Portback feito em docs/CONVENTIONS.md.
- **`ExecuteUpdateAsync`/`ExecuteDeleteAsync` não funcionam no pauta em dev** — o app roda no provider EF in-memory (só os TESTES usam Postgres real). Por isso `SetDefaultAgency` levou `Take(8)` backstop documentando o invariante "≤1 prior default" em vez de ExecuteUpdate — caso residual sem solução sintática na regra.
- **`ExportOperationLogs` é falso-negativo aceito** da LZ0027 v2 (o `Where(l => l.UserId == userId)` opcional dentro de `if` exempta sintaticamente; e export materializa por design). Anotado no relatório; se incomodar, o fix real é streaming/job, não Take.
- **O backend do pauta estava VERMELHO no HEAD antes de mim** — `AppDb.cs` 584 linhas, error LZ0007 pré-existente. Consertei com partial split (`AppDb.cs` = DbSets + OnModelCreating-índice; `AppDb.Model.Directory.cs` + `AppDb.Model.Operations.cs` = storage facts, moves verbatim). Se aparecer regressão de modelo, o diff é mecânico.
- Os testes do pauta com "Offset" em `JobStepsActivities` são **outro conceito** (`OffsetDays` de due dates) — não tocar; idem as menções nos ctx.md (falso alarme, não atualizei nenhum ctx.md).
- O frontend do pauta: os 71 erros TS são SÓ contract-shape (`.items` → `.<plural>.items` + params offset→page/pageSize). O spine `@lazuli/react` (`lazuli-net\frontend-sdk\packages\lazuli-react`) recebeu `usePager`/`useAccumulatedPages` do especialista frontend em paralelo — confirmar versão do plugin/mirror ao adotar.
- Migração em massa: usei 4 subagents paralelos com gabarito rígido (prompt com ANTES/DEPOIS literal + tabela de Output names + regras de tiebreaker/using órfão) — funcionou limpo; o padrão de prompt está na sessão congelada se precisar repetir no hostpoint.
- Knowledge network: 3 entradas no slug `decisions` (design da paginação; shipped 0.3.0; adoção pauta + refinamentos LZ0027). Consultar antes de decidir qualquer coisa de paginação/LZ0027.
