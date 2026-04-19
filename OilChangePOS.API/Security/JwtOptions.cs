namespace OilChangePOS.API.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "OilChangePOS";
    public string Audience { get; set; } = "OilChangePOS";
    /// <summary>Symmetric signing key; must be long enough for HS256 (32+ chars recommended).</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 8;
}
