"use strict";

const fs = require("fs");
const path = require("path");

// eslint-plugin-lazuli — the frontend harness (LZFE*). The front-side parallel of the backend's Roslyn analyzers
// (Lazuli.Doctor): it polices the MVVM seam so React Native + web screens stay wired, not mocked — the View
// renders, the ViewModel is the only data door, and no fixture/mock leaks into production. Doctor-removable: delete
// the plugin and the app still builds; you only lose enforcement. Canonical home: lazuli-net/frontend.

const GENERATED_CLIENT = /(^|\/)client\.gen(\/|$)/;     // the orval-generated typed client
const DATA_LIBS = /^(axios)$|^@tanstack\/react-query$/;  // raw transport / the Model layer
const MOCKS = /(^|\/)(__mocks__|fixtures)(\/|$)|^msw($|\/)/;

const isView = (f) => /\.view\.tsx?$/.test(f);
const isViewModel = (f) => /\.viewModel\.ts$/.test(f);
const isTest = (f) => /\.(test|spec)\.[jt]sx?$/.test(f);
const isGenerated = (f) => GENERATED_CLIENT.test(f.replace(/\\/g, "/"));
// The data doors are two: a screen's *.viewModel.ts (per-screen), AND the framework's auth/routing
// infrastructure — session bootstrap + route guards. Both legitimately read the generated client (refresh, me,
// lifecycle); screens still may NOT bypass their ViewModel. Scoped to lib/session + lib/guards so it stays a
// principled allowance, not a general escape hatch. (This is the cross-cutting infra Angular puts behind
// CanActivate / an AuthService — framework primitives the app composes, not a DSL.)
const isInfraDataDoor = (f) => /(^|\/)lib\/(session|guards)(\.|\/)/.test(f.replace(/\\/g, "/"));

// A type-only import (`import type { X }`) is erased at runtime — it is the shared contract vocabulary, not
// data access. A View taking `kind: LegalDocKind` is wired correctly; only a *value* import (a hook, the
// client) is a data path. So the data-door rules exempt type imports; the contract types are everyone's.
const isTypeOnly = (node) =>
  node.importKind === "type" ||
  (node.specifiers.length > 0 && node.specifiers.every((s) => s.importKind === "type"));

/** Report on any *value* import whose source matches `pattern`. */
function forbidImport(context, pattern, messageId) {
  return {
    ImportDeclaration(node) {
      if (!isTypeOnly(node) && pattern.test(node.source.value)) {
        context.report({ node, messageId });
      }
    },
  };
}

// ── Routing vocabulary (recognized for BOTH expo-router and TanStack Router) ────────────────────────────────────
// The routing rules police a SHAPE (declarative redirect, guarded back, param presence), not a router runtime —
// so they recognize each router's idiom but depend on neither. "Ship the standard, not the adapter."

// A route file — the navigation layer (expo-router `app/`, TanStack `app/routes/`). The only layer that may
// redirect or read route params; both routers live under an `app/` tree, so one test covers them.
const isRoute = (f) => /(^|\/)app\//.test(f.replace(/\\/g, "/"));
// The nav seam — the single guarded back handler (safeBack / useGoBack). The one place a bare back() is allowed.
const isNavSeam = (f) => /(^|\/)lib\/(nav|useGoBack)(\.|\/)/.test(f.replace(/\\/g, "/"));
// The "am I signed in?" boolean a guard must NOT branch a redirect on: it collapses the tri-state (loading vs
// anonymous) into one bit, so the redirect fires before the session settles. Branch on a SessionState instead.
const AUTH_BOOL = /^(is)?(authenticated|authed|loggedin|signedin)$/i;
// A route param whose ABSENCE yields a ghost screen — an id the View needs. Optional filter/search params don't
// qualify; only id-shaped names render an empty detail when missing.
const ID_PARAM = /(^id$)|Id$/;

/** Whether `node` sits lexically inside a useEffect / useLayoutEffect callback (where a redirect re-fires every render). */
function inEffect(node) {
  for (let p = node.parent; p; p = p.parent) {
    if (
      p.type === "CallExpression" &&
      p.callee.type === "Identifier" &&
      (p.callee.name === "useEffect" || p.callee.name === "useLayoutEffect")
    )
      return true;
  }
  return false;
}

/** The nearest enclosing function of `node` (the component body), or null. */
function enclosingFunction(node) {
  for (let p = node.parent; p; p = p.parent) {
    if (
      p.type === "FunctionDeclaration" ||
      p.type === "FunctionExpression" ||
      p.type === "ArrowFunctionExpression"
    )
      return p;
  }
  return null;
}

/** The leaf name of `x` / `x.y` when it reads as an auth boolean (AUTH_BOOL), else null. */
function authBoolName(expr) {
  if (expr.type === "Identifier" && AUTH_BOOL.test(expr.name)) return expr.name;
  if (
    expr.type === "MemberExpression" &&
    expr.property.type === "Identifier" &&
    AUTH_BOOL.test(expr.property.name)
  )
    return expr.property.name;
  return null;
}

