# Travel Assistant — MVP Backlog

---

## Issue: Epic: Azure infrastructure & DevOps

**Labels:** squad, squad:gantz, epic, mvp, infra

**Body:**
Umbrella issue for all Azure resource provisioning and CI/CD pipeline work. All infra is defined in Bicep under `infra/bicep/`. Includes Container Apps, Static Web Apps, Cosmos DB, Key Vault, Entra External ID, and Application Insights.

- [ ] All resources provisionable via `az deployment group create`
- [ ] CI pipeline runs on every PR
- [ ] CD pipeline deploys to staging on merge to main

---

## Issue: Epic: Search flow — flight and hotel search end-to-end

**Labels:** squad, squad:ben-gurion, epic, mvp

**Body:**
Umbrella for the core user journey: user types a travel query → AI extracts intent → Amadeus API returns results → results displayed in chat UI. Spans backend, frontend, and AI integration. This is the MVP's critical path.

---

## Issue: Epic: Auth & user identity

**Labels:** squad, squad:peres, epic, mvp, auth

**Body:**
Umbrella for sign-up, sign-in, JWT validation, and protected API routes using Microsoft Entra External ID. Covers both backend middleware and frontend auth flow.

---

## Issue: Provision Azure infra via Bicep

**Labels:** squad, squad:gantz, mvp, infra

**Body:**
Create Bicep templates for all MVP Azure resources: Container Apps environment, Static Web App, Cosmos DB (serverless, NoSQL), Key Vault, Application Insights + Log Analytics workspace, and Entra External ID tenant config. Use a single resource group. Parameterize environment name for staging/prod.

- [ ] `main.bicep` with modules per resource
- [ ] `parameters.staging.json` and `parameters.prod.json`
- [ ] Deployment succeeds with `az deployment group create`
- [ ] Managed identity on Container App with Key Vault access policy

---

## Issue: Set up GitHub Actions CI pipeline

**Labels:** squad, squad:gantz, mvp, infra, ci

**Body:**
Create `.github/workflows/ci.yml` that runs on every PR. Steps: restore + build + test for .NET API, install + lint + build for Next.js frontend. Fail fast on any step. Cache NuGet and pnpm dependencies.

- [ ] Triggers on `pull_request` to `main`
- [ ] Backend: `dotnet restore`, `dotnet build`, `dotnet test`
- [ ] Frontend: `pnpm install`, `pnpm lint`, `pnpm build`
- [ ] Pipeline completes in under 5 minutes on a cold cache

---

## Issue: Set up GitHub Actions CD pipeline

**Labels:** squad, squad:gantz, mvp, infra, cd

**Body:**
Create `.github/workflows/cd.yml` that deploys on merge to `main`. Deploy API container image to Azure Container Apps, deploy Next.js to Azure Static Web Apps. Use OIDC federated credentials (no stored secrets).

- [ ] Triggers on `push` to `main`
- [ ] Builds and pushes API Docker image to ACR
- [ ] Deploys Container App revision
- [ ] Deploys Static Web App via SWA CLI
- [ ] Staging environment only (prod promotion is manual)

---

## Issue: Scaffold .NET 9 Minimal API project

**Labels:** squad, squad:peres, mvp, backend

**Body:**
Create the `apps/api` project: .NET 9 Minimal API with health-check endpoint, structured logging (Serilog → Application Insights), and Dockerfile. Include `Directory.Build.props` for shared settings. Wire up dependency injection skeleton.

- [ ] `dotnet new webapi` with minimal API style
- [ ] `/health` endpoint returns 200
- [ ] Dockerfile builds and runs locally
- [ ] Serilog configured with Application Insights sink
- [ ] Solution file at repo root

---

## Issue: Scaffold Next.js 15 frontend project

**Labels:** squad, squad:lapid, mvp, frontend

**Body:**
Create the `apps/web` project: Next.js 15 with App Router, TypeScript strict mode, Tailwind CSS, and ESLint. Include a placeholder home page and a `/chat` route stub. Configure for Azure Static Web Apps deployment.

- [ ] `pnpm create next-app` with App Router + TypeScript
- [ ] Tailwind CSS configured
- [ ] ESLint + Prettier configured
- [ ] `staticwebapp.config.json` for SWA routing
- [ ] Dev server starts with `pnpm dev`

---

## Issue: Build chat UI component

**Labels:** squad, squad:lapid, mvp, frontend, ui

**Body:**
Implement the core chat interface in the `/chat` route. Message list with user/assistant bubbles, text input with send button, loading indicator while waiting for API response. Responsive design (mobile-first). Use React Server Components where possible, client components for interactive chat.

- [ ] Message list renders user and assistant messages with distinct styling
- [ ] Input field with send button and Enter-key submit
- [ ] Loading spinner / typing indicator during API call
- [ ] Auto-scroll to latest message
- [ ] Works on mobile viewport (375px+)

---

## Issue: Implement Azure OpenAI integration for intent extraction

