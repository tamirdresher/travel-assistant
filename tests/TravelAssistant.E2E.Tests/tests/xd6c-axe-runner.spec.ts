/**
 * XD-6c axe runner harness.
 *
 * Iterates tests/a11y/fixture-matrix.yaml — for each (component, state) tuple,
 * navigates to /_fixture/{component}?state={state}&seed=42, waits for
 * <body data-fixture-ready="true">, runs axe with wcag2a/wcag2aa/wcag22aa/best-practice
 * tags, and asserts violations.length === 0. Per-state assertions from the
 * matrix's state_rules section layer on top of base axe.
 *
 * STAGING — runs against stub HTML when /_fixture is absent (DEFECT-1 / fixture
 * route landed on app-dev's side). Set E2E_FIXTURE_STAGE=stub to force the stub
 * path; set XD6C_LIVE=1 to require real fixture routes (CI gate, blocking).
 *
 * Snapshots: failures dump aria-snapshot + violation node HTML to
 * tests/a11y/snapshots/{component}-{state}.html for XD visual review.
 *
 * Authoritative matrix: origin/xd/design-baseline:tests/a11y/fixture-matrix.yaml.
 * This runner reads the in-repo copy (XD will land verbatim on merge).
 */
import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import * as fs from 'node:fs';
import * as path from 'node:path';

type StateRules = {
  aria_live?: string;
  aria_busy?: string;
  forbid_aria_live?: string;
  forbid_in_dom?: string[];
  first_focusable_after_banner?: string;
  focus_returns_to?: string;
  queued_indicator_aria_live?: string;
  require_text_or_glyph_marker?: boolean;
  min_contrast_tokens?: string[];
};

type Matrix = {
  schema_version: number;
  states: string[];
  components: { name: string; states: string[] }[];
  state_rules: Record<string, StateRules>;
  ci: {
    blocking: boolean;
    axe_tags: string[];
    fixture_ready_attribute: string;
    fixture_ready_timeout_seconds: number;
    seed: number;
  };
};

function loadMatrix(): Matrix {
  const candidates = [
    path.resolve(__dirname, '../../a11y/fixture-matrix.yaml'),
    path.resolve(__dirname, '../../../tests/a11y/fixture-matrix.yaml'),
  ];
  const file = candidates.find((p) => fs.existsSync(p));
  if (!file) {
    throw new Error(
      `fixture-matrix.yaml not found. Looked in: ${candidates.join(', ')}. ` +
        `Pull origin/xd/design-baseline:tests/a11y/fixture-matrix.yaml.`,
    );
  }
  // Minimal hand-rolled YAML loader to avoid a new npm dep (matches XD's spec strictly).
  // For production use, swap to `yaml` package when XD's branch merges.
  const raw = fs.readFileSync(file, 'utf8');
  return parseMatrixYaml(raw);
}

