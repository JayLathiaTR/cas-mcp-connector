using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CasMcpConnectorServices.DataAccess;
using CasMcpConnectorServices.Security;
using Microsoft.EntityFrameworkCore;

namespace CasMcpConnectorServices.GoFileRoom;

/// <summary>
/// DB-backed <see cref="IGfrTokenService"/>: looks up the encrypted GFR token in cas_mcp_auth_token,
/// exchanges the CIAM token via user/login when needed, and persists it (envelope-encrypted with a
/// per-record data key) keyed by euid.
/// </summary>
public sealed class GfrTokenService : IGfrTokenService
{
    /// <summary>Named HttpClient used for the CIAM to GFR exchange.</summary>
    public const string HttpClientName = "GfrAuth";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ConnectorDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRecordEncryptor _recordEncryptor;
    private readonly ILogger<GfrTokenService> _logger;

    public GfrTokenService(
        ConnectorDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IRecordEncryptor recordEncryptor,
        ILogger<GfrTokenService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _recordEncryptor = recordEncryptor ?? throw new ArgumentNullException(nameof(recordEncryptor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetOrCreateAsync(Guid euid, string ciamToken, CancellationToken cancellationToken)
    {
        CasMcpAuthToken? existing = await FindAsync(euid, cancellationToken).ConfigureAwait(false);
        if (existing is not null && TryDecrypt(existing, euid, out string storedToken))
        {
            return storedToken;
        }

        (string token, string? userEmail) = await ExchangeAsync(euid, ciamToken, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(euid, token, userEmail, cancellationToken).ConfigureAwait(false);
        return token;
    }

    public async Task<string> RefreshAsync(Guid euid, string ciamToken, CancellationToken cancellationToken)
    {
        (string token, string? userEmail) = await ExchangeAsync(euid, ciamToken, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(euid, token, userEmail, cancellationToken).ConfigureAwait(false);
        return token;
    }

    private Task<CasMcpAuthToken?> FindAsync(Guid euid, CancellationToken cancellationToken) =>
        _dbContext.CasMcpAuthTokens.FirstOrDefaultAsync(x => x.CiamUserEuid == euid, cancellationToken);

    private async Task<(string GfrToken, string? UserEmail)> ExchangeAsync(Guid euid, string ciamToken, CancellationToken cancellationToken)
    {
        HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
        var request = new GfrAuthRequest { Euid = euid.ToString(), Token = ciamToken };

        using HttpResponseMessage response = await client
            .PostAsJsonAsync(GfrEndpoints.UserLogin, request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GFR token exchange returned {StatusCode} for euid {Euid}.", (int)response.StatusCode, euid);
            throw new HttpRequestException("GFR token exchange failed.", inner: null, statusCode: response.StatusCode);
        }

        GfrAuthResponse? result = await response.Content
            .ReadFromJsonAsync<GfrAuthResponse>(ResponseJsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result?.AuthSuccess != true || string.IsNullOrEmpty(result.GfrAuthToken))
        {
            _logger.LogWarning("GFR rejected the CIAM token for euid {Euid}.", euid);
            throw new HttpRequestException("GFR rejected the CIAM token.", inner: null, statusCode: HttpStatusCode.Unauthorized);
        }

        return (result.GfrAuthToken, result.UserEmail);
    }

    private bool TryDecrypt(CasMcpAuthToken row, Guid euid, out string token)
    {
        token = string.Empty;
        if (row.GfrTokenEncrypted.Length == 0 || row.RecordDek is null || row.RecordDek.Length == 0)
        {
            return false;
        }

        try
        {
            token = _recordEncryptor.Decrypt(row.RecordDek, row.GfrTokenEncrypted);
            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Stored GFR token for euid {Euid} could not be decrypted; re-exchanging.", euid);
            return false;
        }
    }

    private async Task UpsertAsync(Guid euid, string gfrToken, string? userEmail, CancellationToken cancellationToken)
    {
        CasMcpAuthToken? row = await FindAsync(euid, cancellationToken).ConfigureAwait(false);
        (byte[] recordDek, byte[] encrypted) = EncryptWithRecordKey(row?.RecordDek, gfrToken);

        if (row is null)
        {
            _dbContext.CasMcpAuthTokens.Add(new CasMcpAuthToken
            {
                Id = Guid.NewGuid(),
                CiamUserEuid = euid,
                GfrTokenEncrypted = encrypted,
                RecordDek = recordDek,
                UserEmail = userEmail,
            });
        }
        else
        {
            row.GfrTokenEncrypted = encrypted;
            row.RecordDek = recordDek;
            row.UserEmail = userEmail;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            CasMcpAuthToken? concurrent = await FindAsync(euid, cancellationToken).ConfigureAwait(false);
            if (concurrent is null)
            {
                throw;
            }

            (recordDek, encrypted) = EncryptWithRecordKey(concurrent.RecordDek, gfrToken);
            concurrent.GfrTokenEncrypted = encrypted;
            concurrent.RecordDek = recordDek;
            concurrent.UserEmail = userEmail;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reuses the row's DEK when present (one DEK per record); creates a fresh one otherwise.</summary>
    private (byte[] RecordDek, byte[] Ciphertext) EncryptWithRecordKey(byte[]? existingRecordDek, string plaintext)
    {
        if (existingRecordDek is { Length: > 0 })
        {
            try
            {
                return (existingRecordDek, _recordEncryptor.Encrypt(existingRecordDek, plaintext));
            }
            catch (CryptographicException)
            {
                // Existing record key unusable (e.g. master key rotated) — start a fresh one.
            }
        }

        byte[] recordDek = _recordEncryptor.CreateWrappedDataKey();
        return (recordDek, _recordEncryptor.Encrypt(recordDek, plaintext));
    }
}
