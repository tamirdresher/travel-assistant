# Squad Decisions

## Active Decisions

### 2026-01 — Free-tier demo pivot (Phase 1)
- **Decision:** Ship a public demo on **Vercel Hobby (free)** as a Next.js fullstack app under `apps/web`, so anyone can use it without an account or credit card.
- **Why:** Original Bicep stack (Container Apps + Cosmos + Key Vault + Entra) requires a paid Azure subscription — not viable for "open the link in a meeting and try it" use case.
- **Scope:**
  - Chat UI + `/api/chat` edge route in Next.js.
  - **Mock mode** by default (deterministic replies) so URL works with zero config.
  - **Live mode** via any OpenAI-compatible endpoint (`OPENAI_API_KEY` + `OPENAI_BASE_URL` + `OPENAI_MODEL`) — Groq free tier recommended.
- **Preserved:** `.NET 9 API` in `src/` and `infra/bicep/` remain untouched as Phase 2 production target.
- **Docs:** `README.md` (root) + `docs/architecture.md`.
- **Decided by:** Ralph (Squad), per user directive "ralph go, free tiers only".

### 2026-05-27 — Squad formed for Travel Assistant project
- **Decision:** Stand up a Travel Assistant squad with 5 AI specialists, 1 human approver, plus Scribe and Ralph.
- **Project scope:** Travel assistant that helps customers book flights and vacations. Hosted on Azure. Issues managed in GitHub. Integrates with popular travel agency APIs.
- **Universe:** Israeli government figures (historical + current era) — names are identifiers only, no role-play.
- **Roster:** Ben-Gurion (Lead), Peres (Backend), Lapid (Frontend), Gantz (DevOps), Bennett (Tester), Eddy Ukstein (Human approver), Scribe, Ralph.
- **Customer approver:** Eddy Ukstein — must sign off on customer-facing decisions.
- **Status channel:** Teams chat "Squad: AI agents teams".
- **Decided by:** Copilot (user)

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
