"use client";

// LP-003 tracker: records the current route on every client-side navigation.
// Write-side enforcement lives in setLastPage (deny-list + path validator);
// this hook just feeds it the current pathname+search.

import { useEffect } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { setLastPage, isRememberLastPageEnabled } from "./setLastPage";

export interface UseLastPageTrackerOptions {
  isAuthenticated: boolean;
}

export function useLastPageTracker({
  isAuthenticated,
}: UseLastPageTrackerOptions): void {
  const pathname = usePathname();
  const searchParams = useSearchParams();

  useEffect(() => {
    if (!isAuthenticated) return;
    if (!isRememberLastPageEnabled()) return;
    if (!pathname) return;
    const qs = searchParams ? searchParams.toString() : "";
    const search = qs ? `?${qs}` : "";
    setLastPage(pathname, search);
  }, [pathname, searchParams, isAuthenticated]);
}
