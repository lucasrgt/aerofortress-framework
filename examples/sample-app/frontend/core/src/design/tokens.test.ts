import { describe, it, expect } from "vitest";
import { breakpoints, color, motionMs, radius, space, text, themes } from "./tokens";

// TAXONOMY CONTRACT TESTS — these pin the design-token invariants the constitution promises
// (docs/DESIGN-CONVENTIONS.md): the scales hold their structure no matter what values an app edits in.
// Values are free; structure is not — that is what makes the vocabulary safe to build a kit on.

describe("design tokens", () => {
  it("declares every color role in both themes, with identical key sets", () => {
    expect(Object.keys(themes.dark).sort()).toEqual(Object.keys(themes.light).sort());
    expect(color).toBe(themes.light);
  });

  it("keeps spacing on the 4px grid, strictly increasing", () => {
    const steps = Object.values(space);
    for (const v of steps) expect(v % 4).toBe(0);
    for (let i = 1; i < steps.length; i++) expect(steps[i]!).toBeGreaterThan(steps[i - 1]!);
  });

  it("raises the type scale monotonically caption→display, line-height covering font-size", () => {
    const order = ["caption", "label", "body", "heading", "title", "display"] as const;
    const roles = order.map((r) => text[r]);
    roles.forEach((role) => expect(role.lineHeight).toBeGreaterThanOrEqual(role.fontSize));
    roles.slice(1).forEach((role, index) => {
      const previous = roles[index]!;
      expect(role.fontSize).toBeGreaterThan(previous.fontSize);
      expect(role.lineHeight).toBeGreaterThan(previous.lineHeight);
    });
  });

  it("keeps the interactive pairs distinct so the five states are visible", () => {
    for (const theme of [themes.light, themes.dark]) {
      expect(theme.primaryHover === theme.primary).toBe(false);
      expect(theme.primaryActive === theme.primaryHover).toBe(false);
      expect(theme.dangerHover === theme.danger).toBe(false);
    }
  });

  it("orders the radius scale and keeps motion durations ascending rest→slow", () => {
    const r = Object.values(radius);
    for (let i = 1; i < r.length; i++) expect(r[i]!).toBeGreaterThan(r[i - 1]!);
    const m = Object.values(motionMs);
    for (let i = 1; i < m.length; i++) expect(m[i]!).toBeGreaterThan(m[i - 1]!);
    expect(breakpoints.compact).toBe(0);
    expect(breakpoints.regular).toBeLessThan(breakpoints.wide);
  });
});
