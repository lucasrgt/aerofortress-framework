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

// LZFE010 — a View routes async state through <Resource>, never raw react-query booleans (member access OR
// destructuring). The booleans are the ViewModel's; the View consumes the AsyncState union.
ruleTester.run("state-completeness", plugin.rules["state-completeness"], {
  valid: [
    { filename: "Foo.view.tsx", code: `const { state } = useFooModel(); const data = state.items;` },
    // Non-views own the booleans (the ViewModel projects them through toAsyncState).
    { filename: "Foo.viewModel.ts", code: `const x = query.isPending;` },
  ],
  invalid: [
    { filename: "Foo.view.tsx", code: `const x = query.isPending;`, errors: [{ messageId: "raw" }] },
    { filename: "Foo.view.tsx", code: `const { isError } = useFooModel();`, errors: [{ messageId: "raw" }] },
  ],
});

// LZFE011 — every locale catalog in a *.i18n.ts declares the same keys; a key in one but not its siblings is a
// silent untranslated string. Scope is the *.i18n.ts file only.
ruleTester.run("i18n-completeness", plugin.rules["i18n-completeness"], {
  valid: [
    { filename: "x.i18n.ts", code: `export const ptBR = { a: "1", b: "2" }; export const enUS = { a: "x", b: "y" };` },
    // `as const` is unwrapped; dotted keys compared as flat keys.
    {
      filename: "x.i18n.ts",
      code: `export const ptBR = { "e.t": "1" } as const; export const enUS = { "e.t": "x" } as const;`,
    },
    // A single catalog has nothing to compare against.
    { filename: "x.i18n.ts", code: `export const ptBR = { a: "1" };` },
  ],
  invalid: [
    {
      filename: "x.i18n.ts",
      code: `export const ptBR = { a: "1", b: "2" }; export const enUS = { a: "x" };`,
      errors: [{ messageId: "missing" }],
    },
  ],
});

// LZFE012 — no inline hex color outside the token/theme definition files.
ruleTester.run("design-tokens", plugin.rules["design-tokens"], {
  valid: [
    { filename: "Foo.view.tsx", code: `const c = theme.colors.primary;` },
    // Token definitions are where hex legitimately lives.
    { filename: "src/theme/colors.ts", code: `export const primary = "#3b82f6";` },
  ],
  invalid: [
    { filename: "Foo.view.tsx", code: `const c = "#3b82f6";`, errors: [{ messageId: "hex" }] },
    { filename: "Foo.viewModel.ts", code: `const bg = "#fff";`, errors: [{ messageId: "hex" }] },
  ],
});

// LZFE013 — a ViewModel's mutation must surface its failure (no silent failure). Scoped to *.viewModel.ts. Four
// legitimate surfaces are accepted: (A) inline onError, (B) a read `.isError` state, (C) mutateAsync in try/catch
// or .catch(), (D) a returned/propagated mutateAsync. Each is a real surface; demanding a redundant onError on top
// would be the very test-theater the rule exists to prevent.
ruleTester.run("mutation-error-handled", plugin.rules["mutation-error-handled"], {
  valid: [
    // A) inline onError (also via a spread that may carry it).
    { filename: "Foo.viewModel.ts", code: `m.mutate(data, { onSuccess: ok, onError: fail });` },
    { filename: "Foo.viewModel.ts", code: `m.mutateAsync(data, { onError: fail });` },
    { filename: "Foo.viewModel.ts", code: `m.mutate(data, { ...handlers });` },
    // B) the mutation handle's .isError is read elsewhere in the file (surfaced as state the View renders).
    { filename: "Foo.viewModel.ts", code: `saveMut.mutate(data, { onSuccess: ok }); const e = saveMut.isError ? "x" : null;` },
    // C) mutateAsync inside a try/catch, or chained with .catch().
    { filename: "Foo.viewModel.ts", code: `async function f(){ try { await m.mutateAsync(data); } catch { setErr(); } }` },
    { filename: "Foo.viewModel.ts", code: `m.mutateAsync(data).catch(() => undefined);` },
    // D) a thin wrapper that returns the mutateAsync promise — propagates to the awaiting caller.
    { filename: "Foo.viewModel.ts", code: `const run = () => m.mutateAsync(data);` },
    { filename: "Foo.viewModel.ts", code: `function run(){ return m.mutateAsync(data); }` },
    // Out of scope: a .mutate outside a ViewModel isn't this rule's concern.
    { filename: "Foo.view.tsx", code: `m.mutate(data);` },
    // Not a react-query mutation call shape.
    { filename: "Foo.viewModel.ts", code: `arr.map(x => x);` },
  ],
  invalid: [
    { filename: "Foo.viewModel.ts", code: `m.mutate(data);`, errors: [{ messageId: "unhandled" }] },
    { filename: "Foo.viewModel.ts", code: `m.mutate(data, { onSuccess: ok });`, errors: [{ messageId: "unhandled" }] },
    { filename: "Foo.viewModel.ts", code: `m.mutateAsync(data, {});`, errors: [{ messageId: "unhandled" }] },
    // isPending read is not error handling — only isError/error/failureReason count.
    { filename: "Foo.viewModel.ts", code: `m.mutate(data); const p = m.isPending;`, errors: [{ messageId: "unhandled" }] },
    // a bare awaited mutateAsync (no try/catch, no .catch, not returned) is still a silent failure.
    { filename: "Foo.viewModel.ts", code: `async function f(){ await m.mutateAsync(data); }`, errors: [{ messageId: "unhandled" }] },
  ],
});

