import { describe, expect, it } from "vitest";
// @ts-expect-error - plain .mjs tool module, typed by its JSDoc
import { renderMutator, renderOrvalConfig } from "./client-scaffold.mjs";

// The mutator/config pair every pilot re-derived by hand — rendered conformant by construction. What is
// pinned: the seams the harness depends on, and the LZFE020-blessed base-URL shape.
describe("client-scaffold", () => {
  it("the mutator injects the base URL at boot, never baking a host into the construction", () => {
    const mutator = renderMutator();

    // LZFE020's blessed shape: an injectable default on instance.defaults, no baseURL: in axios.create.
    expect(mutator).toContain("instance.defaults.baseURL =");
    expect(mutator).not.toMatch(/axios\.create\(\{[^}]*baseURL/s);
    expect(mutator).toContain("export function configureClient");
  });

  it("the mutator carries the auth seams: setAccessToken sink + bearer injection", () => {
    const mutator = renderMutator();

    expect(mutator).toContain("export function setAccessToken");
    expect(mutator).toContain("Authorization: `Bearer ${accessToken}`");
  });

  it("web opts into the cookie session via X-Client, with credentials riding cross-origin", () => {
    const mutator = renderMutator();

    expect(mutator).toContain('"X-Client": "web"');
    expect(mutator).toContain("withCredentials: true");
  });

  it("the orval config wires the mutator, filters non-app audiences, and targets client.gen", () => {
    const config = renderOrvalConfig("shop", "./contract/Shop.Api.json");

    expect(config).toContain('target: "./contract/Shop.Api.json"');
    expect(config).toContain('tags: ["lazuli:webhook", "lazuli:internal"]');
    expect(config).toContain('mutator: { path: "./src/lib/lazuli-client.ts", name: "lazuliClient" }');
    expect(config).toContain('target: "./src/client.gen/shop.ts"');
  });
});
