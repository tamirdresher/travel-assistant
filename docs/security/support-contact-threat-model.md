# Support Contact Threat Model

**Scope:** `/support` contact form submission flow, from browser form entry to server validation, storage, support review, and deletion.

**Data collected:** name, email, message, optional trip ID, consent state, anti-abuse fields (`website` honeypot and form-fill timestamp), IP address, user agent, and request metadata needed for abuse controls.

## STRIDE risks and mitigations

| STRIDE | Risk | Mitigations in place after support-form security PRs land | Residual risk |
| --- | --- | --- | --- |
| Spoofing | A bot or attacker submits as another person by using their email address. | Required consent checkbox, server-side validation, per-IP rate limits, form-fill timing check, honeypot field named `website`, correlation IDs, and support replies only to the supplied email. | Email ownership is not proven. Support should avoid sharing account or trip details until identity is verified through an authenticated channel. |
| Tampering | A user changes hidden fields, bypasses client checks, injects script/content, or alters an optional trip ID. | Server treats all client fields as untrusted, repeats validation, rejects populated honeypot, requires minimum fill time, caps field lengths, normalizes input, encodes output, and validates trip ID format/ownership before use. | Plain text messages can still contain misleading links or social-engineering content for support staff. |
| Repudiation | A submitter denies sending a request, or staff cannot trace what happened. | Append-only audit events include timestamp, correlation ID, outcome, rate-limit decision, and hashed or minimized identifiers. Success and rejection paths are logged without message body content. | Audit data proves a request event, not the real-world identity of the sender. |
| Information disclosure | Contact messages expose personal data, trip details, or secrets to logs, telemetry, browser history, or third parties. | Privacy notice explains collection, purpose, 90-day retention, no marketing use, and no third-party sharing without consent. Logs and telemetry redact message bodies and raw email where possible. Security headers reduce framing and referrer leakage. `/privacy` is linked from the form. | Users may still paste sensitive data. Support macros and staff training must keep replies minimal and avoid forwarding data outside approved tools. |
| Denial of service | Automated submissions fill queues, storage, or support capacity. | Rate limits, body-size limits, field-length limits, honeypot rejection, fill-time checks, strict content type, and server-side validation fail closed before persistence. | Distributed low-rate abuse can still create noise and may need CAPTCHA escalation or queue throttling. |
| Elevation of privilege | A contact request is used to change account or trip state without authorization. | Support flow is informational only. Any account, booking, or trip changes require authenticated workflows and authorization checks outside the contact form. | Manual support processes can bypass technical controls if runbooks are unclear. High-risk requests need a separate verification checklist. |

## Privacy and retention controls

- Use the contact data only to respond to the request.
- Keep submissions for 90 days, then delete them unless the request is still open.
- Do not use contact submissions for marketing.
- Do not share contact submissions with third parties without consent, unless required for security, legal, or service-operation needs covered by the privacy policy.
- Keep anti-abuse metadata only as long as needed for abuse investigation and the 90-day support window.

## Open follow-ups

- Confirm Worf's server PR enforces `website` honeypot rejection and a greater-than-2-second fill-time check.
- Add CAPTCHA escalation if production abuse exceeds support capacity.
- Add a support runbook for identity verification before discussing account, booking, payment, or trip-specific details.