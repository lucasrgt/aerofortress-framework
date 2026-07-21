import { describe, expect, it, vi } from "vitest";
import { createBackendGlobalSetup, probeBackend, requireBackend } from "./playwright-backend.mjs";

describe("playwright backend proof", () => {
  it("rejects a backend-bound case when global setup did not prove readiness", () => {
    expect(() => requireBackend({ PW_API_URL: "http://127.0.0.1:5050" })).toThrow("not ready");
  });

  it("rejects a cosmetic ready flag with no configured real API", () => {
    expect(() => requireBackend({ PW_API_READY: "1" })).toThrow("PW_API_URL");
  });

  it("returns the normalized API URL only after the canonical probe succeeds", async () => {
    const env: Record<string, string> = { PW_API_URL: "http://127.0.0.1:5050/" };
    const fetchImpl = vi.fn(async () => ({ ok: true, status: 200 }));

    await probeBackend({ env, fetchImpl, path: "/health", timeoutMs: 0, intervalMs: 0 });

    expect(fetchImpl).toHaveBeenCalledWith("http://127.0.0.1:5050/health", {
      method: "GET",
      redirect: "manual",
    });
    expect(requireBackend(env)).toBe("http://127.0.0.1:5050");
  });

  it("clears stale readiness and fails when the real endpoint rejects the probe", async () => {
    const env: Record<string, string> = { PW_API_URL: "http://127.0.0.1:5050", PW_API_READY: "1" };

    await expect(probeBackend({
      env,
      fetchImpl: async () => ({ ok: false, status: 503 }),
      timeoutMs: 0,
      intervalMs: 0,
    })).rejects.toThrow("HTTP 503");
    expect(env.PW_API_READY).toBeUndefined();
  });

  it("creates a Playwright global setup that performs the canonical probe", async () => {
    const env: Record<string, string> = { PW_API_URL: "http://127.0.0.1:5050" };
    const setup = createBackendGlobalSetup({
      env,
      fetchImpl: async () => ({ ok: true, status: 200 }),
      timeoutMs: 0,
      intervalMs: 0,
    });

    await setup();

    expect(env.PW_API_READY).toBe("1");
  });
});
