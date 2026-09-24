using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CasMcpConnectorServices.GoFileRoom;
using CasMcpConnectorServices.Security;
using Microsoft.Net.Http.Headers;

namespace CasMcpConnectorServices.EngagementManager;

/// <summary>
/// Typed client for the Engagement Manager V1 downstream API. Resolves the caller's GoFileRoom
/// token (CIAM to GFR exchange, via <see cref="IGfrTokenService"/>) and sends it as the raw
/// Authorization value EM V1 expects, with a one-time silent refresh on a 401.
/// </summary>
public sealed class EngagementManagerClient
{
    private const int MaxErrorBodyChars = 1500;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IRequestAuthContext _authContext;
    private readonly IGfrTokenService _gfrTokenService;

    public EngagementManagerClient(HttpClient httpClient, IRequestAuthContext authContext, IGfrTokenService gfrTokenService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authContext = authContext ?? throw new ArgumentNullException(nameof(authContext));
        _gfrTokenService = gfrTokenService ?? throw new ArgumentNullException(nameof(gfrTokenService));
    }

    /// <summary>GETs the engagement's item tree (workpapers + folders).</summary>
    public async Task<JsonElement> GetEngagementItemsAsync(string engagementId, CancellationToken cancellationToken)
    {
        if (!_authContext.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("No authenticated CIAM identity is present on the request.");
        }

        string endpoint = EngagementManagerEndpoints.EngagementItems(engagementId);

        string gfrToken = await _gfrTokenService
            .GetOrCreateAsync(_authContext.Euid, _authContext.CiamToken, cancellationToken)
            .ConfigureAwait(false);
        HttpResponseMessage response = await SendAsync(endpoint, gfrToken, cancellationToken).ConfigureAwait(false);

        // One-time silent refresh if EM rejects the (possibly stale) GFR token.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            gfrToken = await _gfrTokenService
                .RefreshAsync(_authContext.Euid, _authContext.CiamToken, cancellationToken)
                .ConfigureAwait(false);
            response = await SendAsync(endpoint, gfrToken, cancellationToken).ConfigureAwait(false);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string body = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Engagement Manager GET {endpoint} failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string endpoint, string gfrToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, gfrToken);
        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
