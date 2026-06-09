// @lazuli/react — the framework's frontend spine (platform- and design-system-agnostic React primitives the app
// composes; the front-side parallel of the .NET packages). The canonical unit a lazuli-net screen is built from:
// a ViewModel (data door) exposes its resource as AsyncState<T>; the View renders it through <Resource>. Auth/nav
// primitives (SessionState, safeBack) graduate here from app pilots as they stabilize.
export { type AsyncState, type QueryLike, toAsyncState, combineAsyncStates } from "./async-state";
export { Resource } from "./Resource";
export { type SessionState, type SessionQueryLike, toSessionState } from "./session";
export {
  type AuthTokens,
  type RefreshTokenStore,
  type SessionSeam,
  type SessionSeamPorts,
  createSessionSeam,
} from "./session-seam";
export { useSession } from "./useSession";
export { type BackRouter, safeBack } from "./nav";
export { type RequiredParam, requiredParam } from "./params";
