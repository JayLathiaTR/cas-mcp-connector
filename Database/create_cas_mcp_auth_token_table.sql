-- Token store for the CAS MCP connector (EM/GFR only; no GA columns).
CREATE TABLE IF NOT EXISTS cas_mcp_auth_token (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ciam_user_euid      uuid  NOT NULL,
    gfr_token_encrypted bytea NOT NULL,
    record_dek          bytea NULL,
    user_email          text  NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_cas_mcp_auth_token_euid
    ON cas_mcp_auth_token (ciam_user_euid);
