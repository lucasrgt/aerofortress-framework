#!/usr/bin/env node
// LZFE — the generated client's hand-owned half. orval generates the hooks, but the MUTATOR they all call
// through (auth injection, base-URL port, the X-Client header that turns on the web cookie session) and the
// orval config itself are hand-owned files nothing scaffolded — every pilot re-derived them. This renders both,
// conformant by construction: the base URL is an injectable default overridden at boot via configureClient()
// (the LZFE020-blessed shape — never a baked host in axios.create), the token sink is the seam's
// setAccessToken, and the audience filter keeps non-app endpoints out of the client (so LZFE008 stays
// high-signal). Graduated from the hostpoint pilot's lazuli-client.ts + orval.config.ts.
//
// Usage: node tools/client-scaffold.mjs <client-name> <contract-path> [target-dir]
//   node tools/client-scaffold.mjs hostpoint ./contract/Hostpoint.Api.json ../app-core/src

import { mkdirSync, writeFileSync, existsSync } from "node:fs";
import { join } from "node:path";

/** The orval mutator + client seam (lib/lazuli-client.ts) — the single HTTP door every generated hook calls. */
export function renderMutator() {
  return `import axios, { type AxiosRequestConfig } from "axios";

// Web detection without importing react-native's Platform (which would pull the RN runtime into the
// platform-agnostic data-door path + the jsdom test env). \`document\` exists only on web/DOM.
const isWeb = typeof document !== "undefined";

// The single HTTP seam every generated slice-hook calls through — the orval mutator. The opinion lives here
// (not in a fork of any tool): the base URL, the bearer token, and a uniform error shape. The generated
// *.gen.ts hooks are boring plumbing on top of this; all behaviour lives above, in the ViewModels.
//
// withCredentials so the session cookie (set by the API on login) rides cross-origin requests. X-Client: web
// is the Lazuli.Auth convention: the API delivers the refresh token as an httpOnly cookie ONLY when the
// request carries it; without it the refresh rides the response body (the native mode) and the web cookie
// session never forms.
const instance = axios.create({
  withCredentials: true,
  headers: isWeb ? { "X-Client": "web" } : {},
});

// The base URL is INJECTED by the platform shell at boot (configureClient), never read from platform config
// here — so this data-door module stays platform-agnostic. The localhost default keeps tests + pre-configure
// dev working; LZFE020 blesses exactly this injectable-default shape.
instance.defaults.baseURL = "http://localhost:8080";

/** Point the client at the resolved API base URL. Called once at app start by the shell — the same
 * push-don't-pull seam as setAccessToken; the data layer never imports platform config. */
export function configureClient(apiUrl: string): void {
  if (apiUrl) instance.defaults.baseURL = apiUrl;
}

let accessToken: string | null = null;

/** Set (or clear) the bearer token the client sends — called only by the session seam (lib/session). */
export function setAccessToken(token: string | null): void {
  accessToken = token;
}

/** The mutator orval wires every endpoint through: inject auth, return the body. */
export const lazuliClient = async <T>(config: AxiosRequestConfig): Promise<T> => {
  const response = await instance.request<T>({
    ...config,
    headers: {
      ...config.headers,
      ...(accessToken ? { Authorization: \`Bearer \${accessToken}\` } : {}),
    },
  });
  return response.data;
};

export default lazuliClient;
`;
}

/** The orval convention config — one typed TanStack hook per slice, audience-filtered, mutator-wired. */
export function renderOrvalConfig(name, contract) {
  return `import { defineConfig } from "orval";

// The shipped convention config. Generates one typed TanStack Query hook per slice from the .NET API's
// build-time OpenAPI contract (no running server needed). The generated code is plumbing: never hand-edited,
// committed verbatim; all behavior lives above it, in the ViewModels. After generating, stamp the contract
// (tools/contract-freshness.mjs --stamp) so the doctor can prove the mirror is fresh.
export default defineConfig({
  ${name}: {
    input: {
      target: "${contract}",
      // Audience filter: webhooks/internal endpoints carry a lazuli:* tag (WithEndpointKind on the backend)
      // and never become a hook — so they never trip the LZFE008 loose-endpoint warning.
      filters: { mode: "exclude", tags: ["lazuli:webhook", "lazuli:internal"] },
    },
    output: {
      mode: "split",
      target: "./src/client.gen/${name}.ts",
      schemas: "./src/client.gen/model",
      client: "react-query",
      httpClient: "axios",
      clean: true,
      prettier: false,
      override: {
        mutator: { path: "./src/lib/lazuli-client.ts", name: "lazuliClient" },
        query: { useQuery: true, useMutation: true },
      },
    },
  },
});
`;
}

// ── CLI tail (the only I/O) ─────────────────────────────────────────────────────────────────────────────────────
const invokedDirectly =
  process.argv[1] && import.meta.url.endsWith(process.argv[1].replace(/\\/g, "/").split("/").pop());
if (invokedDirectly) {
  const [, , name, contract, targetDir = "."] = process.argv;
  if (!name || !contract) {
    console.error("usage: node tools/client-scaffold.mjs <client-name> <contract-path> [target-dir]");
    process.exit(2);
  }
  const mutatorPath = join(targetDir, "src", "lib", "lazuli-client.ts");
  const orvalPath = join(targetDir, "orval.config.ts");
  for (const [path, content] of [
    [mutatorPath, renderMutator()],
    [orvalPath, renderOrvalConfig(name, contract)],
  ]) {
    if (existsSync(path)) {
      console.error(`client-scaffold: ${path} already exists — hand-owned, not overwritten.`);
      continue;
    }
    mkdirSync(join(path, ".."), { recursive: true });
    writeFileSync(path, content);
    console.log(`created ${path}`);
  }
}
