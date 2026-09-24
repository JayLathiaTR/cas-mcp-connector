namespace CasMcpConnectorServices.Security;

/// <summary>Default per-request implementation of <see cref="IRequestAuthContext"/>.</summary>
public sealed class RequestAuthContext : IRequestAuthContext
{
    public bool IsAuthenticated { get; private set; }

    public Guid Euid { get; private set; }

    public string CiamToken { get; private set; } = string.Empty;

    public string? GfrToken { get; private set; }

    public void SetCiam(Guid euid, string ciamToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciamToken);

        Euid = euid;
        CiamToken = ciamToken;
        IsAuthenticated = true;
    }

    public void SetGfrToken(string gfrToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(gfrToken);

        GfrToken = gfrToken;
    }
}
