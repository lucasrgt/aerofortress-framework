---
id: 477d2a4f-f5e9-4358-8e3e-0b9fecb1ee5a
slug: geral
type: fact
title: Port Lazuli→AeroFortress (rebrand): CONCLUÍDO — 5 repos, commits, e o único resto (mirror package-first)
tags: rebrand, lazuli, aerofortress, port, concluido, package-first
provenance: observado
evidence: commits: aerofortress-framework e6073c0; pleiades-harness 1f8e636; hostpoint 3b65964b/5ab0d460/8c6951dc; pauta f3537bb/003a014/260d942; fluxoterra 2ad9c6c/ba4c2ec/226671a. framework build+249 tests green; fluxoterra api compila (copy falhou só por exe em uso)
decay: seasonal
created: 2026-06-24T00:13:12.587444100+00:00
updated: 2026-06-24T01:01:26.734196500+00:00
validated: 2026-06-24T01:01:26.734196500+00:00
links: 
---

Rebrand Lazuli→AeroFortress portado em TODOS os repos (pedido do Lucas, "porte totalmente"). O código C# real já era AeroFortress; o resto eram a camada de IA + metadados + docs + comentários.

**REGRAS:** `lazuli-net`→`aerofortress-framework` (repo/pkg/slug/checkout — remote GH é aerofortress-framework). `lazuli:` (erro CLI)→`af:`. CLI verb `lazuli`→`af`. `Lazuli`→`AeroFortress`. `LZFE`→`AFFE`, `LZ00nn`→`AF00nn`. `AddLazuli`→`AddAeroFortress`, `LazuliWebTest`→`AeroFortressWebTest`, `Lazuli.Testing.Postgres`→`AeroFortress.Framework.Testing.Postgres`. Agentes `lazuli-{frontend,backend,scaffolder,doctor}`→`aerofortress-*`, skill→`aerofortress-feature`. Mutator orval `lazuli-client.ts`/`lazuliClient`→`aerofortress-client.ts`/`aerofortressClient`. Scope npm `@lazuli/*`→`@aerofortress/*`. PRESERVADO: cautionary tales (Lazuli-1/2/-the-language, old-lazuli) + memória da rede (.aerofortress/items, handoffs, missoes, arquitetura, .specs/archive) + migrate.rs (migração FROM o nome velho).

**COMMITS (locais, não pushados exceto harness):**
- aerofortress-framework `e6073c0` (build + 249 testes VERDE).
- pleiades-harness `1f8e636` (PUSHED, v1.0.36; cargo+tsc verde) — incl. FIX de bug: seeding clonava lazuli-net.git/pleiades-plugin, agora aerofortress-framework.git/aerofortress-plugin.
- hostpoint `3b65964b`(routing)+`5ab0d460`(mutator, lefthook verde)+`8c6951dc`(docs/comentários).
- pauta `f3537bb`+`003a014`+`260d942`.
- fluxoterra `2ad9c6c`+`ba4c2ec`+`226671a` (compila limpo; WIP de products/suppliers PRESERVADO uncommitted).

**ÚNICO RESTO (de propósito — package-first):** nos pilots, o dir `clients/eslint-plugin-lazuli` (mirror) + scripts `lzfe-*.mjs` + as MENSAGENS de regra "LZFE032" vêm do mirror do eslint-plugin, que é REBASEADO do framework (agora AFFE). Resolve no próximo bump do pacote (copiar o index.cjs do framework + renomear lzfe-*→affe-*), NÃO por edição manual no piloto (desincronizaria). Substitui o estado "em andamento" anterior.
