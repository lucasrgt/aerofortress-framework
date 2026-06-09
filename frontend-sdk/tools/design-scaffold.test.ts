import { describe, it, expect } from "vitest";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
// @ts-expect-error - plain .mjs tool module, typed by its JSDoc
import { renderDesign } from "./generate.mjs";

// SCAFFOLD CONTRACT TESTS — the template IS the canonical file. The drift test makes "scaffold output" and
// "committed exemplar" the same artifact by force; the overwrite test keeps the scaffolder a one-shot tool
// (scaffold, never re-emit — the bright line in FRONTEND-CONVENTIONS.md).

const cli = join(process.cwd(), "tools", "design-scaffold.mjs");
const canonical = join(process.cwd(), "..", "examples", "sample-app", "frontend", "core", "src", "design", "tokens.ts");
const lf = (s: string) => s.replace(/\r\n/g, "\n");

describe("design scaffold", () => {
  it("emits the canonical tokens file verbatim — template and exemplar cannot drift", () => {
    const rendered = renderDesign()["tokens.ts"];
    expect(lf(readFileSync(canonical, "utf8"))).toBe(lf(rendered));
  });

  it("refuses to overwrite an existing tokens file", () => {
    const dir = mkdtempSync(join(tmpdir(), "lz-design-"));
    try {
      const first = spawnSync(process.execPath, [cli, dir], { encoding: "utf8" });
      expect(first.status).toBe(0);
      const second = spawnSync(process.execPath, [cli, dir], { encoding: "utf8" });
      expect(second.status).not.toBe(0);
      expect(second.stderr).toContain("refusing to overwrite");
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
});
