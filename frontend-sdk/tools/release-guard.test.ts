import { describe, expect, it } from "vitest";
import { RELEASE_UNITS, violations } from "./release-guard.mjs";

describe("release-guard", () => {
  it("flags a unit that changed without a version bump", () => {
    const result = violations([{ name: "eslint-plugin-aerofortress", changed: true, versionBumped: false }]);
    expect(result).toHaveLength(1);
    expect(result[0]).toContain("eslint-plugin-aerofortress");
  });

  it("passes a unit that changed and was bumped", () => {
    expect(violations([{ name: "x", changed: true, versionBumped: true }])).toEqual([]);
  });

  it("passes a unit that did not change", () => {
    expect(violations([{ name: "x", changed: false, versionBumped: false }])).toEqual([]);
  });

  it("covers every publishable package (framework NuGets, CLI, and the three npm packages)", () => {
    expect(RELEASE_UNITS.map((u) => u.name)).toEqual([
      "AeroFortress.Framework.* (nuget)",
      "aerofortress-framework-cli",
      "@aerofortress/react",
      "eslint-plugin-aerofortress",
      "@aerofortress/frontend-sdk",
    ]);
  });
});
