using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasMcpConnectorServices.Configuration;
using Microsoft.Extensions.Options;

namespace CasMcpConnectorServices.Auth.OAuth;

/// <summary>
/// Minimal OAuth 2.1 authorization-server endpoints so a third-party MCP client (e.g. Claude Desktop
/// via mcp-remote) can complete the discovery + login handshake against THIS connector -- no CIAM
/// client registration or redirect config needed for the spike.
/// <para>
/// POC/mock: the login page collects a real CIAM token, then a consent screen (styled like the TR
/// CIAM screens) confirms access. The access token we return IS that CIAM token, so the existing CIAM
/// validation + GFR exchange downstream work unchanged.
/// </para>
/// </summary>
public static class ConnectorOAuthEndpoints
{
    private const string AppDisplayName = "Cloud Audit Suite - Engagement Manager";
    private const string AuthorizePath = "/connector/oauth/authorize";
    private const string TokenPath = "/connector/oauth/token";
    private const string RegisterPath = "/connector/oauth/register";
    private const string LoginPath = "/connector/oauth/login";
    private const string ConsentPath = "/connector/oauth/consent";

    public static IEndpointRouteBuilder MapConnectorOAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/.well-known/oauth-authorization-server", Metadata).AllowAnonymous();
        endpoints.MapGet("/.well-known/openid-configuration", Metadata).AllowAnonymous();
        endpoints.MapPost(RegisterPath, Register).AllowAnonymous();
        endpoints.MapGet(AuthorizePath, Authorize).AllowAnonymous();
        endpoints.MapPost(LoginPath, Login).AllowAnonymous();
        endpoints.MapPost(ConsentPath, Consent).AllowAnonymous();
        endpoints.MapPost(TokenPath, Token).AllowAnonymous();

