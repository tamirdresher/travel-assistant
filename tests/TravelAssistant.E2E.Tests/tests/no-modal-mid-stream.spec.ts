import { test, expect, Page } from '@playwright/test';

/**
 * QA-MUD-1: Asserts no MudBlazor MudDialog opens during `streaming` or
 * `pending-patch` chat states.
 *
 * Context: per XD ADR-0002 the app uses MudBlazor. MudDialog applies a
 * focus-trap that conflicts with aria-live regions used for token
 * streaming and pending itinerary patches. App-dev agreed to defer all
 * modal triggers until `turn.end`. This test enforces that contract.
 *
 * Detection strategy:
 *   - MudBlazor renders dialogs as <div class="mud-dialog ...">
 *     wrapped in a <div class="mud-dialog-container">. We poll for
 *     `.mud-dialog-container :visible` during the streaming window
 *     and fail if anything appears before the assistant reply settles.
 *   - We also assert no element with [aria-modal="true"] is attached
 *     while [aria-live] regions are mutating.
 *
 * If app-dev introduces a different modal primitive later (Drawer,
 * Popover acting as modal, custom backdrop), extend MODAL_SELECTORS.
 */

const MODAL_SELECTORS = [
  '.mud-dialog-container .mud-dialog',
  '[role="dialog"][aria-modal="true"]',
  '.mud-overlay',
];

async function assertNoModalsAttached(page: Page, context: string) {
  for (const sel of MODAL_SELECTORS) {
    const count = await page.locator(sel).count();
    expect(count, `Modal selector "${sel}" appeared during ${context}`).toBe(0);
  }
}

test('@smoke @mudblazor no MudDialog opens during streaming state', async ({ page }) => {
  await page.goto('/chat');
  await expect(page.getByTestId('chat-input')).toBeVisible();

  await assertNoModalsAttached(page, 'pre-send idle');

  await page.getByTestId('chat-input').fill('Plan me a 3-day trip to Tokyo.');
  await page.getByTestId('chat-send').click();

  // Streaming window: poll every 250ms for up to 10s while the assistant
  // message is being produced. Any modal appearing here is a defect.
  const deadline = Date.now() + 10_000;
  let streamingObserved = false;
  while (Date.now() < deadline) {
    const assistantMsg = page.getByTestId('chat-msg-assistant').first();
    const visible = await assistantMsg.isVisible().catch(() => false);
    if (visible) {
      streamingObserved = true;
      const text = await assistantMsg.textContent().catch(() => '');
      await assertNoModalsAttached(page, 'streaming window');
      // Once a meaningful reply is rendered, treat as turn.end and exit.
      if (text && text.length > 20) break;
    }
    await page.waitForTimeout(250);
  }

  expect(streamingObserved, 'assistant message never rendered within 10s').toBe(true);
});

test('@smoke @mudblazor no MudDialog opens during pending-patch state', async ({ page }) => {
  // Pending-patch fixture URL contract (QA-3b / XD-6c): app exposes
  // ?fixture=pending-patch to render the chat in a pre-set pending state.
  // If the fixture isn't wired yet, this test skips rather than failing.
  await page.goto('/chat?fixture=pending-patch');

  const pendingBanner = page.locator('[data-state="pending-patch"], [data-testid="pending-patch-banner"]').first();
  const present = await pendingBanner.isVisible({ timeout: 3_000 }).catch(() => false);
  test.skip(!present, 'pending-patch fixture not exposed by app yet (QA-3b/XD-6c pending APP-2)');

  await assertNoModalsAttached(page, 'pending-patch state');

  // Simulate the user pressing Undo / Confirm — neither should open a modal
  // while the patch is still pending. The interaction commit must
  // complete first (turn.end), then any confirmation modal may appear.
  const undoBtn = page.getByTestId('itinerary-patch-undo');
  if (await undoBtn.isVisible().catch(() => false)) {
    await undoBtn.click();
    await page.waitForTimeout(500);
    await assertNoModalsAttached(page, 'post-undo before turn.end');
  }
});
