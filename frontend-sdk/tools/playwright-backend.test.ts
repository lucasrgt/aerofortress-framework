import { describe, expect, it, vi } from "vitest";
import {
  createBackendGlobalSetup,
  expectBackendSlices,
  observeBackend,
  probeBackend,
} from "./playwright-backend.mjs";

const contract = {
  paths: {
    "/account/login": { post: { operationId: "Login" } },
    "/customers/{id}": { get: { operationId: "GetCustomer" } },
  },
};

function fakePage() {
  let listener: ((response: unknown) => void) | undefined;
  return {
    page: { on: (_event: "response", next: (response: unknown) => void) => { listener = next; } },
    respond(method: string, url: string, status: number) {
      listener?.({ request: () => ({ method: () => method }), status: () => status, url: () => url });
    },
  };
}

describe("playwright backend proof", () => {
  it("rejects an observation when global setup did not prove readiness", async () => {
    const { page } = fakePage();
    await expect(observeBackend(page, contract, { PW_API_URL: "http://127.0.0.1:5050" })).rejects.toThrow("not ready");
  });

  it("rejects a cosmetic ready flag with no configured real API", async () => {
    const { page } = fakePage();
    await expect(observeBackend(page, contract, { PW_API_READY: "1" })).rejects.toThrow("PW_API_URL");
  });

  it("records a successful browser response by OpenAPI operationId", async () => {
    const browser = fakePage();
    const observation = await observeBackend(
      browser.page,
      contract,
      { PW_API_URL: "http://127.0.0.1:5050", PW_API_READY: "1" },
    );

    browser.respond("GET", "http://localhost:5173/customers/123?expand=contacts", 200);

    expect(() => expectBackendSlices(observation, ["GetCustomer"], { status: "success" })).not.toThrow();
  });

  it("distinguishes a real error proof from a success response", async () => {
    const browser = fakePage();
    const observation = await observeBackend(
      browser.page,
      contract,
      { PW_API_URL: "http://127.0.0.1:5050", PW_API_READY: "1" },
    );
    browser.respond("POST", "http://127.0.0.1:5050/account/login", 401);

    expect(() => expectBackendSlices(observation, ["Login"], { status: "error" })).not.toThrow();
    expect(() => expectBackendSlices(observation, ["Login"], { status: "success" })).toThrow("Login");
  });

  it("rejects unknown operations and forged observations", async () => {
    const browser = fakePage();
    const observation = await observeBackend(
      browser.page,
      contract,
      { PW_API_URL: "http://127.0.0.1:5050", PW_API_READY: "1" },
    );

    expect(() => expectBackendSlices(observation, ["Missing"], { status: "success" })).toThrow("does not declare");
    expect(() => expectBackendSlices({} as never, ["Login"], { status: "success" })).toThrow("observeBackend");
  });

  it("marks readiness only after the canonical probe succeeds", async () => {
    const env: Record<string, string> = { PW_API_URL: "http://127.0.0.1:5050/" };
    const fetchImpl = vi.fn(async () => ({ ok: true, status: 200 }));

    await probeBackend({ env, fetchImpl, path: "/health", timeoutMs: 0, intervalMs: 0 });

    expect(fetchImpl).toHaveBeenCalledWith("http://127.0.0.1:5050/health", {
      method: "GET",
      redirect: "manual",
    });
    expect(env.PW_API_READY).toBe("1");
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
