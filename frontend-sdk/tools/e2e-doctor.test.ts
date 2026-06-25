import { describe, it, expect } from "vitest";
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { join } from "node:path";
import { checkE2e, classifySpec } from "./e2e-doctor.mjs";

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

  it("is runner-agnostic — a target:native Maestro flow (.maestro/ + .yaml) is clean too", () => {
    const dir = tmp();
    try {
      mkdirSync(join(dir, ".maestro"));
      mkdirSync(join(dir, "e2e"));
      writeFileSync(join(dir, "e2e", "flows.json"), JSON.stringify([{ name: "login", target: "native", spec: "e2e/login.yaml" }]));
      writeFileSync(join(dir, "e2e", "login.yaml"), "appId: com.example\n- launchApp\n");
      const r = checkE2e(dir);
      expect(r.runners).toContain("maestro");
      expect(r.gaps).toBe(0);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("is target-aware — a target:native flow with only a web runner is a gap", () => {
    const dir = tmp();
    try {
      writeFileSync(join(dir, "playwright.config.ts"), "export default {};\n"); // web runner only
      mkdirSync(join(dir, "e2e"));
      writeFileSync(join(dir, "e2e", "flows.json"), JSON.stringify([{ name: "cold start", target: "native", spec: "e2e/cold.yaml" }]));
      writeFileSync(join(dir, "e2e", "cold.yaml"), "appId: com.example\n- launchApp\n");
      const r = checkE2e(dir);
      expect(r.gaps).toBe(1);
      expect(r.messages.join(" ")).toContain("native runner");
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  // Depth (AFFE-JOURNEY-002): a spec existing is not coverage — a linked flow must drive its journey to a declared
  // terminal, and the spec must actually assert it. These are depthGaps (warn-tier), NOT hard existence gaps.
  it("flags a linked flow that declares no terminal — as a depth gap, not an existence gap", () => {
    const dir = tmp();
    try {
      writeFileSync(join(dir, "playwright.config.ts"), "export default {};\n");
      mkdirSync(join(dir, "e2e"));
      writeFileSync(
        join(dir, "e2e", "flows.json"),
        JSON.stringify([{ name: "onboarding", backendJourney: "OnboardingFlow", spec: "e2e/onboarding.spec.ts" }]),
      );
      writeFileSync(join(dir, "e2e", "onboarding.spec.ts"), 'await expect(page).toHaveURL(/\\/onboarding/);\n');
      const r = checkE2e(dir);
      expect(r.gaps).toBe(0); // spec exists + runner present — existence is fine
      expect(r.depthGaps).toBe(1);
      expect(r.messages.join(" ")).toContain("terminal");
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("flags a flow whose spec never asserts its declared terminal (stops at entry)", () => {
    const dir = tmp();
    try {
      writeFileSync(join(dir, "playwright.config.ts"), "export default {};\n");
      mkdirSync(join(dir, "e2e"));
      writeFileSync(
        join(dir, "e2e", "flows.json"),
        JSON.stringify([
          { name: "onboarding", backendJourney: "OnboardingFlow", terminal: "/dashboard", spec: "e2e/onboarding.spec.ts" },
        ]),
      );
      // The entry-only spec — asserts the wizard URL and stops; never references the /dashboard terminal.
      writeFileSync(join(dir, "e2e", "onboarding.spec.ts"), 'await expect(page).toHaveURL(/\\/onboarding/);\n');
      const r = checkE2e(dir);
      expect(r.gaps).toBe(0);
      expect(r.depthGaps).toBe(1);
      expect(r.messages.join(" ")).toContain("/dashboard");
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("is clean (no depth gap) when the spec asserts its declared terminal", () => {
    const dir = tmp();
    try {
      writeFileSync(join(dir, "playwright.config.ts"), "export default {};\n");
      mkdirSync(join(dir, "e2e"));
      writeFileSync(
        join(dir, "e2e", "flows.json"),
        JSON.stringify([
          { name: "onboarding", backendJourney: "OnboardingFlow", terminal: "/dashboard", spec: "e2e/onboarding.spec.ts" },
        ]),
      );
      writeFileSync(
        join(dir, "e2e", "onboarding.spec.ts"),
        'await fillWizard(page);\nawait expect(page).toHaveURL("/dashboard");\n',
      );
      const r = checkE2e(dir);
      expect(r.gaps).toBe(0);
      expect(r.depthGaps).toBe(0);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("classifies backend-gated, seed-pending, front-only, and native specs", () => {
    const dir = tmp();
    try {
      mkdirSync(join(dir, "e2e"));
      writeFileSync(join(dir, "e2e", "backend.spec.ts"), "requireBackend();\n");
      writeFileSync(join(dir, "e2e", "seed.spec.ts"), "requireBackend(); requireSeed();\n");
      writeFileSync(join(dir, "e2e", "smoke.spec.ts"), "test('renders', () => {});\n");
      writeFileSync(join(dir, "e2e", "native.yaml"), "appId: com.example\n");
      expect(classifySpec(dir, "e2e/backend.spec.ts")).toBe("ci-gated");
      expect(classifySpec(dir, "e2e/seed.spec.ts")).toBe("seed-pending");
      expect(classifySpec(dir, "e2e/smoke.spec.ts")).toBe("front-only");
      expect(classifySpec(dir, "e2e/native.yaml")).toBe("native");
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
});