**Labels:** squad, squad:peres, mvp, backend, ai

**Body:**
Integrate Azure OpenAI (GPT-4o) to parse user messages into structured travel search parameters: origin, destination, dates, passenger count, hotel preferences. Use function calling / tool-use pattern. Return a typed `SearchIntent` object. Handle ambiguous queries by asking clarifying questions.

- [ ] Azure OpenAI client configured via Key Vault secret
- [ ] System prompt defines travel-assistant persona and extraction schema
- [ ] Function-calling schema for `search_flights` and `search_hotels`
- [ ] Graceful fallback when intent is ambiguous (ask follow-up)
- [ ] Unit tests for intent parsing with sample prompts

---

## Issue: Implement Amadeus API adapter — flight search

**Labels:** squad, squad:peres, mvp, backend, integration

**Body:**
Build an adapter for Amadeus Flight Offers Search API (v2). Authenticate via client credentials, search by origin/destination/dates/passengers, map response to internal `FlightResult` model. Use the Amadeus .NET SDK. Handle rate limits and error responses.

- [ ] Amadeus client credentials stored in Key Vault
- [ ] `IFlightSearchProvider` interface with Amadeus implementation
- [ ] Maps Amadeus response to `FlightResult` DTO
- [ ] Retry with exponential backoff on 429/5xx
- [ ] Integration test against Amadeus sandbox

---

## Issue: Implement Amadeus API adapter — hotel search

**Labels:** squad, squad:peres, mvp, backend, integration

**Body:**
Build an adapter for Amadeus Hotel Search API (v2). Search by city/check-in/check-out/guests. Map response to internal `HotelResult` model. Reuse auth token from flight adapter.

- [ ] `IHotelSearchProvider` interface with Amadeus implementation
- [ ] Maps Amadeus response to `HotelResult` DTO
- [ ] Shares auth token manager with flight adapter
- [ ] Integration test against Amadeus sandbox

---

## Issue: Design and implement Cosmos DB data model

**Labels:** squad, squad:peres, mvp, backend, data

**Body:**
Define Cosmos DB containers for MVP: `conversations` (partition key: userId) and `users` (partition key: id). Implement repository classes with the Azure Cosmos DB .NET SDK (v3). Use serverless throughput mode.

- [ ] Container definitions in Bicep
- [ ] `ConversationRepository` — create, append message, get by id
- [ ] `UserRepository` — create, get by id
- [ ] TTL policy on conversations (90 days default)
- [ ] Unit tests with Cosmos DB emulator or in-memory mock

---

## Issue: Implement search API endpoint

**Labels:** squad, squad:peres, mvp, backend, api

**Body:**
Create `POST /api/chat` endpoint that orchestrates the full search flow: receive user message → call Azure OpenAI for intent → call Amadeus adapter(s) → format results → return assistant response. Store conversation turn in Cosmos DB. Endpoint requires authenticated user (JWT).

- [ ] Accepts `{ conversationId?, message }` body
- [ ] Returns `{ conversationId, response, results[] }`
- [ ] Orchestrates AI → search → response pipeline
- [ ] Stores conversation turn in Cosmos DB
- [ ] Returns 401 if no valid JWT

---

## Issue: Implement Entra External ID auth — backend middleware

**Labels:** squad, squad:peres, mvp, backend, auth

**Body:**
Configure JWT bearer authentication middleware in the .NET API using Microsoft Entra External ID. Validate tokens on protected endpoints. Extract user ID from claims for Cosmos DB queries.

- [ ] `AddAuthentication().AddJwtBearer()` configured with Entra External ID authority
- [ ] `/api/chat` requires authenticated user
- [ ] `/health` remains unauthenticated
- [ ] User ID extracted from `sub` or `oid` claim

---

## Issue: Implement Entra External ID auth — frontend flow

**Labels:** squad, squad:lapid, mvp, frontend, auth

**Body:**
Integrate MSAL.js in the Next.js frontend for sign-up/sign-in with Entra External ID. Store access token, attach as Bearer header on API calls. Show login/logout button in header. Redirect unauthenticated users from `/chat` to login.

- [ ] MSAL React provider wraps app
- [ ] Sign-in / sign-up redirect flow
- [ ] Access token attached to API requests via interceptor
- [ ] Protected route on `/chat`
- [ ] Logout clears session

---

## Issue: Display search results in chat UI

**Labels:** squad, squad:lapid, mvp, frontend, ui

**Body:**
Render flight and hotel search results as rich cards inside the chat message stream. Each card shows: price, airline/hotel name, dates, and a "View Deal" link that opens the provider's booking page in a new tab. Cards should be visually distinct from text messages.

- [ ] Flight result card: airline, route, times, price, external link
- [ ] Hotel result card: name, star rating, dates, price, external link
- [ ] Cards render inline in the chat message list
- [ ] Responsive card layout (stacks vertically on mobile)

---

## Issue: Add Application Insights telemetry to API

