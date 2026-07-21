import { describe, expect, it } from "vitest";
import {
  checkFeatureE2e,
  extractE2eObligations,
  extractSlices,
  sliceHooks,
} from "./feature-e2e-coverage.mjs";

describe("feature E2E coverage", () => {
  it("extracts stable unique obligations", () => {
    expect(extractE2eObligations("/** @e2e login-happy\n * @e2e login-happy\n * @e2e login-sad */"))
      .toEqual(["login-happy", "login-sad"]);
  });

  it("discovers slices from their structural marker", () => {
    expect(extractSlices([
      "[Slice]\npublic static class Login {}",
      "[Slice]\npublic static class Ping {}",
    ])).toEqual(["Login", "Ping"]);
  });

  it("discovers slices whose attribute lines carry explanatory comments", () => {
    const sources = [
      "[Slice] // visible endpoint\npublic static class Login {}",
      "[Slice] // read-only query\npublic static class Profile {}",
    ];

    expect(extractSlices(sources)).toEqual(["Login", "Profile"]);
  });

  it("finds only generated slice hooks consumed by the ViewModel", () => {
    expect(sliceHooks('import { useLogin, usePing } from "@/client.gen/api";\nuseLogin();', ["Login", "Pay"]))
      .toEqual(["Login"]);
    expect(sliceHooks('import { login as signIn } from "@/client.gen/api";\nsignIn();', ["Login"]))
      .toEqual(["Login"]);
    expect(sliceHooks("// useLogin is prose, not a generated import", ["Login"]))
      .toEqual([]);
    expect(sliceHooks([
      'import { useLogin } from "@/lib/local-login";',
      'import { usePing } from "@/client.gen/api";',
    ].join("\n"), ["Login", "Pay"]))
      .toEqual([]);
  });

  it("requires every ViewModel to link an existing flow", () => {
    const result = checkFeatureE2e([
      { path: "src/Login.viewModel.ts", source: "/** @e2e login-happy\n * @e2e login-sad */" },
      { path: "src/Profile.viewModel.ts", source: "export function useProfile() {}" },
      { path: "src/Unknown.viewModel.ts", source: "/** @e2e missing */" },
    ], [
      { id: "login-happy", path: "happy", features: ["Login"] },
      { id: "login-sad", path: "sad", features: ["Login"] },
    ], []);

    expect(result.features).toBe(3);
    expect(result.linked).toBe(1);
    expect(result.gaps).toBe(2);
    expect(result.messages.join("\n")).toContain("declares no");
    expect(result.messages.join("\n")).toContain("resolves to no surface");
  });

  it("requires subject-bound happy and sad frontend flows for every ViewModel", () => {
    const viewModels = [{
      path: "src/Login.viewModel.ts",
      source: '/** @e2e login-happy */\nimport { useLogin } from "@/client.gen/api";',
    }];
    const incomplete = checkFeatureE2e(viewModels, [
      { id: "login-happy", path: "happy", features: ["Login"], backendSlices: ["Login"] },
    ], ["Login"]);
    expect(incomplete.complete).toBe(0);
    expect(incomplete.gaps).toBe(1);
    expect(incomplete.messages.join(" ")).toContain("path:sad");

    const complete = checkFeatureE2e([{
      ...viewModels[0],
      source: "/** @e2e login-happy\n * @e2e login-sad */\nuseLogin();",
    }], [
      { id: "login-happy", path: "happy", features: ["Login"], backendSlices: ["Login"] },
      { id: "login-sad", path: "sad", features: ["Login"], backendSlices: ["Login"] },
    ], ["Login"]);
    expect(complete.gaps).toBe(0);
    expect(complete.linked).toBe(1);
  });

  it("rejects a shared flow that lets multiple ViewModels borrow one proof", () => {
    const result = checkFeatureE2e([
      { path: "src/Cart.viewModel.ts", source: "/** @e2e checkout-happy\n * @e2e checkout-sad */" },
      { path: "src/Payment.viewModel.ts", source: "/** @e2e checkout-happy\n * @e2e checkout-sad */" },
    ], [
      { id: "checkout-happy", path: "happy", features: ["Cart", "Payment"] },
      { id: "checkout-sad", path: "sad", features: ["Cart", "Payment"] },
    ], []);
    expect(result.gaps).toBeGreaterThan(0);
    expect(result.messages.join(" ")).toContain("exactly one ViewModel");
  });

  it("rejects duplicate flow ids across surfaces", () => {
    const result = checkFeatureE2e([], [
      { id: "login", path: "happy", __surface: "web" },
      { id: "login", path: "happy", __surface: "mobile" },
    ], []);
    expect(result.gaps).toBe(1);
    expect(result.messages.join(" ")).toContain("duplicated");
  });

  it("requires every UI-consumed slice to be named by its linked flow", () => {
    const viewModel = [{
      path: "src/Profile.viewModel.ts",
      source: '/** @e2e profile-happy\n * @e2e profile-sad */\nimport { useGetProfile } from "@/client.gen/api";',
    }];
    const missing = checkFeatureE2e(viewModel, [
      { id: "profile-happy", path: "happy", features: ["Profile"] },
      { id: "profile-sad", path: "sad", features: ["Profile"] },
    ], ["GetProfile"]);
    expect(missing.gaps).toBe(1);
    expect(missing.messages.join(" ")).toContain("backendSlices");

    const covered = checkFeatureE2e(viewModel, [
      { id: "profile-happy", path: "happy", features: ["Profile"], backendSlices: ["GetProfile"] },
      { id: "profile-sad", path: "sad", features: ["Profile"], backendSlices: ["GetProfile"] },
    ], ["GetProfile"]);
    expect(covered.gaps).toBe(0);
  });

  it("requires infrastructure data doors to have flows too", () => {
    const infrastructure = [{
      path: "src/lib/session.ts",
      source: 'import { useRefreshSession } from "@/client.gen/api";\nuseRefreshSession();',
    }];
    const missing = checkFeatureE2e([], [], ["RefreshSession"], infrastructure);
    expect(missing.gaps).toBe(1);
    expect(missing.messages.join(" ")).toContain("UI infrastructure");

    const complete = checkFeatureE2e([], [
      { id: "refresh-happy", path: "happy", backendSlices: ["RefreshSession"] },
      { id: "refresh-sad", path: "sad", backendSlices: ["RefreshSession"] },
    ], ["RefreshSession"], infrastructure);
    expect(complete.gaps).toBe(0);
  });
});
