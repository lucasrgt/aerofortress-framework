// The session's WRITE side — the one seam LZFE016 polices toward, graduated from the hostpoint pilot's
// lib/session. The read side (SessionState/toSessionState) projects "who is signed in"; this owns "the token
// changed": persist it, and reset the session cache IN THE SAME MOVE, so the scattered-write bug (a sign-in
// that forgets to reset the `me` query and bounces the fresh user back to login) is unrepresentable, not
// merely linted. Structural like the rest of the spine: the client call, the token sink, and the storage are
// injected ports — the spine depends on no transport, no router, no storage API.

/** Where the refresh token lives between runs — the platform seam. Web apps omit it entirely (the refresh
 * token is an httpOnly cookie, invisible to JS by design); native apps back it with secure storage. */
export interface RefreshTokenStore {
  /** The persisted refresh token, or empty when there is none. */
  load: () => Promise<string>;
  /** Persist a (rotated) refresh token. */
  save: (token: string) => Promise<void>;
  /** Drop the persisted token on sign-out. */
  clear: () => Promise<void>;
}

/** The capabilities a session seam composes over, injected so the spine stays dependency-free. */
export interface SessionSeamPorts {
  /** Sink for the bearer token (e.g. the generated client's `setAccessToken`); `null` clears it. */
  setAccessToken: (token: string | null) => void;
  /** Re-mint tokens from a refresh token (the generated `refresh` endpoint). The argument is empty on web,
   * where the cookie rides the request instead. */
  refresh: (refreshToken: string) => Promise<AuthTokens | null | undefined>;
  /** Reset whatever caches the session shape lives in (e.g. `queryClient.resetQueries` over `me`). Runs after
   * EVERY token transition — sign-in, restore, and sign-out — so a stale snapshot can never survive one. */
  onSessionChanged?: () => void;
  /** The platform token store; omitted = the web posture (httpOnly cookie, nothing stored in JS). */
  store?: RefreshTokenStore;
}

/** The token pair a login/refresh response carries; either half may be absent (web carries no body refresh). */
export interface AuthTokens {
  accessToken?: string;
  refreshToken?: string;
}

/** The seam's surface — the only three ways the app may move the session. */
export interface SessionSeam {
  /** Persist a session from a login/refresh response: bearer to the sink, rotated refresh to the store,
   * caches reset. Call it with every authenticating response. */
  onAuthenticated: (result: unknown) => Promise<void>;
  /** Re-mint the access token from the persisted refresh (cookie on web, stored body on native). Returns
   * whether a session was restored. Safe on every app start — the API rotates the refresh token and
   * onAuthenticated re-saves it, so the next bootstrap uses the latest one. */
  bootstrapSession: () => Promise<boolean>;
  /** Drop the session locally (the server clears the cookie / revokes on its side) and reset the caches. */
  clearSession: () => Promise<void>;
}

const noStore: RefreshTokenStore = {
  load: () => Promise.resolve(""),
  save: () => Promise.resolve(),
  clear: () => Promise.resolve(),
};

/**
 * Build the app's session seam (its `lib/session`) from the injected ports:
 *
 * ```ts
 * export const session = createSessionSeam({
 *   setAccessToken,
 *   refresh: (token) => refresh({ refreshToken: token }),
 *   onSessionChanged: () => queryClient.resetQueries({ queryKey: getMeQueryKey() }),
 *   // store: secureStore  // native only; web omits it (httpOnly cookie)
 * });
 * ```
 */
export function createSessionSeam(ports: SessionSeamPorts): SessionSeam {
  const store = ports.store ?? noStore;

  const onAuthenticated = async (result: unknown): Promise<void> => {
    const tokens = (result ?? undefined) as AuthTokens | undefined;
    if (tokens?.accessToken) ports.setAccessToken(tokens.accessToken);
    if (tokens?.refreshToken) await store.save(tokens.refreshToken);
    ports.onSessionChanged?.();
  };

  return {
    onAuthenticated,
    async bootstrapSession() {
      try {
        const result = await ports.refresh(await store.load());
        await onAuthenticated(result);
        return true;
      } catch {
        return false;
      }
    },
    async clearSession() {
      ports.setAccessToken(null);
      await store.clear();
      ports.onSessionChanged?.();
    },
  };
}
