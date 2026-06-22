import { useEffect, useState } from "react";

// The numbered-pager state for a searchable server-paginated list (the admin-table case). The hook owns the
// STATE the generated hook is parameterized with — it never wraps the generated hook itself: the ViewModel
// stays the one data door (AFFE002), calls its own `use<Slice>({ page: pager.page, q: pager.debouncedQ ||
// undefined })` and hands the pager through to the View. Wire over the data layer, not a dependency on it —
// the same stance as toAsyncState. Graduated from the hostpoint pilot's PointsList.

export interface PagerOptions {
  /** Debounce for the search term, in ms. Default 300 — the pilot-proven feel. */
  debounceMs?: number;
  /** Start with a term already applied (a deep link carrying `?q=`). */
  initialQ?: string;
}

export interface Pager {
  /** 1-based — the generated hook's `page` param. */
  page: number;
  /** The live input value the search field binds. */
  q: string;
  setQ: (q: string) => void;
  /** The settled (debounced, trimmed) term — the generated hook's `q` param. */
  debouncedQ: string;
  /**
   * Advance one page, never past `pageCount` when given. The ViewModel supplies the clamp from the latest
   * response — `next: () => pager.next(info?.pageCount)` — because the total lives in the response, which
   * the fetch-agnostic pager never sees. With the total still unknown the step is unclamped; the server
   * answers an out-of-range page with an empty one, never an error.
   */
  next: (pageCount?: number) => void;
  /** Back one page, never below 1. */
  prev: () => void;
  /** Back to page 1, the term untouched — for when a filter beside `q` changes. */
  reset: () => void;
}

export function usePager(options?: PagerOptions): Pager {
  const debounceMs = options?.debounceMs ?? 300;
  const [q, setQ] = useState(options?.initialQ ?? "");
  // The page and the settled term move together: a term change IS a page rewind, atomically (one render).
  const [server, setServer] = useState(() => ({ page: 1, q: (options?.initialQ ?? "").trim() }));

  useEffect(() => {
    const handle = setTimeout(() => {
      const settled = q.trim();
      // Only an EFFECTIVE term change rewinds the page — retyping the same term (or touching whitespace)
      // must not kick the user off page 3.
      setServer((s) => (s.q === settled ? s : { page: 1, q: settled }));
    }, debounceMs);
    return () => clearTimeout(handle);
  }, [q, debounceMs]);

  return {
    page: server.page,
    q,
    setQ,
    debouncedQ: server.q,
    next: (pageCount) =>
      setServer((s) => ({
        ...s,
        page: pageCount === undefined ? s.page + 1 : Math.max(1, Math.min(pageCount, s.page + 1)),
      })),
    prev: () => setServer((s) => ({ ...s, page: Math.max(1, s.page - 1) })),
    reset: () => setServer((s) => (s.page === 1 ? s : { ...s, page: 1 })),
  };
}
