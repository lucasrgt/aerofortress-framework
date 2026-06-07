import { describe, it, expect } from "vitest";
import { checkJourneyParity } from "./journey-parity.mjs";

// Parity closes the fullstack loop at the journey grain: every backend journey has a frontend flow, and no flow
// links a journey the backend doesn't have. Pin both directions + the UI-only escape.
describe("checkJourneyParity", () => {
  it("is clean when every backend journey is linked by a flow", () => {
    const r = checkJourneyParity(
      ["HostOnboardingFlow", "AuthJourney"],
      [
        { name: "host onboarding", backendJourney: "HostOnboardingFlow" },
        { name: "auth", backendJourney: "AuthJourney" },
      ],
    );
    expect(r.gaps).toBe(0);
    expect(r.linked.sort()).toEqual(["AuthJourney", "HostOnboardingFlow"]);
  });

  it("flags an uncovered backend journey (no frontend flow)", () => {
    const r = checkJourneyParity(
      ["HostOnboardingFlow", "MercadoPagoChargeJourney"],
      [{ name: "host onboarding", backendJourney: "HostOnboardingFlow" }],
    );
    expect(r.uncovered).toEqual(["MercadoPagoChargeJourney"]);
    expect(r.gaps).toBe(1);
    expect(r.messages.join(" ")).toContain("MercadoPagoChargeJourney");
  });

  it("flags an orphan frontend flow (links a journey the backend lacks)", () => {
    const r = checkJourneyParity(
      ["AuthJourney"],
      [
        { name: "auth", backendJourney: "AuthJourney" },
        { name: "ghost", backendJourney: "DeletedJourney" },
      ],
    );
    expect(r.orphans).toEqual([{ flow: "ghost", backendJourney: "DeletedJourney" }]);
    expect(r.gaps).toBe(1);
  });

  it("allows a UI-only flow (no backendJourney) without flagging it", () => {
    const r = checkJourneyParity(["AuthJourney"], [
      { name: "auth", backendJourney: "AuthJourney" },
      { name: "boot smoke" }, // no backendJourney
    ]);
    expect(r.gaps).toBe(0);
  });
});
