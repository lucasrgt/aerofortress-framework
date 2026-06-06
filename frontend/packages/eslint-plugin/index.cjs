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
const isInfraDataDoor = (f) => /(^|\/)lib\/(session|guards)\//.test(f.replace(/\\/g, "/"));

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
  // SCOPE — the screen unit, detected by IMPORT, not by a sibling file. With the monorepo split the ViewModel lives
  // in a `core` package while the View lives in the platform shell (mobile/web) — they are no longer siblings. So a
  // *.view.tsx is a SCREEN when it CONSUMES a ViewModel: it imports a `*.viewModel` module (co-located) OR a
  // `use<Name>Model` data-door hook (cross-package, e.g. from `@scope/app-core`). A View that imports no ViewModel
  // is a presentational FRAGMENT (a wizard step, a settings panel — props-in, no data door); it has nothing to
  // test-mount in isolation and is covered transitively when its shell renders, so it is skipped. The trigger is
  // visible: it reads off the View's own import statements, surfaced in the lint message and inspectable in source.
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
          const spec = node.source.value;
          if (typeof spec === "string" && /\.viewModel$/.test(spec)) consumesViewModel = true;
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
};

module.exports = {
  meta: { name: "eslint-plugin-lazuli", version: "0.1.0" },
  rules,
};
