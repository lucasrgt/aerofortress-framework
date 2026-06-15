import { describe, it, expect } from "vitest";
import { toSessionState } from "./session";

// The session projection is a guard's only source of truth — every protected screen branches on it. Pin each
// branch, especially the load-bearing one: "pending is loading, NOT anonymous". A regression that collapses
// loading into anonymous bounces every freshly-signed-in user back to the login screen.
describe("toSessionState", () => {
  it("is loading while the me-query is pending (defer the decision — never redirect yet)", () => {
    expect(toSessionState({ isPending: true, isError: false, data: undefined })).toEqual({ status: "loading" });
  });

  it("is loading even after a reset clears data (the post-sign-in wait — the bounce-to-login fix)", () => {
    // Resetting the me-query on sign-in returns it to pending with data cleared; the guard must wait here, not
    // read the previous anonymous snapshot.
    expect(toSessionState({ isPending: true, isError: false, data: undefined }).status).toBe("loading");
  });

  it("is anonymous on a settled error (a 401/404 on me is 'not signed in', not a failure surface)", () => {
    expect(toSessionState({ isPending: false, isError: true, data: undefined })).toEqual({ status: "anonymous" });
  });

  it("is anonymous when settled with no data even without an error", () => {
    expect(toSessionState({ isPending: false, isError: false, data: undefined })).toEqual({ status: "anonymous" });
  });

  it("is authenticated with the user once the query resolves", () => {
    const user = { id: "u1", role: "admin" };
    expect(toSessionState({ isPending: false, isError: false, data: user })).toEqual({
      status: "authenticated",
      user,
    });
  });

  // Seed 1 — a transient `me` failure (5xx/timeout/offline flap) on F5 must NOT log an authenticated user out.
  // Opt-in via the isUnauthorized classifier: a non-auth error DEFERS (loading) instead of collapsing to anonymous.
  it("is loading on a TRANSIENT error (isUnauthorized=false) — defer, don't bounce to login on a network flap", () => {
    expect(toSessionState({ isPending: false, isError: true, data: undefined, isUnauthorized: false })).toEqual({
      status: "loading",
    });
  });

  it("is anonymous on an AUTH error (isUnauthorized=true) — a real 401 is 'not signed in'", () => {
    expect(toSessionState({ isPending: false, isError: true, data: undefined, isUnauthorized: true })).toEqual({
      status: "anonymous",
    });
  });

  it("legacy behaviour holds when the classifier is omitted — any error ⇒ anonymous", () => {
    // Documents the pre-0.5.0 contract (and the latent F5-logout risk): without isUnauthorized wired, a 5xx still
    // collapses to anonymous. Apps opt into the safe behaviour by passing the classifier.
    expect(toSessionState({ isPending: false, isError: true, data: undefined })).toEqual({ status: "anonymous" });
  });
});
