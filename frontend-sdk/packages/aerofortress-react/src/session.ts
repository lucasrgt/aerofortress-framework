// SessionState<U> — the spine's auth read-side: the same discriminated-union discipline as AsyncState<T>, but for
// "who is signed in". A route guard branches on this, never on a hand-rolled `isAuthenticated` boolean, so the
// load-bearing distinction — "still resolving" is NOT "anonymous" — is structural rather than something each guard
// must remember. Collapsing `loading` into `anonymous` is THE canonical guard bug (a freshly-signed-in user bounced
// back to the login screen because the guard read the session before it settled); this union makes it unspellable.

/**
 * The three terminal facts a route guard needs, as one closed union. `loading` means defer the decision; the other
 * two are the answer. Mirrors {@link AsyncState} for reads, but a session has no `empty`/`error` surface — an
 * unauthenticated visitor is `anonymous`, a normal state, never a failure.
 *
 * @typeParam U - the authenticated user (the `me` payload).
 */
export type SessionState<U> =
  | { status: "loading" }
  | { status: "authenticated"; user: U }
  | { status: "anonymous" };

/**
 * The current-user ("me") query this projects from — kept structural (not the react-query type) so the spine
 * carries no data-lib dependency, exactly like {@link QueryLike}. Present `data` ⇒ signed in; an error or absent
 * data ⇒ anonymous.
 *
 * @typeParam U - the authenticated user the query resolves to.
 */
export interface SessionQueryLike<U> {
  /** No result has arrived yet (the query has never settled, or was just reset). */
  isPending: boolean;
  /** The query settled in failure — for `me`, that is a 401/404, i.e. "not signed in", not an error to surface. */
  isError: boolean;
  /** The resolved user, or `undefined` until/unless the query succeeds. */
  data: U | undefined;
  /** OPTIONAL classifier for `isError`: whether the failure means *not signed in* (a 401/403) as opposed to a
   * TRANSIENT failure (a 5xx, a timeout, an offline flap). Wire it from the failed request
   * (`isUnauthorized: error?.response?.status === 401`). Omitted ⇒ legacy behaviour (every error ⇒ `anonymous`),
   * which logs an authenticated user out on a transient `me` failure during F5 / a network flap — see
   * {@link toSessionState}. */
  isUnauthorized?: boolean;
}

/**
 * Project a "current user" (`me`) query into a {@link SessionState}. Three rules, each load-bearing:
 *
 * - **pending ⇒ loading.** The answer is not in yet; a guard must DEFER, never redirect. After a sign-in resets
 *   the me-query (the seam pattern an app owns), it returns to pending here — so the guard waits for the
 *   authenticated fetch instead of reading the stale anonymous snapshot. This is the fix for the
 *   "register → bounced back to login" bug.
 * - **a TRANSIENT error (`isError` with `isUnauthorized === false`) ⇒ loading.** A 5xx / timeout / offline flap on
 *   `me` is NOT a sign-out — folding it into `anonymous` logs an authenticated user out on an F5 over a shaky
 *   network (they bounce to login, and re-login fails too). So a classified non-auth error DEFERS: stay on the
 *   splash, let react-query retry, recover to `authenticated` when it does. (Pair with a `useSession` timeout so a
 *   permanently-down API can't pin the splash forever.) This branch is opt-in — it only fires when the app wires
 *   `isUnauthorized`; without it the legacy "any error ⇒ anonymous" holds.
 * - **an AUTH error or absent data ⇒ anonymous.** A 401/403 on `me` is not a failure surface; an unauthenticated
 *   visitor is a normal state. (So the session never routes through a `<Resource>` error slot.)
 * - **data ⇒ authenticated**, carrying the user.
 *
 * `isFetching` is deliberately NOT folded into `loading`: a background revalidation of an already-signed-in session
 * must stay `authenticated` (no blank flash). The post-sign-in wait is guaranteed by RESETTING the query (→
 * pending), not by watching fetches.
 *
 * @typeParam U - the authenticated user the query resolves to.
 * @param query - the structural `me` query result.
 * @returns the closed session state a guard branches on.
 */
export function toSessionState<U>(query: SessionQueryLike<U>): SessionState<U> {
  if (query.isPending) return { status: "loading" };
  // A transient failure is not a sign-out — defer instead of bouncing an authenticated user (opt-in via the
  // classifier; absent ⇒ legacy "any error ⇒ anonymous").
  if (query.isError) return query.isUnauthorized === false ? { status: "loading" } : { status: "anonymous" };
  if (query.data === undefined) return { status: "anonymous" };
  return { status: "authenticated", user: query.data };
}
