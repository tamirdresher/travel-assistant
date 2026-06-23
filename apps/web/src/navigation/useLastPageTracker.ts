"use client";

// LP-003: tracker hook. Records the current route on every navigation when:
//   - user is authenticated (caller-provided predicate)
//   - opt-out preference is ON (default)
//   - current path is not on the LP-001 deny-list
//
// Decoupled from any specific auth lib via the `isAuthenticated` argument so
// it can be wired in LP-004 / future auth work without churn here.

import { useEffect } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { setLastPage, isRememberLastPageEnabled } from "./setLastPage";
import { isDeniedPath } from "./denyList";

export interface UseLastPageTrackerOptions {
  isAuthenticated: boolean;
}

export function useLastPageTracker({ isAuthenticated }: UseLastPageTrackerOptions): void {
  const pathname = usePathname();
  const searchParams = useSearchParams();

  useEffect(() => {
    if (!isAuthenticated) return;
    if (!isRememberLastPageEnabled()) return;
    if (!pathname || isDeniedPath(pathname)) return;
    const search = searchParams ? `?${searchParams.toString()}` : "";
    setLastPage(pathname, search === "?" ? "" : search);
  }, [pathname, searchParams, isAuthenticated]);
}
