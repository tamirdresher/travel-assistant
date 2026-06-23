# Travel Assistant — Security Hardening Plan

Owned by **security-hardening-squad**. Tracks SEC-1 through SEC-5 from the
ideation-research-planning-squad backlog (GH issues blocked by EMU).

| ID    | Topic                                  | Owner   | Status      |
|-------|----------------------------------------|---------|-------------|
| SEC-1 | Secrets policy + Key Vault from day 1  | Ripley  | In PR       |
| SEC-2 | Prompt injection + tool-call defense   | Hicks   | In PR       |
| SEC-3 | SSRF + URL allowlist                   | Hicks   | In PR       |
| SEC-4 | Privacy & data minimization (PII)      | Bishop  | In PR       |
| SEC-5 | Aspire dev-loop hardening for prod     | Vasquez | In PR       |

Each SEC item has its own folder under `docs/security/sec-*` with policy,
threat model, and (where applicable) implementation pointers into `src/`,
`infra/`, and `tests/`.

## Cross-squad dependencies
- **SEC-1 → azure-infrastructure-squad (INF-2):** Bicep module for Key Vault
  + managed identity is published in `infra/modules/keyvault.bicep`. Infra
  squad owns wiring it into the env-level templates.
- **SEC-4 → application-development-squad (APP-6):** PII field-level
  encryption requirements in `docs/security/sec-4/privacy.md`.
- **SEC-5 → azure-infrastructure-squad (INF-4):** Production guard
  endpoint exposed at `/health/prod-guard` — infra runs it as a deploy
  gate post-`azd up`.
