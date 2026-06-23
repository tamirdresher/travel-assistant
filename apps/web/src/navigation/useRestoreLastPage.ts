"use client";

// LP-003: restore-once hook (spec §2 + §3).
//
// First paint is ALWAYS the `/` skeleton; restore happens post-hydration via
// router.replace(). Never SSR-restore (would cause CSR/SSR mismatch).
//
// Skip reasons (mapped 1:1 to telemetry `nav.lastpage.restore_skipped`):
//   opt_out | deep_link | none_stored | deny_list | auth_gated
//
// Deep-link detection: history.length===1 AND pathname !== '/' → user opened
// a specific URL directly; do NOT restore and do NOT clear stored value
// (spec §3, also avoids clobbering a future opt-out toggle's intent).
//
// 404 detection: set sessionStorage breadcrumb LAST_PAGE_RESTORING_KEY right
// before router.replace; destination layout (or not-found.tsx) clears it on
// mount and — if not-found is the destination — fires the restore-failed
// toast and replace('/'). This hook owns the breadcrumb SET; consumers own
// the CLEAR.

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import {
  getLastPage,
  clearLastPage,
  isRememberLastPageEnabled,
} from "./setLastPage";
import { isDenied, isSafeRelativePath } from "./lastPage.denylist";

export const LAST_PAGE_RESTORING_KEY = "ta.nav.lastPage.restoring";

export type RestoreSkipReason =
  | "opt_out"
  | "deep_link"
  | "none_stored"
  | "deny_list"
  | "auth_gated";

export interface UseRestoreLastPageOptions {
  isAuthenticated: boolean;
  /** Fired when restore fails post-navigation (e.g. 404). Caller wires aria-live toast. */
  onRestoreFailed?: () => void;
  /** Diagnostic hook; intentionally pathname-only (search FORBIDDEN per LP-001 §D5). */
  onSkip?: (reason: RestoreSkipReason, pathname?: string) => void;
}

export function useRestoreLastPage({
  isAuthenticated,
  onRestoreFailed: _onRestoreFailed,
  onSkip,
}: UseRestoreLastPageOptions): void {
  const router = useRouter();
  const ranRef = useRef(false);

  useEffect(() => {
    if (ranRef.current) return;
    ranRef.current = true;

    if (typeof window === "undefined") return;

    if (!isRememberLastPageEnabled()) {
      onSkip?.("opt_out");
      return;
    }

    // Deep-link: user explicitly typed/clicked a non-root URL. Do not restore.
    // Do NOT clear — they may navigate to / later and expect restore behavior.
    if (
      window.history.length === 1 &&
      window.location.pathname !== "/"
    ) {
      onSkip?.("deep_link");
      return;
    }

    if (!isAuthenticated) {
      // Auth-gated stored path + signed-out user → silent clear (spec §3).
      // Public-route 404 is a different path handled by the destination.
      const peek = getLastPage();
      if (peek) clearLastPage();
      onSkip?.("auth_gated");
      return;
    }

    const stored = getLastPage();
    if (!stored) {
      onSkip?.("none_stored");
      return;
    }

    // Defense-in-depth: getLastPage already enforces, but recheck so a future
    // refactor can't silently bypass the deny-list.
    if (
      isDenied(stored.pathname, stored.search) ||
      !isSafeRelativePath(stored.pathname + stored.search)
    ) {
      clearLastPage();
      onSkip?.("deny_list", stored.pathname);
      return;
    }

    // Set 404-detection breadcrumb BEFORE the replace. Consumers clear it
    // on successful destination mount; not-found.tsx checks for it.
    try {
      window.sessionStorage.setItem(LAST_PAGE_RESTORING_KEY, "1");
    } catch {
      // sessionStorage unavailable (Safari private). Skip restore rather
      // than risk un-detectable 404.
      onSkip?.("none_stored");
      return;
    }

    try {
      router.replace(`${stored.pathname}${stored.search}`);
    } catch {
      // Router threw synchronously — clear breadcrumb and surface failure.
      try {
        window.sessionStorage.removeItem(LAST_PAGE_RESTORING_KEY);
      } catch {
        /* noop */
      }
      clearLastPage();
      _onRestoreFailed?.();
    }
  }, [isAuthenticated, router, _onRestoreFailed, onSkip]);
}