        return endpoints;
    }

    private static IResult Metadata(HttpContext context, IOptions<ConnectorOAuthOptions> options)
    {
        string issuer = ResolveIssuer(context, options.Value);
        var metadata = new Dictionary<string, object?>
        {
            ["issuer"] = issuer,
            ["authorization_endpoint"] = issuer + AuthorizePath,
            ["token_endpoint"] = issuer + TokenPath,
            ["registration_endpoint"] = issuer + RegisterPath,
            ["response_types_supported"] = new[] { "code" },
            ["grant_types_supported"] = new[] { "authorization_code" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["token_endpoint_auth_methods_supported"] = new[] { "none" },
            ["scopes_supported"] = options.Value.ScopesSupported,
        };

        return Results.Json(metadata);
    }

    private static async Task<IResult> Register(HttpRequest request)
    {
        string clientId = "connector-" + Guid.NewGuid().ToString("N");
        string[] redirectUris = [];

        try
        {
            using JsonDocument doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("redirect_uris", out JsonElement uris) && uris.ValueKind == JsonValueKind.Array)
            {
                redirectUris = [.. uris.EnumerateArray().Select(u => u.GetString() ?? string.Empty)];
            }
        }
        catch (JsonException)
        {
            // No/invalid body -- still return a client id for the POC.
        }

        return Results.Json(new Dictionary<string, object?>
        {
            ["client_id"] = clientId,
            ["redirect_uris"] = redirectUris,
            ["token_endpoint_auth_method"] = "none",
            ["grant_types"] = new[] { "authorization_code" },
            ["response_types"] = new[] { "code" },
        }, statusCode: StatusCodes.Status201Created);
    }

    private static IResult Authorize(HttpContext context)
    {
        IQueryCollection q = context.Request.Query;
        string redirectUri = q["redirect_uri"].ToString();
        string state = q["state"].ToString();
        string codeChallenge = q["code_challenge"].ToString();
        string codeChallengeMethod = q["code_challenge_method"].ToString();

        if (string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(codeChallenge))
        {
            return Results.BadRequest("Missing redirect_uri or code_challenge.");
        }

        return Results.Content(LoginPageHtml(redirectUri, state, codeChallenge, codeChallengeMethod), "text/html");
    }

    private static IResult Login(HttpContext context)
    {
        IFormCollection form = context.Request.Form;
        string ciamToken = form["ciam_token"].ToString().Trim();
        string redirectUri = form["redirect_uri"].ToString();
        string state = form["state"].ToString();
        string codeChallenge = form["code_challenge"].ToString();

        if (string.IsNullOrEmpty(ciamToken) || string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(codeChallenge))
        {
            return Results.BadRequest("Missing CIAM token, redirect_uri or code_challenge.");
        }

        // Second step: show the consent / Authorize access screen.
        return Results.Content(ConsentPageHtml(ciamToken, redirectUri, state, codeChallenge), "text/html");
    }

    private static IResult Consent(HttpContext context, OAuthCodeStore store)
    {
        IFormCollection form = context.Request.Form;
        string action = form["action"].ToString();
        string redirectUri = form["redirect_uri"].ToString();
        string state = form["state"].ToString();

        if (string.IsNullOrEmpty(redirectUri))
        {
            return Results.BadRequest("Missing redirect_uri.");
        }

        string separator = redirectUri.Contains('?', StringComparison.Ordinal) ? "&" : "?";

        if (!string.Equals(action, "accept", StringComparison.Ordinal))
        {
            string denied = redirectUri + separator + "error=access_denied";
            if (!string.IsNullOrEmpty(state))
            {
                denied += "&state=" + Uri.EscapeDataString(state);
            }

            return Results.Redirect(denied);
        }

        string ciamToken = form["ciam_token"].ToString().Trim();
        string codeChallenge = form["code_challenge"].ToString();
        if (string.IsNullOrEmpty(ciamToken) || string.IsNullOrEmpty(codeChallenge))
        {
            return Results.BadRequest("Missing CIAM token or code_challenge.");
        }

        string code = Base64Url(RandomNumberGenerator.GetBytes(32));
        store.Save(code, new OAuthCodeStore.CodeEntry(ciamToken, codeChallenge, redirectUri, DateTimeOffset.UtcNow.AddMinutes(5)));

        string location = redirectUri + separator + "code=" + Uri.EscapeDataString(code);
        if (!string.IsNullOrEmpty(state))
        {
            location += "&state=" + Uri.EscapeDataString(state);
        }

        return Results.Redirect(location);
    }

    private static IResult Token(HttpContext context, OAuthCodeStore store)
    {
        IFormCollection form = context.Request.Form;
        string code = form["code"].ToString();
        string redirectUri = form["redirect_uri"].ToString();
        string codeVerifier = form["code_verifier"].ToString();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(codeVerifier))
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        if (!store.TryConsume(code, out OAuthCodeStore.CodeEntry entry))
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        if (!string.Equals(entry.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "invalid_grant", error_description = "redirect_uri mismatch" });
        }

        string computedChallenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        if (!string.Equals(computedChallenge, entry.CodeChallenge, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE verification failed" });
        }

        return Results.Json(new Dictionary<string, object?>
        {
            ["access_token"] = entry.CiamToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = 3600,
        });
    }

    private static string ResolveIssuer(HttpContext context, ConnectorOAuthOptions options) =>
        !string.IsNullOrWhiteSpace(options.IssuerUrl)
            ? options.IssuerUrl.TrimEnd('/')
            : context.Request.Scheme + "://" + context.Request.Host;

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Enc(string v) => System.Net.WebUtility.HtmlEncode(v);

    private static string LoginPageHtml(string redirectUri, string state, string codeChallenge, string codeChallengeMethod)
    {
        string body = $$"""
    <h1 class="title">Sign in to {{AppDisplayName}}</h1>
    <p class="subtitle">Cloud Audit Suite MCP Connector. Paste a valid CIAM token to continue.</p>
    <form method="post" action="{{LoginPath}}">
      <label for="ciam_token">CIAM access token</label>
      <textarea id="ciam_token" name="ciam_token" placeholder="eyJhbGciOi..." required autofocus></textarea>
      <input type="hidden" name="redirect_uri" value="{{Enc(redirectUri)}}" />
      <input type="hidden" name="state" value="{{Enc(state)}}" />
      <input type="hidden" name="code_challenge" value="{{Enc(codeChallenge)}}" />
      <input type="hidden" name="code_challenge_method" value="{{Enc(codeChallengeMethod)}}" />
      <button class="btn btn-primary" type="submit">Sign in</button>
    </form>
""";
        return Page("Sign in", body);
    }

    private static string ConsentPageHtml(string ciamToken, string redirectUri, string state, string codeChallenge)
    {
        string body = $$"""
    <h1 class="title">Authorize access</h1>
    <div class="consent-icons">
      <span class="consent-avatar">CA</span>
      <span class="consent-arrows">&#8646;</span>
      <span class="consent-logo"></span>
    </div>
    <p class="subtitle">{{AppDisplayName}} is requesting access to your Thomson Reuters account.</p>
    <ul class="scopes">
      <li><b>Profile:</b> access to your profile and email</li>
      <li>Allows Cloud Audit Suite to securely read all CAS data</li>
      <li>Allow offline access</li>
    </ul>
    <form method="post" action="{{ConsentPath}}">
      <input type="hidden" name="ciam_token" value="{{Enc(ciamToken)}}" />
      <input type="hidden" name="redirect_uri" value="{{Enc(redirectUri)}}" />
      <input type="hidden" name="state" value="{{Enc(state)}}" />
      <input type="hidden" name="code_challenge" value="{{Enc(codeChallenge)}}" />
      <div class="btn-row">
        <button class="btn btn-secondary" type="submit" name="action" value="decline">Decline</button>
        <button class="btn btn-primary" type="submit" name="action" value="accept">Accept</button>
      </div>
    </form>
""";
        return Page("Authorize access", body);
    }

    private static string Page(string tabTitle, string cardBody)
    {
        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{tabTitle}} - Cloud Audit Suite | Thomson Reuters</title>
  <style>
    :root { --tr-orange:#ff8000; --tr-orange-dark:#d64000; --ink:#26382e; --muted:#5c677d; --line:#d7dbe0; --bg:#f4f4f4; }
    * { box-sizing:border-box; }
    body { margin:0; min-height:100vh; display:flex; flex-direction:column; background:var(--bg);
           font-family:"Source Sans 3","Segoe UI",system-ui,-apple-system,Helvetica,Arial,sans-serif; color:var(--ink); }
    header.tr { display:flex; align-items:center; gap:14px; padding:14px 28px; background:#fff; border-bottom:1px solid var(--line); }
    .tr-logo { width:30px; height:30px; border-radius:50%; background:
      radial-gradient(circle at 50% 50%, var(--tr-orange) 0 30%, transparent 32%),
      conic-gradient(from 0deg, var(--tr-orange) 0 12%, transparent 12% 25%, var(--tr-orange) 25% 37%, transparent 37% 50%,
                     var(--tr-orange) 50% 62%, transparent 62% 75%, var(--tr-orange) 75% 87%, transparent 87%); }
    .tr-word { font-weight:700; font-size:15px; }
    .tr-sep { color:var(--line); }
    .tr-name { color:var(--tr-orange-dark); font-weight:600; font-size:15px; }
    main { flex:1; display:flex; align-items:center; justify-content:center; padding:24px; }
    .card { background:#fff; border:1px solid var(--line); border-radius:10px; width:min(460px,94vw);
            padding:36px 40px; box-shadow:0 8px 30px rgba(0,0,0,.06); }
    .title { font-size:24px; font-weight:700; margin:0 0 8px; }
    .subtitle { color:var(--muted); font-size:14px; margin:0 0 22px; line-height:1.5; }
    label { display:block; font-size:13px; font-weight:600; margin:12px 0 6px; }
    textarea { width:100%; min-height:120px; padding:10px 12px; border:1px solid var(--line); border-radius:6px;
               font-family:ui-monospace,Consolas,monospace; font-size:12px; resize:vertical; }
    textarea:focus { outline:none; border-color:var(--tr-orange); box-shadow:0 0 0 3px rgba(255,128,0,.15); }
    .btn { display:inline-flex; align-items:center; justify-content:center; padding:11px 18px; border-radius:6px;
           font-size:15px; font-weight:600; cursor:pointer; border:1px solid transparent; }
    .btn-primary { background:var(--tr-orange-dark); color:#fff; width:100%; margin-top:20px; }
    .btn-primary:hover { background:#bf3900; }
    .scopes { list-style:none; margin:0 0 4px; padding:16px; border:1px solid var(--line); border-radius:8px; }
    .scopes li { font-size:14px; color:var(--ink); padding:6px 0; }
    .consent-icons { display:flex; align-items:center; justify-content:center; gap:14px; margin:8px 0 18px; }
    .consent-avatar { width:56px; height:56px; border-radius:8px; background:#2f6fb0; color:#fff; font-weight:700;
                      display:flex; align-items:center; justify-content:center; font-size:18px; }
    .consent-arrows { color:var(--muted); font-size:22px; }
    .consent-logo { width:56px; height:56px; border-radius:50%; background:
      conic-gradient(from 0deg, var(--tr-orange) 0 12%, transparent 12% 25%, var(--tr-orange) 25% 37%, transparent 37% 50%,
                     var(--tr-orange) 50% 62%, transparent 62% 75%, var(--tr-orange) 75% 87%, transparent 87%); }
    .btn-row { display:flex; gap:12px; margin-top:22px; }
    .btn-row .btn { flex:1; margin-top:0; }
    .btn-secondary { background:#fff; color:var(--ink); border-color:var(--line); }
    .btn-secondary:hover { background:#f3f4f6; }
    footer.tr { text-align:center; padding:16px; font-size:12px; color:var(--muted); }
    footer.tr a { color:var(--muted); text-decoration:none; margin:0 10px; }
    footer.tr a:hover { text-decoration:underline; }
  </style>
</head>
<body>
  <header class="tr">
    <span class="tr-logo" aria-hidden="true"></span>
    <span class="tr-word">Thomson Reuters</span>
    <span class="tr-sep">|</span>
    <span class="tr-name">Cloud Audit Suite Account</span>
  </header>
  <main>
    <div class="card">
{{cardBody}}
    </div>
  </main>
  <footer class="tr">
    <a href="https://www.thomsonreuters.com/en/terms-of-use.html">Terms of use</a>
    <a href="https://www.thomsonreuters.com/en/privacy-statement.html">Privacy statement</a>
    <a href="https://www.thomsonreuters.com/en/policies/copyright.html">Copyright</a>
  </footer>
</body>
</html>
""";
    }
}
