// LP-002: Sanctioned writer for the last-viewed-page record.
//
// Semgrep rule (to be authored alongside LP-001) will forbid any direct
// localStorage.setItem("ta.nav.lastPage.v1", …) — all writes MUST go through
// this module. Mirrors the DM-002 setTheme.ts and RM-005 setRememberMe.ts
// patterns so the same lint pattern catches a third storage family without
// regex divergence.

import {
  LAST_PAGE_STORAGE_KEY,
  LAST_PAGE_MAX_BYTES,
  PRIVACY_REMEMBER_LAST_PAGE_KEY,
  isLastPageRecord,
  type LastPageRecord,
} from "./types";

function safeLocalStorage(): Storage | null {
  try {
    if (typeof window === "undefined") return null;
    return window.localStorage;
  } catch {
    return null;
  }
}

/** Default: opt-IN. Returns false only when the user explicitly disabled. */
export function isRememberLastPageEnabled(): boolean {
  const ls = safeLocalStorage();
  if (!ls) return true;
  try {
    const raw = ls.getItem(PRIVACY_REMEMBER_LAST_PAGE_KEY);
    if (raw === null) return true;
    return raw !== "false";
  } catch {
    return true;
  }
}

/** Persist the current page. No-op when opted out (and clears any residue). */
export function setLastPage(pathname: string, search: string): void {
  const ls = safeLocalStorage();
  if (!ls) return;
  if (typeof pathname !== "string") return;
  const safeSearch = typeof search === "string" ? search : "";

  if (!isRememberLastPageEnabled()) {
    clearLastPage();
    return;
  }

  const record: LastPageRecord = {
    pathname,
    search: safeSearch,
    ts: Date.now(),
  };
  let json: string;
  try {
    json = JSON.stringify(record);
  } catch {
    return;
  }
  if (byteLength(json) > LAST_PAGE_MAX_BYTES) return;

  try {
    ls.setItem(LAST_PAGE_STORAGE_KEY, json);
  } catch {
    // Safari private mode, quota exceeded, etc. — silently drop.
  }
}

export function getLastPage(): LastPageRecord | null {
  const ls = safeLocalStorage();
  if (!ls) return null;

  let raw: string | null;
  try {
    raw = ls.getItem(LAST_PAGE_STORAGE_KEY);
  } catch {
    return null;
  }
  if (raw === null) return null;
  if (byteLength(raw) > LAST_PAGE_MAX_BYTES) {
    clearLastPage();
    return null;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    clearLastPage();
    return null;
  }
  if (!isLastPageRecord(parsed)) {
    clearLastPage();
    return null;
  }
  return parsed;
}

export function clearLastPage(): void {
  const ls = safeLocalStorage();
  if (!ls) return;
  try {
    ls.removeItem(LAST_PAGE_STORAGE_KEY);
  } catch {
    // no-op
  }
}

/** Write the privacy opt-out flag. Also clears stored page when disabling. */
export function setRememberLastPagePreference(enabled: boolean): void {
  const ls = safeLocalStorage();
  if (!ls) return;
  try {
    ls.setItem(PRIVACY_REMEMBER_LAST_PAGE_KEY, enabled ? "true" : "false");
  } catch {
    return;
  }
  if (!enabled) clearLastPage();
}

function byteLength(s: string): number {
  if (typeof TextEncoder !== "undefined") {
    return new TextEncoder().encode(s).length;
  }
  // Fallback for ancient runtimes (jsdom always has TextEncoder).
  return unescape(encodeURIComponent(s)).length;
}
