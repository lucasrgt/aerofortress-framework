import { describe, expect, it } from "vitest";
import {
  backendInventoryError,
  checkJourneyParity,
  extractBackendJourneyInventory,
  parseFlowManifests,
} from "./journey-parity.mjs";

function slice(name: string, verb: "Get" | "Post", journeys: string[] = []) {
  return [
    "[Slice]",
    `public static class ${name}`,
    "{",
    `  public static void Map(IEndpointRouteBuilder app) => app.Map${verb}(\"/${name.toLowerCase()}\", () => null);`,
    "}",
    ...journeys.map((path) => `[Journey(typeof(${name}), JourneyPath.${path})]`),
  ].join("\n");
}

describe("journey parity", () => {
  it("derives write shape and subject-bound journey paths from C#", () => {
    const inventory = extractBackendJourneyInventory([
      slice("CreateOrder", "Post", ["Happy", "Sad"]),
      slice("ListOrders", "Get"),
    ]);

    expect(inventory.writes).toEqual(["CreateOrder"]);
    expect(inventory.slices).toEqual(["CreateOrder", "ListOrders"]);
    expect([...inventory.paths.get("CreateOrder")!]).toEqual(["happy", "sad"]);
  });

  it("rejects a misconfigured backend root while allowing a genuinely read-only slice inventory", () => {
    expect(backendInventoryError(extractBackendJourneyInventory([]))).toContain("no [Slice]");
    expect(backendInventoryError(extractBackendJourneyInventory([slice("ListOrders", "Get")]))).toBeNull();
  });

  it("requires both backend paths for a UI-bound write", () => {
    const inventory = extractBackendJourneyInventory([slice("CreateOrder", "Post", ["Happy"])]);
    const result = checkJourneyParity(inventory, [{ backendSlices: ["CreateOrder"] }]);

    expect(result.gaps).toBe(1);
    expect(result.missing).toEqual([{ slice: "CreateOrder", paths: ["sad"] }]);
  });

  it("allows a write with no frontend surface to remain backend-only", () => {
    const inventory = extractBackendJourneyInventory([slice("InternalSweep", "Post")]);
    const result = checkJourneyParity(inventory, [{ backendSlices: ["ListOrders"] }]);

    expect(result.gaps).toBe(0);
    expect(result.backendOnly).toEqual(["InternalSweep"]);
  });

  it("combines independently-owned frontend surfaces without file-name links", () => {
    const flows = parseFlowManifests([
      { path: "app/e2e/flows.json", content: JSON.stringify([{ backendSlices: ["CreateOrder"] }]) },
      { path: "operator/e2e/flows.json", content: JSON.stringify([{ backendSlices: ["ApproveOrder"] }]) },
    ]);
    const inventory = extractBackendJourneyInventory([
      slice("CreateOrder", "Post", ["Happy", "Sad"]),
      slice("ApproveOrder", "Post", ["Happy", "Sad"]),
    ]);

    expect(checkJourneyParity(inventory, flows).gaps).toBe(0);
  });

  it("identifies which surface supplied an invalid manifest", () => {
    expect(() =>
      parseFlowManifests([{ path: "operator/e2e/flows.json", content: "{}" }]),
    ).toThrow("operator/e2e/flows.json: flows manifest must be an array");
  });
});
