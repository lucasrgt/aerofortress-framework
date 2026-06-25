---
id: fba62b6a-0526-4b22-92fc-f6b3d6dc4630
slug: decisions
type: decision
title: Engine anti-drift: supply publicado (nuget.org+npmjs) + SSOT em AeroFortress.lock + enforcement no overlay
tags: anti-drift, package-first, supply, AeroFortress.lock, doctor, decisao, nuget, npm
provenance: dito
evidence: 
decay: stable
created: 2026-06-25T17:36:54.314649100+00:00
updated: 2026-06-25T17:40:14.636026800+00:00
validated: 2026-06-25T17:40:14.636026800+00:00
links: 
---

Decisão do Lucas (2026-06-25) pra resolver o desync recorrente dos pilots (hostpoint/pauta/fluxoterra) com o framework, AVP e harness, de uma vez por todas.

**Supply (CORRIGIDO após ler publish.yml):** registries PÚBLICOS, disparado por tag `v*` no `.github/workflows/publish.yml` que JÁ existe e está certo:
- NuGet → **nuget.org** via Trusted Publishing (OIDC, `NuGet/login@v1`, policy bound a owner lucasrgt/repo aerofortress-framework/workflow publish.yml — sem chave armazenada).
- npm → **registry.npmjs.org** via secret `NPM_TOKEN` (automation token do scope `@aerofortress`).
Os "github tokens" do Lucas = os secrets/OIDC do Actions. A supply NÃO está arquiteturalmente quebrada: as versões 2.0.0 / eslint 0.11.0 / react 0.6.0 / frontend-sdk 0.1.0 nunca foram tagueadas/publicadas — só existem no cache global/%TEMP% desta máquina (ver scar [[verde-falso-supply-efemera]]). Fix = bump consistente → tag → workflow publica. Invariante: clone limpo → restore → build → test VERDE restaurando dos registries públicos, zero seed manual.

**SSOT de versões:** `AeroFortress.lock` por pilot — gerado pelo `af` (deriva da fonte única do framework: `<Version>` + versões dos package.json npm canônicos), commitado, e VERIFICADO pelo doctor. Nada hardcoda versão. `AeroFortress.toml` continua só topologia humana.

**Componentes da engine (no framework, package-first):**
1. SSOT/`AeroFortress.lock` gerado + commitado.
2. Gate AUTO-CONTIDO no `AeroFortress.Framework.Doctor` shipado: carrega versões-peer esperadas (release conjunto), então `af doctor` verifica conformidade em QUALQUER máquina/CI sem checkout-irmão — não degrada mais pra "aviso" (a brecha do verde-falso). Evoluir FrameworkSync.cs (já reescrito pelo GPT: valida versões npm + bane mirror vendorado).
3. Integridade de CONTEÚDO: fingerprint do ruleset do eslint-plugin + pre-pack gate que recusa empacotar versão com conteúdo ≠ do já tagueado (mata "0.10.0 publicado ≠ 0.10.0 canônico").
4. Ban determinístico de vendor + reimplementação de primitives shipados (denylist de símbolos).
5. Scaffolder/templates emitem versão derivada da SSOT (mata o "nasce velho").
6. CI de cada pilot roda `af doctor` (drift falha o build).

**Enforcement comportamental:** overlay "framework-detected" manda a sessão verificar/curar drift (package-first) ANTES de trabalho substancial.

Já existe (GPT, uncommitted, framework VERDE build+test): rebrand lazuli→aerofortress finalizado; FrameworkSync.cs + framework-sync.mjs reescritos; @aerofortress/frontend-sdk (tooling package) com package-versions.mjs; primitives subidas pro @aerofortress/react 0.6.0.
