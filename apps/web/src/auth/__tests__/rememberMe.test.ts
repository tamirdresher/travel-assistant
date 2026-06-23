import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
  getRememberMeChoice,
  setRememberMeChoice,
  REMEMBER_ME_STORAGE_KEY,
} from '../rememberMeStorage';
import { persistRefreshToken, readRefreshToken, clearRefreshToken } from '../tokenStorage';
import { login } from '../login';

beforeEach(() => {
  window.localStorage.clear();
  window.sessionStorage.clear();
});

describe('rememberMeStorage (RM-003)', () => {
  it('defaults to false when unset', () => {
    expect(getRememberMeChoice()).toBe(false);
  });

  it('round-trips true', () => {
    setRememberMeChoice(true);
    expect(window.localStorage.getItem(REMEMBER_ME_STORAGE_KEY)).toBe('true');
    expect(getRememberMeChoice()).toBe(true);
  });

  it('round-trips false', () => {
    setRememberMeChoice(false);
    expect(window.localStorage.getItem(REMEMBER_ME_STORAGE_KEY)).toBe('false');
    expect(getRememberMeChoice()).toBe(false);
  });
});

describe('tokenStorage (RM-003)', () => {
  it('routes to localStorage when rememberMe=true', () => {
    persistRefreshToken({ token: 't1', expiresAt: Date.now() + 60_000, rememberMe: true });
    expect(window.localStorage.getItem('ta.auth.refreshToken')).toContain('t1');
    expect(window.sessionStorage.getItem('ta.auth.refreshToken')).toBeNull();
  });

  it('routes to sessionStorage when rememberMe=false', () => {
    persistRefreshToken({ token: 't2', expiresAt: Date.now() + 60_000, rememberMe: false });
    expect(window.sessionStorage.getItem('ta.auth.refreshToken')).toContain('t2');
    expect(window.localStorage.getItem('ta.auth.refreshToken')).toBeNull();
  });

  it('readRefreshToken finds token regardless of store', () => {
    persistRefreshToken({ token: 't3', expiresAt: Date.now() + 60_000, rememberMe: true });
    expect(readRefreshToken()?.token).toBe('t3');
    clearRefreshToken();
    persistRefreshToken({ token: 't4', expiresAt: Date.now() + 60_000, rememberMe: false });
    expect(readRefreshToken()?.token).toBe('t4');
  });

  it('ignores expired tokens', () => {
    persistRefreshToken({ token: 'old', expiresAt: Date.now() - 1, rememberMe: true });
    expect(readRefreshToken()).toBeNull();
  });
});

describe('login (RM-003 end-to-end)', () => {
  it('posts rememberMe=true and persists choice + token in localStorage', async () => {
    const fakeFetch = vi.fn(async (_url, init) => {
      const body = JSON.parse((init as RequestInit).body as string);
      expect(body.rememberMe).toBe(true);
      return new Response(
        JSON.stringify({
          accessToken: 'at',
          refreshToken: 'rt-long',
          accessTokenExpiresInSeconds: 900,
          refreshTokenExpiresInSeconds: 2_592_000,
          rememberMe: true,
        }),
        { status: 200, headers: { 'content-type': 'application/json' } },
      );
    });
    await login({ email: 'a@b.c', password: 'pw', rememberMe: true }, fakeFetch as unknown as typeof fetch);
    expect(getRememberMeChoice()).toBe(true);
    expect(window.localStorage.getItem('ta.auth.refreshToken')).toContain('rt-long');
    expect(window.sessionStorage.getItem('ta.auth.refreshToken')).toBeNull();
  });

  it('defaults rememberMe to false and persists token in sessionStorage', async () => {
    const fakeFetch = vi.fn(async (_url, init) => {
      const body = JSON.parse((init as RequestInit).body as string);
      expect(body.rememberMe).toBe(false);
      return new Response(
        JSON.stringify({
          accessToken: 'at',
          refreshToken: 'rt-short',
          accessTokenExpiresInSeconds: 900,
          refreshTokenExpiresInSeconds: 86_400,
          rememberMe: false,
        }),
        { status: 200, headers: { 'content-type': 'application/json' } },
      );
    });
    await login({ email: 'a@b.c', password: 'pw', rememberMe: false }, fakeFetch as unknown as typeof fetch);
    expect(getRememberMeChoice()).toBe(false);
    expect(window.sessionStorage.getItem('ta.auth.refreshToken')).toContain('rt-short');
    expect(window.localStorage.getItem('ta.auth.refreshToken')).toBeNull();
  });
});
