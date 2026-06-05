import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Resolve the sample's `@/…` app aliases + the spine to source, so the canonical unit runs exactly as it would
// inside a real lazuli-net app — wired, not mocked. The harness (sample/harness) stands in for the design system,
// the i18n instance, and the generated client; everything else is the real code.
const r = (p: string) => fileURLToPath(new URL(p, import.meta.url));

export default defineConfig({
  esbuild: { jsx: "automatic" },
  test: {
    environment: "jsdom",
    include: ["packages/**/*.test.{ts,tsx}", "sample/**/*.test.{ts,tsx}", "tools/**/*.test.{ts,tsx}"],
  },
  resolve: {
    alias: {
      "@lazuli/react": r("./packages/lazuli-react/src/index.ts"),
      "@/client.gen/sample": r("./sample/harness/client.gen/sample.ts"),
      "@/i18n": r("./sample/harness/i18n.ts"),
      "@/ui": r("./sample/harness/ui.tsx"),
    },
  },
});
