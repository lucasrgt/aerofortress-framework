import { describe, expect, it } from "vitest";
import { guardSession } from "./guard";
import type { SessionState } from "./session";

// guardSession is the symmetric guard primitive: one function, `allow` flipped, decides BOTH a private route and a
// public/guest one. The tests pin the symmetry (the same loading→wait) and the two failure directions — the
// anonymous-on-private bounce AND the signed-in-on-guest bounce (the pauta bug: a logged-in user reaching /login).
describe("guardSession", () => {
  const loading: SessionState<{ id: string }> = { status: "loading" };
  const authed: SessionState<{ id: string }> = { status: "authenticated", user: { id: "u1" } };
  const anon: SessionState<{ id: string }> = { status: "anonymous" };

  it("loading ⇒ wait for BOTH directions — never decide before the session settles", () => {
    expect(guardSession(loading, { allow: "authenticated", redirectTo: "/login" })).toEqual({ action: "wait" });
    expect(guardSession(loading, { allow: "anonymous", redirectTo: "/home" })).toEqual({ action: "wait" });
  });

  it("auth-guard: authenticated renders, anonymous redirects to the sign-in route", () => {
    expect(guardSession(authed, { allow: "authenticated", redirectTo: "/login" })).toEqual({ action: "render" });
    expect(guardSession(anon, { allow: "authenticated", redirectTo: "/login" })).toEqual({
      action: "redirect",
      to: "/login",
    });
  });

  it("guest-guard: anonymous renders, an already-signed-in user is bounced to home (the pauta bug)", () => {
    expect(guardSession(anon, { allow: "anonymous", redirectTo: "/home" })).toEqual({ action: "render" });
    expect(guardSession(authed, { allow: "anonymous", redirectTo: "/home" })).toEqual({
      action: "redirect",
      to: "/home",
    });
  });
});
