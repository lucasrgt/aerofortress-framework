import { describe, expect, it } from "vitest";
import { checkI18nParity } from "./i18n-parity.mjs";

describe("i18n parity", () => {
  it("checks key parity and required locale exports", () => {
    const result = checkI18nParity(
      [{
        path: "feature.i18n.ts",
        source: `export const ptBR = {
  // Comments between entries do not hide the next key.
  title: "Título",
  body: "Corpo",
} as const;
export const enUS = {
  title: "Title",
} as const;`,
      }],
      ["ptBR", "esES", "enUS"],
    );
    expect(result.ok).toBe(false);
    expect(result.messages.join("\n")).toContain("esES");
    expect(result.messages.join("\n")).toContain("body");
  });
});
