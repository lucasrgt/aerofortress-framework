---
id: 482dc03f-b19c-4040-9d0d-d60879242be6
slug: specs
type: doc
title: Decision: verification is universal; criticality controls depth, not coverage
tags: spec, adr, testing, gate, avp
provenance: observado
evidence: docs/decisions/aerofortress-framework-universal-verification-gate.md; release v2.2.0
decay: stable
created: 2026-07-20T22:09:15.306465800+00:00
updated: 2026-07-20T22:09:15.306465800+00:00
validated: 2026-07-20T22:09:15.306465800+00:00
links: 
---

ADR aceito e implementado: toda slice/ViewModel deve declarar critérios e possuir provas AVP executáveis ligadas ao sujeito; `[Critical]` acrescenta apenas profundidade happy/sad de journey. O `af gate` executa todos os testes, Assay direto e E2E, bloqueia skips/focus/placeholders e escala por paralelismo/sharding, nunca por uma tier `Ephemeral` que omite prova.
