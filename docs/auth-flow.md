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
