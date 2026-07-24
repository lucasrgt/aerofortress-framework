// @vitest-environment node
import { describe, expect, it, vi } from "vitest";
import { watchPageQuality } from "./playwright.mjs";

type Listener = (...args: never[]) => void;

function fakePage() {
  const listeners = new Map<string, Set<Listener>>();
  return {
    page: {
      on(event: string, listener: Listener) {
        const eventListeners = listeners.get(event) ?? new Set<Listener>();
        eventListeners.add(listener);
        listeners.set(event, eventListeners);
      },
      off(event: string, listener: Listener) {
        listeners.get(event)?.delete(listener);
      },
    },
    emit(event: string, value: unknown) {
      for (const listener of listeners.get(event) ?? []) listener(value as never);
    },
  };
}

function consoleMessage(type: string, text: string) {
  return {
    type: () => type,
    text: () => text,
    location: () => ({ url: "http://localhost/app.js", lineNumber: 42 }),
  };
}

describe("Playwright page quality fixture", () => {
  it("ignores informational browser output", () => {
    const browser = fakePage();
    const assertQuality = watchPageQuality(browser.page);

    browser.emit("console", consoleMessage("info", "connected"));

    expect(assertQuality).not.toThrow();
  });

  it("fails closed on page errors and warning-tier console output", () => {
    const browser = fakePage();
    const assertQuality = watchPageQuality(browser.page);

    browser.emit("pageerror", new Error("render exploded"));
    browser.emit("console", consoleMessage("warning", "deprecated behavior"));

    expect(assertQuality).toThrow(/pageerror: Error: render exploded[\s\S]*console\.warning: deprecated behavior/);
  });

  it("detaches its listeners after the proof closes", () => {
    const browser = fakePage();
    const assertQuality = watchPageQuality(browser.page);
    const assertion = vi.fn(assertQuality);

    assertion();
    browser.emit("console", consoleMessage("error", "too late"));

    expect(assertion).toHaveReturned();
  });

  it("rejects an object that cannot observe and detach page events", () => {
    expect(() => watchPageQuality({ on: vi.fn() } as never)).toThrow("Playwright page");
  });
});
