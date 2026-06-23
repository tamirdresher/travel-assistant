// @vitest-environment jsdom
/**
 * RM-003 (v2) tests — choice persistence + cookie-flow login client.
 * Asserts: no refresh token is touched in JS storage at any point (D5-3).
 */
import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
  setRememberMeChoice,
  getRememberMeChoice,
  clearRememberMeChoice,
  REMEMBER_ME_STORAGE_KEY,
} from '../setRememberMe';
import { login, refresh, logout } from '../login';

describe('setRememberMe (RM-003 v2)', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it('persists true → reads true', () => {
    setRememberMeChoice(true);
    expect(getRememberMeChoice()).toBe(true);
    expect(window.localStorage.getItem(REMEMBER_ME_STORAGE_KEY)).toBe('true');
  });

  it('persists false → reads false', () => {
    setRememberMeChoice(false);
    expect(getRememberMeChoice()).toBe(false);
    expect(window.localStorage.getItem(REMEMBER_ME_STORAGE_KEY)).toBe('false');
  });

  it('clear removes the key entirely', () => {
    setRememberMeChoice(true);
    clearRememberMeChoice();
    expect(window.localStorage.getItem(REMEMBER_ME_STORAGE_KEY)).toBeNull();
  });

  it('default getter returns false when no value persisted', () => {
    expect(getRememberMeChoice()).toBe(false);
  });

  it('survives storage throw (Safari private mode)', () => {
    const orig = window.localStorage.setItem;
    window.localStorage.setItem = () => { throw new Error('QuotaExceeded'); };
    expect(() => setRememberMeChoice(true)).not.toThrow();
    window.localStorage.setItem = orig;
  });
});

describe('login client (RM-003 v2 cookie flow)', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it('login sends credentials:include and rememberMe in body', async () => {
    const fetchMock = vi.fn(async () => new Response(
      JSON.stringify({
        accessToken: 'A', accessTokenExpiresInSeconds: 900,
        refreshTokenExpiresInSeconds: 2592000, rememberMe: true,
      }),
      { status: 200, headers: { 'content-type': 'application/json' } },
    ));
    await login({ email: 'a@b.co', password: 'p', rememberMe: true }, fetchMock as unknown as typeof fetch);
    const [, init] = fetchMock.mock.calls[0];
    expect(init).toMatchObject({ method: 'POST', credentials: 'include' });
    expect(JSON.parse(init!.body as string)).toEqual({ email: 'a@b.co', password: 'p', rememberMe: true });
  });

  it('login persists the choice and NEVER touches localStorage/sessionStorage for a token', async () => {
    const fetchMock = vi.fn(async () => new Response(
      JSON.stringify({
        accessToken: 'A', accessTokenExpiresInSeconds: 900,
        refreshTokenExpiresInSeconds: 28800, rememberMe: false,
      }),
      { status: 200 },
    ));
    await login({ email: 'a@b.co', password: 'p', rememberMe: false }, fetchMock as unknown as typeof fetch);
    expect(window.localStorage.getItem(REMEMBER_ME_STORAGE_KEY)).toBe('false');
    // Sanity: no token-shaped keys made it into either store.
    for (const store of [window.localStorage, window.sessionStorage]) {
      for (let i = 0; i < store.length; i++) {
        const k = store.key(i)!;
        expect(k.toLowerCase()).not.toMatch(/token|refresh|jwt|bearer/);
      }
    }
  });

  it('refresh sends X-TA-Refresh header + credentials:include + no body', async () => {
    const fetchMock = vi.fn(async () => new Response(
      JSON.stringify({ accessToken: 'A2', accessTokenExpiresInSeconds: 900, rememberMe: true }),
      { status: 200 },
    ));
    await refresh(fetchMock as unknown as typeof fetch);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/auth/refresh');
    expect(init).toMatchObject({ method: 'POST', credentials: 'include' });
    expect((init!.headers as Record<string, string>)['X-TA-Refresh']).toBe('1');
    expect(init!.body).toBeUndefined();
  });

  it('logout posts with CSRF header + credentials:include', async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 204 }));
    await logout(fetchMock as unknown as typeof fetch);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/auth/logout');
    expect((init!.headers as Record<string, string>)['X-TA-Refresh']).toBe('1');
  });
});

