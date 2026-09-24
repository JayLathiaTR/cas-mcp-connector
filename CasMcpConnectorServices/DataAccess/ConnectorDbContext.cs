using Microsoft.EntityFrameworkCore;

namespace CasMcpConnectorServices.DataAccess;

/// <summary>EF Core context for the connector's token store.</summary>
public class ConnectorDbContext : DbContext
{
    public ConnectorDbContext(DbContextOptions<ConnectorDbContext> options)
        : base(options)
    {
    }

    public DbSet<CasMcpAuthToken> CasMcpAuthTokens => Set<CasMcpAuthToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CasMcpAuthToken>()
            .HasIndex(e => e.CiamUserEuid)
            .IsUnique();
    }
}
