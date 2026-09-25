# Spike findings & Go/No-Go recommendation

**Goal:** prove a third-party AI client (Claude Desktop, Copilot, …) can consume CAS Engagement
Manager tools through a standalone MCP connector that performs **its own login** — separate from CoCounsel's CAS-MCP.

## Outcome: PROVEN end-to-end
A third-party client connects to the connector, is driven through a login + consent flow, and calls
an EM tool that returns **real Engagement Manager data** — with **zero new CIAM registration**.
Validated in Claude Desktop: connect → **Sign in to Cloud Audit Suite - Engagement Manager** →
**Authorize access** → `em_list_engagement_workpapers` returns the live engagement tree (matched the EM UI).

## What was built (reference implementation)
- Standalone .NET 10 MCP server (own repo, CI green, port-agnostic).
- MCP OAuth **discovery + login**: the connector acts as its own OAuth 2.1 authorization server
  (DCR, PKCE-S256, authorize/consent/token), with TR-styled Sign-in and Authorize screens.
- **CIAM→GFR** token exchange, GFR token persisted **AES-256-GCM encrypted** in Postgres.
- Reused CAS-MCP / WebHost.Core **auth + audit middleware** and CIAM JWT validation.
- One curated tool (Engagement Contents); additive by design.

## Key finding (the one real productionization dependency)
The spike deliberately uses a **mock login** (the page collects a CIAM token) because a stock desktop
MCP client cannot complete an interactive CIAM login against the **existing EM web-app client**:
- CIAM only redirects to **registered** URIs — a desktop client uses a `localhost` callback that
  isn't (and, for a spike, won't be) registered.
- CIAM (Auth0-style) only mints an API JWT when the request carries the custom `audience`, which
  generic clients don't send.

**For production**, the real login requires a **dedicated CIAM native/public client** (localhost/loopback
redirects + default audience = the connector's resource). That is the single gating dependency — a
scoped CIAM registration request, not a code problem.

## Reusable vs. new (effort signal)
- **Reuse as-is:** WebHost.Core auth/audit, CIAM validation, MCP OAuth discovery, GFR exchange,
  encrypted token store, EM client pattern.
- **New for production:** CIAM native-client registration; replace mock login with the real CIAM
  screen; hardening (rate limiting, observability, secret rotation via Key Vault); the **live-sync**
  gate before any tool is served from cached/AI-Search data; broaden the curated tool set as approved.

## Risks / gaps (tracked, not blockers for the spike)
- Mock login is not real authentication (spike only).
- OAuth authorization codes are in-memory (short-lived, lost on restart); GFR tokens are persisted
  encrypted in Postgres. Single master key; no rate limiting; container app-path needs the master key injected.

## Recommendation: GO
The concept is validated and the architecture is sound and largely reusable. Recommend proceeding to a
productionization phase, **gated on a CIAM native-client registration** (the only hard dependency) and
the standard hardening/live-sync work. No blockers were found that question feasibility.
