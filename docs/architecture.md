# Architecture

**CasMcpConnector** — a standalone MCP server that exposes CAS **Engagement Manager** tools to
third-party AI clients (Claude, Copilot, any MCP-compliant client). EM (GoFileRoom) only — **no GA**.

> "Connector" here always means **CasMcpConnector** (this service), not a Claude/host "connector".

## Container view (C4)
```mermaid
C4Container
    title CasMcpConnector - Container view
    Person(user, "Auditor", "Uses a third-party AI client")
    System_Ext(client, "MCP client", "Claude Desktop / Copilot (via mcp-remote)")
    System_Boundary(b, "CasMcpConnector") {
        Container(app, "MCP + OAuth service", ".NET 10 / ASP.NET", "Serves /mcp; hosts login/consent; CIAM auth; CIAM->GFR exchange; EM tool")
        ContainerDb(db, "Token store", "PostgreSQL", "Encrypted GFR tokens (cas_mcp_auth_token)")
    }
    System_Ext(ciam, "CIAM", "auth-nonprod (issues/validates tokens)")
    System_Ext(gfr, "GoFileRoom", "CIAM->GFR token exchange")
    System_Ext(em, "Engagement Manager V1", "Engagement data")

    Rel(user, client, "Uses")
    Rel(client, app, "MCP over HTTP + OAuth login", "HTTPS")
    Rel(app, ciam, "Validate CIAM JWT")
    Rel(app, gfr, "Exchange CIAM->GFR")
    Rel(app, em, "Read engagement contents")
    Rel(app, db, "Store / read encrypted GFR token")
```

## Component overview
```mermaid
flowchart LR
    MC["MCP client<br/>(Claude / Copilot via mcp-remote)"]

    subgraph CONN["CasMcpConnector (.NET 10)"]
        direction TB
        MCP["MCP server<br/>/mcp (stateless)"]
        OAUTH["Mock OAuth server<br/>authorize · login · consent · token · DCR"]
        AUTH["CIAM auth + PRM discovery<br/>(WebHost.Core)"]
        MW["TokenValidation middleware<br/>→ IRequestAuthContext"]
        GFRS["GfrTokenService<br/>(CIAM→GFR exchange)"]
        ENC["AES-256-GCM<br/>record-DEK encryptor"]
        EMC["EM V1 client + tool<br/>em_list_engagement_workpapers"]
    end

    DB[("Postgres<br/>cas_mcp_auth_token")]
    CIAM[["CIAM<br/>auth-nonprod"]]
    GFR[["GoFileRoom"]]
    EM[["Engagement Manager V1"]]

    MC -->|"1. OAuth login"| OAUTH
    MC -->|"2. Bearer CIAM token"| MCP
    MCP --> AUTH --> MW --> EMC
    AUTH -. "validate JWT (JWKS)" .-> CIAM
    EMC --> GFRS
    GFRS --> GFR
    GFRS --> ENC --> DB
    EMC --> EM
```

## Request flow (discovery → login → tool call)
```mermaid
sequenceDiagram
    autonumber
    participant C as MCP client
    participant K as CasMcpConnector
    participant CIAM as CIAM
    participant GFR as GoFileRoom
    participant EM as Engagement Manager

    C->>K: POST /mcp (no token)
    K-->>C: 401 + PRM (authorization_servers = CasMcpConnector)
    C->>K: GET /.well-known/oauth-authorization-server
    C->>K: POST /connector/oauth/register (DCR)
    K-->>C: client_id
    C->>K: GET /authorize (PKCE challenge)
    K-->>C: Sign-in page
    Note over C,K: paste CIAM token → /login → Authorize/consent → /consent
    K-->>C: redirect with one-time auth code
    C->>K: POST /token (code + PKCE verifier)
    K-->>C: access_token (= the pasted CIAM token)
    C->>K: POST /mcp (Bearer) → tools/call
    K->>CIAM: validate CIAM JWT
    K->>GFR: CIAM→GFR exchange (api/v1/advanceflow/user/login)
    GFR-->>K: GFR token (stored AES-256-GCM encrypted)
    K->>EM: GET /Binder/v4/{id}/Items (raw GFR token)
    EM-->>K: engagement contents
    K-->>C: tool result
```

## Building blocks
- **MCP server** — `ModelContextProtocol.AspNetCore`, stateless HTTP transport, `MapMcp("/mcp")`.
- **Auth (reused from CAS-MCP / WebHost.Core)** — `ConfigureCIAMAuthentication` validates the caller's
  CIAM JWT; `ConfigureMcpOAuthDiscovery` serves the protected-resource metadata (PRM). The PRM's
  `authorization_servers` is repointed to CasMcpConnector per-request (`OnResourceMetadataRequest`).
- **Mock OAuth server** (`Auth/OAuth/`) — POC login/consent so no CIAM client registration is needed;
  the access token returned IS the pasted CIAM token.
- **GFR token exchange** (`GoFileRoom/`) — CIAM→GFR, persisted encrypted in Postgres, refreshed on 401.
- **Token store** (`DataAccess/`, `Security/`) — `cas_mcp_auth_token` (EM-only), AES-256-GCM record-DEK
  envelope encryption.
- **EM client + tool** (`EngagementManager/`, `Tools/`) — `em_list_engagement_workpapers` → EM V1
  `/Binder/v4/{id}/Items` with the resolved GFR token.

## Intentionally excluded
All Guided Assurance (GA/UDS): no GA pipeline, HTTP client, tools, refresh, or token columns.

## Not-for-production (spike shortcuts)
- Login is a **mock** (collects a CIAM token) — the real project uses a registered CIAM native client.
- In-memory auth-code store; single master key (no Key Vault rotation wired); no rate limiting.
