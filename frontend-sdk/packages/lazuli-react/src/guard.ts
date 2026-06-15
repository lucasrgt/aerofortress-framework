// The route-guard decision, as PURE data. The read-side twin of the session seam: `toSessionState` projects "who
// is signed in"; this answers "may this route render for them, and if not, where to". It is router-agnostic by the
// same trick as the rest of the spine (cf. BackRouter/QueryLike) — it NEVER navigates, it returns a {@link
// GuardOutcome} the app's thin `<AuthRoute>`/`<GuestRoute>` turns into its router's `<Redirect>`/`<Navigate>` +
// splash. The point of the symmetry: an auth-guard and a guest-guard are the SAME primitive with `allow` flipped,
// so guarding a PUBLIC route (the login/signup screen a signed-in user must be bounced OFF — the pauta bug where a
// logged-in user reaching /login was let through) stops being something each app re-derives by hand.

import type { SessionState } from "./session";

/**
 * What a guard decided, for the app to act on — three cases, mirroring {@link SessionState}'s discipline:
 *
 * - `wait` — the session is still resolving; render a splash, decide nothing (the load-bearing case a boolean guard
 *   collapses, then bounces a not-yet-settled user — see {@link SessionState}).
 * - `render` — the visitor is allowed; render the route.
 * - `redirect` — the visitor is not allowed; send them to `to`.
 *
 * @typeParam Href - the app's route type (a string on every router; generic so typed routes keep their checking).
 */
export type GuardOutcome<Href = string> =
  | { action: "wait" }
  | { action: "render" }
  | { action: "redirect"; to: Href };

/**
 * How a route is guarded. `allow` is the ONLY axis that differs between an auth-guard and a guest-guard, which is
 * the whole point: one primitive, one flag.
 *
 * @typeParam Href - the app's route type.
 */
export interface GuardOptions<Href = string> {
  /** Who may render this route: `"authenticated"` (a private screen) or `"anonymous"` (a public/guest screen such
   * as login or sign-up, which a signed-in user must be redirected away from). */
  allow: "authenticated" | "anonymous";
  /** Where to send a visitor the guard rejects — the sign-in screen for an auth-guard, the app home for a
   * guest-guard. */
  redirectTo: Href;
}

/**
 * Decide a route guard from a {@link SessionState}, as pure data — no navigation, no JSX. Bind it to your router in
 * a ~10-line app component, written ONCE for both directions:
 *
 * ```tsx
 * function Guard({ allow, redirectTo, children }: GuardOptions & { children: ReactNode }) {
 *   const outcome = guardSession(useSessionState(), { allow, redirectTo });
 *   if (outcome.action === "wait") return <Splash />;
 *   if (outcome.action === "redirect") return <Redirect href={outcome.to} />; // <Navigate to> on TanStack
 *   return <>{children}</>;
 * }
 * const AuthRoute = (p) => <Guard allow="authenticated" redirectTo="/login" {...p} />;
 * const GuestRoute = (p) => <Guard allow="anonymous" redirectTo="/home" {...p} />;
 * ```
 *
 * - **`loading` ⇒ `wait`.** The answer is not in yet; defer (splash), never redirect — the bounce-to-login bug.
 * - **allowed ⇒ `render`.**
 * - **rejected ⇒ `redirect` to `redirectTo`.** A signed-in user on a guest route, or an anonymous one on a private
 *   route, lands on the right screen instead of seeing one they should never reach.
 *
 * @typeParam U - the authenticated user the session carries.
 * @typeParam Href - the app's route type.
 * @param session - the current {@link SessionState}.
 * @param options - which visitor is allowed and where a rejected one goes.
 * @returns the {@link GuardOutcome} the app renders.
 */
export function guardSession<U, Href = string>(
  session: SessionState<U>,
  options: GuardOptions<Href>,
): GuardOutcome<Href> {
  if (session.status === "loading") return { action: "wait" };
  const allowed =
    options.allow === "authenticated"
      ? session.status === "authenticated"
      : session.status === "anonymous";
  return allowed ? { action: "render" } : { action: "redirect", to: options.redirectTo };
}
