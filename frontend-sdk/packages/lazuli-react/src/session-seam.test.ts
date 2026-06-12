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

  it("onSignedIn persists the new identity's tokens AND fires the full-cache wipe", async () => {
    const setAccessToken = vi.fn();
    const onSessionChanged = vi.fn();
    const onIdentityChanged = vi.fn();
    const seam = createSessionSeam({ setAccessToken, onSessionChanged, onIdentityChanged, refresh: async () => null });

    await seam.onSignedIn({ accessToken: "jwt-b" });

    expect(setAccessToken).toHaveBeenCalledWith("jwt-b");
    expect(onIdentityChanged).toHaveBeenCalledOnce();
  });

  it("a rotation never fires the identity wipe — a 15-min re-mint must not nuke warm screens", async () => {
    const onIdentityChanged = vi.fn();
    const seam = createSessionSeam({
      setAccessToken: vi.fn(),
      onIdentityChanged,
      refresh: async () => ({ accessToken: "jwt" }),
    });

    await seam.bootstrapSession();
    await seam.onAuthenticated({ accessToken: "jwt-2" });

    expect(onIdentityChanged).not.toHaveBeenCalled();
  });

  it("clearSession fires the identity wipe — a sign-out leaves nothing for the next account to inherit", async () => {
    const onIdentityChanged = vi.fn();
    const seam = createSessionSeam({ setAccessToken: vi.fn(), onIdentityChanged, refresh: async () => null });

    await seam.clearSession();

    expect(onIdentityChanged).toHaveBeenCalledOnce();
  });
});
