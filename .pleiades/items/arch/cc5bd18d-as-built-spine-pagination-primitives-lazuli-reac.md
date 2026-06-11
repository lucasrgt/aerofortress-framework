---
id: cc5bd18d-4af6-4d43-a678-881a90675a1e
slug: arch
type: doc
title: As-built: spine pagination primitives (@lazuli/react 0.2.0) — Page<T>/toPageInfo, usePager, useAccumulatedPages
tags: lazuli-react, pagination, spine, frontend-sdk, as-built
provenance: observado
evidence: lazuli-net commits 8219ec2, 26c600c, ee61d42; frontend-sdk/packages/lazuli-react/src/{page,usePager,useAccumulatedPages}.ts; 20 testes novos verdes (135 total no workspace)
decay: stable
created: 2026-06-11T02:01:42.721110800+00:00
updated: 2026-06-11T02:01:42.721110800+00:00
validated: 2026-06-11T02:01:42.721110800+00:00
links: 
---

Shipped 2026-06-10 in lazuli-net (commits 8219ec2 code, 26c600c bump 0.1.0→0.2.0, ee61d42 docs). Supersedes the "vale, com shape definido" investigation doc on the frontend half — this is the as-built API.

**Files** (frontend-sdk/packages/lazuli-react/src): `page.ts`, `usePager.ts`, `useAccumulatedPages.ts` + co-located tests, exports in index.ts. Doc section "Pagination — one page shape, two pager hooks" in docs/FRONTEND-CONVENTIONS.md (after "The data layer").

**API:**
- `Page<T> { items: readonly T[]; totalCount; pageNumber; pageSize }` — structural, zero imports; casa com o shape que o backend pina no OpenAPI (4 campos required, numéricos).
- `toPageInfo(page)` → `PageInfo { pageCount(≥1), from, to, totalCount, hasPrev, hasNext }`; overloaded: `undefined` propaga (sem página → sem info → sem clamp).
- `usePager({ debounceMs=300, initialQ }?)` → `{ page, q, setQ, debouncedQ, next(pageCount?), prev(), reset() }`. q cru pro input; debouncedQ (trimmed) pro param do hook gerado. Reset pra page 1 só em mudança EFETIVA do termo settled (re-digitar o mesmo termo não chuta o usuário da página). page+debouncedQ num único useState (rewind atômico).
- `useAccumulatedPages({ keyOf, resetKey? })` → `{ page, fold(current), loadMore(), reset() }`; `fold` retorna `Accumulation<T> { items, hasMore }`.

**Decisão estrutural (a circularidade React):** o hook possui o `page` que a query precisa, e a query possui a resposta que o fold precisa — não há como passar `query.data` como argumento do hook (TDZ). Resolução: **two-step** — hook acima da query, `fold(query.data?.X)` abaixo, rodando em render via o padrão oficial "adjust state when a prop changes" (setState em render + re-render antes do commit), guardado por IDENTIDADE da última página dobrada. Para usePager não precisa two-step: o clamp chega por `next(pageCount?)` em closure de evento (avaliada no clique, sem TDZ).

**Sutilezas que importam (cobertas por teste):**
1. `resetKey` muda → zera acumulado/page MAS mantém `folded` (identidade) — é o que impede o placeholder do keepPreviousData (página do recurso anterior) de dobrar na acumulação nova.
2. merge dedupe: cópia fresca VENCE in-place (não só skip) — lista nunca dobra nem fica stale.
3. `loadMore` é no-op sem totalCount conhecido ou quando tudo chegou; guard de isFetching fica no ViewModel (hook é fetch-agnostic) — doc manda desabilitar o botão.
4. `fold` exige identidade estável (página direto de `query.data`); página reconstruída inline a cada render = loop infinito. Projetar itens DEPOIS do fold.
5. `reset()` (pós-submit) mantém a lista na tela até o head refetchado chegar (page 1 REPLACE).

**Desvios do spec original:** (a) `pager.q` é o valor cru do input; o param do servidor é `debouncedQ` (spec dizia "passa pager.q pro hook gerado") — naming do piloto mantido; (b) `toPageInfo` vive em page.ts como função livre (spec listava sob usePager — ela é pura e o pager é fetch-agnostic); (c) adicionados `reset()` nos dois hooks, `hasPrev/hasNext` no PageInfo e `initialQ` — necessidades provadas pelos fluxos do piloto (pós-submit rewind, disable de botões, deep-link).

Pilots adotam por bump (spine ainda vendorado — cópias auto-contidas, sem react-native/expo/client imports).
