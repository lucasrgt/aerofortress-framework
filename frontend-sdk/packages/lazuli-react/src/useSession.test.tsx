import { describe, expect, it } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { useSession } from "./useSession";

// The boot gate: not ready until the bootstrap settles, ready after — success OR failure (an anonymous
// session is a settled session; only an unsettled one may hold the navigator).
describe("useSession", () => {
  it("is not ready until the bootstrap settles, then ready", async () => {
    let release!: () => void;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const { result } = renderHook(() => useSession(() => gate));

    expect(result.current.ready).toBe(false);
    release();
    await waitFor(() => expect(result.current.ready).toBe(true));
  });

  it("settles ready even when the bootstrap fails — anonymous is a state, not a hang", async () => {
    const { result } = renderHook(() => useSession(() => Promise.reject(new Error("offline"))));

    await waitFor(() => expect(result.current.ready).toBe(true));
  });
});
