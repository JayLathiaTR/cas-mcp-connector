# Architecture

_Standalone MCP connector for third-party AI clients. EM (GFR) only — no GA._

TODO (filled through the POC phases):
- Request flow: client → MCP endpoint → auth/audit middleware → tool → EM downstream
- Reused components from CAS-MCP (auth + audit middleware, GFR token exchange)
- What is intentionally excluded (all GA)
