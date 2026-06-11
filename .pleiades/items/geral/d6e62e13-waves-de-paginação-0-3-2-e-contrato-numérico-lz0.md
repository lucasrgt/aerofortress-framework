---
id: d6e62e13-e862-4594-80f6-2af85fec1323
slug: geral
type: fact
title: Waves de paginação (0.3.2) e contrato numérico + LZ0028 (0.4.0) CONCLUÍDAS nos dois pilots — tudo verde e commitado
tags: pagination, openapi, numeric-contract, shipped, pilots
provenance: observado
evidence: hostpoint db0e1c9..a72abf4 + 81e1eb6 + e194029; pauta bd6f0b7..cecfc88 + 86c5df2 + 32b262b + a177bf5 + 9fbd668; lazuli-net local-feed 0.3.x/0.4.0; gates rodados em 2026-06-11/12
decay: seasonal
created: 2026-06-11T03:27:32.283730+00:00
updated: 2026-06-11T04:06:46.694565500+00:00
validated: 2026-06-11T04:06:46.694565500+00:00
links: 
---

## Wave 0.3.2 — paginação canônica (2026-06-11)
Hostpoint (db0e1c9, 72d7170, a72abf4): 2 slices em ToPageAsync, 3 filas org-wide com bound Take+const, client regenerado, spine vendorado (hostpoint-os/src/ui/{page,usePager}.ts; app-core/src/lib/async/{page,useAccumulatedPages}.ts), 2 VMs nos hooks. Pauta: backend pelos especialistas (bd6f0b7..cecfc88, 794/794); frontend ficou pronto sem commit (relay morreu) → verificado e commitado como 86c5df2.

## Wave 0.4.0 — pin numérico documento-inteiro + LZ0028 (2026-06-11)
Framework: transformer varre schemas/params/bodies, todo numérico vira tipo puro (nullability preservada); LZ0028 warn (chain de ordering do ToPageAsync precisa de PK na chain — `Id` exato ou `{Entity}Id` semântico; FK `*Id` flagga; local pré-ordenado é silêncio documentado). Nasceu com 0 hits nos dois pilots.

**Hostpoint** (81e1eb6, e194029): regen matou 43 model-files de union (−1.188 LOC); typecheck revelou 3 senders mandando STRING em campo numérico (lat/long do property form, priceAmountCents) confiando no AllowReadingFromString — convertidos na fronteira do submit; depois limpeza read-side (3× formatCents estreitados, toNumber/parseIntegerish removidos com seus comentários agora-falsos, ~25 Number() inline → acesso direto; ficaram casts com outra razão: parse de input de usuário, toFixed-trick, filtros UI).

**Pauta** (32b262b, a177bf5, 9fbd668): mesmo padrão — 4 seams de formulário enviavam string crua (fix no submit), regen −1.4k LOC, limpeza read-side em 14 arquivos. 794/794 + 160/160, LZ0028 0 hits.

## Lição da dupla wave (vale pra próxima)
O pin numérico do contrato não revela só coerções REDUNDANTES (read-side) — revela coerções FALTANTES (write-side): formulários que enviavam string contando com a tolerância de leitura do servidor. O typecheck pós-regen é o detector; o fix é sempre na fronteira do submit, nunca relaxar o tipo.

## Follow-up aberto
`Money` ficou opaco (`{}`) no contrato do pauta — se o backend pinar Money como number no OpenAPI numa próxima wave, ~6 coerções a mais caem; o especialista lazuli-frontend do pauta guardou o mapa das três formas numéricas (puro/Money/PositiveDecimal) na rede de lá.
