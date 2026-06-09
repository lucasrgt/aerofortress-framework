import { describe, it, expect } from "vitest";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
// @ts-expect-error - plain .mjs tool module, typed by its JSDoc
import { renderDesign } from "./generate.mjs";
// @ts-expect-error - plain .mjs tool module, typed by its JSDoc
import { renderUiKitWeb } from "./ui-kit-web.mjs";

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

const kitDir = join(process.cwd(), "..", "examples", "sample-app", "frontend", "web", "src", "ui");

describe("web kit scaffold", () => {
  it("emits the canonical kit verbatim — template and exemplar cannot drift", () => {
    const files = renderUiKitWeb() as Record<string, string>;
    for (const [name, contents] of Object.entries(files)) {
      expect(lf(readFileSync(join(kitDir, name), "utf8")), name).toBe(lf(contents));
    }
    // The committed kit holds nothing the template doesn't emit (the test file is framework-side).
    const committed = readdirSync(kitDir)
      .filter((f) => f !== "ui.test.tsx")
      .sort();
    expect(committed).toEqual(Object.keys(files).sort());
  });

  it("refuses to overwrite an existing kit", () => {
    const dir = mkdtempSync(join(tmpdir(), "lz-kit-"));
    try {
      const first = spawnSync(process.execPath, [cli, "--kit", "web", dir], { encoding: "utf8" });
      expect(first.status).toBe(0);
      const second = spawnSync(process.execPath, [cli, "--kit", "web", dir], { encoding: "utf8" });
      expect(second.status).not.toBe(0);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
});
