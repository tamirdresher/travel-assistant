import { test, expect } from '@playwright/test';

/**
 * REL-3 post-deploy smoke. Runs against $STAGING_BASE_URL after cd-staging.yml
 * deploys. Fails fast — total budget 60s. On failure the deploy workflow
 * auto-rolls back to the prior Container App revision.
 *
 * Three checks (folded into QA-1 per planning's request):
 *   1. /healthz returns 200
 *   2. /api/version returns 200 with non-empty `version` (APP-8 owns endpoint)
 *   3. Chat page loads and SignalR negotiates (handshake observed in network)
 */
const BASE = process.env.STAGING_BASE_URL ?? 'http://localhost:5000';

test.describe('@smoke post-deploy', () => {
  test('healthz returns 200', async ({ request }) => {
    const r = await request.get(`${BASE}/healthz`);
    expect(r.status(), `GET ${BASE}/healthz`).toBe(200);
  });

  test('api/version returns version field (APP-8)', async ({ request }) => {
    const r = await request.get(`${BASE}/api/version`);
    expect(r.status(), `GET ${BASE}/api/version`).toBe(200);
    const body = await r.json();
    expect(body.version, 'version field must be non-empty').toBeTruthy();
    expect(typeof body.version).toBe('string');
    expect(body.version.length).toBeGreaterThan(0);
  });

  test('chat page loads and SignalR negotiates', async ({ page }) => {
    const negotiateSeen = new Promise<void>((resolve) => {
      page.on('request', (req) => {
        const u = req.url();
        if (u.includes('/negotiate') || u.includes('signalr')) resolve();
      });
    });
    await page.goto(`${BASE}/chat`, { waitUntil: 'domcontentloaded' });
    await expect(page.getByTestId('chat-input')).toBeVisible({ timeout: 15_000 });
    await Promise.race([
      negotiateSeen,
      new Promise((_, rej) => setTimeout(() => rej(new Error('No SignalR negotiate within 10s')), 10_000)),
    ]);
  });
});
