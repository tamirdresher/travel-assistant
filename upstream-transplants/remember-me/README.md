# remember-me transplant bundle

Artifacts infra owns that must travel with the feature when transplanted upstream to `bradygaster/squad-with-aspire`.

## Contents

- `remember-me-gate.yml` — required-checks gate enforcing the RM-005 contract.

## RM-005 numeric reference

| Knob               | Value      | Where                                              |
|--------------------|------------|----------------------------------------------------|
| Short TTL (sliding)| 28800s     | KV: `Auth--RefreshToken--TtlSeconds`               |
| Long TTL (sliding) | 2592000s   | KV: `Auth--RefreshToken--LongTtlSeconds`           |
| Absolute cap       | 7776000s   | KV: `Auth--RefreshToken--AbsoluteCapSeconds`       |
| Cookie name        | `ta_rt`    | KV: `Auth--Cookie--Name`                           |
| Cookie path        | `/api/auth`| KV: `Auth--Cookie--Path`                           |
| Cookie SameSite    | `Lax`      | KV: `Auth--Cookie--SameSite`                       |
| Cookie Secure      | `true`     | enforced at runtime by API (not a KV value)        |
| CSRF header        | `X-TA-Refresh: 1` | enforced by API on /refresh and /logout      |

## Pipeline secret

`AZURE_REFRESH_TOKEN_SIGNING_KEY` — generate with `openssl rand -base64 64`, store as a GitHub Actions org secret, pass in as env var to the deploy step. Never committed.
