// LP-002: shared types + storage keys for "remember last viewed page".
// Keys use dot-form (matches RM-005 ta.auth.rememberMe; intentionally NOT
// DM-002's colon-form ta:theme:v1 — privacy/nav keys are a separate family).

export const LAST_PAGE_STORAGE_KEY = "ta.nav.lastPage.v1";
export const PRIVACY_REMEMBER_LAST_PAGE_KEY = "ta.privacy.rememberLastPage";

/** Max accepted JSON byte length for a stored last-page record. */
export const LAST_PAGE_MAX_BYTES = 2048;

export interface LastPageRecord {
  pathname: string;
  search: string;
  ts: number;
}

export function isLastPageRecord(value: unknown): value is LastPageRecord {
  if (typeof value !== "object" || value === null) return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.pathname === "string" &&
    typeof v.search === "string" &&
    typeof v.ts === "number" &&
    Number.isFinite(v.ts)
  );
}