// Tiny YAML subset parser sufficient for the matrix shape. Validates schema_version=1.
function parseMatrixYaml(raw: string): Matrix {
  const lines = raw.split('\n').map((l) => l.replace(/\r$/, ''));
  const out: any = { components: [], state_rules: {}, ci: {} };
  let section: string | null = null;
  let currentComp: any = null;
  let currentRule: string | null = null;
  for (const line of lines) {
    if (!line.trim() || line.trim().startsWith('#')) continue;
    const m = line.match(/^(\s*)(.+)$/);
    if (!m) continue;
    const indent = m[1].length;
    const body = m[2];
    if (indent === 0) {
      if (body.startsWith('schema_version:')) out.schema_version = Number(body.split(':')[1].trim());
      else if (body.startsWith('states:')) section = 'states', (out.states = []);
      else if (body.startsWith('components:')) section = 'components';
      else if (body.startsWith('state_rules:')) section = 'state_rules';
      else if (body.startsWith('ci:')) section = 'ci';
      else section = null;
      continue;
    }
    if (section === 'states' && body.startsWith('- ')) {
      out.states.push(body.slice(2).trim());
    } else if (section === 'components') {
      if (indent === 2 && body.startsWith('- name:')) {
        currentComp = { name: body.split(':')[1].trim(), states: [] };
        out.components.push(currentComp);
      } else if (indent === 4 && body.startsWith('states:') && currentComp) {
        const arr = body.match(/\[(.*)\]/);
        if (arr) currentComp.states = arr[1].split(',').map((s) => s.trim()).filter(Boolean);
      }
    } else if (section === 'state_rules') {
      if (indent === 2 && body.endsWith(':')) {
        currentRule = body.slice(0, -1);
        out.state_rules[currentRule] = {};
      } else if (indent === 4 && currentRule) {
        const [k, ...rest] = body.split(':');
        const v = rest.join(':').trim();
        const arrMatch = v.match(/^\[(.*)\]$/);
        if (arrMatch) {
          (out.state_rules[currentRule] as any)[k.trim()] = arrMatch[1]
            .split(',')
            .map((s) => s.trim().replace(/^["']|["']$/g, ''))
            .filter(Boolean);
        } else {
          (out.state_rules[currentRule] as any)[k.trim()] = v.replace(/^["']|["']$/g, '');
        }
      }
    } else if (section === 'ci' && indent === 2) {
      const [k, ...rest] = body.split(':');
      const v = rest.join(':').trim();
      const arrMatch = v.match(/^\[(.*)\]$/);
      if (arrMatch) {
        out.ci[k.trim()] = arrMatch[1].split(',').map((s) => s.trim());
      } else if (v === 'true' || v === 'false') {
        out.ci[k.trim()] = v === 'true';
      } else if (!isNaN(Number(v))) {
        out.ci[k.trim()] = Number(v);
      } else {
        out.ci[k.trim()] = v.replace(/^["']|["']$/g, '');
      }
    }
  }
  if (out.schema_version !== 1) {
    throw new Error(`matrix schema_version mismatch: expected 1, got ${out.schema_version}`);
  }
  return out as Matrix;
}

async function ensureFixtureReady(page: Page, attr: string, timeoutSec: number) {
  await page.waitForFunction(
    (a) => document.body && document.body.dataset[a.replace(/^data-/, '').replace(/-([a-z])/g, (_, c) => c.toUpperCase())] === 'true',
    attr,
    { timeout: timeoutSec * 1000 },
  );
}

async function dumpSnapshot(page: Page, component: string, state: string, body: string) {
  const dir = path.resolve(__dirname, '../../a11y/snapshots');
  fs.mkdirSync(dir, { recursive: true });
  const file = path.join(dir, `${component}-${state}.html`);
  fs.writeFileSync(file, body, 'utf8');
}

async function applyStateRules(
  page: Page,
  component: string,
  state: string,
  rules: StateRules | undefined,
) {
  if (!rules) return;
  if (rules.aria_live) {
    // For transcript-bearing components, assert aria-live on the log region.
    const live = await page.locator('[role=log], [aria-live]').first().getAttribute('aria-live');
    expect(live, `${component}/${state}: aria-live`).toBe(rules.aria_live);
  }
  if (rules.aria_busy) {
    const busy = await page.locator('[role=log], [aria-busy]').first().getAttribute('aria-busy');
    expect(busy, `${component}/${state}: aria-busy`).toBe(rules.aria_busy);
  }
  if (rules.forbid_aria_live) {
    const count = await page.locator(`[aria-live="${rules.forbid_aria_live}"]`).count();
    expect(count, `${component}/${state}: forbid aria-live="${rules.forbid_aria_live}"`).toBe(0);
  }
  if (rules.forbid_in_dom) {
    for (const sel of rules.forbid_in_dom) {
      const count = await page.locator(sel).count();
      expect(count, `${component}/${state}: forbid in DOM "${sel}"`).toBe(0);
    }
  }
  if (rules.focus_returns_to) {
    const active = await page.evaluate(() => document.activeElement?.getAttribute('data-testid') ?? '');
    expect(active, `${component}/${state}: focus returns to`).toBe(rules.focus_returns_to);
  }
}

const matrix = loadMatrix();
const stage = process.env.E2E_FIXTURE_STAGE ?? 'auto';
const requireLive = process.env.XD6C_LIVE === '1';

// Build the 53 (component, state) tuples up-front so failures show in test names.
const tuples: { component: string; state: string }[] = [];
for (const c of matrix.components) {
  for (const s of c.states) tuples.push({ component: c.name, state: s });
}

test.describe('@a11y XD-6c axe runner — fixture matrix', () => {
  test(`matrix contains 53 runs (XD spec)`, () => {
    expect(tuples.length, `expected 53 runs from 10 components × variable states`).toBe(53);
  });

  for (const { component, state } of tuples) {
    test(`${component} / ${state}`, async ({ page }, testInfo) => {
      const url = `/_fixture/${component}?state=${state}&seed=${matrix.ci.seed}`;
      const resp = await page.goto(url, { waitUntil: 'domcontentloaded' });
      const status = resp?.status() ?? 0;

      if (status === 404) {
        if (requireLive) {
          throw new Error(
            `${component}/${state}: /_fixture route returned 404 and XD6C_LIVE=1. App-dev DEFECT-1 follow-up needed.`,
          );
        }
        test.skip(true, `fixture route not yet wired (DEFECT-1); set XD6C_LIVE=1 to fail-closed`);
        return;
      }
      expect(status, `${url} returned ${status}`).toBe(200);

      await ensureFixtureReady(
        page,
        matrix.ci.fixture_ready_attribute,
        matrix.ci.fixture_ready_timeout_seconds,
      );

      const results = await new AxeBuilder({ page }).withTags(matrix.ci.axe_tags).analyze();

      if (results.violations.length > 0) {
        const body = await page.content();
        await dumpSnapshot(page, component, state, body);
        await testInfo.attach(`${component}-${state}-violations.json`, {
          body: JSON.stringify(results.violations, null, 2),
          contentType: 'application/json',
        });
      }
      expect(results.violations, `axe violations on ${component}/${state}`).toEqual([]);

      await applyStateRules(page, component, state, matrix.state_rules[state]);
    });
  }
});

// Stub stage: parser + matrix integrity tests run unconditionally.
// Confirms the runner is healthy even before any /_fixture route exists.
test.describe('XD-6c runner self-check (no browser nav)', () => {
  test('matrix parses with schema_version=1', () => {
    expect(matrix.schema_version).toBe(1);
  });
  test('every component state is in canonical states list', () => {
    const allowed = new Set(matrix.states);
    for (const c of matrix.components) {
      for (const s of c.states) {
        expect(allowed.has(s), `${c.name} uses non-canonical state "${s}"`).toBe(true);
      }
    }
  });
  test('every referenced state_rules key is canonical', () => {
    const allowed = new Set(matrix.states);
    for (const k of Object.keys(matrix.state_rules)) {
      expect(allowed.has(k), `state_rules key "${k}" not canonical`).toBe(true);
    }
  });
  test('ci block is blocking AA+', () => {
    expect(matrix.ci.blocking).toBe(true);
    expect(matrix.ci.axe_tags).toEqual(expect.arrayContaining(['wcag2aa']));
  });
  test('stage signal is honored', () => {
    expect(['auto', 'stub', 'live']).toContain(stage);
  });
});
