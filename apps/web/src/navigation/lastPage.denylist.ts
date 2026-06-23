/**
 * LP-001 — Deny-list for "remember last viewed page".
 *
 * Single source of truth imported by:
 *   - apps/web/src/navigation/setLastPage.ts (LP-002, enforces on write)
 *   - useRestoreLastPage hook (LP-003, enforces on read)
 *   - .semgrep/last-viewed-page-storage.yml (LP-005, references this path)
 *   - apps/web/src/navigation/__tests__/lastPage.denylist.test.ts (LP-006)
 *
 * DO NOT add ad-hoc deny-list entries elsewhere. All additions go here,
 * and require XD ratification per docs/wireframes/last-page/restore-flow.md §11.
 */

/**
 * Pathname patterns — match against `location.pathname` (no search, no hash).
 * Anchored to start of pathname. Case-sensitive (URLs are).
 */
export const PATHNAME_DENY_PATTERNS: readonly RegExp[] = Object.freeze([
  /^\/login(\/|$)/,
  /^\/signup(\/|$)/,
  /^\/logout(\/|$)/,
  /^\/auth(\/|$)/,
  /^\/oauth\//,
  /^\/checkout\/confirm(\/|$)/,
  /^\/_next\//,
  /^\/api\//,
]);

/**
 * Search-param patterns — match against the raw search string including
 * the leading `?`. Case-INSENSITIVE (param keys are commonly mixed-case
 * in third-party redirect payloads).
 *
 * Any match → skip storage / drop on read.
 */
export const SEARCH_DENY_PATTERNS: readonly RegExp[] = Object.freeze([
  /[?&]token=/i,
  /[?&]code=/i,
  /[?&]id_token=/i,
  /[?&]state=/i,
  /[?&]access_token=/i,
  /[?&]refresh_token=/i,
  /[?&]session=/i,
  /[?&]otp=/i,
  /[?&]password=/i,
]);

/**
 * Same-origin relative-path validator. Reject anything that isn't a clean
 * relative pathname optionally followed by a search string.
 *
 * Rejects:
 *   - absolute URLs (https://...)
 *   - protocol-relative (//evil.com)
 *   - javascript: / data: / vbscript:
 *   - URL-encoded scheme/slash variants
 *   - CRLF (\r\n) and other control chars
 *   - paths > 1KB (DoS / storage bloat guard; LP-002 enforces a 2KB total
 *     payload cap separately)
 */
const SAFE_PATH = /^\/[A-Za-z0-9/_\-.~]*(\?[A-Za-z0-9=&_\-.%~+:,]*)?$/;
const MAX_PATH_LEN = 1024;

export function isSafeRelativePath(pathWithSearch: string): boolean {
  if (typeof pathWithSearch !== "string") return false;
  if (pathWithSearch.length === 0 || pathWithSearch.length > MAX_PATH_LEN) return false;
  if (pathWithSearch.startsWith("//")) return false;
  if (/[\r\n\t\0]/.test(pathWithSearch)) return false;
  // Reject percent-encoded slash sequences (defense-in-depth against
  // smuggled `%2F%2Fevil.com` payloads even though SAFE_PATH would catch them).
  if (/%2f%2f/i.test(pathWithSearch)) return false;
  return SAFE_PATH.test(pathWithSearch);
}

/**
 * Returns true if (pathname, search) MUST NOT be stored or restored.
 *
 * Called by:
 *   - setLastPage(pathname, search) — guard write
 *   - getLastPage() consumer in useRestoreLastPage — guard read
 *
 * Pre-condition: pathname is a same-origin relative pathname; search includes
 * the leading `?` (or is the empty string).
 */
export function isDenied(pathname: string, search: string): boolean {
  for (const re of PATHNAME_DENY_PATTERNS) {
    if (re.test(pathname)) return true;
  }
  if (search) {
    for (const re of SEARCH_DENY_PATTERNS) {
      if (re.test(search)) return true;
    }
  }
  return false;
}
