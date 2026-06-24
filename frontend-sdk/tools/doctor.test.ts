import { describe, it, expect } from "vitest";
import { aggregateReport, bucket } from "./doctor.mjs";

// The doctor aggregates the whole lint surface + the fullstack loops into one report. Pin the bucketing, the
// error/warn tallies, the per-rule file counts, and the clean-AFFE roster (the promotion-candidate view).
describe("bucket", () => {
  it("routes AFFE, community, and platform rules", () => {
    expect(bucket("aerofortress/view-purity")).toBe("affe");
    expect(bucket("@tanstack/query/exhaustive-deps")).toBe("community");
    expect(bucket("sonarjs/cognitive-complexity")).toBe("community");
    expect(bucket("@typescript-eslint/no-floating-promises")).toBe("community");
    expect(bucket("react-hooks/set-state-in-effect")).toBe("platform");
    expect(bucket("import/no-duplicates")).toBe("platform");
    expect(bucket(null)).toBe("parse");
  });
});

describe("aggregateReport", () => {
  const eslintResults = [
    {
      filePath: "/app/src/features/auth/Auth.view.tsx",
      messages: [
        { ruleId: "aerofortress/no-hardcoded-copy", severity: 1 },
        { ruleId: "aerofortress/view-purity", severity: 2 },
      ],
    },
    {
      filePath: "/app/src/features/auth/Auth.viewModel.ts",
      messages: [{ ruleId: "aerofortress/mutation-error-handled", severity: 1 }],
    },
    {
      filePath: "/app/src/features/host/Host.view.tsx",
      messages: [{ ruleId: "aerofortress/no-hardcoded-copy", severity: 1 }],
    },
  ];

  it("tallies errors/warnings and per-rule file counts", () => {
    const r = aggregateReport({ eslintResults });
    expect(r.summary).toEqual({ errors: 1, warnings: 3, rules: 3 });
    expect(r.rules["aerofortress/no-hardcoded-copy"]).toMatchObject({ warn: 2, files: 2, bucket: "affe", code: "AFFE014" });
    expect(r.rules["aerofortress/view-purity"]).toMatchObject({ error: 1, code: "AFFE001" });
    expect(r.ok).toBe(false); // a gated error is present
  });

  it("carries each rule's configured level (incl. for fired rules)", () => {
    const r = aggregateReport({
      eslintResults,
      ruleLevels: { "aerofortress/view-purity": "error", "aerofortress/no-hardcoded-copy": "warn" },
    });
    expect(r.rules["aerofortress/view-purity"].level).toBe("error");
    expect(r.rules["aerofortress/no-hardcoded-copy"].level).toBe("warn");
  });

  it("lists clean AFFE rules (0 hits) as promotion candidates", () => {
    const r = aggregateReport({
      eslintResults,
      ruleLevels: { "aerofortress/data-door": "error", "aerofortress/state-completeness": "warn" },
    });
    const clean = Object.fromEntries(r.cleanAffe.map((c) => [c.code, c.level]));
    // view-purity / no-hardcoded-copy / mutation-error-handled fired -> NOT clean
    expect(clean.AFFE001).toBeUndefined();
    expect(clean.AFFE013).toBeUndefined();
    expect(clean.AFFE014).toBeUndefined();
    // data-door clean + gated; state-completeness clean + warn (promotion candidate)
    expect(clean.AFFE002).toBe("error");
    expect(clean.AFFE010).toBe("warn");
  });

  it("is ok when there are no gated errors (warnings are the revealed backlog)", () => {
    const r = aggregateReport({
      eslintResults: [{ filePath: "x", messages: [{ ruleId: "aerofortress/mutation-error-handled", severity: 1 }] }],
      loops: { journey: "8 backend journey(s), 8 linked, 0 parity gap(s)" },
    });
    expect(r.ok).toBe(true);
    expect(r.summary.errors).toBe(0);
    expect(r.loops.journey).toContain("0 parity gap");
  });
});
