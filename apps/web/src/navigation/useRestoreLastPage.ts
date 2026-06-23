"use client";

// LP-003: restore-once hook. Mounts at root layout (client side). Runs at
// most once per app session; subsequent navigations are handled by the
// tracker.
//
// Skip restore when:
//   - opt-out is OFF
//   - user is not authenticated
//   - stored value is missing/corrupt/oversize (handled by getLastPage)
//   - stored path matches the LP-001 deny-list
//   - user deep-linked to a non-`/` URL (they explicitly navigated)
//
// Detect deep-link via `window.history.length === 1 && location.pathname === '/'`
// — the negation of that is "fresh tab on root" which is the ONLY case we
// auto-restore.

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import {
  getLastPage,
  clearLastPage,
  isRememberLastPageEnabled,
} from "./setLastPage";
import { isDeniedPath } from "./denyList";

export interface UseRestoreLastPageOptions {
  isAuthenticated: boolean;
  onToast?: (msg: string) => void;
}

export function useRestoreLastPage({
  isAuthenticated,
  onToast,
}: UseRestoreLastPageOptions): void {
  const router = useRouter();
  const ranRef = useRef(false);

  useEffect(() => {
    if (ranRef.current) return;
    ranRef.current = true;

    if (typeof window === "undefined") return;
    if (!isAuthenticated) return;
    if (!isRememberLastPageEnabled()) return;

    // Only restore on a fresh open of the root page.
    const isFreshRoot =
      window.history.length === 1 && window.location.pathname === "/";
    if (!isFreshRoot) return;

    const stored = getLastPage();
    if (!stored) return;

    if (isDeniedPath(stored.pathname)) {
      clearLastPage();
      onToast?.("Saved page is no longer available.");
      return;
    }

    try {
      router.replace(`${stored.pathname}${stored.search}`);
    } catch {
      clearLastPage();
      onToast?.("Couldn't restore your last page.");
    }
  }, [isAuthenticated, router, onToast]);
}
