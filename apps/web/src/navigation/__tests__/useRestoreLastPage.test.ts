import { describe, it, expect, beforeEach, vi } from "vitest";
import { renderHook } from "@testing-library/react";

const replaceMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
}));

import {
  useRestoreLastPage,
  LAST_PAGE_RESTORING_KEY,
} from "../useRestoreLastPage";
import { setLastPage, clearLastPage } from "../setLastPage";
import { LAST_PAGE_STORAGE_KEY } from "../types";

function setLocation(pathname: string) {
  Object.defineProperty(window, "location", {
    value: { ...window.location, pathname },
    writable: true,
  });
}

describe("useRestoreLastPage", () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    replaceMock.mockReset();
    setLocation("/");
  });

  it("restores when authed + fresh root + valid stored page", () => {
    setLastPage("/trips/42", "?tab=overview");
    renderHook(() => useRestoreLastPage({ isAuthenticated: true }));
    expect(replaceMock).toHaveBeenCalledWith("/trips/42?tab=overview");
    expect(window.sessionStorage.getItem(LAST_PAGE_RESTORING_KEY)).toBe("1");
  });

  it("clears stored value AND skips silently when signed-out (no toast)", () => {
    setLastPage("/trips/42", "");
    const onRestoreFailed = vi.fn();
    const onSkip = vi.fn();
    renderHook(() =>
      useRestoreLastPage({
        isAuthenticated: false,
        onRestoreFailed,
        onSkip,
      }),
    );
    expect(replaceMock).not.toHaveBeenCalled();
    expect(onRestoreFailed).not.toHaveBeenCalled();
    expect(onSkip).toHaveBeenCalledWith("auth_gated");
    expect(window.localStorage.getItem(LAST_PAGE_STORAGE_KEY)).toBeNull();
  });

  it("does NOT clear stored value on deep-link (history.length===1 && pathname !== '/')", () => {
    setLastPage("/trips/42", "");
    setLocation("/itineraries");
    const onSkip = vi.fn();
    renderHook(() =>
      useRestoreLastPage({ isAuthenticated: true, onSkip }),
    );
    expect(replaceMock).not.toHaveBeenCalled();
    expect(onSkip).toHaveBeenCalledWith("deep_link");
    expect(window.localStorage.getItem(LAST_PAGE_STORAGE_KEY)).not.toBeNull();
  });

  it("skips and clears when stored path is on the deny-list (defense-in-depth)", () => {
    // Bypass writer to seed a denied path (writer would reject).
    window.localStorage.setItem(
      LAST_PAGE_STORAGE_KEY,
      JSON.stringify({ pathname: "/login", search: "", ts: Date.now() }),
    );
    const onSkip = vi.fn();
    renderHook(() =>
      useRestoreLastPage({ isAuthenticated: true, onSkip }),
    );
    expect(replaceMock).not.toHaveBeenCalled();
    expect(window.localStorage.getItem(LAST_PAGE_STORAGE_KEY)).toBeNull();
    // getLastPage clears + returns null first; hook reports none_stored.
    expect(onSkip).toHaveBeenCalled();
  });

  it("does nothing when there's no stored value", () => {
    clearLastPage();
    const onSkip = vi.fn();
    renderHook(() =>
      useRestoreLastPage({ isAuthenticated: true, onSkip }),
    );
    expect(replaceMock).not.toHaveBeenCalled();
    expect(onSkip).toHaveBeenCalledWith("none_stored");
  });

  it("runs at most once per mount lifecycle", () => {
    setLastPage("/trips/42", "");
    const { rerender } = renderHook(
      ({ a }: { a: boolean }) => useRestoreLastPage({ isAuthenticated: a }),
      { initialProps: { a: true } },
    );
    rerender({ a: true });
    rerender({ a: true });
    expect(replaceMock).toHaveBeenCalledTimes(1);
  });

  it("skips when opt-out is off", () => {
    setLastPage("/trips/42", "");
    window.localStorage.setItem("ta.privacy.rememberLastPage", "false");
    const onSkip = vi.fn();
    renderHook(() =>
      useRestoreLastPage({ isAuthenticated: true, onSkip }),
    );
    expect(replaceMock).not.toHaveBeenCalled();
    expect(onSkip).toHaveBeenCalledWith("opt_out");
  });
});
