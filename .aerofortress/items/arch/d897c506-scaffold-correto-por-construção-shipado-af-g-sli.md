---
id: d897c506-e498-4a17-84e6-4081b30f7aed
slug: arch
type: fact
title: Scaffold correto-por-construção SHIPADO — af g slice --verify emite manifest+prova; af criteria = menu vivo do catálogo
tags: scaffold, verify, criteria, correct-by-construction, three-monsters
provenance: observado
evidence: aerofortress-framework@2206586; smoke real: af criteria list (19 archetypes/46 criteria), af criteria suggest CreateCheckoutPreference; dotnet test slnx 282/282
decay: stable
created: 2026-07-02T04:08:56.992581300+00:00
updated: 2026-07-02T04:08:56.992581300+00:00
validated: 2026-07-02T04:08:56.992581300+00:00
links: 
---

Workstream B do three-monsters entregue (2026-07-02, commit 2206586, branch engine/anti-drift). A slice nasce com o contrato de aceite fechado em vez do doctor pegar o gap depois.

**`af g slice <M> <N> [--critical] [--verify id,id]`:** além do trio slice+tests+journeys, o --verify (1) declara os criteria no `<M>.spec.toml` via **SpecManifestScaffold** — writer cirúrgico: cria com header da casa, faz append de tabela nova ou merge de ids numa existente SEM reformatar o que humano escreveu (teste garante); (2) scaffolda `<N>.Avp.Tests.cs` co-localizado via **AvpProofScaffold** — cada criterion vira um teste com `[AVP("id")]` (âncora do AF0030) JÁ wired no `Runner.Run` com o archetype certo; o factory do subject lança NotImplementedException até o autor bootar o endpoint real → **RED by design**: obrigação e prova nascem no MESMO change-set. Id off-catalog → stub com Assert.Fail (prova que não pode falhar nunca shipa) + nota ADR 0002.

**`af criteria list|suggest <words>`:** menu do catálogo neutro. **AvpBinding** descobre por REFLEXÃO sobre `Archetype<TSubject>` do Assay.Net referenciado (zero mapa hardcoded — archetype novo/subject renomeado flui sozinho; teste trava que todo oracle id existe no catálogo). O list marca `[mechanical, runnable]` vs "definition only" — partição honesta do que ESTE adapter roda. O suggest tokeniza PascalCase/kebab e ranqueia famílias por stems (money/auth/token/unique/gate/webhook/mutação...) mostrando as palavras que casaram — o híbrido Clockwork (heurística propõe, humano/LLM refina).

**Descoberta no smoke:** o catálogo 0.2.0 tem **19 archetypes / 46 criteria** (bem além dos 12 do rollout plan de 06-25) — inclui tier frontend/design (`action-effect` etc.) sem runner .NET, que o list reporta honesto. Suggest de CreateCheckoutPreference acerta request-idempotency + submission-gate (com variant body-target listada no stub).

16 testes novos (65 no Cli.Tests); slnx inteiro verde (282). Pendente do B: docs CONVENTIONS.md + agente scaffolder + skill clockwork-bind (agrupados com o lado harness).
