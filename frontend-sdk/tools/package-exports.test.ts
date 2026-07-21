import { createRequire } from "node:module";
import { describe, expect, it } from "vitest";

describe("frontend SDK package exports", () => {
  it("loads the Playwright backend observer from CommonJS global setup", () => {
    const require = createRequire(import.meta.url);
    const backend = require("@aerofortress/frontend-sdk/playwright-backend");

    expect(backend).toMatchObject({
      createBackendGlobalSetup: expect.any(Function),
      expectBackendSlices: expect.any(Function),
      waitForBackendSlices: expect.any(Function),
      observeBackend: expect.any(Function),
      probeBackend: expect.any(Function),
    });
  });
});
