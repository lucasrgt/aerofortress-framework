import { describe, it, expect, vi } from "vitest";
import { toAsyncState } from "./async-state";

// The projection is the spine's load-bearing logic: every screen's state flows through it. Pin each branch — a
// regression here silently breaks loading/error/empty/ready across every feature at once.
describe("toAsyncState", () => {
  it("is loading while the query is pending", () => {
    expect(toAsyncState({ isPending: true, isError: false, data: undefined }, { errorMessage: "e" })).toEqual({
      status: "loading",
    });
  });

  it("is error on isError, carrying the localized message and a retry that calls refetch", () => {
    const refetch = vi.fn();
    const s = toAsyncState({ isPending: false, isError: true, data: undefined, refetch }, { errorMessage: "boom" });
    expect(s.status).toBe("error");
    if (s.status === "error") {
      expect(s.message).toBe("boom");
      s.retry?.();
      expect(refetch).toHaveBeenCalledOnce();
    }
  });

  it("is error when data is undefined even without isError", () => {
    expect(
      toAsyncState({ isPending: false, isError: false, data: undefined }, { errorMessage: "e" }).status,
    ).toBe("error");
  });

  it("omits retry when the query has no refetch", () => {
    const s = toAsyncState({ isPending: false, isError: true, data: undefined }, { errorMessage: "e" });
    if (s.status === "error") expect(s.retry).toBeUndefined();
  });

  it("is empty when the isEmpty predicate matches", () => {
    const s = toAsyncState({ isPending: false, isError: false, data: [] as number[] }, {
      errorMessage: "e",
      isEmpty: (d) => d.length === 0,
    });
    expect(s.status).toBe("empty");
  });

  it("is ready with the data otherwise", () => {
    const s = toAsyncState({ isPending: false, isError: false, data: [1, 2] }, {
      errorMessage: "e",
      isEmpty: (d) => d.length === 0,
    });
    expect(s).toEqual({ status: "ready", data: [1, 2] });
  });
});
