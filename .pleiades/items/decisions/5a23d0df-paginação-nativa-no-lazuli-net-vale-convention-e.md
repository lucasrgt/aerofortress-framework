---
id: 5a23d0df-439b-4dcc-b1ae-4cab086ce7a0
slug: decisions
type: decision
title: Paginação nativa no lazuli-net: vale (convention+enforcement), shape Page<T> + ToPageAsync(IOrderedQueryable) + usePager/useAccumulatedPages
tags: pagination, framework-boundary, lazuli-abstractions, spine, doctor, package-first
provenance: inferido
evidence: hostpoint: src/Hostpoint.Api/Modules/Trust/Slices/ListPublicPointReviews.cs (sem tiebreaker, linha 38), Modules/Catalog/Slices/ListOperatorPublicPoints.cs; clients/hostpoint-os/src/features/points/PointsList.viewModel.ts, clients/app-core/src/features/traveler/home/PublicPointReviews.viewModel.ts; lazuli-net: src/* (EF só em Lazuli.Testing.InMemory), src/Lazuli.Abstractions/Result.cs, frontend-sdk/packages/lazuli-react/src/*
decay: stable
created: 2026-06-11T00:17:12.443380+00:00
updated: 2026-06-11T00:17:12.443380+00:00
validated: 2026-06-11T00:17:12.443380+00:00
links: 
---

## Veredito
Paginação offset entra no framework: é shape de contrato repetido por todo list-slice de todo piloto (convention) que destrava a cadeia OpenAPI→orval→spine (enforcement/composição) — análogo coleção do `Result<T>`. NÃO é capability (não é lib de terceiro). Evidência: hostpoint duplicou o boilerplate 2x no backend (Trust/ListPublicPointReviews, Catalog/ListOperatorPublicPoints — clamps+count+skip/take+eco idênticos) e 2x no frontend (PointsList.viewModel pager numerado; PublicPointReviews.viewModel load-more acumulado). Package-first law manda: lazuli-net first.

## Shape decidido
- `Page<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)` → **Lazuli.Abstractions** (ao lado de Result.cs; zero deps). `PageNumber` e não `Page` (CS0542).
- `ToPageAsync<T>(this IOrderedQueryable<T>, int pageNumber, int pageSize, int maxPageSize = 100, ct)` → **pacote satélite novo `Lazuli.EntityFrameworkCore`** — nenhum pacote runtime do framework referencia EF hoje (só Lazuli.Testing.InMemory); a dep EF fica opt-in. Clamp embutido, ecoa valores efetivos; count+page sobre o MESMO queryable (mata count-antes-do-filtro-de-tenant). **`IOrderedQueryable`, não `IQueryable`** — paginar sem OrderBy não compila: enforcement pelo type system, sobrevive à remoção do doctor.
- SEM `PageRequest` no wire: MaxPageSize é policy do servidor (não payload); Input do slice fica flat (`int Page = 1, int PageSize = 20`) — contrato visível (LZ0001), binding minimal-API trivial.
- Output com extras: **composição** (`record Output(Page<ReviewView> Reviews, double AverageRating)`), não herança de record — não mexe em LZ0001, idiomático, o VM seleciona a página.
- Spine `@lazuli/react`: tipo estrutural `Page<T>` + `usePager` (page/q debounce→reset p.1/next-prev clampados + derivação pura pageInfo) + `useAccumulatedPages` (fold: replace na página 1, append+dedupe por keyOf, resetKey, hasMore=acc<totalCount). Hooks possuem estado, NÃO envolvem o hook gerado (fetch-agnostic).
- OpenAPI seam (AddLazuliOpenApi): emitir os 4 campos de Page como required+non-nullable, senão o orval gera `items?`/`totalCount?` e o tipo estrutural TS não casa.

## Doctor
- Regra principal é o TIPO (IOrderedQueryable), não analyzer.
- LZ novo warn: materialização unbounded (ToListAsync de chain de DbSet sem Take/ToPageAsync) — pega lista tenant-wide servida inteira.
- LZ novo warn (fase 2): ToPageAsync com última chave de ordenação não-única sem ThenBy — defeito REAL já presente no piloto: ListPublicPointReviews ordena OrderByDescending(CreatedAt) sem ThenBy(Id), doctor verde.
- NÃO criar: obrigação de Page<T> em todo list-slice; proibição de Skip/Take manual (over-policing, viola doctor-removable spirit).

## Riscos anotados
Count caro (explícito no contrato; cursor = `CursorPage<T>` FUTURO quando um piloto precisar, nunca unificar offset+cursor numa abstração); `.Select()` degrada IOrderedQueryable→IQueryable (idioma: paginar a entidade ordenada, projetar a página em memória — é o que os slices já fazem; documentar ou vira "por que não compila"); drift mid-pagination → dedupe no useAccumulatedPages; pós-bump migrar os 2 slices + 2 VMs do hostpoint (senão o piloto ensina o hand-rolled).
