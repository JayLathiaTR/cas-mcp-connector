# cas-mcp-connector

Standalone MCP server exposing CAS Engagement Manager tools to third-party AI clients
(e.g. **Claude**, **Copilot**) with its **own OAuth/login** — no CIAM client registration needed
for the spike. Reuses the CAS-MCP auth + audit middleware.

## Why a separate service
The existing CAS-MCP (Orchestrator) is built for CoCounsel and accepts a pre-minted CIAM token as a
static header — it has no login of its own. This connector drives the login itself (MCP OAuth
discovery → a mock login that collects a CIAM token → CIAM→GFR exchange → EM), so any MCP-compliant
client can add it. See [docs/architecture.md](docs/architecture.md), [docs/auth-flow.md](docs/auth-flow.md), and [docs/findings-and-recommendation.md](docs/findings-and-recommendation.md).

## Stack
- .NET 10 (`Microsoft.NET.Sdk.Web`)
- `ModelContextProtocol.AspNetCore` (MCP server + stateless HTTP transport)
- `AuditIntelligence.WebHost.Core` (CIAM auth + MCP OAuth discovery)
- Postgres + EF Core (encrypted GFR-token store)

## Layout
```
CasMcpConnectorServices/        # the MCP server
  Auth/                         #   CIAM token middleware + mock OAuth server (login/consent)
  DataAccess/                   #   ConnectorDbContext + cas_mcp_auth_token
  EngagementManager/            #   EM V1 client + endpoints
  GoFileRoom/                   #   CIAM→GFR token exchange
  Security/                     #   record-DEK encryptor, PKCE, request auth context
  Tools/                        #   MCP tools (em_list_engagement_workpapers)
CasMcpConnectorServices.Tests/  # unit tests
Database/                       # token-table DDL
Docker/                         # docker-compose (app + Postgres)
docs/                           # architecture + auth-flow
```

## Local development
1. Start Postgres:
   ```bash
   docker compose -f Docker/docker-compose.yml up -d
   ```
2. Set the encryption master key (local, generated — keep the committed AppSecrets key empty):
   ```bash
   dotnet run --project CasMcpConnectorServices   # or F5; then set the key below and restart
   ```
   Put a base64 32-byte key in `CasMcpConnectorServices/Secrets/AppSecrets.json`
   (`TokenEncryption:Base64EncryptionKey`). Generate one with `openssl rand -base64 32`. **Do not commit it.**
3. Run with **F5** (serves on `http://localhost:5080`). The MCP endpoint is `/mcp`.
4. Inspect the token store any time with the `connectordb` helper (docker exec into Postgres).

## Demo (Claude Desktop)
1. Ensure the connector is running and Postgres is up.
2. Point Claude Desktop at it (`claude_desktop_config.json`):
   ```json
   { "mcpServers": { "cas-connector": {
     "command": "npx",
     "args": ["-y", "mcp-remote", "http://localhost:5080/mcp"]
   }}}
   ```
   (The issuer is derived from the request, so the docker port `7020` works too — just match the URL.)
3. **Reset the login cache** so the sign-in screen appears:
   ```powershell
   ./reset-login.ps1
   ```
4. Restart Claude Desktop → browser opens **Sign in to Cloud Audit Suite - Engagement Manager** →
   paste a CIAM token → **Authorize access** → back to the client.
5. Ask the client to list engagement workpapers → real EM data.

Notes: Claude spawns two client surfaces, so the first login may open two tabs (complete one, close the
other) and may show a one-off "connection timed out" while you sign in — both are normal mcp-remote
behaviour and resolve once the shared token cache is written.

## Tests / CI
```bash
dotnet test
```
CI (`.github/workflows/build.yml`) restores (JFrog-authenticated), builds and tests on every push/PR.
