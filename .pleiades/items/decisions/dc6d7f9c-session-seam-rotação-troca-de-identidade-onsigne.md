---
id: dc6d7f9c-3a04-4d5c-b4e7-c6cbd46132e4
slug: decisions
type: decision
title: Session seam: rotação ≠ troca de identidade — onSignedIn/onIdentityChanged (DECISÃO OK; implementação PERDIDA no crash, re-implantar)
tags: 
provenance: observado
evidence: git grep onSignedIn em todas as refs = vazio (2026-06-12); main tem session-seam.ts só com onSessionChanged (d0fe5ad)
decay: stable
created: 2026-06-12T04:23:03.801609400+00:00
updated: 2026-06-12T22:27:53.961047400+00:00
validated: 2026-06-12T22:27:53.961047400+00:00
links: 
---

Decisão (2026-06-12): o `createSessionSeam` distingue dois tipos de transição de token. ROTAÇÃO (bootstrap/refresh, mesma identidade) → `onSessionChanged` (reset leve, ex. `resetQueries(me)`) — um re-mint de 15 min nunca pode zerar telas quentes. SIGN-IN/SIGN-OUT (identidade pode mudar) → `onSignedIn(result)` nas portas de auth + `clearSession` disparam `onIdentityChanged` (wipe total, `queryClient.clear()`) — qualquer linha em cache da conta A servida aos guards da conta B é corrupta por definição.

Origem: bug do piloto hostpoint — viajante recém-registrado caiu direto no mapa sem onboarding porque um `my-traveler` COMPLETE da conta anterior sobreviveu no cache. O hostpoint corrigiu app-side (lib/session.onSignedIn, hand-rolled).

## CORREÇÃO DE STATUS (2026-06-12, verificado no repo)

A implementação canônica descrita originalmente aqui (lazuli-react "0.3.0", plugin "0.7.2", commit "18a2a8c na branch rescue") **NÃO existe em nenhuma ref do lazuli-net** — `git grep onSignedIn` em todas as refs retorna vazio; a branch rescue/win-crash-2026-06-11 contém só churn de line-endings. O trabalho se perdeu no crash de vídeo do Windows. O que EXISTE no main: `createSessionSeam` com `onSessionChanged` (rotação, commit d0fe5ad), sem a metade identidade.

Nota de versão: @lazuli/react 0.3.0 foi reutilizada pela onda submitOrReveal (2026-06-12); a re-implantação do onSignedIn será outra versão.

A DECISÃO continua válida (o design foi acordado e o shape provado app-side no hostpoint). Re-implantação rastreada na missão [[relandar-session-seam-identidade]].
