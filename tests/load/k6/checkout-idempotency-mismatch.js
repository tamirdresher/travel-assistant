// k6 load test for checkout idempotency under body-mismatch traffic.
// Mandate: 150 RPS sustained with 10% body-mismatch payloads.
// SLO: P95 latency < 800ms, error rate < 0.5%, mismatch path returns 422 (not 5xx).
//
// Source: ideation-research-planning-squad (Aldo) merge-gate test mandate, addendum.
// Author: quality-testing-squad (Hockney)
//
// Run: k6 run --env BASE_URL=https://staging.example.com tests/load/k6/checkout-idempotency-mismatch.js

import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { randomString } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const TOKEN = __ENV.SYNTHETIC_TEST_TOKEN || 'test:k6-user';

const mismatchRate = new Rate('idempotency_mismatch_correct_422');
const replayRate = new Rate('idempotency_replay_correct');
const latencyMatch = new Trend('latency_match_ms');
const latencyMismatch = new Trend('latency_mismatch_ms');

export const options = {
  scenarios: {
    sustained_150rps: {
      executor: 'constant-arrival-rate',
      rate: 150,
      timeUnit: '1s',
      duration: '3m',
      preAllocatedVUs: 50,
      maxVUs: 200,
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<800'],
    http_req_failed: ['rate<0.005'],
    idempotency_mismatch_correct_422: ['rate>0.99'],
    idempotency_replay_correct: ['rate>0.99'],
  },
};

export default function () {
  const headers = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${TOKEN}`,
  };

  const key = randomString(24);
  const original = JSON.stringify({ sessionId: 's-' + randomString(8), paymentToken: 'tok_A' });

  const r1 = http.post(`${BASE_URL}/api/checkout/confirm`, original,
    { headers: { ...headers, 'Idempotency-Key': key } });
  latencyMatch.add(r1.timings.duration);

  if (Math.random() < 0.10) {
    const tampered = JSON.stringify({ sessionId: 's-' + randomString(8), paymentToken: 'tok_EVIL' });
    const r2 = http.post(`${BASE_URL}/api/checkout/confirm`, tampered,
      { headers: { ...headers, 'Idempotency-Key': key } });
    latencyMismatch.add(r2.timings.duration);
    mismatchRate.add(r2.status === 422);
    check(r2, {
      'mismatch returns 422': (r) => r.status === 422,
      'mismatch is not 5xx': (r) => r.status < 500,
    });
  } else {
    const r2 = http.post(`${BASE_URL}/api/checkout/confirm`, original,
      { headers: { ...headers, 'Idempotency-Key': key } });
    replayRate.add(r2.status === r1.status);
    check(r2, {
      'replay status matches first': (r) => r.status === r1.status,
    });
  }
}
