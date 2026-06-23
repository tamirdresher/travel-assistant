## Summary
<!-- Briefly describe the changes in this PR -->

## Linked Issue
Closes #<!-- Issue number -->

## Changes
<!-- Describe the technical changes made -->
- 
- 
- 

## Testing
<!-- How were these changes tested? -->
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed

## Accessibility (XD-4) — required for any UI-touching PR
<!-- Drop-in from docs/design/a11y-checklist.md § "Per-PR checklist". Skip ONLY if PR has zero UI surface area. -->
- [ ] axe-core (`npm run test:a11y`) is green OR new violations are baselined with justification
- [ ] Keyboard-only flow walked: Tab/Shift+Tab order is sensible, Esc cancels mid-stream, Enter submits
- [ ] Screen-reader landmarks present (`<main>`, `<nav>`, headings monotonic)
- [ ] Streaming UI honors `prefers-reduced-motion`
- [ ] All seven screens × six states (see `docs/design/ia.md` §2) covered if PR touches chat surface
- [ ] If baseline updated: linked XD-4 checklist commit in `tests/a11y/baseline.json` diff

## Security review (Rai / SEC)
- [ ] No secrets, PII, or unredacted user content in logs
- [ ] If touching auth, prompts, or LLM tool calls: SEC checklist in `docs/security/sec-1/secrets-policy.md` consulted
- [ ] Rai 🟢 or 🟡 verdict (🔴 blocks merge)

## Checklist
- [ ] Code follows project style guidelines
- [ ] Documentation updated if applicable
- [ ] No breaking changes
- [ ] Related issue(s) linked
- [ ] Tested locally
