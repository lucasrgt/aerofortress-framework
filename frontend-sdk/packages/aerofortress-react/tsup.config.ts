import { defineConfig } from "tsup";

// Build the spine barrel (src/index.ts) into a published ESM package + its .d.ts. React is the only
// runtime peer — externalize it so the consumer's React is used, never a second copy bundled in.
export default defineConfig({
  entry: ["src/index.ts"],
  format: ["esm"],
  dts: true,
  clean: true,
  treeshake: true,
  sourcemap: true,
  external: ["react", "react-dom"],
});
