import { describe, expect, it } from "vitest";
import { checkPackages, declaredVersion } from "./framework-sync.mjs";
import { FRONTEND_PACKAGE_VERSIONS } from "./package-versions.mjs";

const canonical = FRONTEND_PACKAGE_VERSIONS;

describe("framework-sync", () => {
  it("accepts normal semver ranges at the canonical versions", () => {
    const result = checkPackages({
      canonical,
      declarations: [{
        path: "clients/app/package.json",
        packages: {
          "@aerofortress/frontend-sdk": "^0.2.3",
          "eslint-plugin-aerofortress": "^0.12.1",
          "@aerofortress/react": "~0.6.0",
        },
      }],
      hasFrontend: true,
      legacyMirror: false,
    });
    expect(result.status).toBe("ok");
  });

  it("fails stale or missing framework packages", () => {
    const result = checkPackages({
      canonical,
      declarations: [{
        path: "clients/app/package.json",
        packages: {
          "@aerofortress/frontend-sdk": "^0.1.0",
          "eslint-plugin-aerofortress": "^0.10.0",
        },
      }],
      hasFrontend: true,
      legacyMirror: false,
    });
    expect(result.status).toBe("drifted");
    expect(result.messages.join("\n")).toContain("0.10.0");
    expect(result.messages.join("\n")).toContain("@aerofortress/react");
  });

  it("rejects the retired in-repo plugin mirror", () => {
    const result = checkPackages({
      canonical,
      declarations: [],
      hasFrontend: false,
      legacyMirror: true,
    });
    expect(result.status).toBe("drifted");
    expect(result.messages[0]).toContain("legacy vendored");
  });

  it("extracts the concrete version from a dependency range", () => {
    expect(declaredVersion("^0.12.0")).toBe("0.12.0");
  });
});
