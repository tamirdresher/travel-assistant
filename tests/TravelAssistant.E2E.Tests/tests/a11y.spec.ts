import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import * as fs from 'node:fs';
import * as path from 'node:path';

/**
 * QA-3 acceptance: axe-core in Playwright, baseline file committed,
 * fail on NEW violations only.
 *
 * The baseline lives in ../a11y-baselines/<route-slug>.json and holds
 * the set of currently-known violation IDs we accept for that route.
 * Any violation ID not in the baseline fails the test.
 *
 * To refresh a baseline after a deliberate fix or regression-accept:
 *   E2E_A11Y_UPDATE_BASELINE=1 npx playwright test --grep @a11y
 * then commit the diff (XD-4 checklist link required in the PR body).
 */
const ROUTES: { name: string; path: string }[] = [
  { name: 'home', path: '/' },
  { name: 'chat', path: '/chat' },
  { name: 'login', path: '/login' },
];

for (const route of ROUTES) {
  test(`@a11y ${route.name} has no NEW axe violations`, async ({ page }) => {
    await page.goto(route.path);
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    const baselineFile = path.join(__dirname, '..', 'a11y-baselines', `${route.name}.json`);
    const update = process.env.E2E_A11Y_UPDATE_BASELINE === '1';

    const currentIds = results.violations.map(v => v.id).sort();

    if (update || !fs.existsSync(baselineFile)) {
      fs.writeFileSync(baselineFile, JSON.stringify({ allowed: currentIds }, null, 2) + '\n');
      test.info().annotations.push({ type: 'baseline', description: `wrote ${baselineFile}` });
      return;
    }

    const baseline: { allowed: string[] } = JSON.parse(fs.readFileSync(baselineFile, 'utf8'));
    const allowed = new Set(baseline.allowed);
    const newViolations = results.violations.filter(v => !allowed.has(v.id));

    if (newViolations.length > 0) {
      const detail = newViolations
        .map(v => `  - ${v.id} (${v.impact ?? 'unknown'}): ${v.help} → ${v.helpUrl}`)
        .join('\n');
      throw new Error(
        `${newViolations.length} NEW a11y violation(s) on ${route.path}:\n${detail}\n\n` +
        `If intentional: run with E2E_A11Y_UPDATE_BASELINE=1 and link XD-4 checklist in the PR.`,
      );
    }

    expect(newViolations).toHaveLength(0);
  });
}
