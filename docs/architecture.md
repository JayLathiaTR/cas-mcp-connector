# Architecture

Standalone MCP connector that exposes CAS **Engagement Manager** tools to third-party AI clients
(Claude, Copilot, any MCP-compliant client). EM (GoFileRoom) only — **no GA**.

## Request flow
```
MCP client (mcp-remote)                Connector (this service)                 Downstream
──────────────────────                 ─────────────────────────                ──────────
 1. POST /mcp (no token)  ───────────▶ 401 + PRM (authorization_servers = self)
 2. OAuth discovery       ───────────▶ /.well-known/oauth-authorization-server
 3. DCR /register         ───────────▶ client_id
 4. browser /authorize    ───────────▶ Sign-in page  → /login → Authorize page → /consent
 5. /token (PKCE S256)    ───────────▶ access_token (= the pasted CIAM token)
 6. POST /mcp (Bearer)    ───────────▶ CIAM JWT validated ─▶ CIAM→GFR exchange ─▶ EM V1
```

## Building blocks
- **MCP server** — `ModelContextProtocol.AspNetCore`, stateless HTTP transport, `MapMcp("/mcp")`.
- **Auth (reused from CAS-MCP / WebHost.Core)** — `ConfigureCIAMAuthentication` validates the caller's
  CIAM JWT; `ConfigureMcpOAuthDiscovery` serves the protected-resource metadata (PRM). The PRM's
  `authorization_servers` is repointed to THIS connector per-request (`OnResourceMetadataRequest`).
- **Mock OAuth server** (`Auth/OAuth/`) — POC login/consent so no CIAM client registration is needed;
  the access token returned IS the pasted CIAM token.
- **GFR token exchange** (`GoFileRoom/`) — CIAM→GFR (`POST api/v1/advanceflow/user/login`), persisted
  encrypted in Postgres, refreshed on a downstream 401.
- **Token store** (`DataAccess/`, `Security/`) — `cas_mcp_auth_token` (EM-only), AES-256-GCM record-DEK
  envelope encryption.
- **EM client + tool** (`EngagementManager/`, `Tools/`) — `em_list_engagement_workpapers` → EM V1
  `/Binder/v4/{id}/Items` with the resolved GFR token.

## Intentionally excluded
All Guided Assurance (GA/UDS): no GA pipeline, HTTP client, tools, refresh, or token columns.

## Not-for-production (spike shortcuts)
- Login is a **mock** (collects a CIAM token) — the real project uses a registered CIAM native client.
- In-memory auth-code store; single master key (no Key Vault rotation wired); no rate limiting.
