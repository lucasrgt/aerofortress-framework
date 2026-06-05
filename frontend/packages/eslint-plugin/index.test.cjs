"use strict";

// Self-test for the Lazuli frontend harness — the parallel of the backend's self-doctor / SelfHarness. Every LZFE
// rule is proven here with RuleTester: it must FIRE on the violation it polices and PASS on the shapes it allows. A
// rule the linter merely "accepts" is not done until a test pins both edges (the same discipline the framework
// applies to its own backend rules). Run: `node index.test.cjs` (exits non-zero on any failing case). eslint + the
// TS parser are workspace devDependencies, so a plain require resolves them from the hoisted node_modules.

const { RuleTester } = require("eslint");
const tsParser = require("@typescript-eslint/parser");
const plugin = require("./index.cjs");

const ruleTester = new RuleTester({
  languageOptions: {
    parser: tsParser,
    ecmaVersion: 2022,
    sourceType: "module",
    parserOptions: { ecmaFeatures: { jsx: true } },
  },
});

// LZFE001 — a View (*.view.tsx) imports no data layer (generated client / axios / react-query); it consumes its
// ViewModel.
ruleTester.run("view-purity", plugin.rules["view-purity"], {
  valid: [
    // A View may import the contract TYPES (erased at runtime) and the ViewModel.
    { filename: "Foo.view.tsx", code: `import type { Thing } from "@/client.gen/model";` },
    { filename: "Foo.view.tsx", code: `import { useFooModel } from "./Foo.viewModel";` },
    // Non-views are out of this rule's scope.
    { filename: "Foo.viewModel.ts", code: `import axios from "axios";` },
  ],
  invalid: [
    { filename: "Foo.view.tsx", code: `import { useThing } from "@/client.gen/sample";`, errors: [{ messageId: "impure" }] },
    { filename: "Foo.view.tsx", code: `import axios from "axios";`, errors: [{ messageId: "impure" }] },
    { filename: "Foo.view.tsx", code: `import { useQuery } from "@tanstack/react-query";`, errors: [{ messageId: "impure" }] },
  ],
});

// LZFE002 — the generated client has two data doors: a screen's *.viewModel.ts, and the framework auth/routing
// infra (lib/session, lib/guards). Everything else is forbidden; type-only imports are always fine.
ruleTester.run("data-door", plugin.rules["data-door"], {
  valid: [
    { filename: "Foo.viewModel.ts", code: `import { useThing } from "@/client.gen/sample";` },
    { filename: "src/lib/session/session.ts", code: `import { refresh } from "@/client.gen/sample";` },
    { filename: "src/lib/guards/RouteGuard.tsx", code: `import { useMe } from "@/client.gen/sample";` },
    { filename: "Foo.view.tsx", code: `import type { Thing } from "@/client.gen/model";` },
  ],
  invalid: [
    { filename: "Foo.view.tsx", code: `import { useThing } from "@/client.gen/sample";`, errors: [{ messageId: "offdoor" }] },
    { filename: "src/app/_layout.tsx", code: `import { refresh } from "@/client.gen/sample";`, errors: [{ messageId: "offdoor" }] },
    { filename: "src/lib/lazuli-client.ts", code: `import { thing } from "@/client.gen/sample";`, errors: [{ messageId: "offdoor" }] },
  ],
});

// LZFE009 — a *.viewModel.ts is platform-agnostic: no react-native / expo import (value OR type).
ruleTester.run("viewmodel-platform-agnostic", plugin.rules["viewmodel-platform-agnostic"], {
  valid: [
    { filename: "Foo.viewModel.ts", code: `import { useState } from "react";` },
    { filename: "Foo.viewModel.ts", code: `import { useThing } from "@/client.gen/sample";` },
    // A View may import react-native — it is the platform layer.
    { filename: "Foo.view.tsx", code: `import { View } from "react-native";` },
  ],
  invalid: [
    { filename: "Foo.viewModel.ts", code: `import { Platform } from "react-native";`, errors: [{ messageId: "platform" }] },
    { filename: "Foo.viewModel.ts", code: `import * as SecureStore from "expo-secure-store";`, errors: [{ messageId: "platform" }] },
    { filename: "Foo.viewModel.ts", code: `import type { ViewProps } from "react-native";`, errors: [{ messageId: "platform" }] },
  ],
});

// LZFE003 — no mock / fixture / MSW import in production code (only under *.test.*).
ruleTester.run("no-mock", plugin.rules["no-mock"], {
  valid: [
    { filename: "Foo.test.tsx", code: `import { server } from "msw";` },
    { filename: "Foo.viewModel.ts", code: `import { useState } from "react";` },
  ],
  invalid: [
    { filename: "Foo.viewModel.ts", code: `import { server } from "msw";`, errors: [{ messageId: "mock" }] },
    { filename: "Foo.view.tsx", code: `import { fake } from "./__mocks__/thing";`, errors: [{ messageId: "mock" }] },
  ],
});

// eslint-disable-next-line no-console
console.log("eslint-plugin-lazuli: all LZFE rule tests passed");
