---
id: a2776b80-a98d-4cd6-8bb7-439cb2ad9380
slug: decisions
type: decision
title: LZ0027 field-validated em 0.3.2 — corte é parent-scope (não write-slice heuristic); postura classe C = remediation ladder
tags: LZ0027, pagination, analyzer, doctor, lazuli-net, hostpoint, pauta
provenance: observado
evidence: dotnet build --no-incremental Hostpoint.Api @ Lazuli 0.3.2 → 0 Aviso(s); analyzers/Lazuli.Doctor/UnboundedMaterializationAnalyzer.cs (commits 7779457, effa05b); lazuli-net e314fdc (CONVENTIONS.md ladder); hostpoint RevokeOtherSessions.cs:19, MarkChatRead.cs:26, Refresh.cs:60
decay: stable
created: 2026-06-11T02:48:10.113651100+00:00
updated: 2026-06-11T02:48:10.113651100+00:00
validated: 2026-06-11T02:48:10.113651100+00:00
links: 
---

Feedback de campo do bump hostpoint (2026-06-10): LZ0027 @ 0.3.0 disparou ~31 sites, com 3 falso-positivos claros em write-slices (RevokeOtherSessions, MarkChatRead, Refresh.RevokeFamily — materializar-pra-mutar onde Take seria bug de correção).

**Veredito: nenhuma mudança nova no analyzer.** Os refinements 0.3.1 (exemption parent-scope: `Where` com equality/Contains em membro `*Id` entity-side, OrgId/TenantId excluídos) + 0.3.2 (entity-side only; GroupBy é bound próprio) — forçados pela adoção do pauta — já silenciam TODOS os 31 hits: rebuild completo de Hostpoint.Api contra 0.3.2 = **0 × LZ0027**. Os 3 da classe A são parent-scoped por natureza (`UserId ==`, `ChatId ==`, `FamilyId ==`): o idioma EF de mutação carrega filhos de UM agregado por chave.

**Por que parent-scope e NÃO a heurística write-slice** (resultado-não-flui-pro-return / SaveChangesAsync-no-Handle, proposta do orquestrador): a heurística exemptaria o eixo errado — um write-slice PODE materializar set tenant-wide pra mutar em loop (purge, bulk re-status), que é o mesmo defeito (memory spike) com remédio diferente (`ExecuteUpdateAsync`/`ExecuteDeleteAsync`, nunca Take). Parent-scope captura o idioma write legítimo sem abrir esse furo, é sintático/barato/previsível por humano; dataflow-to-return é caro e frágil em analyzer.

**Postura canônica classe C (read-lists "minhas" sem bound escrito)** — a remediation ladder, agora em docs/CONVENTIONS.md (commit e314fdc):
1. Bound de domínio nomeável → `.Take(N)` com const nomeada + comentário do porquê. Cap generoso injustificável = truncamento silencioso, NÃO é fix.
2. Set que acumula com uso (inbox, agenda, reservas, thread) → `ToPageAsync`/`Page<T>` MESMO sem UI de paging (page 1 generosa = mesma resposta, contrato honesto; usePager/useAccumulatedPages prontos no spine).
3. Write path não-aggregate-scoped → set-based ExecuteUpdate/Delete ou job batched.
4. Nenhum ainda → warn fica em pé: warning tier É o ledger de adoção; nunca suprimir.

**Blind spot deliberado documentado**: filho accreting de um agregado (mensagens de UM chat — ListChatThread) passa silencioso sob a exemption (recall trocado por precisão: falso-positivo ensina supressão, falso-negativo só não ensina). Paginar filho accreting é decisão de design fora do alcance do doctor — rung 2 da ladder. Pilots devem migrar ListChatThread/ListChatInbox pra Page<T> por design, não por warning.

**Versão a re-bumpar: 0.3.2** (já no local-feed desde a wave do pauta; nenhum 0.3.3 — empacotar binário idêntico é ruído). Hostpoint working tree já aponta 0.3.2 e builda limpo.
