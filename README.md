# ✈️ Travel Assistant

AI-powered travel planning assistant — ask about destinations, flights, hotels, and day-by-day itineraries.

**Live demo:** 👉 **https://web-sable-rho-16.vercel.app** 👈

Open the link, type a question (e.g. _"plan 3 days in Tokyo"_), and you'll get an itinerary back. No login required.

> Built so anyone in a meeting can open the link and try it. **No login, no account, no API key required** — it ships with a built-in demo mode.

---

## What it is

A minimal Next.js 16 app with:

- A clean chat UI (light + dark mode, suggestion chips, typing indicator)
- A server-side `/api/chat` route handler
- **Demo mode** — deterministic mock responses so the URL "just works" for testers with zero configuration
- **Live mode** — drop in any OpenAI-compatible API key (OpenAI, Groq free tier, Together, OpenRouter, …) and you get real AI replies

```
Browser  ─►  Next.js page (apps/web/src/app/page.tsx)
              │
              └─► POST /api/chat  (edge route)
                    │
                    ├─ no key  → mock reply (demo mode)
                    └─ key set → OpenAI-compatible /chat/completions
```

---

## Run locally

Prereqs: **Node 20+** and **pnpm** (or npm).

```bash
cd apps/web
pnpm install
pnpm dev
```

Open <http://localhost:3000>. It runs in **demo mode** out of the box.

### Optional — connect a real model

Copy the example env file and fill in **one** provider:

```bash
cp .env.example .env.local
```

| Provider                    | `OPENAI_BASE_URL`                     | `OPENAI_MODEL`              | Cost           |
| --------------------------- | ------------------------------------- | --------------------------- | -------------- |
| **Groq** (recommended free) | `https://api.groq.com/openai/v1`      | `llama-3.3-70b-versatile`   | Free tier      |
| OpenAI                      | _(leave blank)_                       | `gpt-4o-mini`               | Pay-as-you-go  |
| OpenRouter                  | `https://openrouter.ai/api/v1`        | `meta-llama/llama-3.3-70b`  | Free models    |

Then restart `pnpm dev`. The header badge will show **live** once a key is detected.

---

## Deploy

The app is designed for **Vercel Hobby (free, no credit card)**.

### Option 1 — one-click

[![Deploy with Vercel](https://vercel.com/button)](https://vercel.com/new/clone?repository-url=https://github.com/tamirdresher/travel-assistant&root-directory=apps/web)

### Option 2 — CLI

```bash
npm i -g vercel        # one-time
cd apps/web
vercel                 # first run: links project & creates preview
vercel --prod          # promotes to production
```

After deploy you'll get a public URL like `https://travel-assistant-xxxx.vercel.app`.

**Optional — add a key in Vercel:** Project → Settings → Environment Variables, add `OPENAI_API_KEY` (and `OPENAI_BASE_URL` / `OPENAI_MODEL` for non-OpenAI providers), then redeploy.

---

## Project structure

```
apps/web/                  Next.js 16 app (the live demo)
  src/app/page.tsx           chat UI
  src/app/api/chat/route.ts  edge route — mock + OpenAI-compatible
  .env.example               env var template
src/TravelAssistant.Api/   .NET 9 API (Phase 2, not deployed yet)
infra/bicep/               Azure Container Apps infra (Phase 2)
docs/architecture.md       design rationale & future roadmap
.squad/                    Squad agent context
```

---

## Why this stack?

- **Free**: Vercel Hobby + demo mode = $0 to host and try.
- **Zero friction for testers**: open URL, type, get response.
- **Provider-agnostic**: any OpenAI-compatible endpoint works — no SDK lock-in.
- **Production-ready path**: the .NET API and Bicep infra in this repo are the Phase 2 production target (Azure Container Apps + Cosmos DB). See [`docs/architecture.md`](./docs/architecture.md).

---

## Roadmap

- [x] Phase 1: Free-tier demo (Next.js on Vercel) ← _you are here_
- [ ] Phase 2: .NET API + Azure Container Apps + Cosmos (existing `src/` + `infra/`)
- [ ] Phase 3: Real flight/hotel search integrations, auth, user trip memory

---

## License

MIT
