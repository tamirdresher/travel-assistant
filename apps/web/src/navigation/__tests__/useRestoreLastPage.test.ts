import { describe, it, expect, beforeEach, vi } from "vitest";
import { renderHook } from "@testing-library/react";

const replaceMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
}));

import { useRestoreLastPage } from "../useRestoreLastPage";
import { setLastPage, clearLastPage } from "../setLastPage";
import { LAST_PAGE_STORAGE_KEY } from "../types";

function makeFreshRoot() {
  // jsdom defaults to history.length===1 and pathname==='/'.
  Object.defineProperty(window, "location", {
    value: { ...window.location, pathname: "/" },
    writable: true,
  });
}

describe("useRestoreLastPage", () => {
  beforeEach(() => {
    window.localStorage.clear();
    replaceMock.mockReset();
    makeFreshRoot();
  });

  it("restores when authed + fresh root + valid stored page", () => {
    setLastPage("/trips/42", "?tab=overview");
    renderHook(() => useRestoreLastPage({ isAuthenticated: true }));
    expect(replaceMock).toHaveBeenCalledWith("/trips/42?tab=overview");
  });

  it("does NOT restore when user is not authenticated", () => {
    setLastPage("/trips/42", "");
    renderHook(() => useRestoreLastPage({ isAuthenticated: false }));
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("skips and clears when stored path is on the deny-list", () => {
    setLastPage("/login", "");
    const toast = vi.fn();
    renderHook(() => useRestoreLastPage({ isAuthenticated: true, onToast: toast }));
    expect(replaceMock).not.toHaveBeenCalled();
    expect(window.localStorage.getItem(LAST_PAGE_STORAGE_KEY)).toBeNull();
    expect(toast).toHaveBeenCalled();
  });

  it("does NOT restore on a deep-link (pathname !== '/')", () => {
    setLastPage("/trips/42", "");
    Object.defineProperty(window, "location", {
      value: { ...window.location, pathname: "/some/other" },
      writable: true,
    });
    renderHook(() => useRestoreLastPage({ isAuthenticated: true }));
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("does nothing when there's no stored value", () => {
    clearLastPage();
    renderHook(() => useRestoreLastPage({ isAuthenticated: true }));
    expect(replaceMock).not.toHaveBeenCalled();
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
    renderHook(() => useRestoreLastPage({ isAuthenticated: true }));
    expect(replaceMock).not.toHaveBeenCalled();
  });
});
