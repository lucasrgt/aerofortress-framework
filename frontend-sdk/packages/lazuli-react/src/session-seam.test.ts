import { describe, expect, it, vi } from "vitest";
import { createSessionSeam, type RefreshTokenStore } from "./session-seam";

// The seam exists so a token write and the session-cache reset are ONE move — the scattered write that
// forgets the reset (and bounces a just-authenticated user back to login) must be unrepresentable.
describe("createSessionSeam", () => {
  const memoryStore = (): RefreshTokenStore & { token: string } => {
    const store = {
      token: "",
      load: () => Promise.resolve(store.token),
      save: (t: string) => {
        store.token = t;
        return Promise.resolve();
      },
      clear: () => {
        store.token = "";
        return Promise.resolve();
      },
    };
    return store;
  };

  it("onAuthenticated pairs the token write with the cache reset by construction", async () => {
    const setAccessToken = vi.fn();
    const onSessionChanged = vi.fn();
    const seam = createSessionSeam({ setAccessToken, onSessionChanged, refresh: async () => null });

    await seam.onAuthenticated({ accessToken: "jwt" });

    expect(setAccessToken).toHaveBeenCalledWith("jwt");
    expect(onSessionChanged).toHaveBeenCalledOnce();
  });

  it("bootstrap restores the session from the stored refresh and re-saves the rotated one", async () => {
    const store = memoryStore();
    store.token = "old-refresh";
    const refresh = vi.fn(async () => ({ accessToken: "jwt", refreshToken: "new-refresh" }));
    const seam = createSessionSeam({ setAccessToken: vi.fn(), refresh, store });

    const restored = await seam.bootstrapSession();

    expect(restored).toBe(true);
    expect(refresh).toHaveBeenCalledWith("old-refresh");
    expect(store.token).toBe("new-refresh");
  });

  it("a failed bootstrap reports anonymous instead of throwing — no session is a state, not an error", async () => {
    const seam = createSessionSeam({
      setAccessToken: vi.fn(),
      refresh: async () => {
        throw new Error("401");
      },
    });

    expect(await seam.bootstrapSession()).toBe(false);
  });

  it("clearSession drops the token, the store, and resets the cache in one move", async () => {
    const store = memoryStore();
    store.token = "refresh";
    const setAccessToken = vi.fn();
    const onSessionChanged = vi.fn();
    const seam = createSessionSeam({ setAccessToken, onSessionChanged, refresh: async () => null, store });

    await seam.clearSession();

    expect(setAccessToken).toHaveBeenCalledWith(null);
    expect(store.token).toBe("");
    expect(onSessionChanged).toHaveBeenCalledOnce();
  });

  it("omitting the store is the web posture — nothing persisted, nothing thrown", async () => {
    const seam = createSessionSeam({ setAccessToken: vi.fn(), refresh: async () => ({ accessToken: "jwt" }) });

    expect(await seam.bootstrapSession()).toBe(true);
  });

  // The split that kills the hostpoint cache-leak: a sign-in is an IDENTITY change (total wipe), a bootstrap is a
  // ROTATION of the same identity (light reset). Conflating them leaked user A's cache into user B's session.
  it("signIn fires the IDENTITY reset (total wipe), not the rotation reset", async () => {
    const onSessionChanged = vi.fn();
    const onIdentityChanged = vi.fn();
    const seam = createSessionSeam({
      setAccessToken: vi.fn(),
      refresh: async () => null,
      onSessionChanged,
      onIdentityChanged,
    });

    await seam.signIn({ accessToken: "jwt" });

    expect(onIdentityChanged).toHaveBeenCalledOnce();
    expect(onSessionChanged).not.toHaveBeenCalled();
  });

  it("bootstrap (rotation) fires only the LIGHT reset — never the identity wipe", async () => {
    const onSessionChanged = vi.fn();
    const onIdentityChanged = vi.fn();
    const seam = createSessionSeam({
      setAccessToken: vi.fn(),
      refresh: async () => ({ accessToken: "jwt" }),
      onSessionChanged,
      onIdentityChanged,
    });

    await seam.bootstrapSession();

    expect(onSessionChanged).toHaveBeenCalledOnce();
    expect(onIdentityChanged).not.toHaveBeenCalled();
  });

  it("clearSession (sign-out) fires the IDENTITY reset — the next user starts clean", async () => {
    const onSessionChanged = vi.fn();
    const onIdentityChanged = vi.fn();
    const seam = createSessionSeam({
      setAccessToken: vi.fn(),
      refresh: async () => null,
      onSessionChanged,
      onIdentityChanged,
    });

    await seam.clearSession();

    expect(onIdentityChanged).toHaveBeenCalledOnce();
    expect(onSessionChanged).not.toHaveBeenCalled();
  });

  it("omitting onIdentityChanged falls back to onSessionChanged — retrocompat, no crash", async () => {
    const onSessionChanged = vi.fn();
    const seam = createSessionSeam({ setAccessToken: vi.fn(), refresh: async () => null, onSessionChanged });

    await seam.signIn({ accessToken: "jwt" });

    expect(onSessionChanged).toHaveBeenCalledOnce(); // identity path falls back to the only reset wired
  });

  it("onAuthenticated stays a working alias of signIn (deprecated, identity semantics)", async () => {
    const onIdentityChanged = vi.fn();
    const setAccessToken = vi.fn();
    const seam = createSessionSeam({ setAccessToken, refresh: async () => null, onIdentityChanged });

    await seam.onAuthenticated({ accessToken: "jwt" });

    expect(setAccessToken).toHaveBeenCalledWith("jwt");
    expect(onIdentityChanged).toHaveBeenCalledOnce();
  });

  // A cold start that double-invokes the boot effect (StrictMode), or a bootstrap racing the client's 401
  // interceptor, must not fire TWO refresh rotations — the backend's theft detection burns the family on the
  // replayed token (LZFE029 at boot). bootstrapSession is single-flighted.
  it("concurrent bootstrapSession calls share ONE refresh rotation (no double-rotation at boot)", async () => {
    let release!: () => void;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const refresh = vi.fn(() => gate.then(() => ({ accessToken: "jwt" })));
    const seam = createSessionSeam({ setAccessToken: vi.fn(), refresh });

    const a = seam.bootstrapSession();
    const b = seam.bootstrapSession();
    release();

    expect(await Promise.all([a, b])).toEqual([true, true]);
    expect(refresh).toHaveBeenCalledOnce();
  });
});
