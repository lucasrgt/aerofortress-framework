import { describe, it, expect } from "vitest";
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { join } from "node:path";
import { checkE2e } from "./e2e-doctor.mjs";

// The e2e doctor enforces "completeness via curated checklist": a curated flow with no spec is a gap; a fully
// covered manifest is clean. Pin both edges + the bootstrap (not-set-up-yet) state. Temp dirs live under cwd.
function tmp() {
  return mkdtempSync(join(process.cwd(), "tools", ".e2e-doctor-tmp-"));
}

describe("checkE2e", () => {
  it("is bootstrap when there is no e2e/ + flows.json yet", () => {
    const dir = tmp();
    try {
      expect(checkE2e(dir).bootstrap).toBe(true);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("is clean when every curated flow has a spec and a runner is configured", () => {
    const dir = tmp();
    try {
      writeFileSync(join(dir, "playwright.config.ts"), "export default {};\n");
      mkdirSync(join(dir, "e2e"));
      writeFileSync(join(dir, "e2e", "flows.json"), JSON.stringify([{ name: "welcome", spec: "e2e/welcome.spec.ts" }]));
      writeFileSync(join(dir, "e2e", "welcome.spec.ts"), "// spec\n");
      const r = checkE2e(dir);
      expect(r.bootstrap).toBe(false);
      expect(r.gaps).toBe(0);
      expect(r.flows).toBe(1);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("flags a curated flow whose spec is missing", () => {
    const dir = tmp();
    try {
      writeFileSync(join(dir, "playwright.config.ts"), "export default {};\n");
      mkdirSync(join(dir, "e2e"));
      writeFileSync(join(dir, "e2e", "flows.json"), JSON.stringify([{ name: "checkout", spec: "e2e/checkout.spec.ts" }]));
      const r = checkE2e(dir);
      expect(r.gaps).toBe(1);
      expect(r.messages.join(" ")).toContain("checkout");
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("flags a missing runner config", () => {
    const dir = tmp();
    try {
      mkdirSync(join(dir, "e2e"));
      writeFileSync(join(dir, "e2e", "flows.json"), JSON.stringify([]));
      const r = checkE2e(dir);
      expect(r.gaps).toBe(1);
      expect(r.messages.join(" ")).toContain("runner");
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("is runner-agnostic — a Maestro flow (.maestro/ + .yaml spec) is clean too", () => {
    const dir = tmp();
    try {
      mkdirSync(join(dir, ".maestro"));
      mkdirSync(join(dir, "e2e"));
      writeFileSync(join(dir, "e2e", "flows.json"), JSON.stringify([{ name: "login", spec: "e2e/login.yaml" }]));
      writeFileSync(join(dir, "e2e", "login.yaml"), "appId: com.example\n- launchApp\n");
      const r = checkE2e(dir);
      expect(r.runner).toBe("maestro");
      expect(r.gaps).toBe(0);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
});
