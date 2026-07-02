---
id: eb7635fc-dcdf-4ae5-8a6d-3f5b0da8f423
slug: build
type: fact
title: SHIPPED: framework v2.1.x + AVP v0.2.0 publicado (NuGet+npm) — pilots conformados ao 0.2.0
tags: shipped, avp, assay.net, 0.2.0, supply, pilots, publicado
provenance: observado
evidence: 2026-07-01: gh run 28551563366 success; nuget flatcontainer assay.net → 0.2.0; npm view 0.2.0 nos dois pacotes; pilots: dotnet test verde (132, 958) e pauta 17 fails idênticos em 0.1.8/0.2.0; commits 69b408d/068603a4/e10cca3 pushed
decay: volatile
created: 2026-06-25T18:08:30.703540900+00:00
updated: 2026-07-01T22:37:21.079201100+00:00
validated: 2026-07-01T22:37:21.079201100+00:00
links: 
---

STATUS (01/07/2026, noite): supply do framework e do AVP publicados; pilots conformados ao AVP 0.2.0.

**Framework** (estado de 25/06, não re-verificado): nuget AeroFortress.Framework + .Doctor 2.1.x; npm eslint-plugin 0.11.0, @aerofortress/react 0.6.0, @aerofortress/frontend-sdk 0.1.1. (Os pilots hoje referenciam Testing 2.1.0–2.1.2.)

**AVP (repo `avp`): v0.2.0 PUBLICADO em 01/07** — tag v0.2.0, workflow publish 28551563366 3/3 verde. NuGet Assay.Net **0.2.0**; npm @aerofortress/assay **0.2.0** + @aerofortress/eslint-plugin-assay **0.2.0** (primeira publicação do plugin), com provenance. GH release com o CHANGELOG (125 pontos da auditoria). Semântica nova relevante pros pilots: **5xx nunca conta como refusal** (crash ≠ proteção) e erro inesperado do oracle vira verdict FAIL com evidência (runner não aborta).

**Pilots — todos bumpados pra Assay.Net 0.2.0, suítes rodadas e pushed em 01/07:**
- **fluxoterra** (branch avp/fluxoterra-deepening, `69b408d`): 132/132 verde.
- **hostpoint** (branch release/consolidate-avp, `068603a4`): 958/958 verde + 1 skip env-gated (MercadoPago sandbox).
- **pauta-web** (main, `e10cca3`): 1212 verdes com todos os proofs AVP passando; **17 falhas pré-existentes** no módulo novo InternalAuthorizations (commit a2c067a do próprio pilot, 01/07) — provado idêntico no 0.1.8, ou seja, main do pauta já estava vermelha antes do bump.
- NENHUM pilot consome os pacotes npm do avp — só Assay.Net via PackageReference.

FALTA: nada do lado AVP/supply. A área vermelha do pauta (ASI/InternalAuthorizations) é dívida do pilot, não do supply.
