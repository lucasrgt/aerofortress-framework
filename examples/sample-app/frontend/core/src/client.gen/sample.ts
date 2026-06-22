import { useMutation, useQuery } from "@tanstack/react-query";
import type { Item } from "../items/Items.viewModel";

// Harness stand-in for the orval-generated typed hook (`@/client.gen/sample`). It is a REAL wired react-query hook
// over a stub fetch — so the sample's test mounts the data door against this exactly as it would a real client
// (AFFE003: wired, not mocked). A generated client would have the same surface: a typed query hook per slice.
export function useListItems() {
  return useQuery({
    queryKey: ["sample", "list_items"],
    queryFn: async (): Promise<{ items: Item[] }> => ({ items: [] }),
  });
}

// Stand-in for the orval hook of the backend's REAL `Deposit` slice (`MapPost("/deposit").WithName(nameof(Deposit))`
// → operationId `Deposit` → `useDeposit`, the AF0012 1:1). A sentinel wallet id mirrors the slice's NotFound sad
// path so the form recipe proves its error surface against a real failure; the small delay keeps the pending
// state observable, as a network would.
export interface DepositInput {
  walletId: string;
  amount: number;
}

export interface DepositOutput {
  walletId: string;
  balance: number;
}

// A valid-but-absent id (a UUID that passes format validation yet matches no wallet) — the NotFound path.
const MISSING_WALLET = "99999999-9999-4999-8999-999999999999";

export function useDeposit() {
  return useMutation({
    mutationFn: async (input: DepositInput): Promise<DepositOutput> => {
      await new Promise((resolve) => setTimeout(resolve, 50));
      if (input.walletId === MISSING_WALLET) throw new Error(`wallet ${input.walletId} not found`);
      return { walletId: input.walletId, balance: input.amount };
    },
  });
}
