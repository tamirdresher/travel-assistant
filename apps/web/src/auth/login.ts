/**
 * RM-003: login client. Wires the checkbox state into POST /api/auth/login as
 * `rememberMe: boolean`, persists the *choice* via the typed setter, and routes
 * the returned refresh token to local- or session-storage per the choice.
 */
import { setRememberMeChoice } from './rememberMeStorage';
import { persistRefreshToken } from './tokenStorage';

export interface LoginInput {
  email: string;
  password: string;
  rememberMe: boolean; // default false — set by the checkbox; unchecked when absent
}

export interface LoginApiResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresInSeconds: number;
  refreshTokenExpiresInSeconds: number;
  rememberMe: boolean;
}

export async function login(
  input: LoginInput,
  fetchImpl: typeof fetch = fetch,
): Promise<LoginApiResponse> {
  const res = await fetchImpl('/api/auth/login', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      email: input.email,
      password: input.password,
      rememberMe: input.rememberMe,
    }),
  });
  if (!res.ok) throw new Error(`login failed: ${res.status}`);
  const body = (await res.json()) as LoginApiResponse;

  // Persist the *choice* (not the token) via the sanctioned setter.
  setRememberMeChoice(input.rememberMe);

  // Route the refresh token based on the choice.
  persistRefreshToken({
    token: body.refreshToken,
    expiresAt: Date.now() + body.refreshTokenExpiresInSeconds * 1000,
    rememberMe: input.rememberMe,
  });

  return body;
}
