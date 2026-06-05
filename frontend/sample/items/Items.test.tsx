import type { ReactNode } from "react";
import { describe, it, expect } from "vitest";
import { renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useItemsModel } from "./Items.viewModel";

// CANONICAL TEST (LZFE005) — mounts the data door against the real generated client (wired, not mocked). On mount
// the query is pending, so the resource is in its `loading` state. Asserting the spine's status is the uniform
// shape every screen test follows.
function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe("Items ViewModel", () => {
  it("starts its resource in loading while the list is fetched", () => {
    const { result } = renderHook(() => useItemsModel(), { wrapper });
    expect(result.current.state.items.status).toBe("loading");
  });
});