// LZFE014 — no hardcoded user-facing copy in a View (JSX text children must go through t()).
ruleTester.run("no-hardcoded-copy", plugin.rules["no-hardcoded-copy"], {
  valid: [
    // t() result is an expression, not text — never flagged.
    { filename: "Foo.view.tsx", code: `const x = <Text>{t("k")}</Text>;` },
    // attributes (className/testID) are not text children.
    { filename: "Foo.view.tsx", code: `const x = <View className="flex-1" testID="x" />;` },
    // whitespace / non-letter text is ignored.
    { filename: "Foo.view.tsx", code: `const x = <Text> </Text>;` },
    { filename: "Foo.view.tsx", code: `const x = <Text>{count}</Text>;` },
    // Phase 2 props: t() / variables on copy props are expressions, not literals — not flagged.
    { filename: "Foo.view.tsx", code: `const x = <Input placeholder={t("k")} />;` },
    { filename: "Foo.view.tsx", code: `const x = <Input placeholder={ph} />;` },
    // non-copy props with literals are fine (name/testID/variant aren't copy).
    { filename: "Foo.view.tsx", code: `const x = <Icon name="check" testID="x" variant="primary" />;` },
    // out of scope: not a *.view.tsx
    { filename: "Foo.tsx", code: `const x = <Text>Entrar</Text>;` },
  ],
  invalid: [
    { filename: "Foo.view.tsx", code: `const x = <Text>Entrar na conta</Text>;`, errors: [{ messageId: "hardcoded" }] },
    { filename: "Foo.view.tsx", code: `const x = <Button>Salvar</Button>;`, errors: [{ messageId: "hardcoded" }] },
    // Phase 2: hardcoded copy in a copy-bearing prop.
    { filename: "Foo.view.tsx", code: `const x = <Input placeholder="Seu e-mail" />;`, errors: [{ messageId: "hardcoded" }] },
    { filename: "Foo.view.tsx", code: `const x = <EmptyState title="Nada aqui" />;`, errors: [{ messageId: "hardcoded" }] },
  ],
});

// LZFE006 — a screen View (one that imports a ViewModel) needs a co-located render test. Detection is by IMPORT,
// not a sibling file, so it survives the monorepo split (ViewModel in `core`, View in the platform shell). In
// RuleTester the co-located test file doesn't exist on disk, so a VM-consuming View reports `missing` (proving the
// gate fires) while a presentational fragment passes (proving the gate skips). A unique base avoids a cwd collision.
ruleTester.run("view-integration-test", plugin.rules["view-integration-test"], {
  valid: [
    // a presentational fragment: imports no ViewModel -> not a screen, skipped (covered via its shell).
    { filename: "Lzfe006Frag.view.tsx", code: `import { View } from "react-native"; export const X = () => <View />;` },
    // out of scope: not a *.view.tsx (even though it names a model hook).
    { filename: "Lzfe006Probe.tsx", code: `import { useLzfe006ProbeModel } from "@scope/app-core";` },
  ],
  invalid: [
    // cross-package: imports a use<Name>Model data-door hook from a core package -> a screen, needs the render test.
    { filename: "Lzfe006Probe.view.tsx", code: `import { useLzfe006ProbeModel } from "@scope/app-core";`, errors: [{ messageId: "missing" }] },
    // co-located (retrocompat): imports the ./X.viewModel module -> still a screen.
    { filename: "Lzfe006Probe.view.tsx", code: `import { useLzfe006ProbeModel } from "./Lzfe006Probe.viewModel";`, errors: [{ messageId: "missing" }] },
  ],
});

// eslint-disable-next-line no-console
console.log("eslint-plugin-lazuli: all LZFE rule tests passed");
