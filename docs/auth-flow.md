# Auth flow (EM / GFR only)

## Discovery + login (connector as its own OAuth server)
1. Client calls `/mcp` without a token → `401` + `WWW-Authenticate: Bearer resource_metadata=<PRM>`.
2. Client reads `/.well-known/oauth-protected-resource` → `authorization_servers = [this connector]`.
3. Client reads `/.well-known/oauth-authorization-server` → authorize/token/register endpoints.
4. Client does Dynamic Client Registration at `/connector/oauth/register` (returns a `client_id`).
5. Browser opens `/connector/oauth/authorize` (PKCE `code_challenge`) → **Sign-in page**.
6. `/connector/oauth/login` (submitted CIAM token) → **Authorize/consent page**.
7. `/connector/oauth/consent`:
   - Accept → issues a one-time `code` (bound to the CIAM token + PKCE + redirect) → redirects back.
   - Decline → redirects with `error=access_denied`.
8. `/connector/oauth/token` verifies PKCE (S256) → returns `access_token = the CIAM token`.

## Using the token
9. Client calls `/mcp` with `Authorization: Bearer <ciam-token>`.
10. `ConfigureCIAMAuthentication` validates it (authority `auth-nonprod`, audience `2329b557-...`).
11. `TokenValidationMiddleware` puts euid + token on `IRequestAuthContext`.
12. On an EM tool call, `EngagementManagerClient` gets a GFR token from `GfrTokenService`
    (stored encrypted, or freshly exchanged CIAM→GFR), sends it to EM V1, and refreshes once on a 401.

## Authorization code vs. access token
The flow deliberately uses two short-vs-long-lived artifacts:

- **Authorization code** — a one-time "claim ticket" issued on **Accept** (step 7). It carries no
  access on its own; it is bound to the CIAM token, the PKCE challenge and the redirect, expires in
  ~5 min, and is single-use. It travels back through the browser redirect, so it is deliberately
  useless if intercepted — the client must still prove possession via PKCE at `/token` (step 8).
  Stored **in-memory** (`OAuthCodeStore`); losing it on restart just means "log in again".
- **Access token** — returned at `/token` (here, the CIAM token). This is what calls `/mcp`, and its
  downstream **GFR token** is persisted **encrypted in Postgres** (`cas_mcp_auth_token`).

