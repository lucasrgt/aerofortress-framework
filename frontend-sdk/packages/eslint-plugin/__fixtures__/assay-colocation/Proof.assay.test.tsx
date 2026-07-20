import { render, renderHook } from "@testing-library/react";
import { useProofModel } from "./Proof.viewModel";
import { ProofView } from "./Proof.view";

renderHook(() => useProofModel());
render(<ProofView />);
