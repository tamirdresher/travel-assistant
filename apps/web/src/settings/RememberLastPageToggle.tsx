"use client";

// LP-004: Settings → Privacy toggle. Locked contracts (LP-001 §D4):
//   id="settings-remember-lastpage"     name="rememberLastPage"
//   data-testid="settings-remember-lastpage"
//   data-testid="settings-remember-lastpage-hint"  (helper text)
//   default ON; flipping OFF clears stored value synchronously (in
//   setRememberLastPagePreference -> clearLastPage).

import { useEffect, useState } from "react";
import {
  isRememberLastPageEnabled,
  setRememberLastPagePreference,
} from "../navigation/setLastPage";

const LABEL_TEXT = "Remember the last page I was on";
const HINT_TEXT =
  "When you reopen the app, we'll take you back to where you left off. " +
  "Turning this off also clears any saved page.";

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
    <div className="flex items-start gap-3 py-2">
      <input
        id="settings-remember-lastpage"
        name="rememberLastPage"
        data-testid="settings-remember-lastpage"
        type="checkbox"
        checked={enabled}
        onChange={onChange}
        disabled={!hydrated}
        aria-describedby="settings-remember-lastpage-hint"
        className="mt-1 size-5 min-h-[20px] min-w-[20px] focus-visible:outline-2 focus-visible:outline-[--color-focus-ring]"
        style={{ minWidth: 32, minHeight: 32 }}
      />
      <span className="flex flex-col">
        <label
          htmlFor="settings-remember-lastpage"
          className="font-medium"
        >
          {LABEL_TEXT}
        </label>
        <span
          id="settings-remember-lastpage-hint"
          data-testid="settings-remember-lastpage-hint"
          className="text-sm opacity-70"
        >
          {HINT_TEXT}
        </span>
      </span>
    </div>
  );
}
