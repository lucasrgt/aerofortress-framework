---
id: cb1ff4ae-760b-4fab-a8b6-7fd98666c4a5
slug: missoes
type: doc
title: Missão: re-implantar o lado identidade do session seam (onSignedIn/onIdentityChanged) — trabalho perdido no crash
tags: 
provenance: observado
evidence: git grep onSignedIn em todas as refs = vazio; rescue/win-crash-2026-06-11 = só line-endings; hostpoint lib/session.onSignedIn é a referência
decay: stable
created: 2026-06-12T22:28:03.725008500+00:00
updated: 2026-06-12T22:28:03.725008500+00:00
validated: 2026-06-12T22:28:03.725008500+00:00
links: 
---

Origem: a decisão [[dc6d7f9c]] descreve `onSignedIn`/`onIdentityChanged` no `createSessionSeam` como entregues (lazuli-react "0.3.0", commit "18a2a8c na branch rescue") — verificado em 2026-06-12 que o código NÃO existe em nenhuma ref do lazuli-net (a branch rescue só tem churn de line-endings). O trabalho se perdeu no crash de vídeo do Windows de 2026-06-11.

Escopo (re-implementação no spine, frontend-sdk/packages/lazuli-react/src/session-seam.ts):
1. `onSignedIn(result)` nas portas de auth + `clearSession` disparam um novo callback `onIdentityChanged` (wipe total — `queryClient.clear()`), distinto do `onSessionChanged` existente (reset leve na rotação). Nada força um sign-out antes de um sign-in na mesma aba.
2. Atualizar a copy da LZFE016 para nomear as duas metades do par token+cache (reset na rotação, wipe na troca de identidade) — a decisão menciona que isso tinha sido feito no "plugin 0.7.2" perdido; hoje o plugin está em 0.8.0 (onda validation-surface), então a mudança entra numa 0.8.x/0.9.0.
3. docs/FRONTEND-CONVENTIONS.md §Session — registrar o par.
4. Shape de referência: hostpoint app-side `lib/session.onSignedIn` (hand-rolled, funcionando em prod).

Critério de aceite: primitivo + teste no spine (o caso "linha COMPLETE da conta A não sobrevive ao sign-in da conta B"), LZFE016 atualizada com teste, catálogo, bump de versão.
