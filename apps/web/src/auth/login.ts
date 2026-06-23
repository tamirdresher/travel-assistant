/**
 * RM-003 (v2): login client.
 *
 * Sends `rememberMe: boolean` to POST /api/auth/login. The server sets the
 * refresh-token httpOnly cookie (`ta_rt`) on the response — client never
 * sees or stores the refresh token (D5-3). We use `credentials: "include"`
 * so the browser keeps the cookie on the auth origin.
 *
 * Only the access token is returned in the response body — held in memory
 * by the caller (out of scope for this module).
 */
import { setRememberMeChoice } from './setRememberMe';

export interface LoginInput {
  email: string;
  password: string;
  rememberMe: boolean;
}

export interface LoginApiResponse {
  accessToken: string;
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
    credentials: 'include',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      email: input.email,
      password: input.password,
      rememberMe: input.rememberMe,
    }),
  });
  if (!res.ok) throw new Error(`login failed: ${res.status}`);
  const body = (await res.json()) as LoginApiResponse;
  setRememberMeChoice(input.rememberMe);
  return body;
}

/**
 * RM-003 (v2): refresh client. Sends the CSRF-defense header `X-TA-Refresh: 1`
 * (§5) and relies on the httpOnly cookie for the actual token. The server
 * rotates the cookie on success and revokes the family on reuse (D5-4).
 */
export async function refresh(
  fetchImpl: typeof fetch = fetch,
): Promise<{ accessToken: string; accessTokenExpiresInSeconds: number; rememberMe: boolean }> {
  const res = await fetchImpl('/api/auth/refresh', {
    method: 'POST',
    credentials: 'include',
    headers: { 'X-TA-Refresh': '1' },
  });
  if (!res.ok) throw new Error(`refresh failed: ${res.status}`);
  return res.json();
}

/**
 * RM-003 (v2): logout. Server clears the cookie and revokes the family.
 */
export async function logout(fetchImpl: typeof fetch = fetch): Promise<void> {
  await fetchImpl('/api/auth/logout', {
    method: 'POST',
    credentials: 'include',
    headers: { 'X-TA-Refresh': '1' },
  });
}
