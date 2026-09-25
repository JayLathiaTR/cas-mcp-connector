using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasMcpConnectorServices.DataAccess;

/// <summary>
/// Persisted GFR session token per CIAM user (euid), stored encrypted (AES-256-GCM).
/// </summary>
[Table("cas_mcp_auth_token")]
public class CasMcpAuthToken
{
    [Key]
    [Column("id", TypeName = "uuid")]
    public Guid Id { get; set; }

    [Required]
    [Column("ciam_user_euid", TypeName = "uuid")]
    public Guid CiamUserEuid { get; set; }

    [Required]
    [Column("gfr_token_encrypted", TypeName = "bytea")]
    public byte[] GfrTokenEncrypted { get; set; } = [];

    /// <summary>The per-record data key (DEK), wrapped by the master key. One DEK encrypts the row's secret columns.</summary>
    [Column("record_dek", TypeName = "bytea")]
    public byte[]? RecordDek { get; set; }

    /// <summary>User email (GFR login) from the exchange. Not a secret; stored in clear.</summary>
    [Column("user_email", TypeName = "text")]
    public string? UserEmail { get; set; }
}
