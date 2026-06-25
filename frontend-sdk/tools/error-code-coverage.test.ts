import { describe, expect, it } from "vitest";
import { catalogKeys, checkErrorCodeCoverage, contractCodes } from "./error-code-coverage.mjs";

describe("error-code coverage", () => {
  it("reads inline code unions", () => {
    expect([...contractCodes("export interface ErrorBody { code: 'auth.invalid' | 'auth.expired'; }")!]).toEqual([
      "auth.invalid",
      "auth.expired",
    ]);
  });

  it("reads Orval const enums", () => {
    const source = `export const ErrorBodyCode = {
  AuthInvalid: 'auth.invalid',
  AuthExpired: "auth.expired",
} as const;`;
    expect([...contractCodes(source)!]).toEqual(["auth.invalid", "auth.expired"]);
  });

  it("reports contract codes missing from the catalog", () => {
    const catalog = catalogKeys(`export const ptBR = {
  "auth.invalid": "Inválido",
} as const;`);
    const result = checkErrorCodeCoverage(catalog, new Set(["auth.invalid", "auth.expired"]));
    expect(result.ok).toBe(false);
    expect(result.uncovered).toEqual(["auth.expired"]);
  });
});
