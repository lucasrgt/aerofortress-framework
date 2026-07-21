import { describe, expect, it } from "vitest";
import {
  checkFeatureE2e,
  criticalHooks,
  extractCriticalSlices,
  extractE2eObligations,
  extractSlices,
} from "./feature-e2e-coverage.mjs";

describe("feature E2E coverage", () => {
  it("extracts stable unique obligations", () => {
    expect(extractE2eObligations("/** @e2e login-happy\n * @e2e login-happy\n * @e2e login-sad */"))
      .toEqual(["login-happy", "login-sad"]);
  });

  it("discovers critical slices regardless of attribute order", () => {
    expect(extractCriticalSlices([
      "[Slice]\n[Critical]\npublic static class Login {}",
      "[Critical]\n[Slice]\npublic static class Pay {}",
      "[Slice]\n[NonCritical]\npublic static class Ping {}",
    ])).toEqual(["Login", "Pay"]);
    expect(extractSlices([
      "[Slice]\n[Critical]\npublic static class Login {}",
      "[Slice]\n[NonCritical]\npublic static class Ping {}",
    ])).toEqual(["Login", "Ping"]);
    expect(extractCriticalSlices([
      "[Slice]\npublic static class Undecided {}",
      "[Slice]\n[NonCritical]\npublic static class Ping {}",
    ], true)).toEqual(["Undecided"]);
  });

  it("discovers slices whose attribute lines carry explanatory comments", () => {
    const sources = [
      "[Slice] // visible endpoint\n[Critical] // auth bypass defense\npublic static class Login {}",
      "[Slice]\n[NonCritical] // read-only query\npublic static class Profile {}",
    ];

    expect(extractSlices(sources)).toEqual(["Login", "Profile"]);
    expect(extractCriticalSlices(sources)).toEqual(["Login"]);
  });

  it("finds only critical hooks consumed by the ViewModel", () => {
    expect(criticalHooks('import { useLogin, usePing } from "@/client.gen/api";\nuseLogin();', ["Login", "Pay"]))
      .toEqual(["Login"]);
    expect(criticalHooks('import { login as signIn } from "@/client.gen/api";\nsignIn();', ["Login"]))
      .toEqual(["Login"]);
    expect(criticalHooks("// useLogin is prose, not a generated import", ["Login"]))
      .toEqual([]);
    expect(criticalHooks([
      'import { useLogin } from "@/lib/local-login";',
      'import { usePing } from "@/client.gen/api";',
    ].join("\n"), ["Login", "Pay"]))
      .toEqual([]);
  });

  it("requires every ViewModel to link an existing flow", () => {
    const result = checkFeatureE2e([
      { path: "src/Login.viewModel.ts", source: "/** @e2e login */" },
      { path: "src/Profile.viewModel.ts", source: "export function useProfile() {}" },
      { path: "src/Unknown.viewModel.ts", source: "/** @e2e missing */" },
    ], [{ id: "login", path: "happy" }], [], []);

    expect(result.features).toBe(3);
    expect(result.linked).toBe(1);
    expect(result.gaps).toBe(2);
    expect(result.messages.join("\n")).toContain("declares no");
    expect(result.messages.join("\n")).toContain("resolves to no surface");
  });

  it("requires happy and sad frontend flows when a critical hook is visible", () => {
    const viewModels = [{
      path: "src/Login.viewModel.ts",
      source: '/** @e2e login-happy */\nimport { useLogin } from "@/client.gen/api";',
    }];
    const incomplete = checkFeatureE2e(viewModels, [
      { id: "login-happy", path: "happy", backendSlices: ["Login"] },
    ], ["Login"], ["Login"]);
    expect(incomplete.critical).toBe(1);
    expect(incomplete.gaps).toBe(1);
    expect(incomplete.messages.join(" ")).toContain("path:sad");

    const complete = checkFeatureE2e([{
      ...viewModels[0],
      source: "/** @e2e login-happy\n * @e2e login-sad */\nuseLogin();",
    }], [
      { id: "login-happy", path: "happy", backendSlices: ["Login"] },
      { id: "login-sad", path: "sad", backendSlices: ["Login"] },
    ], ["Login"], ["Login"]);
    expect(complete.gaps).toBe(0);
    expect(complete.linked).toBe(1);
  });

  it("allows one executable flow to cover multiple collaborating ViewModels", () => {
    const result = checkFeatureE2e([
      { path: "src/Cart.viewModel.ts", source: "/** @e2e checkout */" },
      { path: "src/Payment.viewModel.ts", source: "/** @e2e checkout */" },
    ], [{ id: "checkout", path: "happy" }], [], []);
    expect(result.gaps).toBe(0);
    expect(result.linked).toBe(2);
  });

  it("rejects duplicate flow ids across surfaces", () => {
    const result = checkFeatureE2e([], [
      { id: "login", path: "happy", __surface: "web" },
      { id: "login", path: "happy", __surface: "mobile" },
    ], [], []);
    expect(result.gaps).toBe(1);
    expect(result.messages.join(" ")).toContain("duplicated");
  });

  it("requires every UI-consumed slice to be named by its linked flow", () => {
    const viewModel = [{
      path: "src/Profile.viewModel.ts",
      source: '/** @e2e profile */\nimport { useGetProfile } from "@/client.gen/api";',
    }];
    const missing = checkFeatureE2e(viewModel, [{ id: "profile", path: "happy" }], ["GetProfile"], []);
    expect(missing.gaps).toBe(1);
    expect(missing.messages.join(" ")).toContain("backendSlices");

    const covered = checkFeatureE2e(viewModel, [
      { id: "profile", path: "happy", backendSlices: ["GetProfile"] },
    ], ["GetProfile"], []);
    expect(covered.gaps).toBe(0);
  });

  it("requires infrastructure data doors to have flows too", () => {
    const infrastructure = [{
      path: "src/lib/session.ts",
      source: 'import { useRefreshSession } from "@/client.gen/api";\nuseRefreshSession();',
    }];
    const missing = checkFeatureE2e([], [], ["RefreshSession"], ["RefreshSession"], infrastructure);
    expect(missing.gaps).toBe(1);
    expect(missing.messages.join(" ")).toContain("UI infrastructure");

    const complete = checkFeatureE2e([], [
      { id: "refresh-happy", path: "happy", backendSlices: ["RefreshSession"] },
      { id: "refresh-sad", path: "sad", backendSlices: ["RefreshSession"] },
    ], ["RefreshSession"], ["RefreshSession"], infrastructure);
    expect(complete.gaps).toBe(0);
  });
});
