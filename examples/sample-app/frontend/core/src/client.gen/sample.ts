import { useQuery } from "@tanstack/react-query";
import type { Item } from "../items/Items.viewModel";

// Harness stand-in for the orval-generated typed hook (`@/client.gen/sample`). It is a REAL wired react-query hook
// over a stub fetch — so the sample's test mounts the data door against this exactly as it would a real client
// (LZFE003: wired, not mocked). A generated client would have the same surface: a typed query hook per slice.
export function useListItems() {
  return useQuery({
    queryKey: ["sample", "list_items"],
    queryFn: async (): Promise<{ items: Item[] }> => ({ items: [] }),
  });
}
