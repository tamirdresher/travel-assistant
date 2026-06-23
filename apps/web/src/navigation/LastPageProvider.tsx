"use client";

// LP-003 wiring: composes tracker + restore + the restore-failed aria-live
// toast region. Mount once at the root layout (client tree, below auth
// context boundary so isAuthenticated reflects current session).
//
// Toast contract (LP-001 §D5 / QT-locked):
//   role="status" aria-live="polite"  (NOT alert — non-blocking)
//   data-testid="lastpage-restore-failed-toast"
//   copy verbatim: "We couldn't reopen your last page. You're back on home."

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useLastPageTracker } from "./useLastPageTracker";
import {
  useRestoreLastPage,
  LAST_PAGE_RESTORING_KEY,
} from "./useRestoreLastPage";
import { clearLastPage } from "./setLastPage";

export const LAST_PAGE_RESTORE_FAILED_COPY =
  "We couldn't reopen your last page. You're back on home.";

export interface LastPageProviderProps {
  /** Wire to your real auth context. Defaults to true to preserve dev ergonomics. */
  isAuthenticated?: boolean;
  /** When true (Not-Found boundary or test driver), shows the restore-failed toast and clears state. */
  restoreFailed?: boolean;
  children?: React.ReactNode;
}

export function LastPageProvider({
  isAuthenticated = true,
  restoreFailed = false,
  children,
}: LastPageProviderProps): React.ReactElement {
  const router = useRouter();
  const [toast, setToast] = useState<string | null>(null);

  const onRestoreFailed = useCallback(() => {
    setToast(LAST_PAGE_RESTORE_FAILED_COPY);
    clearLastPage();
    try {
      window.sessionStorage.removeItem(LAST_PAGE_RESTORING_KEY);
    } catch {
      /* noop */
    }
  }, []);

  useLastPageTracker({ isAuthenticated });
  useRestoreLastPage({ isAuthenticated, onRestoreFailed });

  // External 404 signal (from not-found.tsx via prop or router state).
  useEffect(() => {
    if (!restoreFailed) return;
    setToast(LAST_PAGE_RESTORE_FAILED_COPY);
    clearLastPage();
    try {
      window.sessionStorage.removeItem(LAST_PAGE_RESTORING_KEY);
    } catch {
      /* noop */
    }
    try {
      router.replace("/");
    } catch {
      /* noop */
    }
  }, [restoreFailed, router]);

  return (
    <>
      {children}
      {toast !== null ? (
        <div
          role="status"
          aria-live="polite"
          data-testid="lastpage-restore-failed-toast"
          className="fixed bottom-4 left-1/2 -translate-x-1/2 rounded-md bg-[--color-surface-elevated] px-4 py-2 text-sm shadow-md"
        >
          {toast}
        </div>
      ) : null}
    </>
  );
}
