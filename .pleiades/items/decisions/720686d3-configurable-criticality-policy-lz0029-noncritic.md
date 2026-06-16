---
id: 720686d3-e265-4e0a-ad7b-3501cda6ff24
slug: decisions
type: decision
title: Configurable criticality policy (LZ0029 + [NonCritical] + [testing] criticality dial)
tags: criticality, LZ0029, NonCritical, doctor, policy, 0.6.0
provenance: observado
evidence: analyzers/Lazuli.Doctor/CriticalityPolicy.cs, CriticalityPolicyAnalyzer.cs (LZ0029); CriticalJourneyAnalyzer.cs (LZ0008) + JourneyCoversCriticalAnalyzer.cs (LZ0010) rewired; buildTransitive/Lazuli.Doctor.targets (projection); src/Lazuli.Abstractions/NonCriticalAttribute.cs; templates/lazuli-app/Lazuli.toml; src/Lazuli.Cli/LazuliManifest.cs; docs/decisions/lazuli-net-criticality-policy.md; shipped in 0.6.0
decay: stable
created: 2026-06-16T03:21:27.893731+00:00
updated: 2026-06-16T03:21:27.893731+00:00
validated: 2026-06-16T03:21:27.893731+00:00
links: 
---

lazuli-net 0.6.0 adds a **criticality policy** dial: `[testing] criticality` in `Lazuli.toml`, three levels.

- `"opt-in"` (default, and meaning of an absent key) = only `[Critical]` slices need journeys — identical to pre-0.6 behavior.
- `"explicit"` = every `[Slice]` must carry `[Critical]` OR the new `[NonCritical]` marker, else **LZ0029** errors. Mirrors LZ0022's "decision, never omission" posture for authorization.
- `"strict"` = an undecided slice is *treated as* `[Critical]` (LZ0008 demands happy+sad journeys, LZ0010 accepts a journey on it); `[NonCritical]` is the only opt-out.

Key design facts:
- New pure marker `[NonCritical]` lives in `src/Lazuli.Abstractions/NonCriticalAttribute.cs` (sibling of `CriticalAttribute`, same assembly/namespace).
- Shared helper `CriticalityPolicy` (analyzers/Lazuli.Doctor): `enum Level {OptIn,Explicit,Strict}`, `Read(AnalyzerConfigOptionsProvider)`, `IsCriticalUnderPolicy(cls, level)`. The helper REQUIRES `[Slice]`, so under opt-in/explicit it reduces to `Slice && Critical` (regression-safe). LZ0008 and LZ0010 both call it (their inline `HasAttribute` was removed).
- **The analyzer never parses TOML.** The doctor's `buildTransitive/Lazuli.Doctor.targets` locates `Lazuli.toml` via `GetPathOfFileAbove`, regex-reads the key, and projects it as `<CompilerVisibleProperty Include="LazuliCriticality"/>` → analyzer reads GlobalOptions key `build_property.LazuliCriticality` (case-insensitive). Absent ⇒ opt-in. Ships+removes with the doctor (Law 2). MSBuild regex tolerates commas/quotes in the file (verified via `dotnet msbuild -getProperty:LazuliCriticality`).
- `LazuliManifest.Validate` (CLI) does a light textual check the value is one of the three; a typo is reported instead of silently degrading to opt-in.
- Template `templates/lazuli-app/Lazuli.toml` ships `[testing] criticality = "opt-in"` with a 3-line doc comment (`lazuli new` is template-driven).
- ADR: `docs/decisions/lazuli-net-criticality-policy.md` (the repo's ADR home is `docs/decisions/`, NOT `docs/adr/`).

Next free rule id after this wave = **LZ0030** (LZ0029 now taken).
