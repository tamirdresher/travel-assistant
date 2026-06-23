"use client";

// LP-004: Settings → Privacy toggle. Coordinates with XD (LP-001) for final
// copy; the label/description below are placeholders that match the spec.

import { useEffect, useState } from "react";
import {
  isRememberLastPageEnabled,
  setRememberLastPagePreference,
} from "../navigation/setLastPage";

export function RememberLastPageToggle(): React.ReactElement {
  const [enabled, setEnabled] = useState<boolean>(true);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    setEnabled(isRememberLastPageEnabled());
    setHydrated(true);
  }, []);

  function onChange(e: React.ChangeEvent<HTMLInputElement>) {
    const next = e.target.checked;
    setEnabled(next);
    setRememberLastPagePreference(next);
  }

  return (
    <label className="flex items-start gap-3 py-2">
      <input
        type="checkbox"
        checked={enabled}
        onChange={onChange}
        aria-describedby="remember-last-page-desc"
        disabled={!hydrated}
        className="mt-1"
      />
      <span className="flex flex-col">
        <span className="font-medium">Remember the last page I was on</span>
        <span id="remember-last-page-desc" className="text-sm opacity-70">
          When you reopen the app, take you back to where you left off. Turning
          this off also clears any saved page.
        </span>
      </span>
    </label>
  );
}
