import { describe, it, expect } from "vitest";
import { aggregateReport, bucket } from "./doctor.mjs";

// The doctor aggregates the whole lint surface + the fullstack loops into one report. Pin the bucketing, the
// error/warn tallies, the per-rule file counts, and the clean-LZFE roster (the promotion-candidate view).
describe("bucket", () => {
  it("routes LZFE, community, and platform rules", () => {
    expect(bucket("lazuli/view-purity")).toBe("lzfe");
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
        { ruleId: "lazuli/no-hardcoded-copy", severity: 1 },
        { ruleId: "lazuli/view-purity", severity: 2 },
      ],
    },
    {
      filePath: "/app/src/features/auth/Auth.viewModel.ts",
      messages: [{ ruleId: "lazuli/mutation-error-handled", severity: 1 }],
    },
    {
      filePath: "/app/src/features/host/Host.view.tsx",
      messages: [{ ruleId: "lazuli/no-hardcoded-copy", severity: 1 }],
    },
  ];

  it("tallies errors/warnings and per-rule file counts", () => {
    const r = aggregateReport({ eslintResults });
    expect(r.summary).toEqual({ errors: 1, warnings: 3, rules: 3 });
    expect(r.rules["lazuli/no-hardcoded-copy"]).toMatchObject({ warn: 2, files: 2, bucket: "lzfe", code: "LZFE014" });
    expect(r.rules["lazuli/view-purity"]).toMatchObject({ error: 1, code: "LZFE001" });
    expect(r.ok).toBe(false); // a gated error is present
  });

  it("carries each rule's configured level (incl. for fired rules)", () => {
    const r = aggregateReport({
      eslintResults,
      ruleLevels: { "lazuli/view-purity": "error", "lazuli/no-hardcoded-copy": "warn" },
    });
    expect(r.rules["lazuli/view-purity"].level).toBe("error");
    expect(r.rules["lazuli/no-hardcoded-copy"].level).toBe("warn");
  });

  it("lists clean LZFE rules (0 hits) as promotion candidates", () => {
    const r = aggregateReport({
      eslintResults,
      ruleLevels: { "lazuli/data-door": "error", "lazuli/state-completeness": "warn" },
    });
    const clean = Object.fromEntries(r.cleanLzfe.map((c) => [c.code, c.level]));
    // view-purity / no-hardcoded-copy / mutation-error-handled fired -> NOT clean
    expect(clean.LZFE001).toBeUndefined();
    expect(clean.LZFE013).toBeUndefined();
    expect(clean.LZFE014).toBeUndefined();
    // data-door clean + gated; state-completeness clean + warn (promotion candidate)
    expect(clean.LZFE002).toBe("error");
    expect(clean.LZFE010).toBe("warn");
  });

  it("is ok when there are no gated errors (warnings are the revealed backlog)", () => {
    const r = aggregateReport({
      eslintResults: [{ filePath: "x", messages: [{ ruleId: "lazuli/mutation-error-handled", severity: 1 }] }],
      loops: { journey: "8 backend journey(s), 8 linked, 0 parity gap(s)" },
    });
    expect(r.ok).toBe(true);
    expect(r.summary.errors).toBe(0);
    expect(r.loops.journey).toContain("0 parity gap");
  });
});
