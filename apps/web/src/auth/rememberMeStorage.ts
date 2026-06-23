/**
 * RM-003: typed storage for the user's "remember me" *choice*.
 *
 * Mirrors the DM-002 setTheme.ts pattern: a single sanctioned writer with three
 * literal branches so static analysis (Semgrep no-raw-auth-localstorage-write)
 * can prove no raw localStorage.setItem('ta.auth.rememberMe', …) calls exist.
 *
 * We persist only the *choice* (a boolean), never the token. Token placement
 * (sessionStorage vs localStorage) is driven by the choice — see tokenStorage.ts.
 */

export const REMEMBER_ME_STORAGE_KEY = 'ta.auth.rememberMe' as const;

export type RememberMeChoice = boolean;

export function getRememberMeChoice(): RememberMeChoice {
  if (typeof window === 'undefined') return false;
  try {
    const raw = window.localStorage.getItem(REMEMBER_ME_STORAGE_KEY);
    return raw === 'true';
  } catch {
    // Safari private mode, storage quota, etc.
    return false;
  }
}

export function setRememberMeChoice(choice: RememberMeChoice): void {
  if (typeof window === 'undefined') return;
  try {
    // Three literal branches keep the Semgrep allow-list happy.
    if (choice === true) {
      window.localStorage.setItem(REMEMBER_ME_STORAGE_KEY, 'true');
    } else if (choice === false) {
      window.localStorage.setItem(REMEMBER_ME_STORAGE_KEY, 'false');
    } else {
      window.localStorage.removeItem(REMEMBER_ME_STORAGE_KEY);
    }
  } catch {
    // Swallow — choice persistence is best-effort UX.
  }
}
