import { test, expect } from '@playwright/test';

/**
 * QA-1 acceptance: one green Playwright smoke that boots against AppHost
 * and exercises a single chat turn end-to-end.
 *
 * Requires app-dev contracts: chat input has data-testid="chat-input",
 * send button has data-testid="chat-send", assistant message has
 * data-testid="chat-msg-assistant".
 */
test('@smoke user can send one chat message and see an assistant reply', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByTestId('chat-input')).toBeVisible();

  await page.getByTestId('chat-input').fill('Plan me a weekend trip to Lisbon.');
  await page.getByTestId('chat-send').click();

  const reply = page.getByTestId('chat-msg-assistant').first();
  await expect(reply).toBeVisible({ timeout: 15_000 });
  await expect(reply).not.toHaveText('');
});
