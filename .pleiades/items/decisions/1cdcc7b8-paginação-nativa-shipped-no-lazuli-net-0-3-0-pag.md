---
id: 1cdcc7b8-b031-4833-ae84-0df3a14d6546
slug: decisions
type: doc
title: Paginação nativa SHIPPED no lazuli-net 0.3.0 — Page<T>, Lazuli.EntityFrameworkCore, seam OpenAPI, LZ0027
tags: pagination, lazuli-entityframeworkcore, lz0027, openapi, release-0.3.0, package-first
provenance: observado
evidence: lazuli-net main 263d308..0358211: src/Lazuli.Abstractions/Page.cs, src/Lazuli.EntityFrameworkCore/QueryablePageExtensions.cs, src/Lazuli.AspNetCore/OpenApiExtensions.cs, analyzers/Lazuli.Doctor/UnboundedMaterializationAnalyzer.cs, examples/sample-app/backend/Sample.Api/Modules/Wallets/Slices/ListWallets.cs, docs/CONVENTIONS.md; local-feed/Lazuli.*.0.3.0.nupkg (14 pacotes)
decay: stable
created: 2026-06-11T01:43:28.980456+00:00
updated: 2026-06-11T01:43:28.980456+00:00
validated: 2026-06-11T01:43:28.980456+00:00
links: 
---

## Shipped (2026-06-10, 7 commits no main, 192 testes verdes, pack 0.3.0 no local-feed)

Implementação do lado .NET da decisão de paginação (validada na investigação do piloto hostpoint):

- **`Page<T>(Items, TotalCount, PageNumber, PageSize)`** em `src/Lazuli.Abstractions/Page.cs` — sealed record + **`Page<T>.Select(selector)`** (adendo ao shape original, revelado pelo dogfood: sem ele, todo slice re-embrulha os 4 campos à mão ao projetar a página em memória).
- **`Lazuli.EntityFrameworkCore`** (pacote satélite NOVO): `QueryablePageExtensions.ToPageAsync(this IOrderedQueryable<T>, pageNumber, pageSize, maxPageSize=100, ct)`. Único pacote runtime que referencia EF Core (10.0.8); **fora do meta-package `Lazuli` de propósito** (o meta não impõe stack de persistência — comentário gravado no Lazuli.csproj).
- **Seam OpenAPI** (`AddLazuliOpenApi`): (1) reference id de `Page<T>` qualificado pelo slice do item — default era `PageOf<NomeCurto>`, colidindo entre slices com item views homônimos; agora `PageOfListWalletsWalletView`. (2) transformer pina os 4 campos required + **tipos numéricos puros**.
- **LZ0027** (`UnboundedMaterializationAnalyzer`, warn): ToListAsync/ToList/ToArrayAsync/ToArray no fim de chain DbSet-rooted (direta ou via local IQueryable, com rastreio de assignments) sem Take/ToPageAsync, escopado a classes `[Slice]`. Local AsQueryable de coleção em memória NÃO flagga (só locals com DbSet em alguma assignment).
- **Sample dogfood**: `ListWallets` é o slice paginado canônico (examples/sample-app) + `OpenApiPage.Tests.cs` pina o schema.
- **Docs**: seção "Pagination — the canonical page" em docs/CONVENTIONS.md + linha LZ0027 no catálogo. Warn de tiebreaker (fase 2) anotado lá como follow-up.

## Descoberta importante pro frontend (spine/orval)

O contrato OpenAPI dos apps emite ints como `type: ["integer","string"]` + pattern — é o `NumberHandling=AllowReadingFromString` dos Web defaults do ASP.NET refletido no doc 3.1. **É por isso que os ViewModels do hostpoint coagem com `Number(query.data?.totalCount) || 0`.** O seam agora corrige isso PARA O PAGE (integer puro); o resto do contrato continua union — conversa futura sobre corrigir app-wide (mudaria o contrato de todos os endpoints dos pilots).

## Próximo passo nos pilots
Bump pra 0.3.0 + migrar os 2 slices do hostpoint (ListPublicPointReviews — que ganha o fix do tiebreaker ausente — e ListOperatorPublicPoints) + os hooks do spine (`usePager`/`useAccumulatedPages`, lado lazuli-frontend). Re-pack da mesma versão exige purge do cache do consumidor (`rm -rf ~/.nuget/packages/<pkg>/<ver>`).
