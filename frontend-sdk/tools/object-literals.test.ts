import { describe, expect, it } from "vitest";
import { exportedObjectLiterals, topLevelObjectKeys } from "./object-literals.mjs";

describe("object literal reader", () => {
  it("ignores punctuation inside values and reads keys after comments or on packed lines", () => {
    const [object] = exportedObjectLiterals(`export const esES = {
  // section
  "route.intro": "Al llegar, recuerda:",
  first: "one", second: "two",
} as const;`);
    expect([...topLevelObjectKeys(object.source)]).toEqual(["route.intro", "first", "second"]);
  });
});
