---
id: b11b19d5-a362-435e-913b-188ced0dd75f
slug: decisions
type: doc
title: Investigação: paginação nativa no lazuli-net (estilo pagy) — veredito VALE, com shape definido
tags: 
provenance: observado
evidence: Sessão com lazuli-backend (call_agent) em 2026-06-10; slices src/Hostpoint.Api/Modules/Trust/Slices/ListPublicPointReviews.cs e Catalog/Slices/ListOperatorPublicPoints.cs
decay: stable
created: 2026-06-11T00:19:30.410255600+00:00
updated: 2026-06-11T00:19:30.410255600+00:00
validated: 2026-06-11T00:19:30.410255600+00:00
links: 
---

Investigação feita no piloto hostpoint (2026-06-10), validada com o especialista lazuli-backend. **Veredito: vale — é convention+enforcement, não capability** (análogo de coleção do `Result<T>`: sem shape canônico de página, o orval gera tipos ad-hoc e o spine fica cego). Evidência: dois slices paginados hand-rolled idênticos no piloto (`ListPublicPointReviews`, `ListOperatorPublicPoints`) e o frontend duplicou pager numerado + load-more acumulado; uma das duas implementações já divergiu (faltou tiebreaker — corrigido).

Shape recomendado (package-first: nasce no lazuli-net, chega aqui por bump):
1. `Page<T>(Items, TotalCount, PageNumber, PageSize)` em **Lazuli.Abstractions** (PageNumber, não Page — CS0542).
2. `ToPageAsync(this IOrderedQueryable<T>, page, pageSize, maxPageSize=100, ct)` em pacote satélite novo **Lazuli.EntityFrameworkCore** (nenhum pacote runtime referencia EF hoje). `IOrderedQueryable` força OrderBy pelo type system (enforcement que sobrevive sem doctor); clamp embutido; count+página sobre o mesmo queryable.
3. SEM `PageRequest` no wire — Input flat `record Input(int Page = 1, int PageSize = 20)`; MaxPageSize é policy do servidor. Agregados extras (média, fotos) por composição: `Output(Page<T> X, ...)`.
4. **@lazuli/react**: tipo estrutural `Page<T>` + `usePager` (page/q debounce/reset) + `useAccumulatedPages` (load-more com dedupe/replace-on-page-1) — fetch-agnostic, não envolve o hook gerado.
5. Doctor: LZ warn pra materialização unbounded (`ToListAsync` sem Take/ToPageAsync em DbSet); fase 2 warn pra tiebreaker ausente; OpenAPI deve emitir os 4 campos required+non-nullable (senão orval gera opcionais e o tipo estrutural não casa).
Riscos: COUNT extra explícito no contrato (ok p/ admin lists); offset profundo → `CursorPage<T>` como SEGUNDA primitive quando um piloto precisar (YAGNI, nunca unificar); `.Select()` degrada IOrderedQueryable → idioma é paginar a entidade ordenada e projetar a página em memória.
