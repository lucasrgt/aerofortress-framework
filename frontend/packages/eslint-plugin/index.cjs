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
};

module.exports = {
  meta: { name: "eslint-plugin-lazuli", version: "0.1.0" },
  rules,
};
