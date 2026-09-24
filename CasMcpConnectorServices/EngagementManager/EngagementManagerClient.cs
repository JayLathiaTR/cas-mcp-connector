using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace CasMcpConnectorServices.EngagementManager;

/// <summary>
/// Typed HTTP client for the Engagement Manager V1 downstream API.
/// <para>
/// P0 (no login yet): the caller's <c>Authorization</c> header is forwarded verbatim to EM — you
/// paste a GFR token into the MCP client. In P1 this is replaced by the OAuth login + CIAM→GFR
/// exchange, so the tool code below does not change.
/// </para>
/// </summary>
public sealed class EngagementManagerClient
{
    private const int MaxErrorBodyChars = 1500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EngagementManagerClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>GETs the engagement's item tree (workpapers + folders).</summary>
    public async Task<JsonElement> GetEngagementItemsAsync(string engagementId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, EngagementManagerEndpoints.EngagementItems(engagementId));
        request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, GetForwardedAuthorization());

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Engagement Manager GET {request.RequestUri?.PathAndQuery} failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the inbound Authorization header to forward downstream. P0-only shim: replaced by the
    /// login/token-exchange in P1.
    /// </summary>
    private string GetForwardedAuthorization()
    {
        string? auth = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth))
        {
            throw new UnauthorizedAccessException(
                "Missing Authorization header. For P0, supply a GFR token in the request Authorization header.");
        }

        return auth;
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return body.Length > MaxErrorBodyChars ? body[..MaxErrorBodyChars] + "...[truncated]" : body;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Empty;
        }
    }
}
