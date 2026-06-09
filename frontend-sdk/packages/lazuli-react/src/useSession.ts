import { useEffect, useState } from "react";

// The boot gate over the session seam. Runs the one-time bootstrap (re-mint the access token from the persisted
// refresh) and reports when it has settled. Mount at the app root and gate the navigator on `ready`, so no route
// fires an authed request before the session is restored — this is what makes an F5 on any route (web) or a cold
// native start resume the session instead of 401-ing. Graduated from the hostpoint pilot.

/**
 * Settle the session before the navigator renders:
 *
 * ```tsx
 * const { ready } = useSession(session.bootstrapSession);
 * if (!ready) return <Splash />;
 * ```
 *
 * @param bootstrap - the seam's `bootstrapSession` (or any settle-the-session promise).
 */
export function useSession(bootstrap: () => Promise<unknown>): { ready: boolean } {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    // A rejected bootstrap is a settled (anonymous) session, not an unhandled error — absorb it; the seam's
    // own bootstrapSession already maps failure to `false`, this covers any custom promise the app passes.
    void bootstrap()
      .catch(() => undefined)
      .finally(() => {
        if (!cancelled) setReady(true);
      });
    return () => {
      cancelled = true;
    };
    // The bootstrap is a one-time boot gate by contract — re-running it on a new function identity would
    // re-mint mid-session.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return { ready };
}
