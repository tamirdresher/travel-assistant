// Playwright E2E for paymentBridge.ts postMessage contract (SEC-CHK-006 rules #1 + #3).
// Activates when sec/checkout-csp-idempotency-review lands apps/web/src/checkout/paymentBridge.ts.
// Cases 3 & 4 of the merge-gate test mandate from ideation-research-planning-squad.
//
// Author: quality-testing-squad (Hockney)

import { test, expect } from '@playwright/test';

const SKIP_REASON = 'Activates when paymentBridge.ts is wired into apps/web.';

test.describe.skip('paymentBridge — postMessage contract', () => {
  test('Case 3: rejects postMessage from non-allowlisted origin', async ({ page }) => {
    expect(SKIP_REASON).toBeTruthy();
    await page.goto('/checkout');

    const stateMutations: unknown[] = [];
    await page.exposeFunction('__qa_record_mutation', (m: unknown) => stateMutations.push(m));
    await page.addInitScript(() => {
      const orig = (window as any).__paymentResult;
      Object.defineProperty(window, '__paymentResult', {
        set(v) { (window as any).__qa_record_mutation?.(v); },
        get() { return orig; },
      });
    });

    await page.evaluate(() => {
      window.postMessage(
        { type: 'payment-result', orderId: 'ord-evil', amount: 100, currency: 'EUR', nonce: 'fake' },
        '*'
      );
    });

    await page.waitForTimeout(250);
    expect(stateMutations).toHaveLength(0);
  });

  test('Case 4: rejects postMessage with replayed (used) nonce', async ({ page }) => {
    expect(SKIP_REASON).toBeTruthy();
    await page.goto('/checkout');

    const nonce = await page.evaluate(() => (window as any).__paymentBridgeNonce);
    expect(typeof nonce).toBe('string');

    await page.evaluate((n) => {
      const target = document.querySelector('iframe[name="stripe-payment"]') as HTMLIFrameElement;
      target?.contentWindow?.postMessage(
        { type: 'payment-result', orderId: 'ord-1', amount: 100, currency: 'EUR', nonce: n },
        'https://js.stripe.com'
      );
    }, nonce);
    await page.waitForTimeout(100);
    const before = await page.evaluate(() => (window as any).__paymentResult);

    await page.evaluate((n) => {
      const target = document.querySelector('iframe[name="stripe-payment"]') as HTMLIFrameElement;
      target?.contentWindow?.postMessage(
        { type: 'payment-result', orderId: 'ord-2', amount: 200, currency: 'EUR', nonce: n },
        'https://js.stripe.com'
      );
    }, nonce);
    await page.waitForTimeout(100);
    const after = await page.evaluate(() => (window as any).__paymentResult);

    expect(after).toEqual(before);
  });
});