/** Whether a JSX element is a declarative redirect — `<Redirect>` (expo-router) or `<Navigate>` (TanStack). */
function isRedirectElement(arg) {
  if (!arg || arg.type !== "JSXElement") return false;
  const name = arg.openingElement.name;
  return name.type === "JSXIdentifier" && (name.name === "Redirect" || name.name === "Navigate");
}

/** Whether a statement (or block) returns a declarative redirect element. */
function returnsRedirect(stmt) {
  const body = stmt.type === "BlockStatement" ? stmt.body : [stmt];
  return body.some((s) => s.type === "ReturnStatement" && isRedirectElement(s.argument));
}

/** Walk every node under `root` (skipping `parent` back-edges), calling `fn`; `fn` returning true stops the walk. */
function walk(root, fn) {
  let stop = false;
  const visit = (node) => {
    if (stop || !node || typeof node.type !== "string") return;
    if (fn(node) === true) {
      stop = true;
      return;
    }
    for (const key of Object.keys(node)) {
      if (key === "parent") continue;
      const v = node[key];
      if (Array.isArray(v)) v.forEach(visit);
      else if (v && typeof v.type === "string") visit(v);
    }
  };
  visit(root);
}

/** Whether the identifier `name` appears anywhere in `node`'s subtree (a value reference). */
function identifierAppears(node, name) {
  let found = false;
  walk(node, (n) => (n.type === "Identifier" && n.name === name ? (found = true) : false));
  return found;
}

/**
 * The names that stand in for a param in a presence guard: the param itself plus any local initialized from it —
 * a coalesce (`const x = a ?? param`) or a rename (`const x = param`). Guarding any of them guards the param, so a
 * `const id = a ?? b; if (!id) return <Redirect/>` is recognized, not falsely flagged.
 */
function aliasesOf(fn, base) {
  const names = new Set([base]);
  walk(fn.body, (n) => {
    if (
      n.type === "VariableDeclarator" &&
      n.id.type === "Identifier" &&
      n.init &&
      !names.has(n.id.name) &&
      [...names].some((nm) => identifierAppears(n.init, nm))
    )
      names.add(n.id.name);
    return false;
  });
  return names;
}

/**
 * Whether an `if` test guarantees a redirect when some name in `names` is absent: a bare `!X`, or a `||`-chain
 * with `!X` as a disjunct (`!a || !b` redirects when either is missing). `&&` is rejected — `!X && y` does NOT
 * redirect on `X` alone, so it is not a sound presence guard.
 */
function testGuardsAny(test, names) {
  if (test.type === "UnaryExpression" && test.operator === "!" && test.argument.type === "Identifier")
    return names.has(test.argument.name);
  if (test.type === "LogicalExpression" && test.operator === "||")
    return testGuardsAny(test.left, names) || testGuardsAny(test.right, names);
  return false;
}

/** Whether `fn`'s body contains `if (<guards X>) return <Redirect/Navigate …/>` for any name X in `names`. */
function hasPresenceGuard(fn, names) {
  let found = false;
  walk(fn.body, (node) => {
    if (node.type === "IfStatement" && testGuardsAny(node.test, names) && returnsRedirect(node.consequent))
      return (found = true);
    return false;
  });
  return found;
}

