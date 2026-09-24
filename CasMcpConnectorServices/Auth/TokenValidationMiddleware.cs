using CasMcpConnectorServices.Security;

namespace CasMcpConnectorServices.Auth;

/// <summary>
/// After the CIAM auth pipeline validates the caller, copies the euid and raw CIAM token onto the
/// scoped <see cref="IRequestAuthContext"/> so downstream (GFR exchange, EM client) can use them.
/// </summary>
public sealed class TokenValidationMiddleware
{
    private const string EuidClaimType = "https://tr.com/euid";
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;

    public TokenValidationMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context, IRequestAuthContext authContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authContext);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            string? euidClaim = context.User.FindFirst(EuidClaimType)?.Value;
            string token = ExtractCiamToken(context);
            if (Guid.TryParse(euidClaim, out Guid euid) && !string.IsNullOrEmpty(token))
            {
                authContext.SetCiam(euid, token);
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private static string ExtractCiamToken(HttpContext context)
    {
        string authorization = context.Request.Headers.Authorization.ToString();
        return authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[BearerPrefix.Length..].Trim()
            : authorization.Trim();
    }
}
