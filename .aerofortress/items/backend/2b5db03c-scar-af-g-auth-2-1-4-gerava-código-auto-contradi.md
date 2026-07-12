---
id: 2b5db03c-7367-4272-a7cc-8e4f41876c67
slug: backend
type: scar
title: SCAR: `af g auth` 2.1.4 gerava código auto-contraditório e quebrava app novo
tags: af-g-auth, af-new, drift, avp, skip-tenancy, 2.1.4, fixed-2.1.5
provenance: observado
evidence: framework: AuthGenerator/Auth templates/AuthGeneratorTests; smoke AuthSmoke 34/34 + doctor verde em 2026-07-12; adocao-responsavel como reprodução original
decay: stable
created: 2026-07-07T01:21:15.451119400+00:00
updated: 2026-07-12T23:06:35.996284700+00:00
validated: 2026-07-12T23:06:35.996284700+00:00
links: 
---

Rodar `af new` + `af g auth --skip-tenancy` com CLI 0.2.1 / packages 2.1.4 não saía verde: o primeiro doctor falhava antes de qualquer regra de negócio.

Drifts confirmados no app `adocao-responsavel` e reproduzidos no smoke do framework:

1. Login/Register/Refresh eram `[Critical]`, mas o gerador não criava `Account.spec.toml`, provas `*.Avp.Tests.cs`, `Assay.Net`, `AvpAssert` nem `AdditionalFiles Include="**\\*.spec.toml"` (AF0031).
2. A implementação de Refresh aceitava replay imediato como retry por uma janela de 10s, contradizendo o oráculo `replay-burns-family`. A referência verde dos pilots queima a família imediatamente.
3. `--skip-tenancy` emitia quatro doubles de `ICurrentUser` sem `OrgId` (CS0535).
4. `AuthFlow.Tests` desserializava enums sem o `AppJson.Options` compartilhado; ao adicionar AppJson, o smoke ainda revelou que o teste co-locado precisava de `using <App>.Api` para compilá-lo no projeto de testes.
5. `AvpAssert` é helper local do test project, não parte de Assay.Net.
6. O Ping do starter não tinha `.WithName(nameof(Ping))` (AF0012).
7. A fiação de pacotes podia duplicar EF/InMemory.
8. O starter herdava `Microsoft.OpenApi 2.0.0`, vulnerável a encerramento por referência circular (GHSA-v5pm-xwqc-g5wc), porque Microsoft.AspNetCore.OpenApi declarava apenas o mínimo vulnerável.

Cura implementada na onda 2.1.5/CLI 0.2.2: auth nasce com manifest + três provas reais e verdes, Assay + helper, JSON canônico, doubles completos, replay-burn imediato, referências idempotentes e OpenAPI 2.10.0 pinado. Evidência observada antes do commit: solução 269/269 testes, frontend 177/177, app novo single-tenant 34/34 + `af doctor` verde, e `dotnet list package --vulnerable --include-transitive` sem vulnerabilidades no framework nem no smoke.

Detalhe de compilação fixado pelo smoke final: `TestApp.cs` precisa de `using <App>.Api;` para resolver o `AppDb` do namespace raiz; o import redundante é `using <App>.Api.Modules.Account;`. Um teste textual agora pina os dois lados. O smoke fresco terminou com build 0 warnings, 34/34 e doctor verde.
