const DEFAULT_TIMEOUT_MS = 30_000;
const DEFAULT_INTERVAL_MS = 250;

/**
 * Assert that Playwright's global setup reached the configured real API.
 * Backend-bound cases call this synchronously before interacting with the page.
 */
export function requireBackend(env = process.env) {
  const url = configuredBackendUrl(env);
  if (env.PW_API_READY !== "1") {
    throw new Error(
      "AeroFortress E2E backend is not ready. Run probeBackend() from Playwright globalSetup before the suite.",
    );
  }
  return url;
}

/**
 * Poll a real HTTP endpoint and mark it ready for backend-bound Playwright cases.
 * The caller owns starting the API and database; this helper only proves the configured endpoint answered.
 */
export async function probeBackend({
  env = process.env,
  fetchImpl = globalThis.fetch,
  path = "/ping",
  timeoutMs = DEFAULT_TIMEOUT_MS,
  intervalMs = DEFAULT_INTERVAL_MS,
} = {}) {
  const baseUrl = configuredBackendUrl(env);
  const probeUrl = new URL(path, ensureTrailingSlash(baseUrl)).toString();
  const deadline = Date.now() + timeoutMs;
  let lastFailure;

  do {
    try {
      const response = await fetchImpl(probeUrl, { method: "GET", redirect: "manual" });
      if (response.ok) {
        env.PW_API_READY = "1";
        return baseUrl;
      }
      lastFailure = new Error(`HTTP ${response.status}`);
    } catch (error) {
      lastFailure = error;
    }

    if (Date.now() < deadline && intervalMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }
  } while (Date.now() < deadline);

  delete env.PW_API_READY;
  const detail = lastFailure instanceof Error ? lastFailure.message : String(lastFailure ?? "no response");
  throw new Error(`AeroFortress E2E backend probe failed at ${probeUrl}: ${detail}`);
}

/** Create a Playwright globalSetup function backed by the canonical real-API probe. */
export function createBackendGlobalSetup(options = {}) {
  return async function backendGlobalSetup() {
    await probeBackend(options);
  };
}

function configuredBackendUrl(env) {
  const value = env.PW_API_URL?.trim();
  if (!value) {
    throw new Error("PW_API_URL must name the real API used by backend-bound Playwright cases.");
  }
  try {
    return new URL(value).toString().replace(/\/$/, "");
  } catch {
    throw new Error(`PW_API_URL is not an absolute URL: ${value}`);
  }
}

function ensureTrailingSlash(value) {
  return value.endsWith("/") ? value : `${value}/`;
}
