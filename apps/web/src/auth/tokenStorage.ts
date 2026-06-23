/**
 * RM-003: refresh-token placement driven by the user's "remember me" choice.
 *
 *   rememberMe === true  → localStorage (survives tab close, TTL ~30d per API)
 *   rememberMe === false → sessionStorage (cleared on tab close, TTL ~1d)
 *
 * The access token is held in memory by the auth client (not persisted here).
 */

const REFRESH_TOKEN_KEY = 'ta.auth.refreshToken' as const;

export interface StoredRefreshToken {
  token: string;
  expiresAt: number; // epoch ms
  rememberMe: boolean;
}

export function persistRefreshToken(token: StoredRefreshToken): void {
  if (typeof window === 'undefined') return;
  const target = token.rememberMe ? window.localStorage : window.sessionStorage;
  const other = token.rememberMe ? window.sessionStorage : window.localStorage;
  try {
    target.setItem(REFRESH_TOKEN_KEY, JSON.stringify(token));
    // Avoid drift between the two stores.
    other.removeItem(REFRESH_TOKEN_KEY);
  } catch {
    // best-effort
  }
}

export function readRefreshToken(): StoredRefreshToken | null {
  if (typeof window === 'undefined') return null;
  for (const store of [window.localStorage, window.sessionStorage]) {
    try {
      const raw = store.getItem(REFRESH_TOKEN_KEY);
      if (!raw) continue;
      const parsed = JSON.parse(raw) as StoredRefreshToken;
      if (parsed.expiresAt > Date.now()) return parsed;
    } catch {
      // ignore
    }
  }
  return null;
}

export function clearRefreshToken(): void {
  if (typeof window === 'undefined') return;
  try { window.localStorage.removeItem(REFRESH_TOKEN_KEY); } catch { /* */ }
  try { window.sessionStorage.removeItem(REFRESH_TOKEN_KEY); } catch { /* */ }
}
