---
id: 9d8cad9c-0d43-452e-a168-da797ca9fede
slug: decisions
type: decision
title: Lazuli 0.4.0 — contract-honesty wave: pin numérico documento-inteiro (OpenAPI) + LZ0028 tiebreaker do ToPageAsync
tags: lazuli-net, LZ0028, openapi, pagination, doctor, 0.4.0
provenance: observado
evidence: lazuli-net 9cec4e0/53046ab/f11963b/0e7ced4; dotnet test Lazuli.slnx → 208 aprovados/0 falhas; local-feed/*0.4.0.nupkg (13)
decay: stable
created: 2026-06-11T03:43:41.549641700+00:00
updated: 2026-06-11T03:43:41.549641700+00:00
validated: 2026-06-11T03:43:41.549641700+00:00
links: 
---

Shipped 2026-06-11 no lazuli-net (commits 9cec4e0, 53046ab, f11963b, 0e7ced4), 13 nupkgs 0.4.0 no local-feed. Fecha as duas pontas soltas da wave de paginação.

**1. Pin numérico documento-inteiro** (`src/Lazuli.AspNetCore/OpenApiExtensions.cs`): document transformer em `AddLazuliOpenApi` varre components.schemas (recursivo: properties, Items, AdditionalProperties, AllOf/AnyOf/OneOf), parâmetros, request/response bodies — todo schema com flag Integer|Number E String perde o String (e o Pattern de dígitos que só validava a forma string). **Nullability sobrevive** (só a tolerância de leitura é cortada — `double?` continua `["number","null"]`). Nodes `$ref` pulados de propósito (pinados onde definidos → sem ciclo). O pin do Page<T> ficou dono só de required+non-null; os numéricos dele agora andam no pin geral (o teste do Page dogfooda o transformer). Razão: NumberHandling=AllowReadingFromString é tolerância de LEITURA; o serializer sempre ESCREVE número — o documento deve declarar o que o wire fala. Mata os `Number(x)||0` dos ViewModels dos pilots (ex.: averageRating do hostpoint). Testes: `Sample.Api/OpenApiNumerics.Tests.cs` (body numeric fora do Page + query param).

**2. LZ0028 — a paged order needs a unique tiebreaker** (`analyzers/Lazuli.Doctor/PageOrderTiebreakerAnalyzer.cs`, warn, escopado a [Slice]): lê a chain de ordering que alimenta `ToPageAsync`; silencia se QUALQUER key da chain é PK da entidade — membro `Id` exato, ou `{Entity}Id` convencional do EF checado semanticamente contra o TYPE do parâmetro do lambda (`OrderBy(w => w.WalletId)` em Wallet silencia). **FK `*Id` (CustomerId) NÃO conta** — many-rows-shared. Flagga no token do ordering FINAL (onde o ThenBy(Id) entra). Limitação documentada: local pré-ordenado atravessando statement fica silencioso (warn só fala quando lê as keys); selector computado (x.Name.Length) nunca é único. 8 testes em `PageOrderTiebreakerAnalyzerTests.cs`. Nasce com 0 hits nos pilots (34/34 pauta + 2/2 hostpoint já com ThenBy(Id)).

**Versão 0.4.0 (minor, não patch)**: o pin muda o documento OpenAPI de TODOS os pilots (mais estrito apenas) → regen de client nos dois; regra nova junto. Suite 208/208 verde no bump. Pilots: re-bump 0.3.2→0.4.0, regen client, limpar casts Number(x)||0 (trabalho deles).
