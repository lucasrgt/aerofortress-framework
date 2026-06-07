// @lazuli/react — the framework's frontend spine (platform- and design-system-agnostic React primitives the app
// composes; the front-side parallel of the .NET packages). The canonical unit a lazuli-net screen is built from:
// a ViewModel (data door) exposes its resource as AsyncState<T>; the View renders it through <Resource>. More
// primitives graduate here from app pilots (route guards, session) as they stabilize.
export { type AsyncState, type QueryLike, toAsyncState } from "./async-state";
export { Resource } from "./Resource";
