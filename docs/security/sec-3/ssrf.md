# SEC-3 — SSRF & Outbound URL Allowlist

**Owner:** Hicks · **Applies to:** every outbound HTTP call where the URL
or any part of it derives from user input, third-party content, or LLM
tool arguments. This includes "fetch the hotel page" / "look up airline
status" / web-search-tool style features.

## Threat model

A Server-Side Request Forgery (SSRF) lets an attacker make the Travel
Assistant's backend issue HTTP requests to internal targets. In Azure
Container Apps the realistic targets are:

| Target | Why it hurts |
|--------|--------------|
| Azure IMDS (`169.254.169.254`) | Managed-identity token exfiltration → Key Vault access |
| Container App internal endpoints | Cross-service abuse, sidecar metadata |
| Other tenants' private endpoints (DNS-resolved into RFC1918) | Lateral movement |
| Localhost / loopback (`127.0.0.0/8`, `::1`) | Debug ports, dev-only endpoints |
| Link-local / metadata IPv6 (`fe80::/10`, `fd00::/8`) | Same as IMDS via IPv6 |
| AWS / GCP metadata (defense in depth even on Azure) | Future migration / hybrid risk |

## Controls

### C1 — Allowlist (HARD)
Every outbound URL must match an entry in
`docs/security/sec-3/url-allowlist.yaml`. Domains only. No regex
wildcards beyond a single leading `*.`. Default-deny.

### C2 — SSRF guard (HARD)
The `SsrfGuardingHttpHandler` runs after the allowlist and before the
socket connect. It:
1. Re-parses the URL — rejects non-`https` (except documented exceptions).
2. Rejects `userinfo@host` syntax.
3. Rejects IP-literal hosts unless explicitly allowed.
4. Resolves the host to all A / AAAA records and rejects any result in
   the blocked-CIDR set (loopback, link-local, IMDS, RFC1918, ULA, IPv4
   broadcast, IPv4-mapped IPv6, IPv6 unspecified, multicast).
5. Caps follow-up redirects at 3 and re-validates each redirect target
   through C1+C2.
6. Caps response body at 4 MB and total wall-clock at 10 s.

### C3 — No DNS rebinding
The resolved IP is pinned for the connection. The handler opens the
socket against the resolved address, not against the original host
string. Re-resolution between check and connect is the classic DNS
rebinding hole; pinning closes it.

### C4 — Logging
Every blocked attempt logs `ssrf.blocked` with `{host, reason}`. Never
log the full URL (may contain user PII or third-party tokens).

## Documented exceptions (none today)
- `http://` is forbidden everywhere.
- `IsLocalhostAllowed` is `true` only in `Development`; the
  `ProductionGuard` (SEC-5) refuses to start if it's true in any
  other environment.

## Acceptance criteria (from SEC-3)
- [x] Any "fetch a hotel/airline page" feature uses an outbound
      allowlist + SSRF guard (`url-allowlist.yaml` +
      `SsrfGuardingHttpHandler.cs`)
- [x] Documented in `/docs/security/ssrf.md` — this file (path is
      `/docs/security/sec-3/ssrf.md` for grouping; a stub
      `/docs/security/ssrf.md` redirects here)
