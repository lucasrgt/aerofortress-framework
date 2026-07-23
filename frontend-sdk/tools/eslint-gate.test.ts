import { createRequire } from "node:module";
import { describe, expect, it } from "vitest";
import { eslintGateArguments } from "./eslint-gate.mjs";

const require = createRequire(import.meta.url);
const plugin = require("../packages/eslint-plugin/index.cjs");

describe("eslintGateArguments", () => {
  it("forces every shipped AFFE rule with a zero warning budget and no inline overrides", () => {
    const rules = Object.keys(plugin.rules);
    const arguments_ = eslintGateArguments(["src"], rules);
    const forced = arguments_
      .filter((argument) => argument.startsWith("aerofortress/"))
      .map((argument) => argument.replace(/^aerofortress\//, "").replace(/:warn$/, ""));

    expect(arguments_).toContain("--max-warnings=0");
    expect(arguments_).toContain("--no-inline-config");
    expect(forced.sort()).toEqual(rules.sort());
  });
});
