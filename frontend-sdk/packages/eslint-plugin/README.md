# eslint-plugin-lazuli

The frontend harness — the front-side parallel of the backend's Roslyn analyzers (`Lazuli.Doctor`). It polices the
MVVM seam of a lazuli-net screen so React Native + web stays **wired, not mocked**. Doctor-removable: delete the
plugin and the app still builds; you only lose enforcement.

## Rules

| Rule | Code | Polices |
|---|---|---|
| `view-purity` | LZFE001 | A `*.view.tsx` renders only — no generated client / axios / react-query import (contract **types** are fine). |
| `data-door` | LZFE002 | The generated client has exactly two doors: a screen's `*.viewModel.ts`, and the auth/routing infra (`lib/session`, `lib/guards`). |
| `viewmodel-platform-agnostic` | LZFE009 | A `*.viewModel.ts` imports no `react-native` / `expo` (value **or** type) — the core stays shareable web↔mobile and testable in jsdom. |
| `test-colocated` | LZFE005 | Every `*.viewModel.ts` has a co-located `*.test.tsx` that `renderHook()`s it (unit tier — proof the data door mounts). |
| `view-integration-test` | LZFE006 | Every `*.view.tsx` has a co-located test that `render()`s the View (integration tier — proof the screen composes + mounts). |
| `no-mock` | LZFE003 | No mock/fixture/MSW import in production code (only under `*.test.*`). |
| `state-completeness` | LZFE010 | A `*.view.tsx` routes loading/error/empty through `<Resource>` — no raw `isPending`/`isError`/… (the booleans are the ViewModel's). |
| `i18n-completeness` | LZFE011 | Every locale catalog in a `*.i18n.ts` declares the same keys (a key in one but not its siblings is a silent untranslated string). |
| `design-tokens` | LZFE012 | No inline hex color in production code — colors come from a token; only the `theme`/`tokens`/`palette` definition files may hold hex. |
| `mutation-error-handled` | LZFE013 | A `*.viewModel.ts` mutation (`.mutate`/`.mutateAsync`) passes an `onError` — no silent failure (front-side of the backend's error_handling). |
| `no-hardcoded-copy` | LZFE014 | A `*.view.tsx` has no hardcoded user-facing text — JSX text children (`>text<`) **and** copy props (`placeholder`/`title`/`label`/…) go through i18n `t()`. High-signal (`{t()}`/non-copy attrs never flagged). |
| `no-router-replace-in-effect` | LZFE015 | A redirect-on-state is a **declarative** `<Redirect>`/`<Navigate>` returned from render, never an imperative `router.replace`/`router.navigate`/`useNavigate()` call inside `useEffect` (a post-paint flash on TanStack; an infinite navigation/refetch loop on expo-router web). |
| `session-one-door` | LZFE016 | The session token is written through **one seam** (`lib/session`); a `*.viewModel`/`*.view` importing the token setter (`setAccessToken`/…) directly is the scattered write that forgets the cache reset — and bounces the just-authenticated user back to login. |
| `guard-tristate` | LZFE017 | A route guard redirects on a **tri-state** `SessionState` (`loading \| authenticated \| anonymous`), never a raw `isAuthenticated` boolean (which reads "still loading" as "signed out"). The read-side twin of LZFE010. |
| `route-param-guard` | LZFE018 | A route reading a **required id param** (expo-router `useLocalSearchParams`) guards its absence — `if (!id) return <Redirect …/>` — so a param-less hit (bookmark / stale link) can't render a ghost screen on an empty id. |
| `safe-back` | LZFE019 | No bare `router.back()`/`history.back()` — on web a deep-linked screen has no in-app history, so it's a dead button. Route Back through a guarded helper (`safeBack`/`useGoBack`) that falls back to a parent. |
| `no-hardcoded-base-url` | LZFE020 | The API base URL comes from **configuration** (env `VITE_API_URL`/`EXPO_PUBLIC_API_URL`, a relative base, or an injected default), never a hardcoded host baked into `axios.create({ baseURL: "http://…" })` — a baked literal silently 404s when the backend runs on a different port. |

### Routing rules — both routers, one shape

LZFE015–019 are the **routing harness**: the front-side parallel of the backend's slice rules, born from the two app pilots (Pauta on TanStack Router, Hostpoint on expo-router) converging on the same navigation bugs — a freshly-registered user bounced to login, ghost screens on missing params, dead Back buttons, effect-driven redirect loops. They police a **shape** (declarative redirect, tri-state session, guarded param, guarded back), so they recognize each router's idiom (`<Redirect>`/`router.replace`/`useLocalSearchParams` ↔ `<Navigate>`/`useNavigate()`/`Route.useParams`) without depending on either runtime — "ship the standard, not the adapter". The tri-state `SessionState` and the `safeBack` helper they steer toward live in the spine (`@lazuli/react`).

## Self-proving

`npm test` runs `index.test.cjs` — RuleTester cases that pin every rule on **both** edges: it must FIRE on the
violation it polices and PASS on the shapes it allows. A rule isn't done until both are pinned (the discipline the
framework applies to its own backend rules). CI runs this beside `dotnet build` and the spine's vitest suite.

## Consuming it in an app

An app registers the plugin in its flat ESLint config and turns the rules on for its feature tree — see the
`frontend-sdk/eslint.config.mjs` in this repo, which lints the canonical `sample/`. Until this package is published to
npm, a consuming app (e.g. the Hostpoint dogfood) installs it from this repo; once published it is a normal devDep.
