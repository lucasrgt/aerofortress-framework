import { describe, it, expect } from "vitest";
import { extractHooks, checkEndpointCoverage } from "./endpoint-coverage.mjs";

// LZFE008 — every app-facing generated hook should be consumed by a ViewModel; an unconsumed one is a warn-tier
// "loose endpoint" (backend done, UI not wired). Pin extraction + the wired/loose split on the pure core.
describe("extractHooks", () => {
  it("pulls use<Name> hooks from generated-client source (function or const exports)", () => {
    const client = `
      export function useGetThing() {}
      export const useListThings = () => {};
      export function notAHook() {}
      export const helper = 1;
    `;
    expect(extractHooks(client).sort()).toEqual(["useGetThing", "useListThings"]);
  });
});

describe("checkEndpointCoverage", () => {
  it("is fully covered when every hook is referenced by a ViewModel", () => {
    const r = checkEndpointCoverage(["useA", "useB"], "const a = useA(); const b = useB();");
    expect(r.total).toBe(2);
    expect(r.wired).toBe(2);
    expect(r.loose).toEqual([]);
    expect(r.messages).toEqual([]);
  });

  it("flags a loose endpoint (a hook no ViewModel references) as a warning", () => {
    const r = checkEndpointCoverage(["useA", "useB"], "const a = useA();");
    expect(r.wired).toBe(1);
    expect(r.loose).toEqual(["useB"]);
    expect(r.messages.join(" ")).toContain("useB");
    expect(r.messages.join(" ")).toContain("loose endpoint");
  });

  it("dedupes repeated hook names and matches on word boundaries (no substring false-positive)", () => {
    // "useFoo" must not be considered wired just because "useFooBar" appears.
    const r = checkEndpointCoverage(["useFoo", "useFoo", "useFooBar"], "const x = useFooBar();");
    expect(r.total).toBe(2); // deduped
    expect(r.loose).toEqual(["useFoo"]);
  });
});
