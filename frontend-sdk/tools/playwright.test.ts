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

function response(status: number, url: string, resourceType: string) {
  return {
    status: () => status,
    url: () => url,
    request: () => ({ resourceType: () => resourceType }),
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

  it("does not confuse a handled data response with application console output", () => {
    const browser = fakePage();
    const assertQuality = watchPageQuality(browser.page);
    const url = "http://localhost/api/session";

    browser.emit("response", response(401, url, "fetch"));
    browser.emit("console", {
      ...consoleMessage("error", "Failed to load resource: the server responded with a status of 401 (Unauthorized)"),
      location: () => ({ url, lineNumber: 0 }),
    });

    expect(assertQuality).not.toThrow();
  });

  it("still rejects a missing browser asset", () => {
    const browser = fakePage();
    const assertQuality = watchPageQuality(browser.page);
    const url = "http://localhost/app.js";

    browser.emit("response", response(404, url, "script"));
    browser.emit("console", {
      ...consoleMessage("error", "Failed to load resource: the server responded with a status of 404 (Not Found)"),
      location: () => ({ url, lineNumber: 0 }),
    });

    expect(assertQuality).toThrow("console.error");
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