**Labels:** squad, squad:gantz, mvp, infra, observability

**Body:**
Wire Application Insights SDK into the .NET API. Track request duration, dependency calls (Amadeus, Cosmos DB, Azure OpenAI), exceptions, and custom events for search queries. Use connection string from Key Vault.

- [ ] `AddApplicationInsightsTelemetry()` in Program.cs
- [ ] Dependency tracking for HTTP calls and Cosmos DB
- [ ] Custom event `SearchExecuted` with anonymized query metadata
- [ ] Exception telemetry with correlation IDs

---

## Issue: Write e2e test for search flow

**Labels:** squad, squad:bennett, mvp, test, e2e

**Body:**
Create an end-to-end test that exercises the full search flow: authenticate → send a chat message ("Find flights from NYC to London next month") → verify API returns flight results with expected shape. Use a test user in Entra External ID and Amadeus sandbox. Run in CI.

- [ ] Test authenticates with a test user token
- [ ] Sends POST to `/api/chat` with a search query
- [ ] Asserts response contains `results[]` with at least one flight
- [ ] Asserts conversation is persisted (GET returns history)
- [ ] Runs in GitHub Actions CI

---

## Issue: Write contract tests for Amadeus adapter

**Labels:** squad, squad:bennett, mvp, test, integration

**Body:**
Create contract tests that verify our Amadeus adapter correctly handles real API response shapes. Record sandbox responses as fixtures, test deserialization and mapping to internal DTOs. Catch breaking API changes early.

- [ ] Recorded response fixtures for flight search and hotel search
- [ ] Tests verify `FlightResult` mapping (all required fields populated)
- [ ] Tests verify `HotelResult` mapping
- [ ] Tests verify error-response handling (401, 429, 500)

---

## Issue: Write unit tests for AI intent extraction

**Labels:** squad, squad:bennett, mvp, test, ai

**Body:**
Create unit tests for the Azure OpenAI intent-extraction layer. Mock the OpenAI client, provide sample user messages, and assert correct `SearchIntent` output (destination, dates, passengers). Cover edge cases: vague queries, missing dates, multi-city.

- [ ] Test: "Flights from NYC to London in July" → correct intent
- [ ] Test: "Cheap hotels in Paris" → hotel intent with city
- [ ] Test: "I want to travel somewhere warm" → clarifying question returned
- [ ] Test: multi-city query handling

---

## Issue: Create README and developer setup docs

**Labels:** squad, squad:ben-gurion, mvp, docs

**Body:**
Write a README.md at the repo root covering: project overview, architecture diagram (text/mermaid), prerequisites (Node 22, .NET 9, Azure CLI, pnpm), local dev setup steps, environment variables, and how to run tests. Add a `docs/architecture.md` that mirrors the ADR.

- [ ] README with project description and quick-start
- [ ] Prerequisites listed with version requirements
- [ ] Local dev setup: API + frontend running locally
- [ ] Environment variable table
- [ ] Link to architecture doc

---

## Issue: Add Skyscanner adapter (post-MVP)

**Labels:** squad, squad:peres, integration, post-mvp

**Body:**
Implement a second flight search adapter using Skyscanner via RapidAPI. Implement `IFlightSearchProvider` interface so it's swappable with Amadeus. Enables price comparison in a future iteration. RapidAPI free tier covers development.

- [ ] `SkyscannerFlightSearchProvider` implements `IFlightSearchProvider`
- [ ] API key stored in Key Vault
- [ ] Response mapped to same `FlightResult` DTO
- [ ] Feature-flagged (disabled by default)

---

## Issue: Add Booking.com affiliate adapter (post-MVP)

**Labels:** squad, squad:peres, integration, post-mvp

**Body:**
Implement hotel search via Booking.com Affiliate API. Apply for affiliate partner access (lead time ~1-2 weeks). Implement `IHotelSearchProvider` interface. Adds hotel inventory depth beyond Amadeus.

- [ ] Affiliate application submitted
- [ ] `BookingComHotelSearchProvider` implements `IHotelSearchProvider`
- [ ] Response mapped to `HotelResult` DTO
- [ ] Feature-flagged (disabled by default)

---

## Issue: Set up repo hygiene — branch protection, labels, PR template

**Labels:** squad, squad:gantz, mvp, infra, repo

**Body:**
Configure the GitHub repository: branch protection on `main` (require PR, 1 approval, status checks pass), create standard labels (`squad`, `squad:peres`, `squad:lapid`, `squad:gantz`, `squad:bennett`, `squad:ben-gurion`, `mvp`, `epic`, `infra`, `frontend`, `backend`, `test`, `integration`, `post-mvp`, `auth`, `ai`, `docs`), add PR template, and issue templates for bugs and features.

- [ ] Branch protection rule on `main`
- [ ] All labels created
- [ ] PR template in `.github/PULL_REQUEST_TEMPLATE.md`
- [ ] Issue templates in `.github/ISSUE_TEMPLATE/`
