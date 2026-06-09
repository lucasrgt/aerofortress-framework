import { describe, expect, it } from "vitest";
// @ts-expect-error - plain .mjs tool module, typed by its JSDoc
import { checkMirror, fingerprint } from "./framework-sync.mjs";

// The mirror gate: a pilot's plugin copy must match the canonical — drift is how framework code gets
// "lost in time" inside a pilot. CI machines without the checkout skip instead of failing.
describe("framework-sync", () => {
  it("a matching mirror passes", () => {
    expect(checkMirror({ mirror: "// rules", canonical: "// rules" }).status).toBe("ok");
  });

  it("a drifted mirror fails with the rebase + upstream instruction", () => {
    const result = checkMirror({ mirror: "// stale", canonical: "// new rules" });
    expect(result.status).toBe("drifted");
    expect(result.messages[0]).toContain("package-first");
  });

  it("a missing side skips — the CI posture, never a false failure", () => {
    expect(checkMirror({ mirror: null, canonical: "// x" }).status).toBe("skipped");
    expect(checkMirror({ mirror: "// x", canonical: null }).status).toBe("skipped");
  });

  it("a CRLF copy is not drift", () => {
    expect(fingerprint("// a\r\n// b\r\n")).toBe(fingerprint("// a\n// b\n"));
  });
});
