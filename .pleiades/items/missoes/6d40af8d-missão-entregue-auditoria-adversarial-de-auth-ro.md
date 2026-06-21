---
id: 6d40af8d-ddd1-4f66-aa60-ae0db13ccf4a
slug: missoes
type: doc
title: Missão ENTREGUE: auditoria adversarial de auth (round 1+2) — 18 achados, 13 corrigidos+testados, smoke harness criado
tags: auth, audit, security, session, roles, cookies, wave, entregue
provenance: observado
evidence: lazuli-net main, commits fa293e5/6037a69/caf57c7/ecbf23d (lote A) + 75d2f9a/92494a8/71c11f8 (round-2). Verde: frontend 162 testes, Lazuli.Auth.Tests 8/8, Cli.Tests 30/30, auth-smoke 53/53.
decay: stable
created: 2026-06-15T15:20:02.277314300+00:00
updated: 2026-06-15T15:20:02.277314300+00:00
validated: 2026-06-15T15:20:02.277314300+00:00
links: 
---

O usuário pediu o MÁXIMO de bugs de auth além do bug do guest-guard (foco: expiração, roles, cookies, F5), cada um pinado com teste. Dois tracks adversariais em paralelo (lazuli-backend + lazuli-frontend), orquestrados, verificados pelo orquestrador, levados a verde.

## CORRIGIDOS + TESTADOS (13)
**Backend lib (Lazuli.Auth, commit 6037a69 + tests novos):**
- RoleClaimType/NameClaimType não pinados c/ MapInboundClaims=false → `[Authorize(Roles)]`/`IsInRole`/`Identity.Name` falhavam silenciosos (High). Pinados "role"/"name".
- ClockSkew 5min default num token de 15min → ~20min de vida. → 30s.
- ValidAlgorithms não pinado → alg substitution. → [HS256] + RequireSignedTokens/ExpirationTime.
- role/name `""` em vez de null → `Role is null` nunca dispara. Absent-when-empty + readers normalizam.
**Backend templates (CLI, commits caf57c7 + 92494a8):**
- #5 (CRÍTICO): `lazuli g auth` NÃO COMPILAVA — slices públicas (Register/Login/Refresh/Logout + email/oauth) sem decisão de auth → LZ0022 Error. Fix: `.AllowAnonymous()` em 9 slices.
- #8: ResetPassword revoga TODAS as famílias do user (RevokeAllForUser) — recovery de takeover evicta o atacante.
- #9: VerifyPhone loca OTP após 5 tentativas (TooManyAttempts).
- #10: teto absoluto de família = 90d (SessionToken.FamilyMaxAge).
- #11: Login verifica contra hash dummy quando user não existe → sem enumeração por timing.
**Frontend (@lazuli/react 0.4.0→0.5.0, commit fa293e5):**
- toSessionState: erro transitório (5xx/timeout) virava `anonymous` → logout no F5/flap. Classifier opt-in `isUnauthorized` → transitório vira `loading`.
- useSession: bootstrap pendurado → splash infinito. `timeoutMs` opt-in.
- bootstrapSession agora em `singleFlight` → double-bootstrap (StrictMode) não queima a família (LZFE029 no boot). Primitivo `single-flight.ts` exportado.
- guardSession: `allow` aceita predicado (user)=>boolean → guard de role/capability é o mesmo primitivo.

## VERIFICAÇÃO criada
`tools/auth-smoke.sh` (commit 75d2f9a): render+compile+test do scaffold de auth headless (sem Docker). 53/53 testes gerados verdes + afirma ausência de LZ0022. Fecha a lacuna que deixou o #5 passar.

## NÃO shipado / decisões
- #7 (corrida de rotação cross-tab) → spec [[753aa853]] (RowVersion + grace 10s; reescreve journey [Critical] + precisa Postgres). Mitigado parcialmente pelo single-flight.
- #12 (enumeração no registro via 409) e #13 (rate limiting) → decisões documentadas no Account.ctx.md (409 = trade-off de UX; rate-limit = infra de plataforma).
- **DESCOBERTA grande**: o scaffold de auth drifou do doctor → [[a2d917e2]] (LZ0012/0017/0021 Error; app gerado não passa). Round de conformância pendente (decisão do dono em LZ0021).

Continuação da wave de guard/seam [[36c06c8f]]. Limites de honestidade: testes de template shipam nos apps gerados; o path Postgres do #7 e o doctor-100%-limpo não rodaram aqui.
