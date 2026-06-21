---
id: 753aa853-6c7e-4949-b764-a70604ae3977
slug: specs
type: doc
title: Spec: refresh-rotation concurrency (RowVersion + theft grace window) — #7 SHIPADO
tags: auth, session, refresh, concurrency, spec, theft-detection, shipado
provenance: observado
evidence: docs/specs/auth-refresh-rotation-concurrency.md (Status: IMPLEMENTED). Código: Refresh.cs.cstmpl, UserSession.cs.cstmpl, SessionToken.cs.cstmpl, AccountErrorCodes.cs.cstmpl, Refresh.Tests.cs.cstmpl, AuthJourney.Tests.cs.cstmpl.
decay: stable
created: 2026-06-15T15:19:25.087051700+00:00
updated: 2026-06-16T00:34:25.376311900+00:00
validated: 2026-06-16T00:34:25.376311900+00:00
links: 
---

SHIPADO nesta sessão (era spec NÃO-implementada). As 3 partes do design acordado entraram no blueprint `auth`:

1. **`[Timestamp] byte[]? RowVersion`** em `UserSession` (e nas 5 entidades — cobre LZ0026 geral, ver [[a2d917e2]]).
2. **Janela de graça de 10s** — `SessionToken.RefreshReuseGrace`. No `Refresh`: reuso de token rotacionado DENTRO de 10s do `UsedAt` = benigno → `Error.Unauthorized(AccountErrorCodes.SessionRetry)`, NÃO queima a família; DEPOIS de 10s = theft → `RevokeFamily` (comportamento antigo). `session.UsedAt = now` virou `session.MarkUsed(now)`.
3. **catch `DbUpdateConcurrencyException`** no SaveChanges da rotação → mesmo `SessionRetry` benigno. Só um provider relacional dispara (InMemory não enforça); shipado no código mas NÃO coberto por teste gerado (um app consumidor pode não referenciar Lazuli.Testing.Postgres — gerar o teste quebraria a compilação lá).

**Journey [Critical] reescrita** (decisão de design tomada): em vez de avançar o relógio num teste E2E (que não controla o clock do servidor sem mexer no TestApp/JWT — risco), a cobertura foi DIVIDIDA:
- E2E sad journey `A_replayed_refresh_token_is_rejected_without_killing_the_live_one`: prova o que dá pra provar E2E sem avançar relógio — replay do token gasto é rejeitado (401) E o token rotacionado legítimo ainda dá refresh (a família sobrevive ao replay concorrente benigno — exatamente o comportamento NOVO do #7).
- Theft-burn (replay após a janela) + benign-retry (dentro da janela) provados deterministicamente em `RefreshTests` com `MutableClock` (TimeProvider controlável). O teste de aging absoluto também migrou pra backdate via EF metadata (`db.Entry(slot).Property(s => s.CreatedAt).CurrentValue`), já que CreatedAt virou private-set.

Migration note (apps consumidores): adicionar RowVersion é schema change — está nas release notes do doc. Parte da auditoria [[36c06c8f]].
