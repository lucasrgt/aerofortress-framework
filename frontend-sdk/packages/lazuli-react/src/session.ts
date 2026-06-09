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
}

/**
 * Project a "current user" (`me`) query into a {@link SessionState}. Three rules, each load-bearing:
 *
 * - **pending ⇒ loading.** The answer is not in yet; a guard must DEFER, never redirect. After a sign-in resets
 *   the me-query (the seam pattern an app owns), it returns to pending here — so the guard waits for the
 *   authenticated fetch instead of reading the stale anonymous snapshot. This is the fix for the
 *   "register → bounced back to login" bug.
 * - **error or absent data ⇒ anonymous.** A 401/404 on `me` is not a failure surface; an unauthenticated visitor
 *   is a normal state. (So the session never routes through a `<Resource>` error slot.)
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
  if (query.isError || query.data === undefined) return { status: "anonymous" };
  return { status: "authenticated", user: query.data };
}
