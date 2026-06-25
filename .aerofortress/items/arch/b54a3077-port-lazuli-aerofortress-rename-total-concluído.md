---
id: b54a3077-edee-4a30-b7e0-28e68d7c3969
slug: arch
type: fact
title: Port Lazuli→AeroFortress: rename TOTAL concluído nos 5 repos (zero resíduo ativo)
tags: rebrand, lazuli, aerofortress, port, concluído, rule-ids, package-first
provenance: observado
evidence: grep -i 'lazuli|lzfe|LZ[0-9*]' = 0 em 5 repos (excl .aerofortress/.specs-archive/migrate.rs/caches); commits 3cbbeca,17a42c3,6d630d1,15190809,ebaaae3,c58873a,ac001f8
decay: stable
created: 2026-06-24T17:17:23.007124300+00:00
updated: 2026-06-25T15:04:40.151007900+00:00
validated: 2026-06-25T15:04:40.151007900+00:00
links: 
---

Rename completo Lazuli→AeroFortress nos 5 repos ativos — **0 ocorrências `lazuli`/`LZ*`/`lzfe`/`Lzfe` em código/docs/configs/scripts comitados**. Fecha [[477d2a4f]] e a decisão anterior de deferir o mirror (o usuário pediu "renomear tudo, nada pendente").

**Commits desta rodada (além dos comentários de build já feitos antes):**
- aerofortress-framework: `3cbbeca` (frontend-sdk: `"lzfe"` bucket+test, fixtures `Lzfe006`/`lzfe033`+dir→`affe`, `.targets`/`auth-smoke.sh`/docs `LZ*`→`AF*`; eslint-plugin self-test + 162 vitest verdes) + `17a42c3` (cautionary tales reescritas brand-free).
- pauta: `6d630d1` (LZ*/LZFE*→AF*/AFFE* em 337 arquivos comment/doc/ctx; `lzfe-*.mjs`→`affe-*`; tsc verde).
- hostpoint: `15190809` (LZ→AF + 6 `lzfe-*.mjs`→`affe-*`, lefthook verde) + `ebaaae3` (`.csproj` comments + `cleanLzfe`→`cleanAffe`).
- fluxoterra: `c58873a` (LZ→AF + package-lock órfão) + client.gen local realinhado (`lazuliClient`→`aerofortressClient`; gitignored).
- pleiades-harness: `ac001f8` (skills Clockwork + prompts + `clockwork.rs`/`nebula.rs`/components hint: `LZ0030`/`LZ*`/`LZFE033`→`AF0030`/`AF*`/`AFFE033`).

**Decisões-chave:**
- IDs de regra renomeados para `AF*`/`AFFE*` porque o framework já emite isso (pacotes `AeroFortress.Framework.* 2.0.0` consumidos pelos 3 pilots; eslint `eslint-plugin-aerofortress@0.10.0`). Nenhum uso FUNCIONAL de LZ nos pilots (zero `dotnet_diagnostic`/`pragma`/`SuppressMessage`) — rename foi cosmético, sem quebrar build.
- Cautionary tales (framework docs): reescritas SEM marca — `Lazuli-1`/`-the-language`→"the predecessor language", `Lazuli-1 vector/gesture`→"bespoke-compiler vector/gesture", `Lazuli-2 vector`→"source-gen vector", "old lazuli"→"the predecessor". Lição preservada, zero 'Lazuli'.
- **PRESERVADO de propósito**: `.aerofortress/items` (memória de rede — log imutável do que shipou: `@lazuli/react 0.4.0`, commits `lazuli-net`), `migrate.rs`/`plugins.rs` (compat do nome de mount antigo), `.specs/archive` (specs arquivadas), e o histórico git (branches/mensagens). Decisão do usuário: "reescrever tales, manter memória".

**Técnica reutilizável**: script Node de transform (variantes ALL-CAPS/Pascal/lower: `LZFE`/`Lzfe`/`lzfe`, `LZ\d`/`LZ\*`/`\bLZ\b`), com isolamento cirúrgico por-hunk (checkout HEAD→transform→stage→restaura WIP) pra commitar só o rename sem varrer WIP de terceiros — usado em todos os repos com WIP (hostpoint TravelerHome, fluxoterra products/residues, harness sidebar).
