# Architecture

**CasMcpConnector** — a standalone MCP server that exposes CAS **Engagement Manager** tools to
third-party AI clients (Claude, Copilot, any MCP-compliant client). EM (GoFileRoom) only — **no GA**.

> "Connector" here always means **CasMcpConnector** (this service), not a Claude/host "connector".

## Container view
```mermaid
flowchart TB
    user["Auditor<br/><i>Person</i>"]
    client["MCP client<br/><i>Claude Desktop / Copilot</i>"]

    subgraph sys["CasMcpConnector"]
        app["MCP + OAuth service<br/><i>.NET 10 / ASP.NET</i>"]
        db[("Token store<br/><i>PostgreSQL · encrypted GFR tokens</i>")]
    end

    ciam["CIAM<br/><i>auth-nonprod</i>"]
    gfr["GoFileRoom"]
    em["Engagement Manager V1"]

    user --> client
    client -->|"MCP + OAuth (HTTPS)"| app
    app -->|"validate CIAM JWT"| ciam
    app -->|"CIAM to GFR exchange"| gfr
    app -->|"read engagement contents"| em
    app -->|"store / read (encrypted)"| db

    classDef internal fill:#1f6feb,stroke:#79c0ff,stroke-width:1px,color:#ffffff;
    classDef external fill:#57606a,stroke:#8b949e,stroke-width:1px,color:#ffffff;
    class app,db internal;
    class user,client,ciam,gfr,em external;
    style sys fill:transparent,stroke:#8b949e,stroke-dasharray:4 3,color:#8b949e;
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

    classDef internal fill:#1f6feb,stroke:#79c0ff,color:#ffffff;
    classDef external fill:#57606a,stroke:#8b949e,color:#ffffff;
    classDef store fill:#238636,stroke:#56d364,color:#ffffff;
    class MCP,OAUTH,AUTH,MW,GFRS,ENC,EMC internal;
    class MC,CIAM,GFR,EM external;
    class DB store;
    style CONN fill:transparent,stroke:#8b949e,stroke-dasharray:4 3,color:#8b949e;
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
