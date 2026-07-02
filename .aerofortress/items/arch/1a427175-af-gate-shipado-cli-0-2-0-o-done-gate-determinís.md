---
id: 1a427175-5f88-4a31-afe9-5d65a6b79a46
slug: arch
type: fact
title: af gate SHIPADO (CLI 0.2.0) — o done-gate determinístico com matriz de rastreabilidade (Fase 4 do ecossistema)
tags: af-gate, fase-4, matriz, clockwork, cli-0.2.0
provenance: observado
evidence: aerofortress-framework@41ea6bf (branch engine/anti-drift); examples/sample-app/VERIFICATION.md; dotnet test slnx 266/266
decay: stable
created: 2026-07-02T04:00:23.583379100+00:00
updated: 2026-07-02T04:00:23.583379100+00:00
validated: 2026-07-02T04:00:23.583379100+00:00
links: 
---

`af gate` implementado e dogfooded verde (2026-07-02, commit 41ea6bf, branch **engine/anti-drift**). Fecha a Fase 4 (gate+matriz) do ecossistema: UMA chamada determinística = doctor ∧ provas ∧ matriz.

**Composição (não duplica nada):** DoctorCommand (extraído de Program.cs, comportamento idêntico: manifest AeroFortress.toml + FrameworkSync + `dotnet build` AF* + frontend AFFE* por client) ∧ `dotnet test --logger trx` (roda TODAS as provas [AVP]) ∧ GateMatrix (junta declarações×provas×verdicts).

**Fontes da matriz:** `*.spec.toml` via Assay.Net.SpecManifest (o CLI ganhou PackageReference **Assay.Net 0.2.0** — a única direção permitida framework→avp); sites `[AVP("id")]` por scan textual espelhando o AvpProofPattern do AF0030; `[Slice]`/`[Critical]` por scan de bloco de atributos; verdicts por parse dos TRX (className+method → proof). Eixos: gap (declarado sem prova), creep (prova órfã repo-wide), undeclared-critical (espelho AF0031), NOT-RUN (skip nunca é verde — princípio AVP), malformed manifest. DeclaredWithoutClass = nota informacional (manifest pode preceder código).

**Saídas:** tabela console + `VERIFICATION.md` (raiz, viaja no repo, paths com forward-slash pra diff-estabilidade cross-OS) + `VERIFICATION.json` (camelCase, machine) + exit code = verdito.

**Arquivos:** GateCommand/GateMatrix (pura, testável)/GateScan (IO)/GateReport/DoctorCommand em src/AeroFortress.Framework.Cli; 16 testes novos em Cli.Tests (49 total). slnx inteiro verde: 266 testes, 0 falhas. Dogfood real: gate GREEN no sample-app (Wallets 2/2 proven), VERIFICATION.* commitados lá.

**Binding vigente confirmado:** [Verify] inline APOSENTADO; obrigação = spec.toml (AF0031 critical⟹declarado; AF0030 declarado⟹[AVP] repo-wide). Prova é per-criterion-id repo-wide (binding representativo).

Pendente no workstream: skill clockwork-gate usar `af gate`; recipe fan-out nos pilots; gate real em hostpoint/pauta/fluxoterra; pin do compiler na harness.
