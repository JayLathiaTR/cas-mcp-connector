# cas-mcp-connector

Standalone MCP server exposing CAS Engagement Manager tools to third-party AI clients (e.g. **Claude**, **Copilot**) with its own OAuth/CIAM login.

EM-only (no GA). Reuses the CAS-MCP auth flow + audit middleware.

## Why a separate service
The existing CAS-MCP (Orchestrator) is built for CoCounsel (TR's in-house LLM client) and accepts a pre-minted CIAM token as a static header — it has no login of its own. This service is a **standalone connector** that any MCP-compliant client can add, and it drives the **login itself** (MCP OAuth discovery → CIAM → EM-audience token). It exposes a curated, additive tool set, starting with **Engagement Contents**.

## Stack
- .NET 10 (`Microsoft.NET.Sdk.Web`)
- `ModelContextProtocol.AspNetCore` (MCP server + HTTP transport)
- Reuses `AuditIntelligence.WebHost.Core` (auth + audit middleware) — added in the auth phase

## Layout
```
CasMcpConnectorServices/        # the MCP server
CasMcpConnectorServices.Tests/  # unit/smoke tests
Database/                       # token-store DDL (EM/GFR only; added later)
Docker/                         # container + local compose
docs/                           # architecture & auth-flow notes
```

## Run locally
```bash
dotnet run --project CasMcpConnectorServices
```
The MCP endpoint is served at `/mcp`. A placeholder `server_ping` tool is available for wiring verification (removed once real tools land).

## Roadmap (POC)
- **P0** — skeleton + Engagement Contents against a manually-supplied token
- **P1** — MCP OAuth discovery + CIAM login → dynamic EM/GFR token (drop static header)
- **P2** — hardening (validation, refresh, config/secrets, observability, rate limits)
- **P3** — CIAM client registration, deploy pipeline, onboarding docs, Go/No-Go
