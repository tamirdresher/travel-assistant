"use client";

// LP-003: convenience client wrapper. Mount in root layout once the auth
// context is available; it composes the tracker + restore hooks and exposes
// a minimal aria-live region for restore-failure toasts (role=status).

import { useCallback, useState } from "react";
import { useLastPageTracker } from "./useLastPageTracker";
import { useRestoreLastPage } from "./useRestoreLastPage";

export interface LastPageProviderProps {
  /** Replace with your auth-context boolean. Defaults to true for now. */
  isAuthenticated?: boolean;
  children?: React.ReactNode;
}

export function LastPageProvider({
  isAuthenticated = true,
  children,
}: LastPageProviderProps): React.ReactElement {
  const [toast, setToast] = useState<string | null>(null);
  const onToast = useCallback((msg: string) => {
    setToast(msg);
    window.setTimeout(() => setToast(null), 5000);
  }, []);

  useLastPageTracker({ isAuthenticated });
  useRestoreLastPage({ isAuthenticated, onToast });

  return (
    <>
      {children}
      <div role="status" aria-live="polite" className="sr-only">
        {toast ?? ""}
      </div>
    </>
  );
}
