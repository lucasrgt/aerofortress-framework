---
id: 859e424a-a979-488e-93b1-2bb11db147b4
slug: specs
type: project
title: Auditoria docs-que-dirigem-agentes: alinhar absolutos da scope vs convenções opt-in (realtime/frontend) — EM ANDAMENTO
tags: docs, conventions, realtime, hub, scope-discipline, agent-guidance, auditoria, em-andamento
provenance: observado
evidence: CONVENTIONS.md:336 §Real-time; af g surface via grep aerofortress-plugin/docs/CLI; scope-discipline CLAUDE.md:106/CONVENTIONS.md:550
decay: volatile
created: 2026-06-25T15:58:40.541643900+00:00
updated: 2026-06-25T15:58:40.541643900+00:00
validated: 2026-06-25T15:58:40.541643900+00:00
links: 
---

**Gatilho:** um agente em hostpoint ia hand-rollar realtime (presence/typing/live) porque leu "no realtime" na scope-discipline e concluiu que o framework não suporta. Mas o framework TEM `af g hub` (§"Real-time — hubs (opt-in)" em CONVENTIONS.md:336). Os absolutos da scope-discipline ("No frontend/UI generation, no realtime, no multi-app sprawl — designed out") contradizem as convenções opt-in/scaffold refinadas → enganam os agentes.

**Verdade do realtime (regra correta):**
- Backend = framework: `af g hub <Module> <Name>` scaffolda SignalR hub em `Modules/<M>/Realtime/<Name>Hub.cs`. Hub é WIRE não lógica (chama a slice, faz fan-out); presence/typing IN-MEMORY (singleton, não entidade); caller via `Context.User`/`ClaimsCurrentUser`; token no query string; escala via backplane Redis (1 linha no composition root).
- Frontend = app: o CLIENT SignalR é platform capability → injected port no app-core, MVVM (ViewModel = porta de dados AFFE002), manter poll fallback. Framework NÃO shippa hook de realtime no front.
- Regra-mestra: convenção+enforcement → framework; capability/lib/comportamento → app. O PADRÃO de hub é convenção; a conexão/capability é app.

**Superfície real de `af g`** (verificada): auth, crud <M> <E>, entity <M> <N>, hub <M> <N>, module <M>, slice <M> <N>, view <Slice>, vo <N>.

**FEITO nesta orbita:** clarificadas as 3 linhas de scope-discipline (CLAUDE.md:106, AGENTS.md:106, CONVENTIONS.md:550) — "No source-gen of UI behavior, no realtime *on by default*, no multi-app sprawl" + nuance citando `af g view`/`af g hub`. (ainda NÃO commitado quando escrevi isto.)

**FALTA (o "e outros" — auditoria a continuar):**
1. Operating manual (CLAUDE.md/AGENTS.md): a lista de comandos/seção "This repo" não expõe a superfície `af g` (esp. `hub`) → agentes não sabem que hubs/scaffolds existem. Adicionar.
2. agent defs (aerofortress-plugin/agents/aerofortress-scaffolder.md / -backend / -frontend): conferir se citam `af g hub` + o split realtime backend/frontend.
3. FRONTEND-CONVENTIONS.md: NÃO documenta o CLIENT de realtime (injected-port pra conexão SignalR no app-core) — gap que causou o erro. Documentar o padrão do lado-front.
4. aerofortress-plugin/context.md (injetado no system prompt do orquestrador): adicionar roteamento explícito "realtime → backend hub via scaffolder/backend; client via frontend".
5. Varrer OUTROS absolutos que possam enganar (ex.: "no vendor adapters in core" vs plugins de capability são OK em repo separado — está claro? "[Slice] pure marker" etc.). Cross-check toda seção "designed out"/"non-goals" contra features shipadas.
6. Espelhar specs/ADR no item de rede quando docs mudarem (lei de curadoria).

Ver [[6a8b3db9]] (scar do plugin-sync) e [[b54a3077]] (rebrand concluído).
