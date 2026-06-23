/**
 * RM-003 (v2): sanctioned typed setter for the "remember me" UI choice.
 *
 * Storage scope is the CHOICE ONLY — never the refresh token.
 * Refresh token lives in an httpOnly Secure SameSite=Lax cookie (D5-3, binding).
 *
 * Semgrep rule `rememberme-must-use-typed-setter` excludes this file by path;
 * every other module must go through setRememberMeChoice().
 *
 * Three literal branches keep the allow-list narrow.
 */

export const REMEMBER_ME_STORAGE_KEY = 'ta.auth.rememberMe' as const;

export type RememberMeChoice = boolean;

export function getRememberMeChoice(): RememberMeChoice {
  if (typeof window === 'undefined') return false;
  try {
    const raw = window.localStorage.getItem(REMEMBER_ME_STORAGE_KEY);
    return raw === 'true';
  } catch {
    return false;
  }
}

export function setRememberMeChoice(choice: RememberMeChoice): void {
  if (typeof window === 'undefined') return;
  try {
    if (choice === true) {
      window.localStorage.setItem('ta.auth.rememberMe', 'true');
    } else if (choice === false) {
      window.localStorage.setItem('ta.auth.rememberMe', 'false');
    } else {
      window.localStorage.removeItem('ta.auth.rememberMe');
    }
  } catch {
    // best-effort; Safari private mode etc.
  }
}

export function clearRememberMeChoice(): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.removeItem('ta.auth.rememberMe');
  } catch {
    // ignore
  }
}
