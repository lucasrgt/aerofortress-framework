---
id: 9b9dcfce-8ad6-48b1-85e3-444bb20551f5
slug: gate
type: scar
title: Scan textual de marcador confundiu prosa, strings e templates com atributos executáveis
tags: gate, scan-textual, falso-positivo, roslyn, templates, spec-toml
provenance: observado
evidence: framework@d38d397; af gate Release 4/4 GREEN; CI 29782270911; antecedente framework@36c86dd
decay: stable
created: 2026-07-02T04:40:12.230990800+00:00
updated: 2026-07-20T22:09:15.618319600+00:00
validated: 2026-07-20T22:09:15.618319600+00:00
links: 
---

**Sintomas observados:** em 2026-07-02, prosa mencionando `[Critical]` virou atributo no gate. Em 2026-07-20, o dogfood do gate universal revelou a classe maior: raw strings de testes de analyzer, exemplos de `[AVP]` dentro do próprio scanner e arquivos `.cs`/`*.spec.toml` sob `templates/` foram inventariados como código e manifests executáveis, produzindo dezenas de slices/provas falsas e NOT-RUN.

**Causa:** restringir substrings a linhas com shape de atributo não torna um scanner textual consciente da sintaxe; strings e scaffolding continuam parecendo código. O doctor symbol-aware estava correto, mas a matriz tinha um coletor menos honesto.

**Fix definitivo (v2.2.0):** `GateScan` passou a ler classes, métodos e atributos reais via Roslyn `CSharpSyntaxTree`, ignora o diretório `templates`, liga classe namespace-qualified ao TRX e possui regressões para raw strings e manifests de scaffold. O mesmo gate que antes ficou RED falso terminou 4/4 GREEN em Debug, Release e CI Linux.

**Lição:** quando o parser oficial da linguagem está disponível, não use regex para decidir se uma anotação é executável. Regex é aceitável só em superfícies sem AST, com fixture explícita para comentários, strings, templates e duplicidade de sujeito.
