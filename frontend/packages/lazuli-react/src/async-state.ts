// AsyncState<T> — the frontend spine's core, the read-side analogue of the backend's Result<T>. A screen's
// ViewModel (its data door) exposes a resource as this discriminated union instead of ad-hoc isPending/isError
// booleans, so the View handles all four states by construction (an exhaustive switch / the <Resource> gate) —
// the same way an exhaustive `Result` match forces the sad path. Pure TS, design-system- and platform-agnostic.

export type AsyncState<T> =
  | { status: "loading" }
  | { status: "error"; message: string; retry?: () => void }
  | { status: "empty" }
  | { status: "ready"; data: T };

// The result shape this adapts — kept structural (not the react-query type) so the spine carries no data-lib
// dependency. It is wire over react-query (or any equivalent), not a dependency on it.
export interface QueryLike<T> {
  isPending: boolean;
  isError: boolean;
  data: T | undefined;
  refetch?: () => unknown;
}

/**
 * Project a query result into an AsyncState. `errorMessage` is the localized string the ViewModel supplies (the
 * spine never hardcodes copy). `isEmpty` opts the resource into a distinct "empty" state (e.g. an empty list);
 * omit it and a present-but-empty value is simply `ready` for the View to render.
 */
export function toAsyncState<T>(
  query: QueryLike<T>,
  opts: { errorMessage: string; isEmpty?: (data: T) => boolean },
): AsyncState<T> {
  if (query.isPending) return { status: "loading" };
  if (query.isError || query.data === undefined) {
    return {
      status: "error",
      message: opts.errorMessage,
      retry: query.refetch ? () => void query.refetch?.() : undefined,
    };
  }
  if (opts.isEmpty?.(query.data)) return { status: "empty" };
  return { status: "ready", data: query.data };
}
