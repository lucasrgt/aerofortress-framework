---
id: 90aea040-5e73-431a-8c91-18793cd6521a
slug: handoffs
type: doc
title: Handoff — lazuli-backend (especialista)
tags: lazuli-backend, echo
provenance: observado
evidence: echo 4188d501 — ask_session: 4188d501-97b3-491f-9a30-31e34d7a059e
decay: seasonal
created: 2026-06-16T03:07:29.858216600+00:00
updated: 2026-06-16T03:07:29.858216600+00:00
validated: 2026-06-16T03:07:29.858216600+00:00
links: 
---

## Current specialist goal and state
MISSION: add a configurable **criticality policy** (a `Lazuli.toml` dial) to lazuli-net, package-first, no pilot touched. Status: **recon complete, design locked, NOTHING implemented yet** (stopped at context rotation to avoid a half-done red tree). Tree is currently GREEN and untouched. Bump `build/Lazuli.Library.props` `<Version>` 0.5.0→**0.6.0** as part of the wave.

Three policy levels via new `[testing] criticality` key: `"opt-in"` (DEFAULT = today's behavior, absence ⇒ opt-in), `"explicit"` (every `[Slice]` must carry `[Critical]` OR `[NonCritical]`, else build error — mirrors LZ0022), `"strict"` (undecided slice is TREATED AS `[Critical]`; only `[NonCritical]` downgrades it).

## Decisions / important files
- **New marker `[NonCritical]`** → add `src/Lazuli.Abstractions/NonCriticalAttribute.cs` (sibling of `CriticalAttribute.cs`). Pure marker: `[AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=false)] public sealed class NonCriticalAttribute : Attribute;`. Gold XML docs (CS1591 is error here). Frame it as the positive, reviewable downgrade — symmetric to `[Critical]`.
- **Next free rule id = LZ0029.** New analyzer `analyzers/Lazuli.Doctor/CriticalityPolicyAnalyzer.cs` — "slice must resolve its criticality under the active policy". Behavior: opt-in ⇒ inert; explicit ⇒ a `[Slice]` with neither `[Critical]` nor `[NonCritical]` → **ERROR** (RegisterSyntaxNodeAction over ClassDeclaration is simplest); strict ⇒ inert (the "treated as critical" effect is realized in LZ0008/LZ0010, not here).
- **Shared helper** (new file `analyzers/Lazuli.Doctor/CriticalityPolicy.cs`): `enum Level { OptIn, Explicit, Strict }`; `Level Read(AnalyzerConfigOptionsProvider p)` reading `build_property.LazuliCriticality` from `p.GlobalOptions.TryGetValue(...)`, default OptIn, parse case-insensitively ("opt-in"→OptIn); `bool IsCriticalUnderPolicy(ClassDeclarationSyntax cls, Level lvl)` = `HasAttr("Critical") || (lvl==Strict && HasAttr("Slice") && !HasAttr("NonCritical"))`. Move/duplicate the `HasAttribute` string-match used by LZ0008/0010.
- **LZ0008 (`CriticalJourneyAnalyzer.cs`) & LZ0010 (`JourneyCoversCriticalAnalyzer.cs`)**: both currently collect criticals via inline `HasAttribute(cls,"Slice") && HasAttribute(cls,"Critical")` in a SyntaxNodeAction, matched at CompilationEnd against journeys in AdditionalFiles (textual regex `JourneyPattern`). Change: read `Level` once at `OnStart` from `context.Options.AnalyzerConfigOptionsProvider`, and replace the inline critical-check with `CriticalityPolicy.IsCriticalUnderPolicy(cls, level)`. In opt-in/explicit this reduces to `HasAttr("Critical")` → **behavior identical to today** (regression-safe). In strict, unmarked slices count as critical for BOTH.
- **Projection (no TOML in analyzer)**: in `analyzers/Lazuli.Doctor/buildTransitive/Lazuli.Doctor.targets` add `<CompilerVisibleProperty Include="LazuliCriticality" />` and a target that locates Lazuli.toml via `$([MSBuild]::GetPathOfFileAbove('$(MSBuildProjectDirectory)','Lazuli.toml'))`, reads `criticality = "..."` under `[testing]` with a `Regex::Match` property-function, and sets `<LazuliCriticality>` (default opt-in if file/key absent). This ships WITH the doctor (Law 2 removable).
- **CLI**: `lazuli new` == `dotnet new lazuli` (Program.cs `["new", var name] => Tooling.Dotnet("new",["lazuli",...])`), template-driven → add `[testing]\ncriticality = "opt-in"` (with a comment documenting the 3 levels) to `templates/lazuli-app/Lazuli.toml`. Extend `src/Lazuli.Cli/LazuliManifest.cs::Validate` to textually check that, if a `criticality = "x"` line exists, x ∈ {opt-in,explicit,strict} (light/textual, matching its style).
- **Docs**: update `docs/CONVENTIONS.md` (Critical/Journey section + the LZ rule table → add LZ0029). Create `docs/adr/` + an ADR (e.g. `0001-criticality-policy.md`) with RATIONALE: why a dial; why forced+reviewable `[NonCritical]` beats "all critical by default" (preserves signal; doesn't invert the assertion into a rubber-stampable negation); why not parse TOML in the analyzer (the two laws); the LZ0022 parallel.

## Pending work and pitfalls
- **Tests** (must be green): new `tests/Lazuli.Doctor.Tests/CriticalityPolicyAnalyzerTests.cs` covering all 3 levels; add strict-mode cases to `CriticalJourneyAnalyzerTests.cs` and `JourneyCoversCriticalAnalyzerTests.cs`. Inject policy in the `CSharpAnalyzerTest` harness via `TestState.AnalyzerConfigFiles.Add(("/.globalconfig", "is_global = true\nbuild_property.LazuliCriticality = explicit\n"))`. Stubs needed in test sources: `SliceAttribute`, `CriticalAttribute`, `NonCriticalAttribute` (see how `EndpointAuthAnalyzerTests.cs` defines stubs). Add a `LazuliManifestTests.cs` case for the criticality validation.
- **Self-harness (inescapable)**: every Lazuli.* file (incl. the analyzers) is under LZSELF + CS1591-as-error → gold XML docs on ALL public members, files ≤500 LOC, zero junk comments. Fix code, never suppress.
- **Verify green**: `dotnet build Lazuli.slnx` AND `dotnet test Lazuli.slnx`. Also run `bash tools/auth-smoke.sh` — it now asserts the generated app is doctor-CLEAN; LZ0029 is inert in opt-in and the smoke scratch has no Lazuli.toml (⇒ opt-in), so it should stay green. Caveat: the smoke references Lazuli.Doctor as a ProjectReference analyzer (not via the buildTransitive targets), so the `<CompilerVisibleProperty>` projection is NOT exercised there — that's fine (defaults to opt-in). To actually test the toml→property projection (constraint 8, "if viable") you'd need package-based consumption or a manual `<LazuliCriticality>` in a scratch csproj.
- **Don't confuse versions**: bump `build/Lazuli.Library.props` (0.5.0→0.6.0); `Lazuli.Cli` has its own `<Version>0.1.2</Version>` — leave unless intentionally releasing the CLI too.
- **No commit** (owner commits/pushes). **No pilot edits.** Report back: changed files, new rule id (LZ0029), real build+test output, ADR path, and a 1-paragraph release note explaining the 3 levels.
- Interim history note: since round 2, the conformance gap (LZ0012/0017/0021) and #7 (RowVersion + grace window) were already closed by someone — `tools/auth-smoke.sh` and `docs/specs/auth-refresh-rotation-concurrency.md` were updated to reflect doctor-clean + #7 IMPLEMENTED. Don't reopen those.
