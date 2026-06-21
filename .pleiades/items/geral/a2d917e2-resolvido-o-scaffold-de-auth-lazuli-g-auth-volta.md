---
id: a2d917e2-b3e1-4f16-8005-8756ce8148bc
slug: geral
type: fact
title: RESOLVIDO: o scaffold de auth (lazuli g auth) volta a passar no doctor 100% limpo (todas as variantes)
tags: auth, doctor, scaffold, conformance, resolvido
provenance: observado
evidence: tools/auth-smoke.sh (doctor leg agora AFIRMA zero diagnósticos LZ); verificado nesta sessão: bare `lazuli g auth` limpo; auth+email+otp limpo + 54/54 testes; auth+oauth limpo + 43/43 testes; `dotnet build/test Lazuli.slnx` verde.
decay: seasonal
created: 2026-06-15T15:19:36.334197500+00:00
updated: 2026-06-16T00:34:06.495591400+00:00
validated: 2026-06-16T00:34:06.495591400+00:00
links: 
---

O round de conformância do scaffold de auth está FECHADO — `lazuli g auth` (e os flows email/otp/oauth) gera app doctor-clean. Histórico do drift e como fechou:

- **LZ0012** (.WithName) — commit c6db64a (sessão anterior).
- **LZ0017** (composition root fino) — commit 2ef81f7 (sessão anterior).
- **LZ0021 + LZ0026** (esta sessão): as 5 entidades DbSet (`User`, `UserSession`, `PhoneOtp`, `EmailVerificationToken`, `PasswordResetToken`) viraram `[Entity]` — ctor privado, setters privados, factories `Result<T>` afuniladas por `EnsureValid` (via `AccountErrorCodes.InvalidState`), mutators void intention-revealing, e `[Timestamp] byte[]? RowVersion`. O RowVersion zera LZ0026 em TODAS as slices [Critical] de uma vez (elas só tocam essas 5 entidades). Slices reescritas pra usar as factories/mutators; criação de sessão/token desembrulhada inline (`.Start(...).Value`) — legal no LZ0025 (inline sobre fresh-construction). `AuthFlowGenerator.AugmentUser` agora injeta campos `{ get; private set; }` + mutators/factory (CompletePhoneVerification/MarkEmailVerified/RegisterViaGoogle), dedup por token.
- **LZ0005** (esta sessão): o `Account.ctx.md` base citava em backtick `VerifyPhone`/`MaxAttempts` (símbolos só presentes com o flow otp) → falso "no longer exists" em bare auth / auth+oauth. Descitado (prosa), conforme a regra prática do [[4f2347bf]]. O smoke mascarava (renderiza otp).
- **CS7036** (esta sessão): os testes oauth (Register/LoginWithGoogle.Tests) chamavam `new AccessTokens(secret, clock)` sem issuer/audience (drift do round-2 que pinou issuer/audience). Corrigido pra `(secret, "myapp", "myapp", clock)`.

O `#7` (corrida de rotação) shipou junto — ver [[753aa853]]. O smoke (`tools/auth-smoke.sh`) foi APERTADO: a leg DOCTOR agora falha se QUALQUER diagnóstico LZ aparecer (não só LZ0022). PENDENTE menor: codificar o smoke como teste xunit em tests/Lazuli.Cli.Tests (hoje é script bash). oauth NÃO é coberto pelo smoke bash — verificado à mão nesta sessão.
