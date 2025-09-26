namespace Backend.Configuration;

/// <summary>
/// Configuration settings for Azure AD authentication
/// </summary>
public class AuthConfig
{
    public const string SectionName = "EntraId";

    /// <summary>
    /// Azure AD Tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Client ID (Application ID)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Client Secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Domain
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Authority URL for token validation
    /// </summary>
    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";

    /// <summary>
    /// Audience for JWT token validation
    /// </summary>
    public string Audience => ClientId;

    /// <summary>
    /// Issuer for JWT token validation
    /// </summary>
    public string Issuer => $"https://login.microsoftonline.com/{TenantId}/v2.0";

    /// <summary>
    /// Token validation parameters
    /// </summary>
    public TokenValidationConfig TokenValidation { get; set; } = new();

    /// <summary>
    /// Session configuration
    /// </summary>
    public SessionConfig Session { get; set; } = new();
}

/// <summary>
/// Token validation configuration
/// </summary>
public class TokenValidationConfig
{
    /// <summary>
    /// Validate the token issuer
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Validate the token audience
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Validate the token lifetime
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Validate the issuer signing key
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Clock skew tolerance in minutes
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 5;
}

/// <summary>
/// Session configuration
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// Session timeout in days
    /// </summary>
    public int TimeoutDays { get; set; } = 30;

    /// <summary>
    /// Cookie name for session
    /// </summary>
    public string CookieName { get; set; } = "__RRRealty_Session";

    /// <summary>
    /// Cookie secure policy
    /// </summary>
    public string SecurePolicy { get; set; } = "Always";

    /// <summary>
    /// Cookie SameSite policy
    /// </summary>
    public string SameSitePolicy { get; set; } = "Lax";

    /// <summary>
    /// Cookie HTTP only flag
    /// </summary>
    public bool HttpOnly { get; set; } = true;

    /// <summary>
    /// Cookie essential flag
    /// </summary>
    public bool IsEssential { get; set; } = true;
}
