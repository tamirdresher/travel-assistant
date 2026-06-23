import { describe, it, expect, beforeEach, vi } from "vitest";
import {
  setLastPage,
  getLastPage,
  clearLastPage,
  isRememberLastPageEnabled,
  setRememberLastPagePreference,
} from "../setLastPage";
import { LAST_PAGE_STORAGE_KEY, PRIVACY_REMEMBER_LAST_PAGE_KEY } from "../types";

describe("setLastPage / getLastPage", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("round-trips a normal record", () => {
    setLastPage("/trips/42", "?tab=overview");
    const got = getLastPage();
    expect(got?.pathname).toBe("/trips/42");
    expect(got?.search).toBe("?tab=overview");
    expect(typeof got?.ts).toBe("number");
  });

  it("clears stored value on read when payload exceeds 2KB (treat as absent)", () => {
    // Seed an oversize raw value directly (bypasses the writer's own guard).
    const huge = "x".repeat(3000);
    window.localStorage.setItem(
      LAST_PAGE_STORAGE_KEY,
      JSON.stringify({ pathname: "/" + huge, search: "", ts: 1 }),
    );
    expect(getLastPage()).toBeNull();
    expect(window.localStorage.getItem(LAST_PAGE_STORAGE_KEY)).toBeNull();
  });

  it("refuses to write oversize records", () => {
    setLastPage("/" + "x".repeat(3000), "");
    expect(window.localStorage.getItem(LAST_PAGE_STORAGE_KEY)).toBeNull();
  });

  it("is a no-op AND clears any existing value when opt-out is off", () => {
    setLastPage("/trips/1", "");
    expect(getLastPage()).not.toBeNull();
    setRememberLastPagePreference(false);
    expect(getLastPage()).toBeNull();
    setLastPage("/trips/2", "");
    expect(window.localStorage.getItem(LAST_PAGE_STORAGE_KEY)).toBeNull();
  });

  it("isRememberLastPageEnabled defaults to true", () => {
    expect(isRememberLastPageEnabled()).toBe(true);
    window.localStorage.setItem(PRIVACY_REMEMBER_LAST_PAGE_KEY, "false");
    expect(isRememberLastPageEnabled()).toBe(false);
  });

  it("treats Safari-private-mode setItem throws as no-op", () => {
    const spy = vi
      .spyOn(Storage.prototype, "setItem")
      .mockImplementation(() => {
        throw new Error("QuotaExceededError");
      });
    expect(() => setLastPage("/trips/1", "")).not.toThrow();
    spy.mockRestore();
  });

  it("clearLastPage removes the record", () => {
    setLastPage("/trips/1", "");
    clearLastPage();
    expect(getLastPage()).toBeNull();
  });

  it("returns null and clears on corrupt JSON", () => {
    window.localStorage.setItem(LAST_PAGE_STORAGE_KEY, "{not-json");
    expect(getLastPage()).toBeNull();
    expect(window.localStorage.getItem(LAST_PAGE_STORAGE_KEY)).toBeNull();
  });

  it("returns null and clears on wrong-shape JSON", () => {
    window.localStorage.setItem(
      LAST_PAGE_STORAGE_KEY,
      JSON.stringify({ pathname: 7, search: "", ts: "x" }),
    );
    expect(getLastPage()).toBeNull();
  });
});
