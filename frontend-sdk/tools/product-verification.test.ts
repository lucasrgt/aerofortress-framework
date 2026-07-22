import { describe, expect, it } from "vitest";
import { productVerification } from "./product-verification.mjs";

describe("productVerification", () => {
  it("returns a criterion-specific Assay tuple without executing the product assertion eagerly", () => {
    let calls = 0;

    const [archetype, subject, options] = productVerification(
      "Checkout",
      "rejects-empty-cart",
      "an empty cart remains unsubmitted",
      () => { calls += 1; },
    );

    expect(archetype).toBeTruthy();
    expect(subject.name).toBe("Checkout");
    expect(options.label).toBe("Checkout · rejects-empty-cart");
    expect(calls).toBe(0);
  });

  it("rejects empty or unstable declarations before they can enter the AVP matrix", () => {
    expect(() => productVerification("", "valid-id", "holds", () => {})).toThrow("subject name");
    expect(() => productVerification("Checkout", "Not Stable", "holds", () => {})).toThrow("criterion id");
    expect(() => productVerification("Checkout", "valid-id", "", () => {})).toThrow("statement");
  });
});
