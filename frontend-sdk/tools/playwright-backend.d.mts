/** Environment contract shared by the Playwright global setup and test workers. */
export interface BackendEnvironment {
  PW_API_URL?: string;
  PW_API_READY?: string;
  [name: string]: string | undefined;
}

/** The small fetch surface needed by the backend preflight. */
export type BackendProbeFetch = (
  url: string,
  init: { method: "GET"; redirect: "manual" },
) => Promise<{ ok: boolean; status: number }>;

/** Configuration for the canonical real-backend preflight. */
export interface BackendProbeOptions {
  env?: BackendEnvironment;
  fetchImpl?: BackendProbeFetch;
  path?: string;
  timeoutMs?: number;
  intervalMs?: number;
}

/** Assert that global setup proved the configured real API reachable. */
export function requireBackend(env?: BackendEnvironment): string;

/** Poll the configured API and mark it ready only after a successful HTTP response. */
export function probeBackend(options?: BackendProbeOptions): Promise<string>;

/** Build the Playwright globalSetup function for the configured real API. */
export function createBackendGlobalSetup(options?: BackendProbeOptions): () => Promise<void>;
