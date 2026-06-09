import { describe, expect, it } from "vitest";
// @ts-expect-error - plain .mjs tool module, typed by its JSDoc
import { checkFreshness, stampOf } from "./contract-freshness.mjs";

// Contract freshness — the client is pinned to the exact spec it was generated from; a moved contract must
// surface as a gate, an unstamped client as a notice (never a block before codegen has run).
describe("contract-freshness", () => {
  const spec = JSON.stringify({ openapi: "3.1.0", paths: { "/wallets/deposit": {} } });

  it("matches when the stamp equals the live spec's fingerprint", () => {
    expect(checkFreshness({ specText: spec, stamp: stampOf(spec) }).status).toBe("ok");
  });

  it("is stale when the contract moved after generation", () => {
    const moved = JSON.stringify({ openapi: "3.1.0", paths: { "/wallets/deposit": {}, "/wallets/freeze": {} } });
    expect(checkFreshness({ specText: moved, stamp: stampOf(spec) }).status).toBe("stale");
  });

  it("is a notice (not a gate) before the first stamp exists", () => {
    expect(checkFreshness({ specText: spec, stamp: null }).status).toBe("unstamped");
  });

  it("a reformat of the spec is not a drift — the fingerprint is whitespace-insensitive", () => {
    const reformatted = JSON.stringify(JSON.parse(spec), null, 2);
    expect(stampOf(reformatted)).toBe(stampOf(spec));
  });
});