const rules = {
  // LZFE001 — a View renders only; it owns no data access. Server data comes from its ViewModel.
  "view-purity": {
    meta: {
      type: "problem",
      docs: { description: "A View (*.view.tsx) imports no data layer; it consumes its ViewModel." },
      messages: {
        impure:
          "LZFE001: a View renders only — get server data from its ViewModel (*.viewModel.ts), not the client/axios/react-query.",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isView(f)) return {};
      return {
        ImportDeclaration(node) {
          if (isTypeOnly(node)) return; // contract types are fine in a View; only data access is not
          const src = node.source.value;
          if (GENERATED_CLIENT.test(src) || DATA_LIBS.test(src)) {
            context.report({ node, messageId: "impure" });
          }
        },
      };
    },
  },

  // LZFE002 — the ViewModel is the only data door: only *.viewModel.ts may import the generated client.
  "data-door": {
    meta: {
      type: "problem",
      docs: { description: "Only a *.viewModel.ts may import the generated client." },
      messages: {
        offdoor:
          "LZFE002: the generated client is the ViewModel's alone — import it only from a *.viewModel.ts (one data door).",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (isViewModel(f) || isGenerated(f) || isInfraDataDoor(f)) return {};
      return forbidImport(context, GENERATED_CLIENT, "offdoor");
    },
  },

  // LZFE009 — the ViewModel is platform-agnostic: no react-native / expo import. Platform capabilities
  // (storage, navigation, push) are injected ports, not direct imports — so the ViewModel + the rest of the
  // core (client, types) stay shareable web<->mobile and testable in Vitest (jsdom), with the View the only
  // platform-specific layer.
  "viewmodel-platform-agnostic": {
    meta: {
      type: "problem",
      docs: { description: "A *.viewModel.ts imports no react-native / expo (platform-agnostic core)." },
      messages: {
        platform:
          "LZFE009: a ViewModel is platform-agnostic — no react-native/expo import. Inject platform capabilities as ports so the core stays shareable web↔mobile.",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isViewModel(f)) return {};
      const platform = /^react-native($|\/|-)|^@react-native|^expo($|[-/])/;
      // Flag value AND type imports — an RN type leaks the platform into the agnostic core (a web client has
      // no react-native types), defeating the web↔mobile sharing the rule protects.
      return {
        ImportDeclaration(node) {
          if (platform.test(node.source.value)) context.report({ node, messageId: "platform" });
        },
      };
    },
  },

  // LZFE005 — every ViewModel has a co-located test that *exercises* it. The triple's third leg: a screen is
  // not done when it renders + has a data door but no proof the wiring mounts. The test need not assert
  // behavior — mounting useXModel() inside QueryClientProvider already compiles the ViewModel against the real
  // generated client and proves the hook is callable without crashing. That is the "wired, not mocked"
  // guarantee, the front-side parallel of the backend's test_discipline. Behavior assertions stay
  // per-screen judgment, never doctor-enforced (that way lies test-theater).
  "test-colocated": {
    meta: {
      type: "problem",
      docs: { description: "Every *.viewModel.ts has a co-located *.test.tsx that renderHook()s it." },
      messages: {
        missing:
          "LZFE005: a ViewModel needs a co-located test — create {{test}} that renderHook()s {{model}} (prove the wiring mounts; wired, not just typed).",
        inert:
          "LZFE005: {{test}} exists but doesn't exercise the ViewModel — it must import ./{{base}}.viewModel and call renderHook() (mount the data door against the real client).",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isViewModel(f)) return {};
      return {
        Program(node) {
          const base = path.basename(context.filename).replace(/\.viewModel\.ts$/, "");
          const testPath = path.join(path.dirname(context.filename), `${base}.test.tsx`);
          if (!fs.existsSync(testPath)) {
            context.report({ node, messageId: "missing", data: { test: `${base}.test.tsx`, model: `${base}.viewModel` } });
            return;
          }
          const src = fs.readFileSync(testPath, "utf8");
          const importsViewModel = new RegExp(`["']\\./${base}\\.viewModel["']`).test(src);
          const usesRenderHook = /\brenderHook\b/.test(src);
          if (!importsViewModel || !usesRenderHook) {
            context.report({ node, messageId: "inert", data: { test: `${base}.test.tsx`, base } });
          }
        },
      };
    },
  },

  // LZFE006 — the integration tier: every SCREEN (a *.view.tsx that consumes a ViewModel) has a co-located test
  // that RENDERS the View (not just renderHook on the ViewModel). renderHook (LZFE005) proves the data door mounts;
  // render(<XView/>) proves the View composes with its ViewModel + children + design system and mounts without
  // crashing — the front-side of the backend's integration tests. Same anti-test-theater line as LZFE005: presence
  // + that it renders the View is enforced, the assertions stay per-screen judgment. Start as "warn" in an app with
  // a test backlog; promote to "error" once every screen has its render test.
  //
  // SCOPE — the screen unit, detected by the data-door HOOK it imports, not by a sibling file. With the monorepo
  // split the ViewModel lives in a `core` package while the View lives in the platform shell — no longer siblings.
  // A *.view.tsx is a SCREEN when it imports a `use<Name>Model` hook (co-located OR cross-package, e.g. from
  // `@scope/app-core`) — that hook IS the data door. A View that imports no such hook is a presentational FRAGMENT
  // (a wizard step / settings panel that takes its data + form `control` as PROPS from a parent shell); it is
  // covered transitively when its shell renders, so it is skipped. Note: importing only a TYPE from a *.viewModel
  // module (e.g. a shared `PanelProps`) does NOT gate a View — that is props-in, not a data door. The trigger reads
  // off the View's own import statements, surfaced in the lint message and inspectable in source.
  "view-integration-test": {
    meta: {
      type: "problem",
      docs: { description: "Every screen (a *.view.tsx that imports a ViewModel) has a co-located *.test.tsx that render()s the View." },
      messages: {
        missing:
          "LZFE006: a screen View needs a co-located integration test — create {{test}} that render()s <{{component}}> (prove the screen mounts + composes, not only the ViewModel).",
        inert:
          "LZFE006: {{test}} exists but doesn't render the View — it must import ./{{base}}.view and call render() (mount the View, not only renderHook the ViewModel).",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isView(f)) return {};
      const base = path.basename(context.filename).replace(/\.view\.tsx$/, "");
      // A View is a SCREEN if it consumes a ViewModel — detected off its import statements so it works whether the
      // ViewModel is co-located (`./X.viewModel`) or in a `core` package (a `use<Name>Model` hook). A View with no
      // such import is a presentational fragment (covered via its shell) — never gated here.
      let consumesViewModel = false;
      return {
        ImportDeclaration(node) {
          // Key on the data-door HOOK specifier (`use<Name>Model`), not the module path. A View that imports a
          // use<Name>Model hook consumes a data door => it is a screen. A View that imports only a TYPE from a
          // *.viewModel module (e.g. a shared `PanelProps`) is props-in (its data arrives from a parent shell) =>
          // a presentational fragment, NOT gated. Keying on the path would wrongly gate those type-only importers.
          for (const s of node.specifiers || []) {
            const name = s.imported?.name ?? s.local?.name ?? "";
            if (/^use[A-Z]\w*Model$/.test(name)) consumesViewModel = true;
          }
        },
        "Program:exit"(node) {
          if (!consumesViewModel) return;
          const testPath = path.join(path.dirname(context.filename), `${base}.test.tsx`);
          if (!fs.existsSync(testPath)) {
            context.report({ node, messageId: "missing", data: { test: `${base}.test.tsx`, component: `${base}View` } });
            return;
          }
          const src = fs.readFileSync(testPath, "utf8");
          const importsView = new RegExp(`["']\\./${base}\\.view["']`).test(src);
          // `render(` is the RTL integration call; `\\brender\\s*\\(` does NOT match `renderHook(` (the unit call).
          const usesRender = /\brender\s*\(/.test(src);
          if (!importsView || !usesRender) {
            context.report({ node, messageId: "inert", data: { test: `${base}.test.tsx`, base } });
          }
        },
      };
    },
  },

  // LZFE003 — no mock/fixture/MSW import in production code (only under *.test.*).
  "no-mock": {
    meta: {
      type: "problem",
      docs: { description: "No mock/fixture/MSW import outside tests." },
      messages: {
        mock: "LZFE003: no mock/fixture/MSW in production code — mocks live only under *.test.* (wired, not mocked).",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (isTest(f)) return {};
      return forbidImport(context, MOCKS, "mock");
    },
  },

  // LZFE010 — a View routes loading/error/empty through the spine (<Resource> over an AsyncState), never raw
  // react-query booleans. The moment a View hand-reads isPending/isError it has taken on state handling the spine
  // exists to make exhaustive — and the forgotten branch (no empty state, no error UI) is exactly what slips
  // through. So the booleans are the ViewModel's (it projects them via toAsyncState); the View consumes the union.
  "state-completeness": {
    meta: {
      type: "problem",
      docs: { description: "A View handles async state through <Resource>, not raw isPending/isError." },
      messages: {
        raw:
          "LZFE010: a View routes loading/error/empty through <Resource> (the spine), not raw `{{name}}` — expose the resource as AsyncState in the ViewModel and render it via <Resource>, so every state is handled by construction.",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isView(f)) return {};
      const RAW = /^(isPending|isLoading|isError|isFetching|isRefetching|isSuccess)$/;
      return {
        // `query.isPending`
        MemberExpression(node) {
          if (!node.computed && node.property.type === "Identifier" && RAW.test(node.property.name)) {
            context.report({ node: node.property, messageId: "raw", data: { name: node.property.name } });
          }
        },
        // `const { isError } = useFooModel()`
        Property(node) {
          if (
            node.parent.type === "ObjectPattern" &&
            !node.computed &&
            node.key.type === "Identifier" &&
            RAW.test(node.key.name)
          ) {
            context.report({ node: node.key, messageId: "raw", data: { name: node.key.name } });
          }
        },
      };
    },
  },

  // LZFE011 — every locale in a *.i18n.ts declares the same keys. A feature's copy lives as sibling catalogs
  // (ptBR / esES / enUS …); a key added to one but not the others is a silent untranslated string at runtime. The
  // rule compares the top-level key sets across the file's exported object literals and flags any catalog missing a
  // key its siblings have. (Catalog assembly + the "no hardcoded string" half are the generator's / a later rule's
  // job; this pins parity, the failure that actually ships.)
  "i18n-completeness": {
    meta: {
      type: "problem",
      docs: { description: "Every locale catalog in a *.i18n.ts declares the same keys." },
      messages: {
        missing:
          "LZFE011: i18n catalog `{{catalog}}` is missing key(s) {{keys}} that sibling locales declare — every locale must carry the same keys.",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!/\.i18n\.ts$/.test(f)) return {};
      const keysOf = (objExpr) => {
        const keys = new Set();
        for (const p of objExpr.properties) {
          if (p.type !== "Property" || p.computed) continue;
          const k = p.key.type === "Identifier" ? p.key.name : p.key.type === "Literal" ? String(p.key.value) : null;
          if (k !== null) keys.add(k);
        }
        return keys;
      };
      return {
        "Program:exit"(program) {
          const catalogs = [];
          for (const stmt of program.body) {
            const decl =
              stmt.type === "ExportNamedDeclaration" && stmt.declaration && stmt.declaration.type === "VariableDeclaration"
                ? stmt.declaration
                : stmt.type === "VariableDeclaration"
                  ? stmt
                  : null;
            if (!decl) continue;
            for (const d of decl.declarations) {
              let init = d.init;
              while (init && init.type === "TSAsExpression") init = init.expression; // unwrap `as const`
              if (init && init.type === "ObjectExpression" && d.id.type === "Identifier") {
                catalogs.push({ name: d.id.name, keys: keysOf(init), node: d });
              }
            }
          }
          if (catalogs.length < 2) return; // need >= 2 locales to compare
          const union = new Set();
          for (const c of catalogs) for (const k of c.keys) union.add(k);
          for (const c of catalogs) {
            const missing = [...union].filter((k) => !c.keys.has(k));
            if (missing.length) {
              context.report({
                node: c.node,
                messageId: "missing",
                data: { catalog: c.name, keys: missing.map((k) => `"${k}"`).join(", ") },
              });
            }
          }
        },
      };
    },
  },

  // LZFE012 — no inline hex color in production code. Color is a design-system decision; a literal `#3b82f6` in a
  // screen forks the palette and defeats theming (dark mode, white-label). Colors come from a token (the theme),
  // so the only place a hex literal belongs is where the tokens are DEFINED — those files (theme/tokens/palette)
  // are exempt; everywhere else a hex is the smell the rule catches.
  "design-tokens": {
    meta: {
      type: "problem",
      docs: { description: "No inline hex color outside the token/theme definition files." },
      messages: {
        hex: "LZFE012: no inline hex color (`{{hex}}`) — use a design token from the theme, not a literal.",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (isTest(f) || !/\.(ts|tsx)$/.test(f)) return {};
      if (/(^|\/|\.)(theme|tokens|palette|colors)(\/|\.|$)/i.test(f)) return {}; // token definitions live somewhere
      const HEX = /^#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;
      return {
        Literal(node) {
          if (typeof node.value === "string" && HEX.test(node.value.trim())) {
            context.report({ node, messageId: "hex", data: { hex: node.value } });
          }
        },
      };
    },
  },

  // LZFE013 — every mutation surfaces its error. A react-query `.mutate(...)` / `.mutateAsync(...)` whose options
  // object has no `onError` is a SILENT failure: the command fails and the user sees nothing. The front-side of the
  // backend's error_handling discipline — there, a Result's sad path is forced; here, a mutation must route its
  // error somewhere (a toast, a saveError state, a banner). Scoped to *.viewModel.ts (the data door owns commands).
  // It enforces PRESENCE of onError, not what it does (anti-test-theater): wiring the error out is the bar, the
  // UX of it stays per-screen judgment.
  "mutation-error-handled": {
    meta: {
      type: "problem",
      docs: {
        description:
          "Every mutation surfaces its failure — via an onError handler, a read .isError state, or a try/catch around mutateAsync (no silent failure).",
      },
      messages: {
        unhandled:
          "LZFE013: a mutation must surface its error (no silent failure; the front-side of the backend's error_handling). Use ANY ONE: pass `onError` to .{{method}}(args, { onError }); OR read `{{name}}.isError` and expose it as state the View renders; OR `await {{name}}.mutateAsync()` inside a try/catch that sets an error surface.",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isViewModel(f)) return {};
      // The whole-file text lets us see the `.isError` surface pattern: react-query's idiom is to read the mutation
      // handle's `isError`/`error` and expose it as returned state (the View renders it via <ErrorBanner> / toast),
      // which is a real error surface — just not an inline onError. Recognizing it stops the rule demanding a
      // redundant second handler (which would be the very test-theater the rule exists to prevent).
      const src = (context.sourceCode ?? context.getSourceCode()).getText();
      const hasKey = (obj, name) =>
        obj &&
        obj.type === "ObjectExpression" &&
        obj.properties.some(
          (p) =>
            (p.type === "Property" || p.type === "SpreadElement") &&
            (p.type === "SpreadElement" || // a spread may carry onError — don't false-positive
              (!p.computed &&
                ((p.key.type === "Identifier" && p.key.name === name) ||
                  (p.key.type === "Literal" && p.key.value === name)))),
        );
      // mutateAsync REJECTS on failure, so a try/catch around it IS the handler. .mutate() is fire-and-forget (it
      // never throws), so a try/catch around .mutate() catches nothing — only mutateAsync earns this pass. The try
      // must be in the same function as the call (a try in an outer function across a callback boundary doesn't wrap
      // the await), so we stop the walk at the first enclosing function.
      const inTryBlock = (node) => {
        let prev = node;
        for (let n = node.parent; n; prev = n, n = n.parent) {
          if (n.type === "TryStatement" && n.block === prev) return true;
          if (
            n.type === "FunctionDeclaration" ||
            n.type === "FunctionExpression" ||
            n.type === "ArrowFunctionExpression"
          )
            return false;
        }
        return false;
      };
      // mutateAsync(...).catch(...) is the promise-equivalent of the try/catch above — the rejection path is
      // acknowledged. The rule trusts the STRUCTURE (you handled the rejection); whether the handler body is
      // meaningful is the same human judgment as a try/catch body, not something a lint rule should grade.
      const hasCatch = (node) =>
        node.parent &&
        node.parent.type === "MemberExpression" &&
        node.parent.object === node &&
        node.parent.property.type === "Identifier" &&
        node.parent.property.name === "catch" &&
        node.parent.parent &&
        node.parent.parent.type === "CallExpression";
      // A thin wrapper that RETURNS the mutateAsync promise (`(args) => mut.mutateAsync(...)` or `return
      // mut.mutateAsync(...)`) delegates error handling to whoever awaits it — propagation, not a silent swallow.
      // Unwrap `as`-casts / parens that wrap the returned expression.
      const isReturned = (node) => {
        let n = node;
        while (
          n.parent &&
          (n.parent.type === "TSAsExpression" ||
            n.parent.type === "TSNonNullExpression" ||
            n.parent.type === "ParenthesizedExpression")
        )
          n = n.parent;
        const p = n.parent;
        if (!p) return false;
        if (p.type === "ReturnStatement") return true;
        if (p.type === "ArrowFunctionExpression" && p.body === n) return true;
        return false;
      };
      return {
        CallExpression(node) {
          const callee = node.callee;
          if (callee.type !== "MemberExpression" || callee.computed) return;
          if (callee.property.type !== "Identifier") return;
          const method = callee.property.name;
          if (method !== "mutate" && method !== "mutateAsync") return;
          // A) inline onError in the options arg.
          if (hasKey(node.arguments[1], "onError")) return;
          // B) the mutation handle's error state is read in this file (surfaced as state the View renders).
          const obj = callee.object;
          const name = obj.type === "Identifier" ? obj.name : null;
          if (name && new RegExp(`\\b${name}\\.(isError|error|failureReason)\\b`).test(src)) return;
          // C) mutateAsync awaited inside a try/catch, chained with .catch(), or returned (propagated to the caller).
          if (method === "mutateAsync" && (inTryBlock(node) || hasCatch(node) || isReturned(node))) return;
          context.report({ node, messageId: "unhandled", data: { method, name: name ?? "the mutation" } });
        },
      };
    },
  },

  // LZFE014 — no hardcoded user-facing copy in a View. Targets JSX text children (the `>text<` between tags) that
  // contain a letter — almost always visible copy that must go through i18n (t()), so it lands in the catalog
  // LZFE011 then keeps complete across locales. Deliberately scoped to JSXText (high signal, near-zero false
  // positives): `{t("…")}` is an expression (not text) so it's never flagged, and className / testID / name /
  // variant are attributes (not children) so they're never flagged either. The trade-off is COVERAGE not noise —
  // copy hidden in props (placeholder=…) or variables is NOT caught here (a Phase-2 copy-prop whitelist can add
  // it). Warn-first: it surfaces hardcoded strings without crying wolf.
  "no-hardcoded-copy": {
    meta: {
      type: "problem",
      docs: { description: "A View has no hardcoded user-facing text — JSX text goes through i18n (t())." },
      messages: {
        hardcoded:
          'LZFE014: user-facing text must go through i18n — wrap "{{text}}" in t() (no hardcoded copy in a View).',
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isView(f)) return {};
      // Phase 2: props that carry user-facing copy. Only STRING-LITERAL values are flagged — `{t()}` and variables
      // are JSXExpressionContainers, not literals, so they're never touched. `value`/`name`/`testID`/`variant`/
      // `accessibilityRole` are deliberately NOT here (they're data/ids/enums, not copy).
      const COPY_PROPS = new Set([
        "placeholder", "label", "title", "subtitle", "heading", "description", "message",
        "helperText", "caption", "errorMessage", "emptyTitle", "emptyDescription",
        "accessibilityLabel", "accessibilityHint",
      ]);
      const flag = (node, raw) => {
        const text = raw.trim();
        if (!text || !/[a-zA-Z]/.test(text)) return; // whitespace / numbers / punctuation only
        context.report({
          node,
          messageId: "hardcoded",
          data: { text: text.length > 40 ? `${text.slice(0, 40)}…` : text },
        });
      };
      return {
        JSXText(node) {
          flag(node, node.value);
        },
        JSXAttribute(node) {
          if (node.name.type !== "JSXIdentifier" || !COPY_PROPS.has(node.name.name)) return;
          const v = node.value;
          if (!v || v.type !== "Literal" || typeof v.value !== "string") return; // only literal copy
          flag(v, v.value);
        },
      };
    },
  },

  // LZFE015 — no imperative redirect inside useEffect. A redirect-on-state belongs in a DECLARATIVE element returned
  // from render (<Redirect> on expo-router, <Navigate> on TanStack), never an effect: an effect runs AFTER paint and
  // re-fires on every re-render. On expo-router web it is catastrophic — the router FREEZES the source screen instead
  // of unmounting it, so the effect loops (replace -> remount target -> refetch a guard's my-X 404 -> re-render ->
  // replace …) into an infinite navigation/refetch loop that crashes the screen (shipped twice in the pilot: Splash,
  // then ChooseRole + 5 screens). On TanStack it is "merely" a post-paint flash + redundant nav. Either way the fix
  // is the same: `if (<terminal state>) return <Redirect/Navigate … />`. Recognizes both idioms — `router.replace` /
  // `router.navigate` (a router instance) and `navigate(...)` bound from TanStack's `useNavigate()`. Imperative
  // push/back on a USER action stay allowed (completions, not render loops). Scoped to the navigating layer.
  "no-router-replace-in-effect": {
    meta: {
      type: "problem",
      docs: {
        description:
          "No imperative redirect (router.replace / router.navigate / a useNavigate() call) inside useEffect — redirect declaratively with <Redirect>/<Navigate> from render (an effect runs after paint and re-fires every render: a flash on TanStack, an infinite loop on expo-router web).",
      },
      messages: {
        effectReplace:
          "LZFE015: no imperative redirect (`{{call}}`) inside useEffect — it runs after render and re-fires on every re-render (a flash on TanStack; on expo-router web an infinite navigation/refetch loop that crashes the screen). Redirect declaratively instead: `if (<terminal state>) return <Redirect href={…} />;` (expo) / `<Navigate to={…} />` (TanStack).",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isView(f) && !isRoute(f)) return {};
      // Identifiers bound from `useNavigate()` (TanStack) — so a bare `navigate(...)` in an effect is recognized.
      const navigators = new Set();
      return {
        VariableDeclarator(node) {
          if (
            node.id.type === "Identifier" &&
            node.init &&
            node.init.type === "CallExpression" &&
            node.init.callee.type === "Identifier" &&
            node.init.callee.name === "useNavigate"
          )
            navigators.add(node.id.name);
        },
        CallExpression(node) {
          const callee = node.callee;
          let call = null;
          // expo-router / a router instance: router.replace(...) / router.navigate(...)
          if (
            callee.type === "MemberExpression" &&
            !callee.computed &&
            callee.object.type === "Identifier" &&
            callee.object.name === "router" &&
            callee.property.type === "Identifier" &&
            (callee.property.name === "replace" || callee.property.name === "navigate")
          )
            call = `router.${callee.property.name}`;
          // TanStack: navigate({ to }) where navigate = useNavigate()
          else if (callee.type === "Identifier" && navigators.has(callee.name)) call = `${callee.name}(...)`;
          // Flag only inside an effect. A user-action push/back/replace is a completion, not a render loop.
          if (call && inEffect(node)) context.report({ node, messageId: "effectReplace", data: { call } });
        },
      };
    },
  },

  // LZFE016 — the session is written through ONE seam (lib/session). The bug: token writes scattered across
  // viewModels (login, signup, impersonate), each of which must REMEMBER to reset the `me` cache — and the one that
  // forgets bounces the just-authenticated user back to login (a stale anonymous `me` error survives the sign-in).
  // Centralizing the write in the seam pairs token + cache-reset by construction. Same "one door" shape as LZFE002:
  // only the seam may import the token setter; everywhere else goes through the seam's signIn/signOut.
  "session-one-door": {
    meta: {
      type: "problem",
      docs: {
        description:
          "Write the session token through one seam (lib/session) — a scattered token write that forgets to reset the session cache bounces the just-authenticated user back to login.",
      },
      messages: {
        offdoor:
          "LZFE016: write the session only through the seam — import the token setter (`{{name}}`) into `lib/session` and expose signIn/signOut, not here. A scattered token write that forgets to reset the `me` query bounces the just-authenticated user back to login.",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (isInfraDataDoor(f)) return {}; // the seam (lib/session) legitimately imports the setter
      return {
        ImportDeclaration(node) {
          for (const s of node.specifiers) {
            if (s.type === "ImportSpecifier" && /^set(Access)?(Token|Session)$/.test(s.imported.name))
              context.report({ node: s, messageId: "offdoor", data: { name: s.imported.name } });
          }
        },
      };
    },
  },

  // LZFE017 — a route guard branches its redirect on a tri-state SessionState, NEVER a raw `isAuthenticated`
  // boolean. The boolean has no "still loading" — it is false while the session is in flight, so the guard fires its
  // redirect before the answer settles (the canonical bounce-to-login). Branch on `session.status` instead, where
  // `loading` is a distinct case you must handle. The read-side twin of LZFE010 (a View routes state through the
  // spine's union, not raw `isPending`). Scoped to route/guard files — where redirects live.
  "guard-tristate": {
    meta: {
      type: "problem",
      docs: {
        description:
          "A guard redirects on a tri-state SessionState (loading | authenticated | anonymous), not a raw isAuthenticated boolean (which reads 'still loading' as 'signed out' and bounces a not-yet-settled user to login).",
      },
      messages: {
        boolRedirect:
          "LZFE017: don't redirect on a raw `{{name}}` boolean — it reads 'still loading' as 'signed out', bouncing a not-yet-settled user to login. Branch on a tri-state session: handle `loading` (defer), then `if (session.status === 'anonymous') return <Navigate…/>` (use the spine's SessionState).",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isRoute(f) && !isInfraDataDoor(f)) return {};
      return {
        IfStatement(node) {
          // `if (!<authBool>) return <Redirect/Navigate …/>` — the boolean-collapse redirect.
          if (node.test.type !== "UnaryExpression" || node.test.operator !== "!") return;
          const name = authBoolName(node.test.argument);
          if (name && returnsRedirect(node.consequent))
            context.report({ node: node.test, messageId: "boolRedirect", data: { name } });
        },
      };
    },
  },

  // LZFE018 — a route that reads a REQUIRED id param must guard its absence with a declarative redirect. Hitting the
  // route param-less (a bookmark, a stale/mis-wired link) otherwise renders a "ghost" screen bound to an empty id —
  // the pilot's empty "Propriedade" thread. The fix is `if (!id) return <Redirect href={…} />` before the View. Scoped
  // to expo-router's `useLocalSearchParams` (the one router where a path/search param can be absent at render; on
  // TanStack a matched route guarantees its path param) and to id-shaped names (optional filter params don't ghost).
  "route-param-guard": {
    meta: {
      type: "problem",
      docs: {
        description:
          "A route reading a required id param (useLocalSearchParams) must guard its absence with a declarative redirect — a param-less hit otherwise renders a ghost screen on an empty id.",
      },
      messages: {
        unguarded:
          "LZFE018: the route reads `{{name}}` from useLocalSearchParams but never guards its absence — a param-less hit (bookmark / stale link) renders a ghost screen on an empty id. Add `if (!{{name}}) return <Redirect href={…} />;` before rendering the View.",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if (!isRoute(f)) return {};
      return {
        VariableDeclarator(node) {
          if (!node.init || node.init.type !== "CallExpression") return;
          if (node.init.callee.type !== "Identifier" || node.init.callee.name !== "useLocalSearchParams") return;
          if (node.id.type !== "ObjectPattern") return;
          const fn = enclosingFunction(node);
          if (!fn) return;
          for (const prop of node.id.properties) {
            if (prop.type !== "Property" || prop.key.type !== "Identifier" || !ID_PARAM.test(prop.key.name)) continue;
            const local = prop.value.type === "Identifier" ? prop.value.name : prop.key.name;
            if (!hasPresenceGuard(fn, aliasesOf(fn, local)))
              context.report({ node: prop, messageId: "unguarded", data: { name: local } });
          }
        },
      };
    },
  },

  // LZFE019 — no bare `router.back()` / `history.back()`. On web a deep-linked / refreshed screen has no in-app
  // history, so back() is a no-op and the "Back" button is dead (the pilot migrated ~13 screens off it). Route every
  // Back affordance through a guarded helper — the spine's `safeBack(router, fallback)` / an app `useGoBack` — that
  // pops when it can and otherwise replaces to a parent. A file that already guards with `canGoBack` is fine; the
  // nav seam (where the helper lives) is exempt. Scoped to the screens/routes that hold Back buttons.
  "safe-back": {
    meta: {
      type: "problem",
      docs: {
        description:
          "No bare router.back() — on web a deep-linked screen has no in-app history, so it is a no-op (a dead Back button). Use a guarded helper (safeBack / useGoBack) that falls back to a parent.",
      },
      messages: {
        bareBack:
          "LZFE019: no bare `{{call}}` — on web a deep-linked / refreshed screen has no in-app history, so it does nothing (a dead 'Back' button). Use a guarded helper: `useGoBack(fallback)` / `safeBack(router, fallback)` (pops when it can, else replaces to a parent).",
      },
    },
    create(context) {
      const f = context.filename.replace(/\\/g, "/");
      if ((!isView(f) && !isRoute(f)) || isNavSeam(f)) return {};
      // An inline `canGoBack`-guarded back() is the safe shape — exempt files that already do it.
      if (/canGoBack/.test((context.sourceCode ?? context.getSourceCode()).getText())) return {};
      return {
        CallExpression(node) {
          const callee = node.callee;
          if (callee.type !== "MemberExpression" || callee.computed) return;
          if (callee.property.type !== "Identifier" || callee.property.name !== "back") return;
          const obj = callee.object;
          const isRouterBack = obj.type === "Identifier" && obj.name === "router";
          const isHistoryBack =
            obj.type === "MemberExpression" && obj.property.type === "Identifier" && obj.property.name === "history";
          if (!isRouterBack && !isHistoryBack) return;
          context.report({ node, messageId: "bareBack", data: { call: isRouterBack ? "router.back()" : "history.back()" } });
        },
      };
    },
  },
};

module.exports = {
  meta: { name: "eslint-plugin-lazuli", version: "0.2.0" },
  rules,
};
